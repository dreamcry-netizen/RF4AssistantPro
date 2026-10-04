using System.Globalization;
using System.IO.Compression;
using RF4AssistantPro.Baits;
using RF4AssistantPro.Cafe;
using RF4AssistantPro.Fish;
using RF4AssistantPro.Capture;
using RF4AssistantPro.Models;
using RF4AssistantPro.Ocr;
using RF4AssistantPro.Services;
using RF4AssistantPro.Statistics;
using RF4AssistantPro.Storage;
using RF4AssistantPro.ViewModels;
using RF4AssistantPro.WaterBodies;

var tests = new (string Name, Action Run)[]
{
    ("Граммы и десятичная запятая", ParseGramsAndCommaLength),
    ("Килограммы без потери точности", ParseKilograms),
    ("Приоритет заголовка рыбы", PreferredTitleWins),
    ("Ближайший к длине вес", NearestWeightWins),
    ("Диапазон теста удилища не является весом", RejectRodRange),
    ("Без длины улов сохраняется", AcceptWithoutLength),
    ("Сбой OCR длины не теряет улов", AcceptCurrentOcrWithoutLength),
    ("Заголовок восстанавливает пропущенный вес", RecoverWeightFromTitle),
    ("Латинская r в весе улова означает граммы", ParseCatchLatinWeightUnit),
    ("Одна рыба поддерживает граммы и килограммы",
        SameFishSupportsGramsAndKilograms),
    ("Нереалистичные значения отклоняются", RejectUnrealisticValues),
    ("Пробелы и название реки нормализуются", NormalizeWhitespaceAndRiver),
    ("Форматирование веса", FormatWeights),
    ("Процесс RF4 распознаётся", AcceptRf4Process),
    ("Проводник не принимается за игру", RejectExplorerProcess),
    ("Обрезанное имя ОЧКА отклоняется", RejectTruncatedFishName),
    ("Выбирается полное название рыбы", PreferCompleteFishName),
    ("Карточка Ельца читается со скриншота", ParseCatchCardScreenshot),
    ("Строка чата без карточки отклоняется", RejectChatOnlyCatch),
    ("Разделённые буквы названия склеиваются", NormalizeSpacedFishTitle),
    ("Карточка садка в граммах", ParseKeepnetCardGrams),
    ("Карточка садка с составным названием", ParseKeepnetCardMultiword),
    ("Карточка садка без веса отклоняется", RejectKeepnetCardWithoutWeight),
    ("Единица веса удаляется из названия садка", CleanKeepnetWeightUnit),
    ("Составное название садка сохраняется", KeepKeepnetMultiwordName),
    ("Латинская r распознаётся как граммы", ParseKeepnetLatinWeightUnit),
    ("Полное название наживки приоритетнее общего", PreferSpecificBaitName),
    ("OCR-суффикс сохраняет хорошее совпадение", MatchBaitWithOcrSuffix),
    ("Вес на 1 г ниже порога кафе отклоняется", RejectCafeWeightOneGramBelow),
    ("Вес на пороге кафе принимается", AcceptCafeWeightAtMinimum),
    ("Вес на 1 г выше порога кафе принимается", AcceptCafeWeightOneGramAbove),
    ("Неизвестный порог кафе не принимается", RejectCafeWeightWithoutMinimum),
    ("Рыба из снимка не дублирует существующую", SkipExistingKeepnetFish),
    ("Новый экземпляр рыбы добавляется", AddMissingKeepnetFish),
    ("Счётчик садка ограничивает добавление", LimitKeepnetByGameCount),
    ("Лимит садка распределяется между группами рыбы",
        LimitKeepnetAcrossFishGroups),
    ("Транзакция JSON записывает связанные файлы вместе",
        JsonTransactionWritesAllFiles),
    ("Backup переносит данные и assets обратно",
        BackupRoundTripRestoresAssets),
    ("Backup переносит обученные OCR-псевдонимы",
        BackupRoundTripRestoresCafeOcrAliases),
    ("Backup переносит OCR-псевдонимы садка",
        BackupRoundTripRestoresKeepnetOcrAliases),
    ("Ошибка записи транзакции откатывает предыдущие файлы",
        JsonTransactionRollsBackAfterWriteFailure),
    ("Новая рыба из садка добавляется в уловы", MirrorKeepnetFishToCatches),
    ("Существующий улов не дублируется из садка", SkipMirroredCatchDuplicate),
    ("Папки логов и снимков находятся рядом с программой",
        PortableDirectoriesBesideExecutable),
    ("Диагностический ZIP содержит разделённые логи",
        DiagnosticArchiveContainsLogs),
    ("CSV-экспорт содержит уловы, садок и кафе",
        CsvExportContainsAllSections),
    ("Расширенная аналитика группирует периоды и сессии",
        ExtendedAnalyticsGroupsPeriodsAndSessions),
    ("Кафе водоёма не смешивает предложения других локаций",
        WaterBodyCafeFiltersOffers),
    ("Карточка водоёма показывает все предложения кафе",
        WaterBodyCafeShowsAllOffers),
    ("Пустой водоём карточки наследует локацию снимка",
        WaterBodyCafeIncludesBlankLocation),
    ("Кафе определяет основной водоём снимка",
        InferDominantCafeWaterBody),
    ("Явный водоём карточки не перезаписывается",
        PreserveExplicitCafeWaterBody),
    ("Сводный экспорт содержит таблицы аналитики",
        CsvExportContainsAnalyticsTables),
    ("Прогресс кафе считает оставшихся рыб",
        CalculateCafeRemainingFish),
    ("Прогресс кафе не становится отрицательным",
        CompleteCafeOrderWithoutNegativeRemaining),
    ("Неизвестное количество кафе отмечается отдельно",
        TrackUnknownCafeQuantity),
    ("Автокалибровка находит шкалу 1920×1080",
        CalibrateCatchLayoutAtFullHd),
    ("Автокалибровка учитывает другой масштаб интерфейса",
        CalibrateCatchLayoutAtScaledUi),
    ("Автокалибровка безопасно отклоняет кадр без шкалы",
        RejectCatchLayoutWithoutRuler),
    ("Проверка кафе переводит килограммы в граммы",
        ParseReviewedCafeKilograms),
    ("Проверка кафе сохраняет граммы",
        ParseReviewedCafeGrams),
    ("Проверка кафе требует количество",
        RejectReviewedCafeWithoutQuantity),
    ("Проверка кафе требует единицу веса",
        RejectReviewedCafeWithoutWeightUnit),
    ("OCR карточки приоритетнее общего OCR",
        PreferCafeCardWeight),
    ("Конфликт веса кафе сохраняет альтернативу",
        KeepCafeWeightAlternative),
    ("Одинаковый вес кафе объединяет источники",
        MergeEqualCafeWeightSources),
    ("Количество кафе читается из раздельных OCR-блоков",
        ParseCafeQuantityFromSplitBlocks),
    ("PaddleOCR-блоки одной строки объединяются слева направо",
        ComposeCafeOcrBlocksIntoLine),
    ("OCR-блоки соседних карточек не объединяются",
        KeepSeparateCafeCardsApart),
    ("Разметка кафе всегда сохраняет десять слотов",
        PreserveTenCafeSlots),
    ("Пустая позиция сетки не создаёт карточку кафе",
        SkipEmptyCafeGridPosition),
    ("Частично распознанная карточка кафе сохраняется",
        PreservePartiallyRecognizedCafeCard),
    ("Название кафе канонизируется по псевдониму",
        CanonicalizeCafeFishAlias),
    ("Слитное название кафе канонизируется",
        CanonicalizeCompactCafeFishName),
    ("Название кафе в нижнем регистре канонизируется",
        CanonicalizeLowercaseCafeFishName),
    ("Опечатка OCR канонизируется по расстоянию",
        CanonicalizeCafeFishOcrTypo),
    ("Задвоенная буква Карасьс исправляется без каталога",
        CorrectDuplicatedKarasWithoutCatalog),
    ("Латинские двойники букв канонизируются",
        CanonicalizeCafeFishLatinLookalikes),
    ("Смешанное OCR-написание Густеры канонизируется",
        CanonicalizeCafeFishMixedAlphabet),
    ("Латинское Rotan исправляется без каталога",
        CorrectLatinRotanWithoutCatalog),
    ("Смешанное Ротаn исправляется без каталога",
        CorrectMixedRotanWithoutCatalog),
    ("Ошибка Фустера исправляется без каталога",
        CorrectFusteraWithoutCatalog),
    ("Смешанная Фyстepa исправляется без каталога",
        CorrectMixedFusteraWithoutCatalog),
    ("Слитная Доросомасеверная разделяется без каталога",
        CorrectCompactDorosomaWithoutCatalog),
    ("Количество кафе читается с единицей шт",
        ParseCafeQuantityWithUnit),
    ("Прогресс кафе считается отдельно по предложениям",
        CalculateCafeProgressPerOffer),
    ("Каталог рыбы форматирует трофейный вес",
        FishCatalogFormatsTrophyWeight),
    ("Изображение рыбы находится по нормализованному имени",
        FishImageResolverMatchesNormalizedName),
    ("Водоёмы сохраняют заданный порядок",
        WaterBodiesKeepRequiredOrder),
    ("Новые записи получают разные идентификаторы",
        NewRecordsGetDifferentIds),
    ("Очистка снимков сохраняет защищённые файлы",
        ScreenshotCleanupPreservesProtectedFiles),
    ("Очистка снимков соблюдает общий лимит",
        ScreenshotCleanupRespectsTotalLimit),
    ("Coordinator сохраняет retention и защищает связанные снимки",
        DiagnosticsRetentionCoordinatorProtectsScreenshots),
    ("Capture coordinator атомарно управляет режимами",
        CaptureWorkflowCoordinatorControlsModes),
    ("Ручная корректировка завершает проверку садка",
        KeepnetReviewCorrectionCompletesReview),
    ("Ручная корректировка обучает OCR-словарь садка",
        KeepnetOcrAliasStoreLearnsCorrection),
    ("OCR-словарь садка поддерживает редактирование и очистку",
        KeepnetOcrAliasStoreReplacesAndClears),
    ("Импорт снимка кафе копирует PNG в хранилище",
        ImportCafeScreenshotCopiesPng),
    ("Импорт снимка кафе отклоняет не-PNG",
        ImportCafeScreenshotRejectsNonPng),
    ("Ручное исправление обучает OCR-псевдоним",
        LearnCafeOcrAlias),
    ("Неизменённое имя не создаёт OCR-псевдоним",
        SkipUnchangedCafeOcrAlias),
    ("Пустое восстановление очищает OCR-псевдонимы",
        ReplaceCafeOcrAliasesWithEmptySet),
    ("Редактирование заменяет значение OCR-псевдонима",
        ReplaceCafeOcrAliasValue),
    ("Регистр OCR-псевдонима не создаёт дубликат",
        ReplaceCafeOcrAliasIsCaseInsensitive),
    ("Доменные модели загружаются из Core-сборки",
        DomainModelsLoadFromCoreAssembly),
    ("Хранилище и backup загружаются из Infrastructure-сборки",
        StorageLoadsFromInfrastructureAssembly),
    ("Планировщик запускает одну рыболовную сессию",
        FishingSessionPlannerStartsSingleSession),
    ("Планировщик завершает выбранную рыболовную сессию",
        FishingSessionPlannerStopsSelectedSession),
    ("Копирование предложения кафе сохраняет OCR-метаданные",
        CafeOfferClonePreservesMetadata),
    ("Data transfer сохраняет и загружает JSON уловов",
        DataTransferJsonRoundTrip),
    ("Coordinator атомарно применяет импортированный backup",
        ImportedDataCoordinatorAppliesBackup),
    ("Windows QA сохраняет успехи, сбои и лог",
        WindowsQaChecklistPersistsStatusesAndLog),
    ("Замена коллекции сохраняет ленивую проекцию самой коллекции",
        ReplaceCollectionFromOwnProjection),
    ("Ошибка Пескарь обыкновённый исправляется без каталога",
        CorrectPeskarYoWithoutCatalog),
    ("Windows CI содержит обязательные этапы релиза",
        WindowsReleaseWorkflowDefinesRequiredStages)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS: {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL: {test.Name}");
        Console.Error.WriteLine(exception.Message);
    }
}

Console.WriteLine();
Console.WriteLine($"Всего: {tests.Length}; успешно: {tests.Length - failed}; ошибок: {failed}.");
return failed == 0 ? 0 : 1;

static void ParseGramsAndCommaLength()
{
    var result = CatchTextParser.Parse(
    [
        "Карась серебряный",
        "Вес 78 г",
        "Длина 16,4 см",
        "Водоём: оз. Комариное"
    ]);

    var parsed = NotNull(result);
    Equal("Карась серебряный", parsed.FishName);
    Equal(0.078m, parsed.WeightKg);
    Equal(16.4m, parsed.LengthCm);
    Equal("оз. Комариное", parsed.WaterBodyName);
}

static void ParseKilograms()
{
    var result = CatchTextParser.Parse(
        ["Лосось атлантический", "3.427 кг", "64.2 см", "зачётная"]);

    var parsed = NotNull(result);
    Equal(3.427m, parsed.WeightKg);
    Equal("зачётная", parsed.Quality);
}

static void SameFishSupportsGramsAndKilograms()
{
    var grams = NotNull(CatchTextParser.Parse(["Окунь", "79 г"]));
    var kilograms = NotNull(
        CatchTextParser.Parse(["Окунь", "2,149 кг"]));
    Equal(0.079m, grams.WeightKg);
    Equal(2.149m, kilograms.WeightKg);
}

static void PreferredTitleWins()
{
    var result = CatchTextParser.Parse(
        ["Неверная строка", "1,2 кг", "42 см"],
        ["БОНУС", "Щука обыкновенная"]);

    var parsed = NotNull(result);
    Equal("Щука обыкновенная", parsed.FishName);
}

static void NearestWeightWins()
{
    var result = CatchTextParser.Parse(
    [
        "Карп",
        "Цена корма 100 г",
        "Информация",
        "Вес 2,350 кг",
        "Длина 51 см"
    ]);

    var parsed = NotNull(result);
    Equal(2.350m, parsed.WeightKg);
}

static void RejectRodRange()
{
    Null(CatchTextParser.Parse(
        ["Спиннинг", "Тест 1–50 г", "Длина 210 см"]));
}

static void AcceptWithoutLength()
{
    var parsed = NotNull(CatchTextParser.Parse(["Окунь", "350 г"]));
    Equal("Окунь", parsed.FishName);
    Equal(0.350m, parsed.WeightKg);
    Equal<decimal?>(null, parsed.LengthCm);
}

static void AcceptCurrentOcrWithoutLength()
{
    var result = CatchTextParser.Parse(
        ["Елец", "52 г всм", "БОНУС", "В садок"],
        ["ЕЛ е ц", "57 г"]);

    var parsed = NotNull(result);
    Equal("Елец", parsed.FishName);
    Equal(0.052m, parsed.WeightKg);
    Equal<decimal?>(null, parsed.LengthCm);
}

static void RecoverWeightFromTitle()
{
    var source = new[] { "БОНУС", "В садок", "Пробел" };
    var title = new[] { "Окунь", "168 г" };
    var parsed = NotNull(CatchTextParser.Parse(
        source.Concat(title),
        title));

    Equal("Окунь", parsed.FishName);
    Equal(0.168m, parsed.WeightKg);
}

static void ParseCatchLatinWeightUnit()
{
    var parsed = NotNull(CatchTextParser.Parse(
        ["Окунь", "521 r"],
        ["Окунь"]));

    Equal("Окунь", parsed.FishName);
    Equal(0.521m, parsed.WeightKg);
    Equal<decimal?>(null, parsed.LengthCm);
}

static void RejectUnrealisticValues()
{
    Null(CatchTextParser.Parse(["Карась", "6000 кг", "20 см"]));
    Null(CatchTextParser.Parse(["Карась", "1 кг", "2500 см"]));
}

static void NormalizeWhitespaceAndRiver()
{
    var result = CatchTextParser.Parse(
    [
        "  Голавль   обыкновенный ",
        "  840   г ",
        "  31,5   см ",
        "р. Вьюнок"
    ]);

    var parsed = NotNull(result);
    Equal("Голавль обыкновенный", parsed.FishName);
    Equal("р. Вьюнок", parsed.WaterBodyName);
}

static void FormatWeights()
{
    var previousCulture = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        Equal("37 г", WeightDisplayFormatter.Format(0.037m));
        Equal("78,5 г", WeightDisplayFormatter.Format(0.0785m));
        Equal("1 кг", WeightDisplayFormatter.Format(1m));
        Equal("1,235 кг", WeightDisplayFormatter.Format(1.2345m));
    }
    finally
    {
        CultureInfo.CurrentCulture = previousCulture;
    }
}

static void AcceptRf4Process()
{
    Equal(true, GameWindowMatcher.IsGameProcess("rf4_x64"));
    Equal(true, GameWindowMatcher.IsGameProcess("RF4.exe"));
    Equal(true, GameWindowMatcher.IsGameProcess("RussianFishing4"));
}

static void RejectExplorerProcess()
{
    Equal(false, GameWindowMatcher.IsGameProcess("explorer"));
    Equal(false, GameWindowMatcher.IsGameProcess("chrome"));
    Equal(false, GameWindowMatcher.IsGameProcess(""));
}

static void RejectTruncatedFishName()
{
    Null(CatchTextParser.Parse(["ОЧКА", "111 г", "21 см"]));
}

static void PreferCompleteFishName()
{
    var result = CatchTextParser.Parse(
        ["Елец", "124 г", "24 см"],
        ["ОЧКА", "Плотва обыкновенная"]);

    var parsed = NotNull(result);
    Equal("Плотва обыкновенная", parsed.FishName);
}

static void ParseCatchCardScreenshot()
{
    var result = CatchTextParser.Parse(
    [
        "111 г",
        "17 см",
        "Зачётная",
        "524 ОЧКА ОПЫТА",
        "В садок"
    ],
    ["Елец"]);

    var parsed = NotNull(result);
    Equal("Елец", parsed.FishName);
    Equal(0.111m, parsed.WeightKg);
    Equal(17m, parsed.LengthCm);
    Equal("Зачётная", parsed.Quality);
}

static void RejectChatOnlyCatch()
{
    Null(CatchTextParser.Parse(["ДримСтим Елец, 131 г"]));
}

static void NormalizeSpacedFishTitle()
{
    var result = CatchTextParser.Parse(
        ["57 г", "15 см", "Зачётная"],
        ["Ел е ц"]);

    var parsed = NotNull(result);
    Equal("Елец", parsed.FishName);
    Equal(0.057m, parsed.WeightKg);
    Equal(15m, parsed.LengthCm);
}

static void ParseKeepnetCardGrams()
{
    var result = KeepnetCardParser.Parse(
        ["1 ч 58 мин — 92%", "101 г", "Елец"]);

    var parsed = NotNull(result);
    Equal("Елец", parsed.FishName);
    Equal(0.101m, parsed.WeightKg);
}

static void ParseKeepnetCardMultiword()
{
    var result = KeepnetCardParser.Parse(
        ["2 ч 12 мин — 91%", "116 г", "Плотва обыкновенная"]);

    var parsed = NotNull(result);
    Equal("Плотва обыкновенная", parsed.FishName);
    Equal(0.116m, parsed.WeightKg);
}

static void RejectKeepnetCardWithoutWeight()
{
    Null(KeepnetCardParser.Parse(["Елец", "2 ч 8 мин — 91%"]));
}

static void CleanKeepnetWeightUnit()
{
    Equal("Елец", KeepnetNameNormalizer.Clean("  Елец  г "));
    Equal(
        "Плотва обыкновенная",
        KeepnetNameNormalizer.Clean("Плотва обыкновенная кг"));
}

static void KeepKeepnetMultiwordName()
{
    Equal(
        "Плотва обыкновенная",
        KeepnetNameNormalizer.Clean(" Плотва   обыкновенная "));
}

static void ParseKeepnetLatinWeightUnit()
{
    var result = KeepnetCardParser.Parse(["101 r", "Елец"]);
    var parsed = NotNull(result);
    Equal("Елец", parsed.FishName);
    Equal(0.101m, parsed.WeightKg);
}

static void PreferSpecificBaitName()
{
    var candidate = "Nature Червь навозный";
    var genericScore = BaitMatchScorer.Score(candidate, "Nature Червь");
    var specificScore =
        BaitMatchScorer.Score(candidate, "Nature Червь навозный");

    Equal(true, specificScore > genericScore);
    Equal(1d, specificScore);
}

static void MatchBaitWithOcrSuffix()
{
    var score = BaitMatchScorer.Score("Nature Муха О", "Nature Муха");
    Equal(true, score >= 0.90d);
}

static void RejectCafeWeightOneGramBelow()
{
    Equal(false, CafeOfferMatcher.IsWeightEligible(0.149m, 150m));
}

static void AcceptCafeWeightAtMinimum()
{
    Equal(true, CafeOfferMatcher.IsWeightEligible(0.150m, 150m));
}

static void AcceptCafeWeightOneGramAbove()
{
    Equal(true, CafeOfferMatcher.IsWeightEligible(0.151m, 150m));
}

static void RejectCafeWeightWithoutMinimum()
{
    Equal(false, CafeOfferMatcher.IsWeightEligible(0.054m, null));
}

static void SkipExistingKeepnetFish()
{
    Equal(0, KeepnetMergePlanner.GetCountToAdd(1, 1, 1, 2));
}

static void AddMissingKeepnetFish()
{
    Equal(1, KeepnetMergePlanner.GetCountToAdd(1, 2, 1, 2));
}

static void LimitKeepnetByGameCount()
{
    Equal(2, KeepnetMergePlanner.GetCountToAdd(0, 3, 0, 2));
    Equal(0, KeepnetMergePlanner.GetCountToAdd(0, 1, 2, 2));
}

static void LimitKeepnetAcrossFishGroups()
{
    var plan = KeepnetMergePlanner.PlanCountsToAdd(
        0,
        2,
        [
            (ExistingForKey: 0, ScannedForKey: 3),
            (ExistingForKey: 0, ScannedForKey: 3)
        ]);

    Equal(2, plan.Count);
    Equal(2, plan[0] + plan[1]);
    Equal(2, plan[0]);
    Equal(0, plan[1]);
}

static void JsonTransactionWritesAllFiles()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"rf4-transaction-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var first = Path.Combine(directory, "catches.json");
    var second = Path.Combine(directory, "keepnet.json");
    File.WriteAllText(first, "old-catches");
    File.WriteAllText(second, "old-keepnet");

    try
    {
        new JsonFileTransaction().Commit(
        [
            new JsonFileTransactionItem(first, "new-catches"),
            new JsonFileTransactionItem(second, "new-keepnet")
        ]);

        Equal("new-catches", File.ReadAllText(first));
        Equal("new-keepnet", File.ReadAllText(second));
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}

static void BackupRoundTripRestoresAssets()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"rf4-backup-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var screenshot = Path.Combine(directory, "catch.png");
    var cafeImage = Path.Combine(directory, "cafe.png");
    var baitImage = Path.Combine(directory, "bait.png");
    File.WriteAllBytes(screenshot, [1, 2, 3]);
    File.WriteAllBytes(cafeImage, [4, 5, 6]);
    File.WriteAllBytes(baitImage, [7, 8, 9]);
    var archivePath = Path.Combine(directory, "roundtrip.rf4backup");
    BackupData? imported = null;

    try
    {
        new Rf4BackupService().Export(
            archivePath,
            [
                new CatchRecord
                {
                    FishName = "Щука",
                    WeightKg = 2.4m,
                    ScreenshotPath = screenshot,
                    BaitImagePath = baitImage
                }
            ],
            [
                new CafeSnapshot
                {
                    CapturedAt = new DateTime(2026, 10, 3),
                    FullImagePath = cafeImage,
                    ThumbnailPath = cafeImage,
                    Offers = [new CafeOffer { FishName = "Щука" }]
                }
            ],
            [
                new KeepnetRecord
                {
                    FishName = "Щука",
                    WeightKg = 2.4m,
                    ScreenshotPath = screenshot
                }
            ],
            [
                new BaitCatalogItem
                {
                    Name = "Тестовая наживка",
                    ImagePath = baitImage
                }
            ],
            [
                new UnrecognizedBait
                {
                    CandidateName = "OCR наживка",
                    ScreenshotPath = screenshot
                }
            ],
            [
                new FishingSession
                {
                    StartedAt = new DateTime(2026, 10, 3, 10, 0, 0)
                }
            ]);

        imported = new Rf4BackupService().Import(archivePath);
        Equal(1, imported.Catches.Count);
        Equal(1, imported.CafeSnapshots.Count);
        Equal(1, imported.Keepnet.Count);
        Equal(1, imported.BaitCatalog.Count);
        Equal(1, imported.UnrecognizedBaits.Count);
        Equal(1, imported.Sessions.Count);
        Equal(true, imported.HasBaitCatalog);
        Equal(true, File.Exists(imported.Catches[0].ScreenshotPath));
        Equal(true, File.Exists(imported.Catches[0].BaitImagePath));
        Equal(true, File.Exists(imported.CafeSnapshots[0].FullImagePath));
        Equal(true, File.Exists(imported.BaitCatalog[0].ImagePath));
    }
    finally
    {
        if (imported is not null)
        {
            new Rf4BackupService().CleanupFailedImport(imported);
        }

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}

static void BackupRoundTripRestoresCafeOcrAliases()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"rf4-alias-backup-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var archivePath = Path.Combine(directory, "aliases.rf4backup");
    BackupData? imported = null;
    try
    {
        new Rf4BackupService().Export(
            archivePath,
            [],
            [],
            statistics: null,
            cafeOcrAliases: new Dictionary<string, string>
            {
                ["Rотан"] = "Ротан",
                ["Фустера"] = "Густера"
            });
        imported = new Rf4BackupService().Import(archivePath);
        Equal(true, imported.HasCafeOcrAliases);
        Equal(2, imported.CafeOcrAliases!.Count);
        Equal("Ротан", imported.CafeOcrAliases["Rотан"]);
        Equal("Густера", imported.CafeOcrAliases["Фустера"]);
    }
    finally
    {
        if (imported is not null)
        {
            new Rf4BackupService().CleanupFailedImport(imported);
        }
        Directory.Delete(directory, true);
    }
}

static void BackupRoundTripRestoresKeepnetOcrAliases()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"rf4-keepnet-alias-backup-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var archivePath = Path.Combine(directory, "keepnet-aliases.rf4backup");
    BackupData? imported = null;
    try
    {
        new Rf4BackupService().Export(
            archivePath,
            [],
            [],
            statistics: null,
            keepnetOcrAliases: new Dictionary<string, string>
            {
                ["Карасьс серебряный"] = "Карась серебряный",
                ["Пескарь обыкновённый"] = "Пескарь обыкновенный"
            });
        imported = new Rf4BackupService().Import(archivePath);
        Equal(true, imported.HasKeepnetOcrAliases);
        Equal(2, imported.KeepnetOcrAliases!.Count);
        Equal(
            "Карась серебряный",
            imported.KeepnetOcrAliases["Карасьс серебряный"]);
        Equal(
            "Пескарь обыкновенный",
            imported.KeepnetOcrAliases["Пескарь обыкновённый"]);
    }
    finally
    {
        if (imported is not null)
        {
            new Rf4BackupService().CleanupFailedImport(imported);
        }
        Directory.Delete(directory, true);
    }
}

static void JsonTransactionRollsBackAfterWriteFailure()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"rf4-transaction-failure-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var first = Path.Combine(directory, "catches.json");
    var blocked = Path.Combine(directory, "blocked");
    File.WriteAllText(first, "old-catches");
    Directory.CreateDirectory(blocked);

    try
    {
        var failed = false;
        try
        {
            new JsonFileTransaction().Commit(
            [
                new JsonFileTransactionItem(first, "new-catches"),
                new JsonFileTransactionItem(blocked, "new-keepnet")
            ]);
        }
        catch (Exception)
        {
            failed = true;
        }

        Equal(true, failed);
        Equal("old-catches", File.ReadAllText(first));
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}

static void MirrorKeepnetFishToCatches()
{
    Equal(
        1,
        KeepnetMergePlanner.GetCountToMirrorToCatches(0, 1, 1));
}

static void SkipMirroredCatchDuplicate()
{
    Equal(
        0,
        KeepnetMergePlanner.GetCountToMirrorToCatches(1, 1, 1));
}

static void PortableDirectoriesBesideExecutable()
{
    Equal(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Logs")),
        Path.GetFullPath(AppLog.DirectoryPath));
    Equal(
        Path.Combine(AppLog.DirectoryPath, "Events.log"),
        AppLog.FilePath);
    Equal(
        Path.Combine(AppLog.DirectoryPath, "Errors.log"),
        AppLog.ErrorsFilePath);
    Equal(
        Path.Combine(AppLog.DirectoryPath, "Screenshots.log"),
        AppLog.ScreenshotsFilePath);
    Equal(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Screenshots")),
        Path.GetFullPath(PortableDataPaths.ScreenshotsDirectory));
    Equal(
        Path.Combine(PortableDataPaths.ScreenshotsDirectory, "Space_Catches"),
        PortableDataPaths.GetScreenshotDirectory("rf4_catch"));
    Equal(
        Path.Combine(PortableDataPaths.ScreenshotsDirectory, "M_WaterBody"),
        PortableDataPaths.GetScreenshotDirectory("rf4_waterbody"));
    Equal(
        Path.Combine(PortableDataPaths.ScreenshotsDirectory, "V_Bait"),
        PortableDataPaths.GetScreenshotDirectory("rf4_bait"));
    Equal(
        Path.Combine(PortableDataPaths.ScreenshotsDirectory, "C_Keepnet"),
        PortableDataPaths.GetScreenshotDirectory("rf4_keepnet_final"));
    Equal(
        Path.Combine(PortableDataPaths.ScreenshotsDirectory, "Cafe"),
        PortableDataPaths.GetScreenshotDirectory("rf4_cafe"));
}

static void DiagnosticArchiveContainsLogs()
{
    AppLog.Info("Тест диагностического архива.");
    AppLog.Warn("Тест предупреждения.");
    var path = DiagnosticExportService.Create(false);
    try
    {
        using var archive = ZipFile.OpenRead(path);
        Equal(
            true,
            archive.Entries.Any(entry =>
                entry.FullName == "Logs/Events.log"));
        Equal(
            true,
            archive.Entries.Any(entry =>
                entry.FullName == "Logs/Errors.log"));
        Equal(
            false,
            archive.Entries.Any(entry =>
                entry.FullName.StartsWith("Screenshots/")));
    }
    finally
    {
        File.Delete(path);
    }
}

static void CsvExportContainsAllSections()
{
    var caughtAt = new DateTime(2026, 10, 3, 12, 30, 0);
    var path = CsvExportService.Create(
        [
            new CatchRecord
            {
                CaughtAt = caughtAt,
                FishName = "Окунь",
                WaterBodyName = "р. Вьюнок",
                BaitName = "Червь",
                WeightKg = 0.580m,
                LengthCm = 29m,
                Quality = "Зачётная"
            }
        ],
        [
            new KeepnetRecord
            {
                RecordedAt = caughtAt,
                FishName = "Окунь",
                WeightKg = 0.580m
            }
        ],
        [
            new CafeSnapshot
            {
                CapturedAt = caughtAt,
                Offers =
                [
                    new CafeOffer
                    {
                        FishName = "Окунь",
                        WaterBodyName = "р. Вьюнок",
                        Quantity = 1,
                        MinimumWeightGrams = 2149m
                    }
                ]
            }
        ],
        [
            new FishingSession
            {
                StartedAt = caughtAt.AddMinutes(-30),
                EndedAt = caughtAt,
                WaterBodyName = "р. Вьюнок",
                BaitName = "Червь"
            }
        ]);
    try
    {
        using var archive = ZipFile.OpenRead(path);
        foreach (var name in new[]
                 {
                     "Catches.csv",
                     "Keepnet.csv",
                     "Cafe.csv",
                     "Sessions.csv"
                 })
        {
            var entry = NotNull(archive.GetEntry(name));
            using var reader = new StreamReader(entry.Open());
            var text = reader.ReadToEnd();
            Equal(
                true,
                text.Contains(name == "Sessions.csv"
                    ? "р. Вьюнок"
                    : "Окунь"));
        }
    }
    finally
    {
        File.Delete(path);
    }
}

static void ExtendedAnalyticsGroupsPeriodsAndSessions()
{
    var sessionId = Guid.NewGuid();
    var service = new StatisticsService();
    var snapshot = service.GetStatistics(
    [
        new CatchRecord
        {
            CaughtAt = new DateTime(2026, 10, 1, 10, 0, 0),
            FishingSessionId = sessionId,
            FishName = "Щука",
            WaterBodyName = "Яр",
            BaitName = "Воблер",
            WeightKg = 2.5m
        },
        new CatchRecord
        {
            CaughtAt = new DateTime(2026, 10, 1, 11, 0, 0),
            FishingSessionId = sessionId,
            FishName = "Щука",
            WaterBodyName = "Яр",
            BaitName = "Воблер",
            WeightKg = 1.5m
        },
        new CatchRecord
        {
            CaughtAt = new DateTime(2026, 10, 2, 9, 0, 0),
            FishName = "Окунь",
            WaterBodyName = "Озеро",
            BaitName = "Джиг",
            WeightKg = 0.5m
        }
    ],
    [
        new FishingSession
        {
            Id = sessionId,
            StartedAt = new DateTime(2026, 10, 1, 9, 0, 0),
            EndedAt = new DateTime(2026, 10, 1, 12, 0, 0),
            WaterBodyName = "Яр",
            BaitName = "Воблер"
        }
    ]);

    Equal(2, snapshot.PeriodAnalytics.Count);
    Equal(3, snapshot.PeriodAnalytics.Sum(item => item.CatchCount));
    Equal(
        2,
        snapshot.PeriodAnalytics
            .Single(item => item.PeriodStart.Date ==
                new DateTime(2026, 10, 1))
            .CatchCount);
    Equal(2, snapshot.SessionAnalytics.Count);
    Equal(2, snapshot.SessionAnalytics[0].CatchCount);
    Equal(2, snapshot.FishAnalytics[0].CatchCount);
    Equal("Воблер", snapshot.BaitAnalytics[0].Label);
    Equal(4.0m, snapshot.SessionAnalytics[0].TotalWeightKg);
    var yar = snapshot.WaterBodies.Single(item => item.Name == "Яр");
    Equal(2, yar.CatchCount);
    Equal(2.0m, yar.AverageWeightKg);
    Equal(2.5m, yar.BestWeightKg);
    Equal(1, yar.UniqueFishCount);
    Equal(new DateTime(2026, 10, 1, 11, 0, 0), yar.LastCatchAt!.Value);
    var lake = snapshot.WaterBodies.Single(item => item.Name == "Озеро");
    Equal(1, lake.CatchCount);
    Equal(0.5m, lake.BestWeightKg);
}

static void WaterBodyCafeFiltersOffers()
{
    var komarinoeOffer = new CafeOffer
    {
        FishName = "Карась серебряный",
        WaterBodyName = "Комариное",
        Quantity = 3,
        MinimumWeightGrams = 120m,
        Price = 42m
    };
    var otherOffer = new CafeOffer
    {
        FishName = "Щука",
        WaterBodyName = "р. Вьюнок",
        Quantity = 2,
        MinimumWeightGrams = 1000m
    };
    var snapshot = new CafeSnapshot
    {
        CapturedAt = new DateTime(2026, 10, 3, 18, 0, 0),
        Offers = [komarinoeOffer, otherOffer],
        OfferMatchedCounts = new Dictionary<Guid, int>
        {
            [komarinoeOffer.Id] = 1,
            [otherOffer.Id] = 2
        }
    };
    var viewModel = new WaterBodyDetailViewModel(
        new WaterBodyRating
        {
            Name = "оз. Комариное"
        },
        [snapshot]);

    Equal(1, viewModel.CafeOffers.Count);
    Equal("Карась серебряный", viewModel.CafeOffers[0].FishName);
    Equal(3, viewModel.CafeRequestedCount);
    Equal(1, viewModel.CafeMatchedCount);
    Equal(2, viewModel.CafeRemainingCount);
    Equal(true, viewModel.CafeStatusDisplay.Contains("Осталось 2"));
}

static void WaterBodyCafeShowsAllOffers()
{
    var offers = Enumerable.Range(1, 10)
        .Select(index => new CafeOffer
        {
            FishName = $"Рыба {index}",
            WaterBodyName = "оз. Комариное",
            Quantity = 1,
            MinimumWeightGrams = 30m
        })
        .ToList();
    var viewModel = new WaterBodyDetailViewModel(
        new WaterBodyRating { Name = "Комариное" },
        [new CafeSnapshot
        {
            CapturedAt = DateTime.Now,
            Offers = offers
        }]);

    Equal(10, viewModel.CafeOffers.Count);
    Equal(0, viewModel.HiddenCafeOfferCount);
}

static void WaterBodyCafeIncludesBlankLocation()
{
    var viewModel = new WaterBodyDetailViewModel(
        new WaterBodyRating { Name = "оз. Комариное" },
        [new CafeSnapshot
        {
            CapturedAt = DateTime.Now,
            Offers =
            [
                new CafeOffer
                {
                    FishName = "Лягушка",
                    WaterBodyName = "оз. Комариное",
                    Quantity = 1
                },
                new CafeOffer
                {
                    FishName = "Густера",
                    WaterBodyName = "",
                    Quantity = 6
                },
                new CafeOffer
                {
                    FishName = "Чужая рыба",
                    WaterBodyName = "р. Вьюнок",
                    Quantity = 1
                }
            ]
        }]);

    Equal(2, viewModel.CafeOffers.Count);
    Equal(true, viewModel.CafeOffers.Any(item => item.FishName == "Густера"));
    Equal(false, viewModel.CafeOffers.Any(item => item.FishName == "Чужая рыба"));
}

static void InferDominantCafeWaterBody()
{
    var dominant = CafeWaterBodyInference.FindDominant(
        ["оз.Лосиное", "оз.Лосиное", "", null, "оз.Лосиное"]);
    Equal("оз.Лосиное", dominant);
    Equal("оз.Лосиное", CafeWaterBodyInference.Resolve("", dominant));
}

static void PreserveExplicitCafeWaterBody()
{
    Equal(
        "р.Вьюнок",
        CafeWaterBodyInference.Resolve("р.Вьюнок", "оз.Лосиное"));
}

static void CsvExportContainsAnalyticsTables()
{
    var caughtAt = new DateTime(2026, 10, 3, 12, 30, 0);
    var statistics = new StatisticsService().GetStatistics(
    [
        new CatchRecord
        {
            CaughtAt = caughtAt,
            FishName = "Окунь",
            WaterBodyName = "р. Вьюнок",
            BaitName = "Червь",
            WeightKg = 0.580m
        }
    ]);
    var path = CsvExportService.Create(
        [
            new CatchRecord
            {
                CaughtAt = caughtAt,
                FishName = "Окунь",
                WeightKg = 0.580m
            }
        ],
        [],
        [],
        [],
        statistics);
    try
    {
        using var archive = ZipFile.OpenRead(path);
        foreach (var name in new[]
                 {
                     "Summary.csv",
                     "Summary.json",
                     "AnalyticsByPeriod.csv",
                     "AnalyticsBySession.csv",
                     "AnalyticsByWaterBody.csv",
                     "AnalyticsByFish.csv",
                     "AnalyticsByBait.csv"
                 })
        {
            NotNull(archive.GetEntry(name));
        }

        using var reader = new StreamReader(
            NotNull(archive.GetEntry("Summary.json")).Open());
        Equal(true, reader.ReadToEnd().Contains("TotalCatches"));
    }
    finally
    {
        File.Delete(path);
    }
}

static void CalculateCafeRemainingFish()
{
    var progress = CafeOrderProgressCalculator.Calculate(
        new CafeSnapshot
        {
            MatchedCatchCount = 2,
            Offers =
            [
                new CafeOffer { Quantity = 1 },
                new CafeOffer { Quantity = 4 }
            ]
        });

    Equal(5, progress.TotalRequested);
    Equal(2, progress.Matched);
    Equal(3, progress.Remaining);
    Equal(false, progress.IsCompleted);
}

static void CompleteCafeOrderWithoutNegativeRemaining()
{
    var progress = CafeOrderProgressCalculator.Calculate(
        new CafeSnapshot
        {
            MatchedCatchCount = 4,
            Offers =
            [
                new CafeOffer { Quantity = 3 }
            ]
        });

    Equal(0, progress.Remaining);
    Equal(true, progress.IsCompleted);
}

static void TrackUnknownCafeQuantity()
{
    var progress = CafeOrderProgressCalculator.Calculate(
        new CafeSnapshot
        {
            MatchedCatchCount = 1,
            Offers =
            [
                new CafeOffer { Quantity = 1 },
                new CafeOffer { Quantity = 0 }
            ]
        });

    Equal(1, progress.UnknownQuantityOfferCount);
    Equal(false, progress.IsCompleted);
}

static void CalibrateCatchLayoutAtFullHd()
{
    const int width = 1920;
    const int height = 1080;
    var luminance = CreateSyntheticRuler(
        width,
        height,
        190,
        1729,
        229,
        10);

    var layout = NotNull(
        CatchLayoutCalibrator.TryCalibrate(luminance, width, height));
    Equal(190, layout.RulerLeft);
    Equal(1729, layout.RulerRight);
    Equal(229, layout.RulerY);
    Equal(826, layout.Title.X);
    Equal(59, layout.Title.Y);
    Equal(758, layout.ThreeBadgeWeight.X);
}

static void CalibrateCatchLayoutAtScaledUi()
{
    const int width = 2560;
    const int height = 1440;
    const int rulerLeft = 318;
    const int rulerRight = 2242;
    const int rulerY = 286;
    var luminance = CreateSyntheticRuler(
        width,
        height,
        rulerLeft,
        rulerRight,
        rulerY,
        12);

    var layout = NotNull(
        CatchLayoutCalibrator.TryCalibrate(luminance, width, height));
    Equal(rulerLeft, layout.RulerLeft);
    Equal(rulerRight, layout.RulerRight);
    Equal(rulerY, layout.RulerY);
    Equal(true, layout.Scale > 1.24 && layout.Scale < 1.26);
    Equal(true, layout.Title.X > 1110 && layout.Title.X < 1120);
}

static void RejectCatchLayoutWithoutRuler()
{
    var luminance = new byte[1280 * 720];
    Equal<CatchCalibratedLayout?>(
        null,
        CatchLayoutCalibrator.TryCalibrate(luminance, 1280, 720));
}

static byte[] CreateSyntheticRuler(
    int width,
    int height,
    int left,
    int right,
    int top,
    int thickness)
{
    var luminance = new byte[width * height];
    Array.Fill(luminance, (byte)20);
    for (var y = top; y < top + thickness; y++)
    {
        for (var x = left; x <= right; x++)
        {
            luminance[y * width + x] = 90;
        }
    }

    return luminance;
}

static void ParseReviewedCafeKilograms()
{
    var ocrWeight = CafeWeightTextParser.TryParse(["Масса от 2,149 кг"]);
    Equal(2149m, NotNull(ocrWeight).Grams);
    var wholeKilograms = CafeWeightTextParser.TryParse(["Масса от 11 кг"]);
    Equal(11000m, NotNull(wholeKilograms).Grams);

    var success = CafeOfferReviewParser.TryCreate(
        new CafeOfferReviewInput(
            "Окунь",
            "р. Вьюнок",
            "1",
            "2,149",
            "кг"),
        out var offer,
        out _);

    Equal(true, success);
    Equal(2149m, NotNull(offer).MinimumWeightGrams);
}

static void ParseReviewedCafeGrams()
{
    var success = CafeOfferReviewParser.TryCreate(
        new CafeOfferReviewInput(
            "Плотва обыкновенная",
            "р. Вьюнок",
            "4",
            "50",
            "г"),
        out var offer,
        out _);

    Equal(true, success);
    Equal(50m, NotNull(offer).MinimumWeightGrams);
}

static void RejectReviewedCafeWithoutQuantity()
{
    var success = CafeOfferReviewParser.TryCreate(
        new CafeOfferReviewInput(
            "Окунь",
            "р. Вьюнок",
            "",
            "580",
            "г"),
        out _,
        out var error);

    Equal(false, success);
    Equal(true, error.Contains("количество"));
}

static void RejectReviewedCafeWithoutWeightUnit()
{
    var success = CafeOfferReviewParser.TryCreate(
        new CafeOfferReviewInput(
            "Окунь",
            "р. Вьюнок",
            "1",
            "2.149",
            ""),
        out _,
        out var error);

    Equal(false, success);
    Equal(true, error.Contains("единицу"));
}

static void PreferCafeCardWeight()
{
    var decision = CafeWeightResolver.Resolve(
        new CafeWeightObservation(
            52m,
            "г",
            "Масса от 52 г",
            "общий OCR"),
        new CafeWeightObservation(
            57m,
            "г",
            "57 г",
            "OCR отдельной карточки"));

    Equal(57m, NotNull(decision.Selected).Grams);
    Equal(true, decision.HasConflict);
}

static void KeepCafeWeightAlternative()
{
    var decision = CafeWeightResolver.Resolve(
        new CafeWeightObservation(52m, "г", "52 г", "общий OCR"),
        new CafeWeightObservation(
            57m,
            "г",
            "57 г",
            "OCR отдельной карточки"));

    var alternative = NotNull(decision.Alternative);
    Equal(52m, alternative.Grams);
    Equal("общий OCR", alternative.Source);
}

static void ParseCafeQuantityFromSplitBlocks()
{
    var quantity = CafeQuantityTextParser.TryParse(
        ["Количество", "3", "шт"]);

    if (quantity is not { } value)
    {
        throw new InvalidOperationException(
            "Количество не распознано.");
    }

    Equal(3, value);
}

static void ParseCafeQuantityWithUnit()
{
    var quantity = CafeQuantityTextParser.TryParse(
        ["Рыба", "Количество 11 шт", "Масса от 2,149 кг"]);

    if (quantity is not { } value)
    {
        throw new InvalidOperationException(
            "Количество не распознано.");
    }

    Equal(11, value);
}

static void MergeEqualCafeWeightSources()
{
    var decision = CafeWeightResolver.Resolve(
        new CafeWeightObservation(2149m, "кг", "2,149 кг", "общий OCR"),
        new CafeWeightObservation(
            2149m,
            "кг",
            "2,149 кг",
            "OCR отдельной карточки"));

    Equal(false, decision.HasConflict);
    Equal<CafeWeightObservation?>(null, decision.Alternative);
    Equal(
        "OCR карточки + общий OCR",
        NotNull(decision.Selected).Source);
}

static void NewRecordsGetDifferentIds()
{
    var firstCatch = new CatchRecord();
    var secondCatch = new CatchRecord();
    var firstKeepnet = new KeepnetRecord();
    var secondKeepnet = new KeepnetRecord();

    Equal(false, firstCatch.Id == Guid.Empty);
    Equal(false, secondCatch.Id == Guid.Empty);
    Equal(false, firstCatch.Id == secondCatch.Id);
    Equal(false, firstKeepnet.Id == Guid.Empty);
    Equal(false, secondKeepnet.Id == Guid.Empty);
    Equal(false, firstKeepnet.Id == secondKeepnet.Id);
}

static void CalculateCafeProgressPerOffer()
{
    var roach = new CafeOffer
    {
        FishName = "Плотва",
        Quantity = 4
    };
    var perch = new CafeOffer
    {
        FishName = "Окунь",
        Quantity = 2
    };
    var progress = CafeOrderProgressCalculator.Calculate(
        new CafeSnapshot
        {
            Offers = [roach, perch],
            OfferMatchedCounts = new Dictionary<Guid, int>
            {
                [roach.Id] = 1,
                [perch.Id] = 2
            },
            MatchedCatchCount = 3
        });

    Equal(6, progress.TotalRequested);
    Equal(3, progress.Matched);
    Equal(3, progress.Remaining);
    Equal(2, progress.Offers.Count);
    Equal(3, progress.Offers[0].Remaining);
    Equal(true, progress.Offers[1].IsCompleted);
    Equal(false, progress.IsCompleted);
}

static void ScreenshotCleanupPreservesProtectedFiles()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"RF4AssistantPro_screenshots_{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var oldPath = Path.Combine(root, "old.png");
    var protectedPath = Path.Combine(root, "protected.png");
    try
    {
        File.WriteAllBytes(oldPath, new byte[12]);
        File.WriteAllBytes(protectedPath, new byte[24]);
        File.SetLastWriteTimeUtc(
            oldPath,
            DateTime.UtcNow.AddDays(-40));
        File.SetLastWriteTimeUtc(
            protectedPath,
            DateTime.UtcNow.AddDays(-40));

        var service = new ScreenshotStorageService(root);
        var result = service.Cleanup(
            new ScreenshotRetentionSettings
            {
                MaxAgeDays = 30,
                MaxTotalMegabytes = 0
            },
            [protectedPath],
            DateTime.UtcNow);

        Equal(1, result.DeletedFileCount);
        Equal(12L, result.FreedBytes);
        Equal(false, File.Exists(oldPath));
        Equal(true, File.Exists(protectedPath));
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void ScreenshotCleanupRespectsTotalLimit()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"RF4AssistantPro_screenshots_{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var oldestPath = Path.Combine(root, "oldest.png");
    var newestPath = Path.Combine(root, "newest.png");
    try
    {
        File.WriteAllBytes(oldestPath, new byte[1024 * 1024]);
        File.WriteAllBytes(newestPath, new byte[1024 * 1024]);
        File.SetLastWriteTimeUtc(
            oldestPath,
            DateTime.UtcNow.AddMinutes(-2));
        File.SetLastWriteTimeUtc(
            newestPath,
            DateTime.UtcNow.AddMinutes(-1));

        var service = new ScreenshotStorageService(root);
        var result = service.Cleanup(
            new ScreenshotRetentionSettings
            {
                MaxAgeDays = 0,
                MaxTotalMegabytes = 1
            },
            [],
            DateTime.UtcNow);

        Equal(1, result.DeletedFileCount);
        Equal(1024L * 1024L, result.RemainingBytes);
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void WaterBodiesKeepRequiredOrder()
{
    var expected = new[]
    {
        "Комариное", "Лосиное", "Вьюнок", "Старый острог", "Белая",
        "Куори", "Медвежье", "Волхов", "Северский Донец", "Сура",
        "Ладожское озеро", "Янтарное", "Ладожский архипелаг", "Ахтуба",
        "Медное", "Нижняя Тунгуска", "Яма", "Норвежское море"
    };
    Equal(
        string.Join("|", expected),
        string.Join("|", WaterBodyCatalogRegistry.Definitions.Select(item => item.Name)));
}

static void FishCatalogFormatsTrophyWeight()
{
    var previousCulture = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        var fish = new FishCatalogItem
        {
            Name = "Щука обыкновенная",
            TrophyWeightGrams = 4820m
        };
        Equal("4,82 кг", fish.TrophyWeightDisplay);
    }
    finally
    {
        CultureInfo.CurrentCulture = previousCulture;
    }
}

static T NotNull<T>(T? value) where T : class
{
    if (value is null)
    {
        throw new InvalidOperationException("Ожидалось непустое значение.");
    }

    return value;
}

static void Null<T>(T? value) where T : class
{
    if (value is not null)
    {
        throw new InvalidOperationException("Ожидалось пустое значение.");
    }
}

static void ComposeCafeOcrBlocksIntoLine()
{
    var lines = CafeOcrTextLayout.ComposeLines(
    [
        new CafeOcrBlock("3", 130, 100, 150, 120),
        new CafeOcrBlock("шт", 160, 101, 190, 121),
        new CafeOcrBlock("Количество", 10, 99, 120, 121)
    ]);

    Equal(1, lines.Count);
    Equal("Количество 3 шт", lines[0].Text);
    Equal(3, CafeQuantityTextParser.TryParse(
        lines.Select(line => line.Text)));
}

static void KeepSeparateCafeCardsApart()
{
    var lines = CafeOcrTextLayout.ComposeLines(
    [
        new CafeOcrBlock("Карп", 10, 100, 70, 120),
        new CafeOcrBlock("Окунь", 350, 100, 420, 120)
    ]);

    Equal(2, lines.Count);
}

static void PreserveTenCafeSlots()
{
    var lines = Enumerable.Range(0, 8)
        .Select(index => new CafePositionedLine(
            $"Рыба {index + 1}",
            250 + index % 5 * 150,
            index < 5 ? 300 : 700))
        .ToList();
    var slots = CafeCardSlotLayout.Build(lines, 1000, 1000);

    Equal(10, slots.Count);
    Equal(8, slots.Count(slot => slot.RawLines.Count > 0));
    Equal(2, slots.Count(slot => slot.RawLines.Count == 0));
}

static void SkipEmptyCafeGridPosition()
{
    var slot = new CafeCardSlot(5, []);
    Equal(false, CafeCardSlotEvidence.ShouldInclude(slot, false, false));
}

static void PreservePartiallyRecognizedCafeCard()
{
    var slot = new CafeCardSlot(5, ["Количество 7 шт", "Масса от 110 г"]);
    Equal(true, CafeCardSlotEvidence.ShouldInclude(slot, true, false));
    Equal(true, CafeCardSlotEvidence.ShouldInclude(
        new CafeCardSlot(5, []),
        false,
        true));
}

static void CanonicalizeCafeFishAlias()
{
    var match = CafeFishNameCanonicalizer.Canonicalize(
        "Щука",
        [new FishCatalogItem
        {
            Id = "pike",
            Name = "Щука обыкновенная",
            Aliases = "Щука"
        }]);

    Equal("Щука обыкновенная", match.Name);
    Equal("pike", match.CatalogId);
    Equal("Щука", match.RawName);
    Equal(true, match.IsCanonicalized);
}

static void CanonicalizeCompactCafeFishName()
{
    var match = CafeFishNameCanonicalizer.Canonicalize(
        "Лососьатлантический",
        [new FishCatalogItem
        {
            Id = "salmon",
            Name = "Лосось атлантический"
        }]);

    Equal("Лосось атлантический", match.Name);
    Equal(true, CafeFishNameCanonicalizer.NamesMatch(
        "Лососьатлантический",
        "Лосось атлантический"));
}

static void CanonicalizeLowercaseCafeFishName()
{
    var match = CafeFishNameCanonicalizer.Canonicalize(
        "карась золотой",
        [new FishCatalogItem
        {
            Id = "gold-crucian",
            Name = "Карась золотой"
        }]);

    Equal("Карась золотой", match.Name);
    Equal("gold-crucian", match.CatalogId);
}

static void CanonicalizeCafeFishOcrTypo()
{
    var match = CafeFishNameCanonicalizer.Canonicalize(
        "Карасьз золотой",
        [new FishCatalogItem
        {
            Id = "gold-crucian",
            Name = "Карась золотой"
        }]);

    Equal("Карась золотой", match.Name);
}

static void CorrectDuplicatedKarasWithoutCatalog()
{
    var match = CafeFishNameCanonicalizer.Canonicalize(
        "Карасьс серебряный",
        []);

    Equal("Карась серебряный", match.Name);
    Equal("Карасьс серебряный", match.RawName);
    Equal(true, match.IsCanonicalized);
}

static void CanonicalizeCafeFishLatinLookalikes()
{
    var match = CafeFishNameCanonicalizer.Canonicalize(
        "Potah",
        [new FishCatalogItem
        {
            Id = "rotan",
            Name = "Ротан"
        }]);

    Equal("Ротан", match.Name);
}

static void CanonicalizeCafeFishMixedAlphabet()
{
    var match = CafeFishNameCanonicalizer.Canonicalize(
        "Гycтepa",
        [new FishCatalogItem
        {
            Id = "gustera",
            Name = "Густера"
        }]);

    Equal("Густера", match.Name);
}

static void CorrectLatinRotanWithoutCatalog()
{
    var match = CafeFishNameCanonicalizer.Canonicalize("Rotan", []);
    Equal("Ротан", match.Name);
    Equal("Rotan", match.RawName);
}

static void CorrectMixedRotanWithoutCatalog()
{
    var match = CafeFishNameCanonicalizer.Canonicalize("Ротаn", []);
    Equal("Ротан", match.Name);
    Equal("Ротаn", match.RawName);
}

static void CorrectFusteraWithoutCatalog()
{
    var match = CafeFishNameCanonicalizer.Canonicalize("Фустера", []);
    Equal("Густера", match.Name);
    Equal("Фустера", match.RawName);
}

static void CorrectMixedFusteraWithoutCatalog()
{
    var match = CafeFishNameCanonicalizer.Canonicalize("Фyстepa", []);
    Equal("Густера", match.Name);
}

static void CorrectCompactDorosomaWithoutCatalog()
{
    var match = CafeFishNameCanonicalizer.Canonicalize(
        "Доросомасеверная",
        []);
    Equal("Доросома северная", match.Name);
    Equal("Доросомасеверная", match.RawName);
}

static void DiagnosticsRetentionCoordinatorProtectsScreenshots()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"rf4-diagnostics-retention-{Guid.NewGuid():N}");
    var screenshots = Path.Combine(root, "Screenshots");
    var settingsPath = Path.Combine(root, "retention.json");
    Directory.CreateDirectory(screenshots);
    var protectedPath = Path.Combine(screenshots, "protected.png");
    var oldPath = Path.Combine(screenshots, "old.png");
    File.WriteAllBytes(protectedPath, new byte[16]);
    File.WriteAllBytes(oldPath, new byte[16]);
    var oldTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    File.SetLastWriteTimeUtc(protectedPath, oldTime);
    File.SetLastWriteTimeUtc(oldPath, oldTime);

    try
    {
        var coordinator = new DiagnosticsRetentionCoordinator(
            new ScreenshotStorageService(screenshots),
            new ScreenshotRetentionSettingsStore(settingsPath));
        var settings = new ScreenshotRetentionSettings
        {
            MaxAgeDays = 1,
            MaxTotalMegabytes = 100,
            AutoCleanupEnabled = true
        };

        var result = coordinator.Cleanup(
            settings,
            [protectedPath]);

        Equal(1, result.DeletedFileCount);
        Equal(true, File.Exists(protectedPath));
        Equal(false, File.Exists(oldPath));
        Equal(true, coordinator.LoadSettings().AutoCleanupEnabled);
        Equal(1, coordinator.GetStorageSummary([protectedPath]).FileCount);
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void ImportCafeScreenshotCopiesPng()
{
    var root = Path.Combine(Path.GetTempPath(), $"rf4-import-{Guid.NewGuid():N}");
    var source = Path.Combine(root, "source.png");
    var target = Path.Combine(root, "Cafe");
    Directory.CreateDirectory(root);
    try
    {
        File.WriteAllBytes(source, [1, 2, 3, 4]);
        var imported = new CafeScreenshotImportService(target).Import(
            source,
            new DateTime(2026, 10, 4, 12, 0, 0));
        Equal(true, File.Exists(imported));
        Equal(4L, new FileInfo(imported).Length);
        Equal(true, Path.GetFileName(imported).StartsWith(
            "rf4_cafe_import_20261004_120000_000_",
            StringComparison.Ordinal));
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void ImportCafeScreenshotRejectsNonPng()
{
    var root = Path.Combine(Path.GetTempPath(), $"rf4-import-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var source = Path.Combine(root, "source.jpg");
    try
    {
        File.WriteAllBytes(source, [1]);
        var failed = false;
        try
        {
            _ = new CafeScreenshotImportService(root).Import(source);
        }
        catch (InvalidDataException)
        {
            failed = true;
        }

        Equal(true, failed);
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void LearnCafeOcrAlias()
{
    var root = Path.Combine(Path.GetTempPath(), $"rf4-alias-{Guid.NewGuid():N}");
    var path = Path.Combine(root, "aliases.json");
    try
    {
        var store = new CafeOcrAliasStore(path);
        var changed = store.LearnFromOffers(
        [
            new CafeOffer
            {
                RawFishName = "Piranha",
                FishName = "Пиранья"
            }
        ]);
        Equal(1, changed);
        var aliases = store.Load();
        var match = CafeFishNameCanonicalizer.Canonicalize(
            "Piranha",
            [],
            aliases);
        Equal("Пиранья", match.Name);
        Equal("Piranha", match.RawName);
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void SkipUnchangedCafeOcrAlias()
{
    var root = Path.Combine(Path.GetTempPath(), $"rf4-alias-{Guid.NewGuid():N}");
    var path = Path.Combine(root, "aliases.json");
    try
    {
        var changed = new CafeOcrAliasStore(path).LearnFromOffers(
        [
            new CafeOffer
            {
                RawFishName = "Окунь",
                FishName = "Окунь"
            }
        ]);
        Equal(0, changed);
        Equal(false, File.Exists(path));
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void ReplaceCafeOcrAliasesWithEmptySet()
{
    var root = Path.Combine(Path.GetTempPath(), $"rf4-alias-{Guid.NewGuid():N}");
    var path = Path.Combine(root, "aliases.json");
    try
    {
        var store = new CafeOcrAliasStore(path);
        store.Replace(new Dictionary<string, string>
        {
            ["Raw"] = "Исправлено"
        });
        Equal(true, File.Exists(path));
        store.Replace(new Dictionary<string, string>());
        Equal(false, File.Exists(path));
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void ReplaceCafeOcrAliasValue()
{
    var root = Path.Combine(Path.GetTempPath(), $"rf4-alias-{Guid.NewGuid():N}");
    var path = Path.Combine(root, "aliases.json");
    try
    {
        var store = new CafeOcrAliasStore(path);
        store.Replace(new Dictionary<string, string> { ["Raw"] = "Первое" });
        store.Replace(new Dictionary<string, string> { ["Raw"] = "Второе" });
        Equal("Второе", store.Load()["Raw"]);
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}

static void ReplaceCafeOcrAliasIsCaseInsensitive()
{
    var root = Path.Combine(Path.GetTempPath(), $"rf4-alias-{Guid.NewGuid():N}");
    var path = Path.Combine(root, "aliases.json");
    try
    {
        var store = new CafeOcrAliasStore(path);
        store.Replace(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ROTAN"] = "Ротан"
        });
        var aliases = store.Load();
        Equal(1, aliases.Count);
        Equal("Ротан", aliases["rotan"]);
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}

static void DomainModelsLoadFromCoreAssembly()
{
    Equal("RF4Assistant.Core", typeof(CatchRecord).Assembly.GetName().Name);
    Equal(
        typeof(CatchRecord).Assembly,
        typeof(CafeOfferReviewParser).Assembly);
    Equal(
        typeof(CatchRecord).Assembly,
        typeof(CatchTextParser).Assembly);
}

static void StorageLoadsFromInfrastructureAssembly()
{
    Equal(
        "RF4Assistant.Infrastructure",
        typeof(Rf4BackupService).Assembly.GetName().Name);
    Equal(
        typeof(Rf4BackupService).Assembly,
        typeof(PortableDataPaths).Assembly);
    Equal(
        typeof(Rf4BackupService).Assembly,
        typeof(CafeOcrAliasStore).Assembly);
}

static void FishingSessionPlannerStartsSingleSession()
{
    var startedAt = new DateTime(2026, 10, 4, 8, 30, 0);
    var sessions = FishingSessionPlanner.Start(
        [],
        startedAt,
        "  оз. Комариное ",
        " Червь ");
    Equal(1, sessions.Count);
    Equal(startedAt, sessions[0].StartedAt);
    Equal("оз. Комариное", sessions[0].WaterBodyName);
    Equal("Червь", sessions[0].BaitName);
    Equal(true, sessions[0].IsActive);

    var rejected = false;
    try
    {
        _ = FishingSessionPlanner.Start(
            sessions,
            startedAt.AddMinutes(1),
            "",
            "");
    }
    catch (InvalidOperationException)
    {
        rejected = true;
    }

    Equal(true, rejected);
}

static void FishingSessionPlannerStopsSelectedSession()
{
    var active = new FishingSession
    {
        StartedAt = new DateTime(2026, 10, 4, 8, 30, 0)
    };
    var endedAt = active.StartedAt.AddHours(2);
    var sessions = FishingSessionPlanner.Stop([active], active.Id, endedAt);
    Equal(endedAt, sessions[0].EndedAt);
    Equal(false, sessions[0].IsActive);
}

static void CafeOfferClonePreservesMetadata()
{
    var source = new CafeOffer
    {
        Id = Guid.NewGuid(),
        FishName = "Ротан",
        RawFishName = "Rotan",
        FishCatalogId = "rotan",
        RecognitionSource = "общий OCR + OCR карточки",
        WaterBodyName = "оз. Комариное",
        Quantity = 2,
        MinimumWeightGrams = 150m,
        MinimumWeightUnit = "г",
        RawWeightText = "Масса от 150 г",
        WeightSource = "OCR карточки",
        AlternativeMinimumWeightGrams = 149m,
        AlternativeMinimumWeightUnit = "г",
        AlternativeRawWeightText = "149 г",
        AlternativeWeightSource = "общий OCR",
        Price = 250m
    };

    var cloned = CafeOfferCloner.WithId(source, Guid.NewGuid());

    Equal(false, source.Id == cloned.Id);
    Equal(source.RawFishName, cloned.RawFishName);
    Equal(source.FishCatalogId, cloned.FishCatalogId);
    Equal(source.RecognitionSource, cloned.RecognitionSource);
    Equal(source.AlternativeRawWeightText, cloned.AlternativeRawWeightText);
}

static void WindowsQaChecklistPersistsStatusesAndLog()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"rf4-windows-qa-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var statePath = Path.Combine(root, "qa.json");
    var logPath = Path.Combine(root, "WindowsQa.log");
    try
    {
        var store = new WindowsQaChecklistStore(statePath, logPath);
        var passed = store.SetStatus(
            "WPF-01",
            WindowsQaCheckStatus.Passed,
            "окно запустилось",
            new DateTime(2026, 10, 4, 12, 0, 0));
        Equal(1, passed.PassedCount);
        Equal(0, passed.FailedCount);

        var failed = store.SetStatus(
            "HOOK-01",
            WindowsQaCheckStatus.Failed,
            "клавиша C не сработала",
            new DateTime(2026, 10, 4, 12, 1, 0));
        Equal(1, failed.PassedCount);
        Equal(1, failed.FailedCount);
        Equal(false, failed.ReleaseReady);

        var reloaded = store.Load();
        var hook = reloaded.Items.First(item => item.Id == "HOOK-01");
        Equal(WindowsQaCheckStatus.Failed, hook.Status);
        Equal("клавиша C не сработала", hook.Note);
        var log = File.ReadAllText(logPath);
        Equal(true, log.Contains("WPF-01; status=Passed"));
        Equal(true, log.Contains("HOOK-01; status=Failed"));
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void DataTransferJsonRoundTrip()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"rf4-transfer-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var jsonPath = Path.Combine(root, "catches.json");
    try
    {
        var transfer = new DataTransferService(
            new CatchRecordStore(),
            new Rf4BackupService());
        var source = new CatchRecord
        {
            FishName = "Елец",
            WaterBodyName = "р. Вьюнок",
            BaitName = "Nature Муха",
            WeightKg = 0.104m,
            CaughtAt = new DateTime(2026, 10, 4, 10, 58, 0)
        };

        transfer.SaveCatchJson([source], jsonPath);
        var loaded = transfer.LoadCatchJson(jsonPath);

        Equal(1, loaded.Count);
        Equal(source.FishName, loaded[0].FishName);
        Equal(source.WeightKg, loaded[0].WeightKg);
        Equal(source.WaterBodyName, loaded[0].WaterBodyName);
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void ImportedDataCoordinatorAppliesBackup()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"rf4-import-coordinator-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        var targets = new ImportedDataTargetPaths(
            Path.Combine(root, "catches.json"),
            Path.Combine(root, "cafe.json"),
            Path.Combine(root, "keepnet.json"),
            Path.Combine(root, "sessions.json"),
            Path.Combine(root, "bait-catalog.json"),
            Path.Combine(root, "unrecognized.json"),
            Path.Combine(root, "aliases.json"),
            Path.Combine(root, "keepnet-aliases.json"));
        var coordinator = new ImportedDataApplyCoordinator(
            new DataTransferService(
                new CatchRecordStore(Path.Combine(root, "unused.json")),
                new Rf4BackupService()));
        var backup = new BackupData(
            Catches:
            [
                new CatchRecord
                {
                    FishName = "Елец",
                    WeightKg = 0.092m
                }
            ],
            CafeSnapshots: Enumerable.Range(0, 25)
                .Select(index => new CafeSnapshot
                {
                    CapturedAt = new DateTime(2026, 10, 4).AddMinutes(index)
                })
                .ToList(),
            Keepnet:
            [
                new KeepnetRecord
                {
                    FishName = "Елец",
                    WeightKg = 0.092m
                }
            ],
            BaitCatalog:
            [
                new BaitCatalogItem { Name = "Муха" }
            ],
            UnrecognizedBaits:
            [
                new UnrecognizedBait { CandidateName = "OCR наживка" }
            ],
            Sessions: Enumerable.Range(0, 505)
                .Select(index => new FishingSession
                {
                    StartedAt = new DateTime(2026, 1, 1).AddMinutes(index)
                })
                .ToList(),
            ImportDirectory: "",
            HasBaitCatalog: true,
            HasUnrecognizedBaits: true,
            CafeOcrAliases: new Dictionary<string, string>
            {
                ["  Фустера  "] = " Густера "
            },
            HasCafeOcrAliases: true,
            KeepnetOcrAliases: new Dictionary<string, string>
            {
                ["  Карасьс   серебряный  "] = " Карась серебряный "
            },
            HasKeepnetOcrAliases: true);

        var state = coordinator.ApplyBackup(backup, targets);

        Equal(1, state.Catches.Count);
        Equal(20, state.CafeSnapshots.Count);
        Equal(1, state.Keepnet.Count);
        Equal(500, state.Sessions.Count);
        Equal(true, state.BaitCatalogImported);
        Equal(true, state.CafeOcrAliasesImported);
        Equal(true, state.KeepnetOcrAliasesImported);
        Equal(true, File.Exists(targets.CatchesPath));
        Equal(true, File.Exists(targets.BaitCatalogPath));
        var aliases = System.Text.Json.JsonSerializer.Deserialize<
            Dictionary<string, string>>(
                File.ReadAllText(targets.CafeOcrAliasesPath))!;
        Equal("Густера", aliases["Фустера"]);
        var keepnetAliases = System.Text.Json.JsonSerializer.Deserialize<
            Dictionary<string, string>>(
                File.ReadAllText(targets.KeepnetOcrAliasesPath))!;
        Equal(
            "Карась серебряный",
            keepnetAliases["Карасьс серебряный"]);
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void FishImageResolverMatchesNormalizedName()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"rf4-fish-image-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var imagePath = Path.Combine(root, "elets.png");
    File.WriteAllBytes(imagePath, [137, 80, 78, 71]);

    try
    {
        var catalog = new[]
        {
            new FishCatalogItem
            {
                Name = "Елец",
                Aliases = "Елец речной; Eлец",
                ImagePath = new Uri(imagePath).AbsoluteUri
            }
        };

        var resolved = FishImageResolver.Resolve(
            catalog,
            "  ЕЛЕЦ-  ");

        Equal(imagePath, resolved);
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void ReplaceCollectionFromOwnProjection()
{
    var values = new System.Collections.ObjectModel.ObservableCollection<int>(
        [1, 2, 3]);

    ObservableCollectionReplaceHelper.Replace(
        values,
        values.Select(value => value * 10));

    Equal(3, values.Count);
    Equal(10, values[0]);
    Equal(20, values[1]);
    Equal(30, values[2]);
}

static void KeepnetOcrAliasStoreLearnsCorrection()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"rf4-keepnet-alias-{Guid.NewGuid():N}");
    var path = Path.Combine(root, "aliases.json");

    try
    {
        var store = new KeepnetOcrAliasStore(path);
        Equal(true, store.Learn("Карасьс серебряный", "Карась серебряный"));
        Equal(
            "Карась серебряный",
            store.Resolve("  Карасьс   серебряный "));
        Equal(false, store.Learn("Карасьс серебряный", "Карась серебряный"));
        Equal(false, store.Learn("Не распознана", "Елец"));
        Equal(true, File.Exists(path));
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void KeepnetOcrAliasStoreReplacesAndClears()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"rf4-keepnet-alias-edit-{Guid.NewGuid():N}");
    var path = Path.Combine(root, "aliases.json");

    try
    {
        var store = new KeepnetOcrAliasStore(path);
        store.Replace(new Dictionary<string, string>
        {
            ["  Карасьс   серебряный  "] = " Карась серебряный ",
            ["Не распознана"] = "Елец"
        });

        var loaded = store.Load();
        Equal(1, loaded.Count);
        Equal(
            "Карась серебряный",
            loaded["Карасьс серебряный"]);

        store.Replace(new Dictionary<string, string>
        {
            ["Карасьс серебряный"] = "Карась золотой"
        });
        Equal(
            "Карась золотой",
            store.Load()["Карасьс серебряный"]);

        store.Replace(new Dictionary<string, string>());
        Equal(false, File.Exists(path));
        Equal(0, store.Load().Count);
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void KeepnetReviewCorrectionCompletesReview()
{
    var source = new KeepnetRecord
    {
        FishName = "Не распознана",
        WeightKg = 0m,
        NeedsReview = true,
        ScreenshotPath = "keepnet.png"
    };

    var corrected = KeepnetReviewPlanner.ApplyCorrection(
        source,
        "  Пескарь   обыкновенный ",
        0.024m);

    Equal(source.Id, corrected.Id);
    Equal("Пескарь обыкновенный", corrected.FishName);
    Equal(0.024m, corrected.WeightKg);
    Equal(false, corrected.NeedsReview);
    Equal("keepnet.png", corrected.ScreenshotPath);
}

static void CaptureWorkflowCoordinatorControlsModes()
{
    var coordinator = new CaptureWorkflowCoordinator();

    Equal(true, coordinator.TryBeginCapture());
    Equal(false, coordinator.TryBeginCapture());
    coordinator.EndCapture();

    coordinator.ArmBait();
    Equal(true, coordinator.State.BaitArmed);
    Equal(true, coordinator.TryBeginBaitCapture());
    Equal(false, coordinator.State.BaitArmed);
    Equal(false, coordinator.TryBeginWaterBodyCapture());
    coordinator.EndCapture();

    coordinator.ArmWaterBody();
    Equal(true, coordinator.TryBeginWaterBodyCapture());
    Equal(false, coordinator.State.WaterBodyArmed);
    coordinator.EndCapture();

    coordinator.ArmKeepnet();
    Equal(true, coordinator.TryStartKeepnetSeries());
    Equal(true, coordinator.State.KeepnetSeriesRunning);
    Equal(true, coordinator.TryBeginKeepnetCapture());
    Equal(false, coordinator.TryBeginCapture());
    coordinator.EndKeepnetSeries();

    Equal(false, coordinator.State.KeepnetSeriesRunning);
    Equal(false, coordinator.State.CaptureInProgress);
}

static void CorrectPeskarYoWithoutCatalog()
{
    var match = CafeFishNameCanonicalizer.Canonicalize(
        "Пескарь обыкновённый",
        []);

    Equal("Пескарь обыкновенный", match.Name);
    Equal("Пескарь обыкновённый", match.RawName);
}

static void WindowsReleaseWorkflowDefinesRequiredStages()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    string? workflowPath = null;
    while (directory is not null)
    {
        var candidate = Path.Combine(
            directory.FullName,
            ".github",
            "workflows",
            "windows-release.yml");
        if (File.Exists(candidate))
        {
            workflowPath = candidate;
            break;
        }

        directory = directory.Parent;
    }

    if (workflowPath is null)
    {
        throw new InvalidOperationException(
            "Не найден .github/workflows/windows-release.yml.");
    }

    var workflow = File.ReadAllText(workflowPath);
    foreach (var required in new[]
             {
                 "runs-on: windows-latest",
                 "dotnet restore",
                 "Run automated scenarios",
                 "dotnet build",
                 "dotnet publish",
                 "Expected 3 ONNX models",
                 "Get-FileHash",
                 "actions/upload-artifact@v4"
             })
    {
        if (!workflow.Contains(required, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Windows CI не содержит обязательный этап: {required}.");
        }
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException(
            $"Ожидалось: {expected}; получено: {actual}.");
    }
}