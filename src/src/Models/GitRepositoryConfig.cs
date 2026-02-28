using System.Collections.Generic;

namespace AppPilot.Models;

public class GitRepositoryConfig
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    /// <summary>Path to .sln / .slnx / .csproj used for "Build Solution". Relative to LocalPath or absolute.</summary>
    public string SolutionPath { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    /// <summary>Service Names whose individual builds / restarts are linked to this repo.</summary>
    public List<string> LinkedServiceNames { get; set; } = [];
}
