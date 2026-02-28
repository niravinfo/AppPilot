using AppPilot.Models;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace AppPilot.Services;

public static class ThemeManager
{
    private static readonly string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "ui-settings.json");

    public static bool IsLight { get; private set; } = true;

    public static void Initialize()
    {
        var settings = LoadSettings();
        ApplyInternal(settings.Theme);
    }

    public static void Toggle()
    {
        var newTheme = IsLight ? "Dark" : "Light";
        ApplyInternal(newTheme);
        SaveSettings(new UiSettings { Theme = newTheme });
    }

    private static void ApplyInternal(string theme)
    {
        IsLight = !string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);
        var themeName = IsLight ? "Light" : "Dark";
        var uri = new Uri($"pack://application:,,,/Themes/{themeName}Theme.xaml");
        var dicts = Application.Current.Resources.MergedDictionaries;
        var existing = dicts.FirstOrDefault(d => d.Source?.ToString().Contains("Theme.xaml") == true);
        if (existing != null) dicts.Remove(existing);
        dicts.Add(new ResourceDictionary { Source = uri });
        ColorProvider.Initialize();
    }

    private static UiSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch { }
        return new();
    }

    private static void SaveSettings(UiSettings settings)
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
