using AppPilot.Models;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;

namespace AppPilot.Services.Configuration;

public interface IConfigurationService
{
    AppSettings Load();
    void Save(AppSettings settings);
    string GetConfigFilePath();
}

public class ConfigurationService : IConfigurationService
{
    private readonly string _configFilePath;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;

    public ConfigurationService(ILogger logger)
    {
        _logger = logger;
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        _configFilePath = Path.Combine(basePath, "appsettings.json");

        if (!File.Exists(_configFilePath))
        {
            _logger.Warning("Configuration file not found at {Path}, using default configuration", _configFilePath);
        }

        _configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .Build();
    }

    public AppSettings Load()
    {
        try
        {
            var settings = new AppSettings();
            _configuration.Bind(settings);
            ResolvePaths(settings);
            _logger.Information("Configuration loaded successfully with {Count} services", settings.Services.Count);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load configuration");
            return new AppSettings();
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

    public void Save(AppSettings settings)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_configFilePath, json);
            _logger.Information("Configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save configuration");
        }
    }

    public string GetConfigFilePath() => _configFilePath;
}
