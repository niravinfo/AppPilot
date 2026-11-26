using System;

namespace AppPilot.AdminBroker;

public enum BrokerOperation
{
    Install,
    Uninstall,
    Start,
    Stop,
    Shutdown
}

public sealed class BrokerRequest
{
    public BrokerOperation Operation { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string Arguments { get; set; } = "";
}

public sealed class BrokerResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";
}

public static class BrokerProtocol
{
    public const string PipePrefix = "AppPilot.AdminBroker.";

    public static bool IsValidServiceName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 80)
            return false;

        foreach (var c in name)
        {
            var allowed = char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or ' ';
            if (!allowed)
                return false;
        }

        return true;
    }
}
