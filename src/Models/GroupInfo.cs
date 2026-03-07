namespace AppPilot.Models;

/// <summary>
/// Lightweight group information to pass to ViewModels.
/// Breaks circular dependency between MainViewModel and ServiceItemViewModel.
/// </summary>
public class GroupInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ColorCode { get; set; } = string.Empty;

    public static GroupInfo FromConfig(GroupConfig config)
    {
        return new GroupInfo
        {
            Id = config.Id,
            Name = config.Name,
            ColorCode = config.ColorCode
        };
    }

    public static GroupInfo Empty => new() { Name = "Default" };
}
