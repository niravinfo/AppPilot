using System.Collections.Generic;
using System.Windows.Media;
using AppPilot.Domain.Enums;
using AppPilot.Services;

namespace AppPilot.Models;

public static class ColorProvider
{
    private static readonly Dictionary<string, Color> _groupColorsLight = new();
    private static readonly Dictionary<string, Color> _groupColorsDark = new();
    private static readonly Dictionary<ServiceType, Color> _serviceTypeColorsLight = new();
    private static readonly Dictionary<ServiceType, Color> _serviceTypeColorsDark = new();
    private static int _groupColorIndex = 0;

    private static readonly Color[] GroupPalette = new[]
    {
        Color.FromRgb(99, 102, 241),   // Indigo
        Color.FromRgb(236, 72, 153),    // Pink
        Color.FromRgb(34, 197, 94),     // Green
        Color.FromRgb(249, 115, 22),    // Orange
        Color.FromRgb(6, 182, 212),     // Cyan
        Color.FromRgb(168, 85, 247),   // Purple
        Color.FromRgb(234, 179, 8),    // Yellow
        Color.FromRgb(239, 68, 68),     // Red
        Color.FromRgb(20, 184, 166),   // Teal
        Color.FromRgb(132, 204, 22),   // Lime
        Color.FromRgb(244, 63, 94),    // Rose
        Color.FromRgb(121, 85, 72),    // Brown
    };

    public static void Initialize()
    {
        _groupColorIndex = 0;
        _groupColorsLight.Clear();
        _groupColorsDark.Clear();

        _serviceTypeColorsLight[ServiceType.Worker] = Color.FromRgb(99, 102, 241);
        _serviceTypeColorsLight[ServiceType.Grpc] = Color.FromRgb(22, 163, 74);
        _serviceTypeColorsLight[ServiceType.WebApi] = Color.FromRgb(217, 119, 6);
        _serviceTypeColorsLight[ServiceType.NodeApp] = Color.FromRgb(34, 197, 94); // Node.js green

        _serviceTypeColorsDark[ServiceType.Worker] = Color.FromRgb(129, 140, 248);
        _serviceTypeColorsDark[ServiceType.Grpc] = Color.FromRgb(74, 222, 128);
        _serviceTypeColorsDark[ServiceType.WebApi] = Color.FromRgb(251, 191, 36);
        _serviceTypeColorsDark[ServiceType.NodeApp] = Color.FromRgb(74, 222, 128); // Node.js green
    }

    public static Color GetGroupColor(string groupName, bool isDarkTheme)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            groupName = "General";

        var colorDict = isDarkTheme ? _groupColorsDark : _groupColorsLight;

        if (!colorDict.TryGetValue(groupName, out var color))
        {
            color = GroupPalette[_groupColorIndex % GroupPalette.Length];
            colorDict[groupName] = color;
            _groupColorIndex++;
        }

        return color;
    }

    public static SolidColorBrush GetGroupBrush(string groupName, bool isDarkTheme)
    {
        return new SolidColorBrush(GetGroupColor(groupName, isDarkTheme));
    }

    public static Color GetServiceTypeColor(ServiceType serviceType, bool isDarkTheme)
    {
        var colorDict = isDarkTheme ? _serviceTypeColorsDark : _serviceTypeColorsLight;
        return colorDict.TryGetValue(serviceType, out var color) ? color : Colors.Gray;
    }

    public static SolidColorBrush GetServiceTypeBrush(ServiceType serviceType, bool isDarkTheme)
    {
        return new SolidColorBrush(GetServiceTypeColor(serviceType, isDarkTheme));
    }

    public static Color GetTypeBadgeBackground(bool isDarkTheme)
    {
        return isDarkTheme ? Color.FromRgb(49, 46, 129) : Color.FromRgb(238, 242, 255);
    }

    public static Color GetTypeBadgeForeground(bool isDarkTheme)
    {
        return isDarkTheme ? Color.FromRgb(199, 210, 254) : Color.FromRgb(79, 70, 229);
    }

    public static Color GetGroupHeaderBackground(bool isDarkTheme)
    {
        return isDarkTheme ? Color.FromRgb(39, 39, 42) : Color.FromRgb(249, 250, 251);
    }

    public static Color GetGroupHeaderBorder(bool isDarkTheme)
    {
        return isDarkTheme ? Color.FromRgb(63, 63, 70) : Color.FromRgb(229, 231, 235);
    }
}
