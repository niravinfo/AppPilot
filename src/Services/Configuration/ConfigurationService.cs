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
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
    }

    public AppSettings Load()
    {
        try
        {
            var settings = new AppSettings();
            _configuration.Bind(settings);
            _logger.Information("Configuration loaded successfully with {Count} services", settings.Services.Count);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load configuration");
            return new AppSettings();
        }
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
