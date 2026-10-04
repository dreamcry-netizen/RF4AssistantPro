using System.IO;
using System.Windows;
using System.Windows.Threading;
using RF4AssistantPro.Composition;
using RF4AssistantPro.Services;

namespace RF4AssistantPro;

public partial class App : System.Windows.Application
{
    public App()
    {
        AppLog.Info(
            $"Запуск приложения. Версия: " +
            $"{typeof(App).Assembly.GetName().Version}; " +
            $"ОС: {Environment.OSVersion}; " +
            $"64-bit: {Environment.Is64BitProcess}.");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLog.Info("WPF OnStartup.");
        base.OnStartup(e);

        try
        {
            MainWindow = AppCompositionRoot.CreateMainWindow();
            MainWindow.Show();
        }
        catch (Exception exception)
        {
            ReportStartupFailure(exception);
            Shutdown(1);
        }
    }

    private static void ReportStartupFailure(Exception exception)
    {
        AppLog.Error("Критическая ошибка запуска приложения.", exception);
        var emergencyPath = Path.Combine(
            Path.GetTempPath(),
            "RF4AssistantPro-startup-error.txt");

        try
        {
            File.WriteAllText(
                emergencyPath,
                $"{DateTime.Now:O}{Environment.NewLine}{exception}");
        }
        catch
        {
            // Основное исключение важнее сбоя аварийного журнала.
        }

        try
        {
            System.Windows.MessageBox.Show(
                $"Приложение не удалось запустить: {exception.Message}\n\n" +
                $"Журнал: {AppLog.ErrorsFilePath}\n" +
                $"Резервный журнал: {emergencyPath}",
                "RF4 Assistant Pro — ошибка запуска",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        catch
        {
            // Не скрываем исходную ошибку запуска.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Info($"Завершение приложения. Код: {e.ApplicationExitCode}.");
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Необработанная ошибка интерфейса.", e.Exception);

        try
        {
            System.Windows.MessageBox.Show(
                $"Произошла ошибка: {e.Exception.Message}\n\n" +
                $"Журнал ошибок: {AppLog.ErrorsFilePath}",
                "RF4 Assistant Pro",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        catch
        {
            // Не скрываем исходное исключение.
        }
    }

    private static void OnUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        AppLog.Error(
            $"Необработанная системная ошибка. Завершение: {e.IsTerminating}.",
            e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        AppLog.Error("Необработанная ошибка фоновой задачи.", e.Exception);
        e.SetObserved();
    }
}