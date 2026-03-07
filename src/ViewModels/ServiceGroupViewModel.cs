using AppPilot.Models;
using AppPilot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
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
        // Optimize: Use ThemeManager cached brushes instead of creating new ones
        GroupAccentBrush = ThemeManager.GetGroupBrush(Group.Id, Group.Name, Group.ColorCode);
        GroupBadgeBrush = ThemeManager.GetGroupBadgeBrush(Group.Id, Group.Name, Group.ColorCode);
    }

    [RelayCommand]
    private async Task StartGroupAsync()
    {
        // Optimize: Avoid LINQ allocations - use List and manual sort
        var services = new List<ServiceItemViewModel>();
        foreach (var service in Items)
        {
            if (service.CanStart)
            {
                services.Add(service);
            }
        }

        services.Sort((a, b) => a.Config.StartOrder.CompareTo(b.Config.StartOrder));

        foreach (var service in services)
        {
            await service.StartAsync();
            await Task.Delay(200);
        }
    }

    [RelayCommand]
    private async Task StopGroupAsync()
    {
        // Optimize: Avoid LINQ allocations - use List and manual sort
        var services = new List<ServiceItemViewModel>();
        foreach (var service in Items)
        {
            if (service.CanStop)
            {
                services.Add(service);
            }
        }

        services.Sort((a, b) => b.Config.StartOrder.CompareTo(a.Config.StartOrder));

        foreach (var service in services)
        {
            await service.StopAsync();
            await Task.Delay(200);
        }
    }
}
