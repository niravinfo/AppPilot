using System;
using AppPilot.AdminBroker;
using Xunit;

namespace AppPilot.Tests;

public class ScArgumentBuilderTests
{
    [Fact]
    public void Create_UsesDemandStart()
    {
        var args = ScArgumentBuilder.Create("MyWorker", @"C:\apps\worker.exe", "--port 5000");

        Assert.Equal(new[] { "create", "MyWorker", "binPath=", @"C:\apps\worker.exe --port 5000", "start=", "demand" }, args);
    }

    [Fact]
    public void Create_QuotesAreHandledByArgumentList()
    {
        var args = ScArgumentBuilder.Create("MyWorker", @"C:\Program Files\worker.exe", "--port 5000");

        Assert.All(args, arg => Assert.DoesNotContain('"', arg));
        Assert.Contains(@"C:\Program Files\worker.exe --port 5000", args);
    }

    [Fact]
    public void Delete_And_Start_And_Stop_AreSingleVerbCommands()
    {
        Assert.Equal(new[] { "delete", "MyWorker" }, ScArgumentBuilder.Delete("MyWorker"));
        Assert.Equal(new[] { "start", "MyWorker" }, ScArgumentBuilder.Start("MyWorker"));
        Assert.Equal(new[] { "stop", "MyWorker" }, ScArgumentBuilder.Stop("MyWorker"));
    }
}
