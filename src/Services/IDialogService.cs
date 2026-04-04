using AppPilot.ViewModels;

namespace AppPilot.Services;

public interface IDialogService
{
    bool? ShowServiceEditor(ServiceEditorViewModel vm);
    bool? ShowGitRepositoryEditor(GitRepositoryEditorViewModel vm);
    bool? ShowServiceDiscovery(ServiceDiscoveryViewModel vm);
    bool Confirm(string message, string title = "Confirm");
}
