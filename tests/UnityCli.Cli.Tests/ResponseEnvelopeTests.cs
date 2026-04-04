using System.Text.Json;
using UnityCli.Protocol;

namespace UnityCli.Cli.Tests;

public sealed class ResponseEnvelopeTests
{
    [Fact]
    public void Success_WithDataJson_LeavesDataNullUntilEnsureData()
    {
        var response = ResponseEnvelope.Success(
            requestId: "req-1",
            target: "target-1",
            dataJson: "{\"message\":\"hello\"}",
            durationMs: 12);

        Assert.Null(response.data);

        response.EnsureData();

        var data = Assert.IsType<JsonElement>(response.data);
        Assert.Equal("hello", data.GetProperty("message").GetString());
    }

    [Fact]
    public void Success_WithDirectData_PreservesData()
    {
        var payload = new { message = "hello" };
        var response = ResponseEnvelope.Success(
            requestId: "req-1",
            target: "target-1",
            dataJson: "{\"message\":\"legacy\"}",
            durationMs: 12,
            data: payload);

        Assert.Same(payload, response.data);
    }

    [Fact]
    public void EnsureData_WithBridgeStyleResponse_PopulatesDataFromDataJson()
    {
        var response = new ResponseEnvelope
        {
            requestId = "req-1",
            target = "target-1",
            status = ProtocolConstants.StatusSuccess,
            durationMs = 12,
            dataJson = "{\"message\":\"hello\"}",
            transport = ProtocolConstants.TransportLive,
        };

        response.EnsureData();

        var data = Assert.IsType<JsonElement>(response.data);
        Assert.Equal("hello", data.GetProperty("message").GetString());
    }
}
