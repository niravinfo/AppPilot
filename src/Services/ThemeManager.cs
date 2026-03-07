using AppPilot.Domain.Enums;
using AppPilot.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace AppPilot.Services;

public static class ThemeManager
{
    private static readonly string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "ui-settings.json");

    public static bool IsLight { get; private set; } = true;

    // Brush cache to avoid repeated allocations (~2 MB savings)
    private static readonly Dictionary<string, SolidColorBrush> _brushCache = new();
    private static readonly object _cacheLock = new();

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

        // Clear brush cache on theme change
        lock (_cacheLock)
        {
            _brushCache.Clear();
        }
    }

    /// <summary>
    /// Get a cached brush for a group. Creates and caches if not exists.
    /// </summary>
    public static SolidColorBrush GetGroupBrush(string groupId, string groupName, string colorCode)
    {
        var cacheKey = $"group_{groupId}_{(IsLight ? "light" : "dark")}";

        lock (_cacheLock)
        {
            if (_brushCache.TryGetValue(cacheKey, out var brush))
                return brush;

            Color color;
            if (!string.IsNullOrWhiteSpace(colorCode))
            {
                color = (Color)ColorConverter.ConvertFromString(colorCode);
            }
            else
            {
                color = ColorProvider.GetGroupColor(groupName, !IsLight);
            }

            brush = new SolidColorBrush(color);
            brush.Freeze(); // Frozen brushes are more efficient
            _brushCache[cacheKey] = brush;
            return brush;
        }
    }

    /// <summary>
    /// Get a cached badge background brush for a group.
    /// </summary>
    public static SolidColorBrush GetGroupBadgeBrush(string groupId, string groupName, string colorCode)
    {
        var cacheKey = $"groupbadge_{groupId}_{(IsLight ? "light" : "dark")}";

        lock (_cacheLock)
        {
            if (_brushCache.TryGetValue(cacheKey, out var brush))
                return brush;

            Color baseColor;
            if (!string.IsNullOrWhiteSpace(colorCode))
            {
                baseColor = (Color)ColorConverter.ConvertFromString(colorCode);
            }
            else
            {
                baseColor = ColorProvider.GetGroupColor(groupName, !IsLight);
            }

            var alpha = (byte)(IsLight ? 35 : 40);
            var color = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
            brush = new SolidColorBrush(color);
            brush.Freeze();
            _brushCache[cacheKey] = brush;
            return brush;
        }
    }

    /// <summary>
    /// Get a cached brush for a service type.
    /// </summary>
    public static SolidColorBrush GetServiceTypeBrush(ServiceType serviceType)
    {
        var cacheKey = $"servicetype_{serviceType}_{(IsLight ? "light" : "dark")}";

        lock (_cacheLock)
        {
            if (_brushCache.TryGetValue(cacheKey, out var brush))
                return brush;

            var color = ColorProvider.GetServiceTypeColor(serviceType, !IsLight);
            brush = new SolidColorBrush(color);
            brush.Freeze();
            _brushCache[cacheKey] = brush;
            return brush;
        }
    }

    /// <summary>
    /// Get a cached badge background brush for a service type.
    /// </summary>
    public static SolidColorBrush GetServiceTypeBadgeBrush(ServiceType serviceType)
    {
        var cacheKey = $"servicetypebadge_{serviceType}_{(IsLight ? "light" : "dark")}";

        lock (_cacheLock)
        {
            if (_brushCache.TryGetValue(cacheKey, out var brush))
                return brush;

            var baseColor = ColorProvider.GetServiceTypeColor(serviceType, !IsLight);
            var alpha = (byte)(IsLight ? 40 : 50);
            var color = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
            brush = new SolidColorBrush(color);
            brush.Freeze();
            _brushCache[cacheKey] = brush;
            return brush;
        }
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
