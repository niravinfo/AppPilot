using AppPilot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace AppPilot.Services.Configuration;

public interface IConfigurationService
{
    AppSettings Settings { get; }
    void Save();
}

public class ConfigurationService : IConfigurationService
{
    private readonly string _configFilePath;
    private readonly ILogger<ConfigurationService> _logger;
    private readonly IConfiguration _configuration;

    public AppSettings Settings { get; private set; } = new();

    public ConfigurationService(
        ILogger<ConfigurationService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        _configFilePath = Path.Combine(basePath, "AppData.json");

        Load();
    }

    private void Load()
    {
        try
        {
            _configuration.Bind(Settings);
            ResolvePaths(Settings);
            _logger.LogInformation("Configuration loaded successfully with {Count} services", Settings.Services.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration");
            Settings = new AppSettings();
        }
    }

    private void ResolvePaths(AppSettings settings)
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;

        // BasePath itself can use env vars or be relative to the app directory
        var basePath = string.IsNullOrWhiteSpace(settings.AppPilot.BasePath)
            ? appDir
            : ResolveSinglePath(settings.AppPilot.BasePath, appDir);

        settings.AppPilot.BasePath = basePath;

        foreach (var service in settings.Services)
        {
            service.ExecutablePath = ResolveSinglePath(service.ExecutablePath, basePath);
            service.WorkingDirectory = ResolveSinglePath(service.WorkingDirectory, basePath);
            service.CsprojPath = ResolveSinglePath(service.CsprojPath, basePath);
            service.ProjectPath = ResolveSinglePath(service.ProjectPath, basePath);
        }

        foreach (var repo in settings.GitRepositories)
        {
            repo.LocalPath = ResolveSinglePath(repo.LocalPath, basePath);
            // SolutionPath is relative to LocalPath (resolved after LocalPath is finalised)
            if (!string.IsNullOrWhiteSpace(repo.SolutionPath))
                repo.SolutionPath = ResolveSinglePath(repo.SolutionPath, repo.LocalPath);
        }
    }

    private static string ResolveSinglePath(string path, string basePath)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        // Expand environment variables, e.g. %USERPROFILE% or %MY_PROJECTS%
        path = Environment.ExpandEnvironmentVariables(path);

        // Resolve relative paths against basePath
        if (!Path.IsPathRooted(path))
            path = Path.GetFullPath(Path.Combine(basePath, path));

        return path;
    }

    public void Save()
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(Settings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_configFilePath, json);
            _logger.LogInformation("Configuration saved successfully to {Path}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration to {Path}", _configFilePath);
        }
    }
}
