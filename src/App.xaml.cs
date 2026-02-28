using AppPilot.Services.Configuration;
using AppPilot.Services.HealthCheck;
using AppPilot.Services.ServiceControl;
using AppPilot.ViewModels;
using AppPilot.Views;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AppPilot;

public partial class App : Application
{
    private ILogger _logger = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!IsRunningAsAdministrator())
        {
            RestartAsAdministrator();
            return;
        }

        base.OnStartup(e);

        SetupLogging();
        SetupExceptionHandling();
        SetupDependencyInjection();
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RestartAsAdministrator()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName,
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        catch
        {
            MessageBox.Show(
                "AppPilot requires administrator privileges to install and manage Windows services.\n\nPlease run the application as Administrator.",
                "Administrator Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        Environment.Exit(0);
    }

    private void SetupLogging()
    {
        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "AppPilot_.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _logger.Information("AppPilot starting up");
    }

    private void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            _logger.Fatal(exception, "Unhandled domain exception");
            LogAndExit(exception);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            _logger.Error(args.Exception, "Unhandled dispatcher exception");
            args.Handled = true;
            MessageBox.Show(
                $"An error occurred: {args.Exception.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            _logger.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
    }

    private void LogAndExit(Exception? exception)
    {
        var message = exception?.Message ?? "Unknown error";
        MessageBox.Show(
            $"A fatal error occurred: {message}\n\nThe application will now close.",
            "Fatal Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Environment.Exit(1);
    }

    private void SetupDependencyInjection()
    {
        try
        {
            var configurationService = new ConfigurationService(_logger);
            var windowsServiceController = new WindowsServiceController(_logger);
            var processService = new ProcessService(_logger);
            var healthChecker = new HttpHealthChecker(_logger);

            var mainViewModel = new MainViewModel(
                configurationService,
                windowsServiceController,
                processService,
                healthChecker,
                _logger);

            var mainWindow = new MainWindow(mainViewModel);
            mainWindow.Show();

            mainWindow.ContentRendered += (_, _) => TrimMemory();

            _logger.Information("Application started successfully");
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "Failed to start application");
            LogAndExit(ex);
        }
    }

    private static void TrimMemory()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger.Information("AppPilot shutting down");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
