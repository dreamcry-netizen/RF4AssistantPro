using RF4AssistantPro.Baits;
using RF4AssistantPro.Cafe;
using RF4AssistantPro.Ocr;
using RF4AssistantPro.Services;
using RF4AssistantPro.ViewModels;

namespace RF4AssistantPro;

public partial class MainWindow
{
    private async Task CaptureScreenshotAsync()
    {
        if (!_captureWorkflowCoordinator.TryBeginCapture())
        {
            return;
        }
        var viewModel = (StatisticsViewModel)DataContext;

        try
        {
            AppLog.Info("Начат ручной снимок карточки улова.");
            if (!_screenCaptureService.ActivateBoundGame())
            {
                throw new InvalidOperationException(
                    "Не удалось активировать окно RF4.");
            }

            await Task.Delay(100);
            var path = _screenCaptureService.CaptureBoundGame();
            AppLog.Info($"Ручной снимок улова сохранён: {path}.");
            await RecognizeCapturedCatchCoreAsync(path, viewModel);
        }
        catch (Exception exception)
        {
            LogCaptureFailure("Ошибка снимка или OCR улова.", exception);
            viewModel.SetScreenshotError(exception.Message);
        }
        finally
        {
            RunAutomaticScreenshotCleanup();
            _captureWorkflowCoordinator.EndCapture();
        }
    }

    private async Task RecognizeCapturedCatchAsync(string path)
    {
        var viewModel = (StatisticsViewModel)DataContext;
        try
        {
            await RecognizeCapturedCatchCoreAsync(path, viewModel);
        }
        catch (Exception exception)
        {
            AppLog.Error("Ошибка OCR карточки улова.", exception);
            viewModel.SetScreenshotError(exception.Message);
        }
        finally
        {
            RunAutomaticScreenshotCleanup();
            _captureWorkflowCoordinator.EndCapture();
        }
    }

    private async Task RecognizeCapturedCatchCoreAsync(
        string path,
        StatisticsViewModel viewModel)
    {
        viewModel.SetScreenshotCaptured(path);
        var recognized = await _screenshotRecognizer.RecognizeAsync(path);
        if (recognized is null)
        {
            AppLog.Info(
                "Карточка улова не распознана: отсутствует название " +
                "или вес.");
            viewModel.SetRecognitionError(
                "на карточке не удалось определить название или вес рыбы");
            ReviewCatchAndSave(path, null, viewModel);
            return;
        }

        AppLog.Info(
            $"OCR карточки: рыба={recognized.FishName}; " +
            $"вес={recognized.WeightKg}; длина={recognized.LengthCm}; " +
            $"водоём={recognized.WaterBodyName}.");
        if (recognized.NeedsReview)
        {
            viewModel.SetRecognitionError(
                $"нужна проверка: {recognized.ReviewReason}");
            ReviewCatchAndSave(path, recognized, viewModel);
            return;
        }

        viewModel.AddRecognizedCatch(recognized, path);
    }

    private void ReviewCatchAndSave(
        string path,
        RecognizedCatch? recognized,
        StatisticsViewModel viewModel)
    {
        var review = new Views.CatchReviewWindow(path, recognized)
        {
            Owner = this
        };
        if (review.ShowDialog() == true &&
            review.Result is not null)
        {
            viewModel.AddRecognizedCatch(review.Result, path);
            AppLog.Info(
                $"Улов сохранён после ручной проверки: " +
                $"{review.Result.FishName}; вес={review.Result.WeightKg}.");
        }
    }

    private async Task CaptureCafeAsync()
    {
        if (!_captureWorkflowCoordinator.TryBeginCapture())
        {
            return;
        }
        var viewModel = (StatisticsViewModel)DataContext;

        try
        {
            AppLog.Info("Начат снимок кафе.");
            // CopyFromScreen снимает фактические пиксели экрана. Возвращаем
            // игру на передний план, чтобы помощник не попал на снимок.
            if (!_screenCaptureService.ActivateBoundGame())
            {
                throw new InvalidOperationException(
                    "Не удалось активировать окно RF4. Переключись в игру и повтори снимок.");
            }

            await Task.Delay(300);
            var fullImagePath = _screenCaptureService.CaptureBoundGame("rf4_cafe");
            AppLog.Info($"Снимок кафе сохранён: {fullImagePath}.");
            await ProcessCafeScreenshotAsync(fullImagePath, viewModel);
        }
        catch (Exception exception)
        {
            LogCaptureFailure("Ошибка снимка или OCR кафе.", exception);
            viewModel.SetCafeError(exception.Message);
        }
        finally
        {
            RunAutomaticScreenshotCleanup();
            _captureWorkflowCoordinator.EndCapture();
        }
    }

    private async Task ImportCafeAsync()
    {
        if (_captureWorkflowCoordinator.State.CaptureInProgress)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Выберите сохранённый снимок кафе",
            Filter = "PNG (*.png)|*.png",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!_captureWorkflowCoordinator.TryBeginCapture())
        {
            return;
        }

        var viewModel = (StatisticsViewModel)DataContext;
        try
        {
            var importedPath = _cafeImportService.Import(dialog.FileName);
            AppLog.Info(
                $"Снимок кафе импортирован для повторного OCR: " +
                $"{importedPath}.");
            await ProcessCafeScreenshotAsync(importedPath, viewModel);
        }
        catch (Exception exception)
        {
            LogCaptureFailure("Ошибка импорта или OCR кафе.", exception);
            viewModel.SetCafeError(exception.Message);
        }
        finally
        {
            RunAutomaticScreenshotCleanup();
            _captureWorkflowCoordinator.EndCapture();
        }
    }

    private async Task ProcessCafeScreenshotAsync(
        string fullImagePath,
        StatisticsViewModel viewModel)
    {
        var thumbnailPath = _cafeThumbnailService.CreateThumbnail(fullImagePath);
        var offers = await _cafeScreenshotRecognizer.RecognizeAsync(fullImagePath);
        AppLog.Info($"OCR кафе завершён. Предложений: {offers.Count}.");

        Activate();
        var review = new Views.CafeReviewWindow(fullImagePath, offers)
        {
            Owner = this
        };
        if (review.ShowDialog() != true)
        {
            viewModel.SetCafeError(
                "проверка отменена, снимок оставлен в папке Screenshots.");
            AppLog.Info("Сохранение кафе отменено в окне проверки.");
            return;
        }

        AppLog.Info(
            $"Проверка кафе подтверждена. Предложений: " +
            $"{review.Result.Count}.");
        var learnedAliasCount = _cafeOcrAliasStore.LearnFromOffers(review.Result);
        if (learnedAliasCount > 0)
        {
            AppLog.Info(
                $"Кафе: сохранено новых OCR-псевдонимов: " +
                $"{learnedAliasCount}.");
        }

        viewModel.AddCafeSnapshot(new CafeSnapshot
        {
            CapturedAt = DateTime.Now,
            FullImagePath = fullImagePath,
            ThumbnailPath = thumbnailPath,
            Offers = review.Result.ToList()
        });
    }

    private async Task CaptureBaitAsync()
    {
        if (!_captureWorkflowCoordinator.TryBeginBaitCapture())
        {
            return;
        }

        // Режим одноразовый: следующее нажатие V расходует активацию.
        var viewModel = (StatisticsViewModel)DataContext;

        try
        {
            // Даём игре время открыть окно снасти после нажатия V.
            await Task.Delay(700);
            AppLog.Info("Начат снимок для определения наживки.");

            var path = _screenCaptureService.CaptureBoundGame("rf4_bait");
            AppLog.Info($"Снимок снасти сохранён: {path}.");

            var bait = await _baitScreenshotRecognizer.RecognizeAsync(path);
            if (bait is null)
            {
                viewModel.SetBaitRecognitionError(
                    "название не найдено. Открой экран снасти и повтори.");
                AppLog.Info("OCR наживки не нашёл подходящее название.");
                return;
            }

            var match = new BaitCatalogMatcher(_baitCatalogStore)
                .Find(bait.Name, bait.RawText);
            if (match is not null)
            {
                bait = new RecognizedBait(
                    match.Item.DisplayName,
                    bait.RawText,
                    match.Item.Id,
                    match.Item.ImagePath,
                    match.Confidence);
                AppLog.Info(
                    $"Наживка сопоставлена с каталогом: {bait.Name}; " +
                    $"уверенность={bait.MatchConfidence:P0}.");
            }
            else
            {
                _baitCatalogStore.AddUnrecognized(
                    bait.Name,
                    bait.RawText,
                    path);
                AppLog.Info(
                    $"Наживка «{bait.Name}» не найдена в каталоге.");
            }

            viewModel.SetRecognizedBait(bait, path);
            System.Windows.MessageBox.Show(
                this,
                match is null
                    ? $"OCR определил: {bait.Name}\n\n" +
                      "Совпадение не найдено. Запись добавлена в раздел " +
                      "«Нераспознанные»."
                    : $"Наживка определена: {bait.Name}\n\n" +
                      "Название и изображение будут добавляться к следующим уловам.",
                "Наживка определена",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            LogCaptureFailure("Ошибка снимка или OCR наживки.", exception);
            viewModel.SetBaitRecognitionError(exception.Message);
        }
        finally
        {
            RunAutomaticScreenshotCleanup();
            _captureWorkflowCoordinator.EndCapture();
        }
    }

    private async Task CaptureWaterBodyAsync()
    {
        if (!_captureWorkflowCoordinator.TryBeginWaterBodyCapture())
        {
            return;
        }
        var viewModel = (StatisticsViewModel)DataContext;

        try
        {
            // Даём карте полностью открыться после нажатия M.
            await Task.Delay(700);
            AppLog.Info("Начат снимок для определения водоёма.");

            var path = _screenCaptureService.CaptureBoundGame(
                "rf4_waterbody");
            AppLog.Info($"Снимок карты сохранён: {path}.");

            var waterBody =
                await _waterBodyScreenshotRecognizer.RecognizeAsync(path);
            if (waterBody is null)
            {
                viewModel.SetWaterBodyRecognitionError(
                    "название не найдено. Открой карту и повтори.");
                AppLog.Info("OCR карты не нашёл название водоёма.");
                return;
            }

            viewModel.SetRecognizedWaterBody(waterBody, path);
            System.Windows.MessageBox.Show(
                this,
                $"Водоём определён: {waterBody.Name}\n\n" +
                "Он будет добавляться к следующим уловам.",
                "Водоём определён",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            LogCaptureFailure("Ошибка снимка или OCR водоёма.", exception);
            viewModel.SetWaterBodyRecognitionError(exception.Message);
        }
        finally
        {
            RunAutomaticScreenshotCleanup();
            _captureWorkflowCoordinator.EndCapture();
        }
    }

    private async Task CaptureKeepnetAsync()
    {
        if (!_captureWorkflowCoordinator.TryBeginKeepnetCapture())
        {
            return;
        }
        var viewModel = (StatisticsViewModel)DataContext;
        var paths = new List<string>();
        lock (_keepnetSeriesPathGate)
        {
            _keepnetSeriesPaths = paths;
        }
        using var cancellation = new CancellationTokenSource();
        _keepnetSeriesCancellation = cancellation;

        try
        {
            try
            {
                // Первое C открывает садок; ждём полной отрисовки.
                await Task.Delay(1500, cancellation.Token);
                while (true)
                {
                    var number = paths.Count + 1;
                    string path;
                    lock (_keepnetSeriesPathGate)
                    {
                        path = _screenCaptureService.CaptureBoundGame(
                            $"rf4_keepnet_{number:00}");
                        paths.Add(path);
                    }
                    viewModel.SetKeepnetSeriesCapturing(number);
                    AppLog.Info(
                        $"Снимок серии садка {number}: {path}.");
                    System.Media.SystemSounds.Asterisk.Play();
                    await Task.Delay(3000, cancellation.Token);
                }
            }
            catch (OperationCanceledException)
                when (cancellation.IsCancellationRequested)
            {
                AppLog.Info(
                    $"Серия снимков садка остановлена. Кадров: " +
                    $"{paths.Count}.");
            }

            if (paths.Count == 0)
            {
                viewModel.SetKeepnetError(
                    "серия завершена до первого снимка.");
                return;
            }

            viewModel.SetKeepnetSeriesProcessing(paths.Count);
            var expectedCount =
                await _keepnetScreenshotRecognizer.DetectFishCountAsync(
                    paths[0]);
            var baselineCount = viewModel.KeepnetRecords.Count(record =>
                !record.NeedsReview);
            AppLog.Info(
                $"Сверка садка перед OCR: уже записано={baselineCount}; " +
                $"счётчик игры=" +
                $"{expectedCount?.ToString() ?? "не распознан"}.");
            var totalAdded = 0;
            var totalSkipped = 0;
            foreach (var path in paths)
            {
                var fish =
                    await _keepnetScreenshotRecognizer.RecognizeAsync(path);
                if (fish.Count == 0)
                {
                    continue;
                }

                var result =
                    viewModel.AddKeepnetSnapshot(
                        fish,
                        path,
                        expectedCount);
                totalAdded += result.Added;
                totalSkipped += result.Skipped;
            }

            var needsReview = expectedCount.HasValue
                ? viewModel.EnsureKeepnetCount(
                    expectedCount.Value,
                    paths[^1])
                : 0;
            totalAdded += needsReview;
            if (totalAdded == 0 && totalSkipped == 0)
            {
                viewModel.SetKeepnetError(
                    "на серии снимков рыба и счётчик не распознаны.");
                return;
            }

            viewModel.SetKeepnetSeriesComplete(
                paths.Count,
                totalAdded,
                totalSkipped,
                needsReview);
            System.Media.SystemSounds.Exclamation.Play();
        }
        catch (Exception exception)
        {
            LogCaptureFailure("Ошибка снимка или OCR садка.", exception);
            viewModel.SetKeepnetError(exception.Message);
        }
        finally
        {
            lock (_keepnetSeriesPathGate)
            {
                if (ReferenceEquals(_keepnetSeriesPaths, paths))
                {
                    _keepnetSeriesPaths = null;
                }
            }

            if (ReferenceEquals(
                    _keepnetSeriesCancellation,
                    cancellation))
            {
                _keepnetSeriesCancellation = null;
            }

            _captureWorkflowCoordinator.EndKeepnetSeries();
            RunAutomaticScreenshotCleanup();
        }
    }

    private void RunAutomaticScreenshotCleanup()
    {
        try
        {
            if (DataContext is StatisticsViewModel viewModel)
            {
                viewModel.RunAutomaticScreenshotCleanup();
            }
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                "Автоочистка снимков не выполнена: " + exception.Message);
        }
    }

    private static void LogCaptureFailure(string message, Exception exception)
    {
        if (exception is InvalidOperationException &&
            (exception.Message.Contains(
                 "не найдено",
                 StringComparison.OrdinalIgnoreCase) ||
             exception.Message.Contains(
                 "не активно",
                 StringComparison.OrdinalIgnoreCase) ||
             exception.Message.Contains(
                 "свёрнуто",
                 StringComparison.OrdinalIgnoreCase) ||
             exception.Message.Contains(
                 "Не удалось активировать",
                 StringComparison.OrdinalIgnoreCase)))
        {
            AppLog.Warn($"{message} {exception.Message}");
            return;
        }

        AppLog.Error(message, exception);
    }
}
