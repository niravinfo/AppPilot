using AppPilot.Models;
using AppPilot.Services;
using AppPilot.Services.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AppPilot.ViewModels;

public partial class GroupManagementViewModel : ViewModelBase
{
    private readonly IConfigurationService _configService;
    private readonly ILogger _logger;
    private readonly Dictionary<string, int> _serviceCounts;

    public ObservableCollection<GroupItemViewModel> Groups { get; } = [];

    [ObservableProperty]
    private GroupItemViewModel? _selectedGroup;

    [ObservableProperty]
    private string _newGroupName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public GroupManagementViewModel(
        ObservableCollection<GroupConfig> groups,
        IConfigurationService configService,
        ILogger logger,
        Dictionary<string, int> serviceCounts)
    {
        _configService = configService;
        _logger = logger;
        _serviceCounts = serviceCounts;

        foreach (var group in groups.OrderBy(g => g.DisplayOrder).ThenBy(g => g.Name))
        {
            var item = new GroupItemViewModel(group);
            if (_serviceCounts.TryGetValue(group.Id, out var count))
            {
                item.ServiceCount = count;
            }
            Groups.Add(item);
        }
    }

    [RelayCommand]
    private void AddGroup()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName))
            return;

        var trimmed = NewGroupName.Trim();
        if (Groups.Any(g => g.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "A group with this name already exists";
            return;
        }

        var maxOrder = Groups.Count > 0 ? Groups.Max(g => g.DisplayOrder) : 0;
        var newGroup = new GroupConfig
        {
            Id = Guid.NewGuid().ToString(),
            Name = trimmed,
            DisplayOrder = maxOrder + 1,
            ColorCode = string.Empty
        };

        var item = new GroupItemViewModel(newGroup);
        Groups.Add(item);
        NewGroupName = string.Empty;
        StatusMessage = $"Group '{trimmed}' added";
    }

    [RelayCommand]
    private void RemoveGroup(GroupItemViewModel? item)
    {
        if (item == null) return;

        Groups.Remove(item);
        if (SelectedGroup == item)
            SelectedGroup = null;

        StatusMessage = $"Group '{item.Name}' removed";
    }

    public void SaveAllChanges(ObservableCollection<GroupConfig> sourceGroups)
    {
        foreach (var item in Groups)
        {
            item.ApplyToGroup();
        }

        sourceGroups.Clear();
        foreach (var item in Groups.OrderBy(g => g.DisplayOrder))
        {
            sourceGroups.Add(item.Group);
        }

        var settings = _configService.Load();
        settings.Groups = sourceGroups.ToList();
        _configService.Save(settings);
    }
}
