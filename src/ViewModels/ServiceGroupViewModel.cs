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
    public string GroupName { get; }
    public ObservableCollection<ServiceItemViewModel> Items { get; } = new();
    public bool ShowHeader { get; set; }

    [ObservableProperty]
    private Brush _groupAccentBrush = Brushes.Gray;
    [ObservableProperty]
    private Brush _groupBadgeBrush = Brushes.Gray;

    public ServiceGroupViewModel(string groupName)
    {
        GroupName = groupName;
        InitializeColors();
    }

    private void InitializeColors()
    {
        var isDark = !ThemeManager.IsLight;
        GroupAccentBrush = ColorProvider.GetGroupBrush(GroupName, isDark);
        var groupColor = ColorProvider.GetGroupColor(GroupName, isDark);
        GroupBadgeBrush = new SolidColorBrush(Color.FromArgb((byte)(isDark ? 40 : 35), groupColor.R, groupColor.G, groupColor.B));
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
