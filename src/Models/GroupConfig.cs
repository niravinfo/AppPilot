namespace AppPilot.Models;

public class GroupConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
    public string ColorCode { get; set; }

    public override string ToString()
    {
        return Name;
    }
}