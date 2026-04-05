using AppPilot.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppPilot.ViewModels;

public partial class GroupItemViewModel : ViewModelBase
{
    public GroupConfig Group { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _colorCode;

    [ObservableProperty]
    private int _displayOrder;

    public int ServiceCount { get; set; }

    public GroupItemViewModel(GroupConfig group)
    {
        Group = group;
        _name = group.Name;
        _colorCode = group.ColorCode ?? string.Empty;
        _displayOrder = group.DisplayOrder;
    }

    public void ApplyToGroup()
    {
        Group.Name = Name;
        Group.ColorCode = ColorCode;
        Group.DisplayOrder = DisplayOrder;
    }
}
