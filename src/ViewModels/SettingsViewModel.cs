using AppPilot.Models;
using AppPilot.Services;
using AppPilot.Services.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.Reflection;

namespace AppPilot.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigurationService _configService;
    private readonly AppPilotSettings _originalSettings;

    [ObservableProperty]
    private int _pollingIntervalSeconds;

    [ObservableProperty]
    private string _logDirectory = string.Empty;

    [ObservableProperty]
    private string _theme;

    [ObservableProperty]
    private string _version;

    [ObservableProperty]
    private string _author;

    [ObservableProperty]
    private string _githubUrl = "https://github.com/niravinfo/AppPilot";

    [ObservableProperty]
    private bool _hasChanges;

    public SettingsViewModel(IConfigurationService configService)
    {
        _configService = configService;

        var settings = _configService.Settings;
        _originalSettings = settings.AppPilot;

        PollingIntervalSeconds = Math.Max(1, _originalSettings.PollingIntervalMs / 1000);
        LogDirectory = _originalSettings.LogDirectory;
        Theme = ThemeManager.IsLight ? "Light" : "Dark";

        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        Version = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        Author = "Nirav Patel";
    }

    partial void OnPollingIntervalSecondsChanged(int value) => CheckForChanges();
    partial void OnLogDirectoryChanged(string value) => CheckForChanges();
    partial void OnThemeChanged(string value) => CheckForChanges();

    private void CheckForChanges()
    {
        var currentPollingMs = PollingIntervalSeconds * 1000;
        HasChanges = currentPollingMs != _originalSettings.PollingIntervalMs ||
                     LogDirectory != _originalSettings.LogDirectory ||
                     (Theme == "Light" && !ThemeManager.IsLight) ||
                     (Theme == "Dark" && ThemeManager.IsLight);
    }

    [RelayCommand]
    private void Save()
    {
        var settings = _configService.Settings;

        var newInterval = PollingIntervalSeconds * 1000;
        if (newInterval < 1000) newInterval = 1000;
        if (newInterval > 3600000) newInterval = 3600000;

        settings.AppPilot.PollingIntervalMs = newInterval;
        settings.AppPilot.LogDirectory = string.IsNullOrEmpty(LogDirectory) ? "Logs" : LogDirectory;
        _configService.Save();

        if ((Theme == "Light" && !ThemeManager.IsLight)
            || (Theme == "Dark" && ThemeManager.IsLight))
        {
            ThemeManager.Toggle();
        }

        HasChanges = false;
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GithubUrl,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        PollingIntervalSeconds = 30;
        LogDirectory = "Logs";
        Theme = "Light";
    }
}