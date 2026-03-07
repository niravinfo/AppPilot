using AppPilot.Models;
using AppPilot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace AppPilot.ViewModels;


public partial class ServiceGroupViewModel : ViewModelBase
{
    public GroupConfig Group { get; }
    public ObservableCollection<ServiceItemViewModel> Items { get; } = new();
    public bool ShowHeader { get; set; }

    [ObservableProperty]
    private Brush _groupAccentBrush = Brushes.Gray;
    [ObservableProperty]
    private Brush _groupBadgeBrush = Brushes.Gray;

    public string GroupName => Group.Name;
    public string GroupColorCode => Group.ColorCode;
    public int DisplayOrder => Group.DisplayOrder;

    public ServiceGroupViewModel(GroupConfig group)
    {
        Group = group;
        InitializeColors();
    }

    private void InitializeColors()
    {
        var isDark = !ThemeManager.IsLight;
        if (!string.IsNullOrWhiteSpace(Group.ColorCode))
        {
            var color = (Color)ColorConverter.ConvertFromString(Group.ColorCode);
            GroupAccentBrush = new SolidColorBrush(color);
            GroupBadgeBrush = new SolidColorBrush(Color.FromArgb((byte)(isDark ? 40 : 35), color.R, color.G, color.B));
        }
        else
        {
            var color = ColorProvider.GetGroupColor(Group.Name, isDark);
            GroupAccentBrush = new SolidColorBrush(color);
            GroupBadgeBrush = new SolidColorBrush(Color.FromArgb((byte)(isDark ? 40 : 35), color.R, color.G, color.B));
        }
    }

    [RelayCommand]
    private async Task StartGroupAsync()
    {
        foreach (var service in Items.Where(s => s.CanStart).OrderBy(s => s.Config.StartOrder))
        {
            await service.StartAsync();
            await Task.Delay(200);
        }
    }

    [RelayCommand]
    private async Task StopGroupAsync()
    {
        foreach (var service in Items.Where(s => s.CanStop).OrderByDescending(s => s.Config.StartOrder))
        {
            await service.StopAsync();
            await Task.Delay(200);
        }
    }
}
