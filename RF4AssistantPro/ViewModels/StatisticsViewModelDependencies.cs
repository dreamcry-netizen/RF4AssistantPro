using RF4AssistantPro.Baits;
using RF4AssistantPro.Cafe;
using RF4AssistantPro.Services;
using RF4AssistantPro.Storage;

namespace RF4AssistantPro.ViewModels;

public sealed record StatisticsViewModelDependencies(
    StatisticsService StatisticsService,
    CatchRecordStore CatchRecordStore,
    CafeSnapshotStore CafeSnapshotStore,
    DataTransferService DataTransferService,
    ImportedDataApplyCoordinator ImportedDataApplyCoordinator,
    KeepnetRecordStore KeepnetRecordStore,
    FishingSessionStore FishingSessionStore,
    BaitCatalogStore BaitCatalogStore,
    DiagnosticsRetentionCoordinator DiagnosticsRetentionCoordinator,
    CafeOcrAliasStore CafeOcrAliasStore,
    KeepnetOcrAliasStore KeepnetOcrAliasStore);