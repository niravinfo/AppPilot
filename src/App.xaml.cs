using AppPilot.Services;
using AppPilot.Services.Build;
using AppPilot.Services.Configuration;
using AppPilot.Services.Git;
using AppPilot.Services.HealthCheck;
using AppPilot.Services.ServiceControl;
using AppPilot.ViewModels;
using AppPilot.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;

namespace AppPilot;

public partial class App : Application
{
    private IHost? _host;
    private ILogger<App>? _logger;
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!IsRunningAsAdministrator())
        {
            RestartAsAdministrator();
            return;
        }

        base.OnStartup(e);

        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "AppPilot_.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        string configFilePath = Path.Combine(basePath, "appsettings.json");

        if (!File.Exists(configFilePath))
        {
            Log.Warning("Configuration file not found at {Path}, using default configuration", configFilePath);
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .Build();

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSerilog();
            })
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddConfiguration(configuration);
            })
            .ConfigureServices((context, services) =>
            {
                // Register services and viewmodels with logging abstraction
                services.AddSingleton<IConfigurationService, ConfigurationService>();
                services.AddSingleton<IServiceController, WindowsServiceController>();
                services.AddSingleton<IProcessService, ProcessService>();
                services.AddSingleton<IHealthChecker, HttpHealthChecker>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<IBuildService, BuildService>();
                services.AddSingleton<IGitService, GitService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _logger = _host.Services.GetRequiredService<ILogger<App>>();
        Services = _host.Services;

        _logger.LogInformation("AppPilot starting up");

        SetupExceptionHandling();

        try
        {
            ThemeManager.Initialize();
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            mainWindow.ContentRendered += (_, _) => TrimMemory();
            _logger.LogInformation("Application started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to start application");
            LogAndExit(ex);
        }
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

    // Logging is now configured via HostBuilder and Serilog

    private void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            _logger?.LogCritical(exception, "Unhandled domain exception");
            LogAndExit(exception);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            _logger?.LogError(args.Exception, "Unhandled dispatcher exception");
            args.Handled = true;
            MessageBox.Show(
                $"An error occurred: {args.Exception.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            _logger?.LogError(args.Exception, "Unobserved task exception");
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

    // Dependency injection is now handled by HostBuilder

    private static void TrimMemory()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("AppPilot shutting down");
        Log.CloseAndFlush();
        _host?.Dispose();
        base.OnExit(e);
    }
}
