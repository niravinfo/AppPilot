using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppPilot.AdminBroker;
using Xunit;

namespace AppPilot.Tests;

public class BrokerProtocolTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Request_RoundTripsThroughJson()
    {
        var request = new BrokerRequest
        {
            Operation = BrokerOperation.Install,
            Name = "MyWorker",
            DisplayName = "My Worker",
            ExecutablePath = @"C:\apps\worker.exe",
            Arguments = "--port 5000"
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var result = JsonSerializer.Deserialize<BrokerRequest>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(BrokerOperation.Install, result.Operation);
        Assert.Equal("MyWorker", result.Name);
        Assert.Equal("My Worker", result.DisplayName);
        Assert.Equal(@"C:\apps\worker.exe", result.ExecutablePath);
        Assert.Equal("--port 5000", result.Arguments);
    }

    [Fact]
    public void UnknownOperation_IsRejected()
    {
        const string json = """
            {"Operation":99,"Name":"x","DisplayName":"","ExecutablePath":"","Arguments":""}
            """;

        var result = JsonSerializer.Deserialize<BrokerRequest>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal((BrokerOperation)99, result.Operation);
        Assert.False(Enum.IsDefined(result.Operation));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a\"b")]
    [InlineData("a\\b")]
    [InlineData("a/b")]
    [InlineData("a\nb")]
    [InlineData("a;b")]
    public void UnsafeServiceName_IsRejected(string name)
    {
        Assert.False(BrokerProtocol.IsValidServiceName(name));
    }

    [Fact]
    public void TooLongServiceName_IsRejected()
    {
        Assert.False(BrokerProtocol.IsValidServiceName(new string('a', 81)));
    }

    [Fact]
    public void SafeServiceName_IsAccepted()
    {
        Assert.True(BrokerProtocol.IsValidServiceName("My.Service_1-2"));
    }

    [Fact]
    public void PipePrefix_IsNamespaced()
    {
        Assert.Equal("AppPilot.AdminBroker.", BrokerProtocol.PipePrefix);
    }
}
