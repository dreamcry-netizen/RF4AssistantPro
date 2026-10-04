using System.Diagnostics;
using System.IO;
using RF4AssistantPro.Fish;
using RF4AssistantPro.Services;
using RF4AssistantPro.Storage;
using RF4AssistantPro.ViewModels;

namespace RF4AssistantPro;

public partial class MainWindow
{
    public void CaptureScreenshotFromView()
    {
        _ = CaptureScreenshotAsync();
    }

    public void CaptureCafeFromView()
    {
        _ = CaptureCafeAsync();
    }

    public void ImportCafeFromView()
    {
        _ = ImportCafeAsync();
    }

    public void OpenCafeOcrAliasesFromView()
    {
        var window = new Views.CafeOcrAliasWindow(_cafeOcrAliasStore)
        {
            Owner = this
        };
        if (window.ShowDialog() == true)
        {
            AppLog.Info("OCR-словарь кафе обновлён пользователем.");
        }
    }

    public void OpenKeepnetOcrAliasesFromView()
    {
        var window = new Views.KeepnetOcrAliasWindow(_keepnetOcrAliasStore)
        {
            Owner = this
        };
        if (window.ShowDialog() == true)
        {
            AppLog.Info("OCR-словарь садка обновлён пользователем.");
        }
    }

    public void ArmBaitRecognitionFromView()
    {
        var viewModel = (StatisticsViewModel)DataContext;
        if (_spaceListener is null)
        {
            viewModel.SetBaitRecognitionError(
                "глобальная клавиша V недоступна. Перезапусти программу.");
            return;
        }

        _captureWorkflowCoordinator.ArmBait();
        viewModel.SetBaitRecognitionArmed();
        AppLog.Info("Одноразовое определение наживки активировано.");
    }

    public void ArmKeepnetCaptureFromView()
    {
        var viewModel = (StatisticsViewModel)DataContext;
        if (_spaceListener is null)
        {
            viewModel.SetKeepnetError(
                "глобальная клавиша C недоступна. Перезапустите программу.");
            return;
        }

        _keepnetSeriesCancellation?.Cancel();
        _captureWorkflowCoordinator.ArmKeepnet();
        viewModel.SetKeepnetCaptureArmed();
        AppLog.Info("Режим серии снимков садка активирован.");
    }

    public void ReviewKeepnetRecordFromView(
        RF4AssistantPro.Models.KeepnetRecord record)
    {
        if (DataContext is not StatisticsViewModel viewModel)
        {
            return;
        }

        var recognized = record.NeedsReview
            ? null
            : new RF4AssistantPro.Ocr.RecognizedCatch
            {
                FishName = record.FishName,
                WeightKg = record.WeightKg
            };
        var review = new Views.CatchReviewWindow(
            record.ScreenshotPath,
            recognized,
            "Корректировка записи садка",
            "Сохранить запись")
        {
            Owner = this
        };
        if (review.ShowDialog() == true && review.Result is not null)
        {
            viewModel.CorrectKeepnetRecord(
                record.Id,
                review.Result.FishName,
                review.Result.WeightKg);
        }
    }

    public void ArmWaterBodyRecognitionFromView()
    {
        var viewModel = (StatisticsViewModel)DataContext;
        if (_spaceListener is null)
        {
            viewModel.SetWaterBodyRecognitionError(
                "глобальная клавиша M недоступна. Перезапусти программу.");
            return;
        }

        _captureWorkflowCoordinator.ArmWaterBody();
        viewModel.SetWaterBodyRecognitionArmed();
        AppLog.Info("Одноразовое определение водоёма активировано.");
    }

    public void OpenLogsFromView()
    {
        try
        {
            Directory.CreateDirectory(AppLog.DirectoryPath);
            AppLog.Info("Пользователь открыл папку журналов.");
            Process.Start(new ProcessStartInfo
            {
                FileName = AppLog.DirectoryPath,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            AppLog.Error("Не удалось открыть папку журналов.", exception);
            System.Windows.MessageBox.Show(
                this,
                $"Не удалось открыть папку журналов.\n\n" +
                $"Путь:\n{AppLog.DirectoryPath}\n\n" +
                exception.Message,
                "Журналы RF4 Assistant Pro",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public void OpenWindowsQaFromView()
    {
        var window = new Views.WindowsQaChecklistWindow(
            _windowsQaChecklistStore)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    public void OpenScreenshotStorageSettingsFromView()
    {
        if (DataContext is not StatisticsViewModel viewModel)
        {
            return;
        }

        var settingsWindow = new Views.ScreenshotStorageSettingsWindow(
            viewModel.LoadScreenshotRetentionSettings(),
            viewModel.GetScreenshotStorageSummary(),
            viewModel.SaveScreenshotRetentionSettings,
            viewModel.CleanupScreenshots)
        {
            Owner = this
        };
        settingsWindow.ShowDialog();
    }

    public async void ReviewSavedScreenshotFromView()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите снимок карточки улова",
                Filter = "Изображения PNG (*.png)|*.png",
                InitialDirectory =
                    PortableDataPaths.GetScreenshotDirectory("rf4_catch"),
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var viewModel = (StatisticsViewModel)DataContext;
            if (viewModel.IsScreenshotAlreadySaved(dialog.FileName))
            {
                System.Windows.MessageBox.Show(
                    this,
                    "Этот снимок уже связан с сохранённым уловом. " +
                    "Повторная запись не создана.",
                    "Проверка снимка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            AppLog.Info(
                $"Запущено повторное распознавание снимка: " +
                $"{dialog.FileName}.");
            var recognized = await _screenshotRecognizer
                .RecognizeAsync(dialog.FileName);
            ReviewCatchAndSave(dialog.FileName, recognized, viewModel);
        }
        catch (Exception exception)
        {
            AppLog.Error(
                "Не удалось повторно распознать снимок улова.",
                exception);
            System.Windows.MessageBox.Show(
                this,
                exception.Message,
                "Проверка снимка",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public void ExportDiagnosticsFromView()
    {
        try
        {
            var answer = System.Windows.MessageBox.Show(
                this,
                "Включить в архив пять последних скриншотов?\n\n" +
                "Скриншоты могут содержать имя игрового профиля. " +
                "Да — добавить снимки, Нет — только обезличенные логи.",
                "Экспорт диагностики",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Warning);
            if (answer == System.Windows.MessageBoxResult.Cancel)
            {
                return;
            }

            var path = _diagnosticsRetentionCoordinator.CreateDiagnosticArchive(
                answer == System.Windows.MessageBoxResult.Yes);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            AppLog.Error(
                "Не удалось создать диагностический архив.",
                exception);
            System.Windows.MessageBox.Show(
                this,
                exception.Message,
                "Экспорт диагностики",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public void ExportCsvFromView()
    {
        try
        {
            var viewModel = (StatisticsViewModel)DataContext;
            var path = viewModel.ExportCsvArchive();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            AppLog.Error("Не удалось создать CSV-экспорт.", exception);
            System.Windows.MessageBox.Show(
                this,
                exception.Message,
                "Экспорт CSV",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public void OpenBaitCatalogFromView(
        bool openEditor = false,
        string? editItemId = null)
    {
        AppLog.Info("Открыт раздел наживок.");
        var window = new Views.BaitCatalogWindow(
            _baitCatalogStore,
            openEditor,
            editItemId)
        {
            Owner = this
        };
        window.ShowDialog();

        if (DataContext is StatisticsViewModel viewModel)
        {
            viewModel.SetBaitCatalog(_baitCatalogStore.LoadCatalog());
        }
    }

    public void OpenFishCatalogFromView(
        bool openEditor = false,
        string? editItemId = null)
    {
        AppLog.Info("Открыт каталог рыбы.");
        var window = new Views.FishCatalogWindow(
            _fishCatalogStore,
            openEditor,
            editItemId)
        {
            Owner = this
        };
        window.ShowDialog();

        if (DataContext is StatisticsViewModel viewModel)
        {
            viewModel.SetFishCatalog(_fishCatalogStore.LoadCatalog());
        }
    }

    public void OpenFishProfileFromView(
        RF4AssistantPro.Fish.FishCatalogItem fish)
    {
        if (DataContext is not StatisticsViewModel viewModel)
        {
            return;
        }

        AppLog.Info($"Открыта карточка рыбы: {fish.Name}.");
        var window = new Views.FishProfileWindow(
            fish,
            viewModel.GetCatchesForFish(fish.Name))
        {
            Owner = this
        };
        window.ShowDialog();
    }

    public void ShowCafeImageFromView(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            return;
        }

        var preview = new Views.ImagePreviewWindow(imagePath)
        {
            Owner = this
        };
        preview.ShowDialog();
    }

    public void BindGameFromView()
    {
        var viewModel = (StatisticsViewModel)DataContext;

        try
        {
            AppLog.Info("Поиск окна RF4.");
            var game = _screenCaptureService.BindGame();
            if (game is null)
            {
                viewModel.SetGameBindingError(
                    "окно Russian Fishing 4 не найдено");
                AppLog.Info("Окно RF4 не найдено.");
                return;
            }

            viewModel.SetGameBound(game.Title);
            AppLog.Info($"Окно RF4 привязано: {game.Title}.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Ошибка привязки окна RF4.", exception);
            viewModel.SetGameBindingError(exception.Message);
        }
    }
}
