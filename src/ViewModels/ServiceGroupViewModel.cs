using AppPilot.Domain.Enums;
using AppPilot.Models;
using AppPilot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
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
    private readonly Action<IEnumerable<ServiceItemViewModel>>? _onGroupStateChanged;

    [ObservableProperty]
    private Brush _groupAccentBrush = Brushes.Gray;
    [ObservableProperty]
    private Brush _groupBadgeBrush = Brushes.Gray;

    public string GroupName => Group.Name;
    public string GroupColorCode => Group.ColorCode;
    public int DisplayOrder => Group.DisplayOrder;

    public ServiceGroupViewModel(GroupConfig group, Action<IEnumerable<ServiceItemViewModel>>? onGroupStateChanged = null)
    {
        Group = group;
        _onGroupStateChanged = onGroupStateChanged;
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
        // Exclude NodeApp services from group start (they don't support automatic start/stop)
        var services = new List<ServiceItemViewModel>();
        foreach (var service in Items)
        {
            if (service.CanStart && service.Config.Type != ServiceType.NodeApp)
            {
                services.Add(service);
            }
        }

        services.Sort((a, b) =>
            (a.Config.DisplayOrder ?? 999).CompareTo(b.Config.DisplayOrder ?? 999) != 0
                ? (a.Config.DisplayOrder ?? 999).CompareTo(b.Config.DisplayOrder ?? 999)
                : string.Compare(a.Config.Name, b.Config.Name, StringComparison.OrdinalIgnoreCase));

        foreach (var service in services)
        {
            await service.StartAsync();
            await Task.Delay(200);
        }

        _onGroupStateChanged?.Invoke(services);
    }

    [RelayCommand]
    private async Task StopGroupAsync()
    {
        // Optimize: Avoid LINQ allocations - use List and manual sort
        // Exclude NodeApp services from group stop (they don't support automatic start/stop)
        var services = new List<ServiceItemViewModel>();
        foreach (var service in Items)
        {
            if (service.CanStop && service.Config.Type != ServiceType.NodeApp)
            {
                services.Add(service);
            }
        }

        services.Sort((a, b) =>
            (b.Config.DisplayOrder ?? 999).CompareTo(a.Config.DisplayOrder ?? 999) != 0
                ? (b.Config.DisplayOrder ?? 999).CompareTo(a.Config.DisplayOrder ?? 999)
                : string.Compare(b.Config.Name, a.Config.Name, StringComparison.OrdinalIgnoreCase));

        foreach (var service in services)
        {
            await service.StopAsync();
            await Task.Delay(200);
        }

        _onGroupStateChanged?.Invoke(services);
    }
}
