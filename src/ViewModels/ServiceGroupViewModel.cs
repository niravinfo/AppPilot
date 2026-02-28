using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AppPilot.ViewModels;

public partial class ServiceGroupViewModel : ViewModelBase
{
    public string GroupName { get; }
    public ObservableCollection<ServiceItemViewModel> Items { get; } = new();
    public bool ShowHeader { get; set; }

    public ServiceGroupViewModel(string groupName) => GroupName = groupName;

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
