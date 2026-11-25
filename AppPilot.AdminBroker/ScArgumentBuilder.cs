using System;

namespace AppPilot.AdminBroker;

public static class ScArgumentBuilder
{
    public static string[] Create(string name, string executablePath, string arguments)
    {
        var binPath = string.IsNullOrWhiteSpace(arguments)
            ? executablePath
            : $"{executablePath} {arguments}";

        return new[] { "create", name, "binPath=", binPath, "start=", "demand" };
    }

    public static string[] Description(string name, string displayName) =>
        new[] { "description", name, displayName };

    public static string[] Delete(string name) => new[] { "delete", name };

    public static string[] Start(string name) => new[] { "start", name };

    public static string[] Stop(string name) => new[] { "stop", name };
}
