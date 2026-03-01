using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AppPilot.Controls;

public partial class SvgIcon : UserControl
{
    public static readonly DependencyProperty IconKeyProperty =
        DependencyProperty.Register(nameof(IconKey), typeof(string), typeof(SvgIcon),
            new PropertyMetadata(string.Empty, OnIconKeyChanged));

    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.Register(nameof(IconData), typeof(Geometry), typeof(SvgIcon),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IconFillProperty =
        DependencyProperty.Register(nameof(IconFill), typeof(Brush), typeof(SvgIcon),
            new PropertyMetadata(Brushes.Black));

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(SvgIcon),
            new PropertyMetadata(16.0));

    public static readonly DependencyProperty ScaleXProperty =
        DependencyProperty.Register(nameof(ScaleX), typeof(double), typeof(SvgIcon),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty ScaleYProperty =
        DependencyProperty.Register(nameof(ScaleY), typeof(double), typeof(SvgIcon),
            new PropertyMetadata(1.0));

    public string IconKey
    {
        get => (string)GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public Geometry IconData
    {
        get => (Geometry)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public Brush IconFill
    {
        get => (Brush)GetValue(IconFillProperty);
        set => SetValue(IconFillProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public double ScaleX
    {
        get => (double)GetValue(ScaleXProperty);
        set => SetValue(ScaleXProperty, value);
    }

    public double ScaleY
    {
        get => (double)GetValue(ScaleYProperty);
        set => SetValue(ScaleYProperty, value);
    }

    private static readonly Dictionary<string, Geometry> IconPaths = new()
    {
        ["Close"] = Geometry.Parse("M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"),
        ["Check"] = Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"),
    };

    public SvgIcon()
    {
        InitializeComponent();
    }

    private static void OnIconKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SvgIcon icon && e.NewValue is string key && IconPaths.TryGetValue(key, out var geometry))
        {
            icon.IconData = geometry;
        }
    }
}
