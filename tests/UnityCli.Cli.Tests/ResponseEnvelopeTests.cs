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

    [Fact]
    public void EnsureData_WithMutationWarnings_PreservesWarningsArray()
    {
        var payload = new PrefabMutationPayload
        {
            patched = false,
            warnings = ["Unknown key: m_LocalScal.x"],
        };
        var response = ResponseEnvelope.Success(
            requestId: "req-1",
            target: "target-1",
            dataJson: ProtocolJson.Serialize(payload),
            durationMs: 12);

        response.EnsureData();

        var data = Assert.IsType<JsonElement>(response.data);
        Assert.False(data.GetProperty("patched").GetBoolean());
        Assert.Equal("Unknown key: m_LocalScal.x", data.GetProperty("warnings")[0].GetString());
    }

    [Fact]
    public void EnsureData_WithScreenshotPayload_PreservesCoordinateMetadata()
    {
        var payload = new ScreenshotPayload
        {
            savedPath = "/tmp/shot.png",
            width = 960,
            height = 540,
            screenWidth = 1920,
            screenHeight = 1080,
            coordinateOrigin = "bottom-left",
            imageOrigin = "top-left",
            fileSizeBytes = 1234,
        };
        var response = ResponseEnvelope.Success(
            requestId: "req-1",
            target: "target-1",
            dataJson: ProtocolJson.Serialize(payload),
            durationMs: 12);

        response.EnsureData();

        var data = Assert.IsType<JsonElement>(response.data);
        Assert.Equal("/tmp/shot.png", data.GetProperty("savedPath").GetString());
        Assert.Equal(960, data.GetProperty("width").GetInt32());
        Assert.Equal(540, data.GetProperty("height").GetInt32());
        Assert.Equal(1920, data.GetProperty("screenWidth").GetInt32());
        Assert.Equal(1080, data.GetProperty("screenHeight").GetInt32());
        Assert.Equal("bottom-left", data.GetProperty("coordinateOrigin").GetString());
        Assert.Equal("top-left", data.GetProperty("imageOrigin").GetString());
        Assert.Equal(1234, data.GetProperty("fileSizeBytes").GetInt64());
    }
}
