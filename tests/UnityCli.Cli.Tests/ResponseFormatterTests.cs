using UnityCli.Cli.Models;
using UnityCli.Cli.Services;
using UnityCli.Protocol;

namespace UnityCli.Cli.Tests;

public sealed class ResponseFormatterTests
{
    [Fact]
    public void Format_PrettyTextOutput_PreservesUnicodeCharacters()
    {
        var parsed = new ParsedCommand(CommandKind.Refresh);
        var response = ResponseEnvelope.Success(
            requestId: "req-1",
            target: "target-1",
            dataJson: "{\"message\":\"AssetDatabase.Refresh 완료\"}",
            durationMs: 12);

        var text = ResponseFormatter.Format(parsed, response);

        Assert.Contains("AssetDatabase.Refresh 완료", text);
        Assert.DoesNotContain("\\uC644\\uB8CC", text);
    }

    [Fact]
    public void Format_CompactOutput_ReturnsPayloadOnlyJson()
    {
        var response = ResponseEnvelope.Success(
            requestId: "req-1",
            target: "target-1",
            dataJson: "{\"path\":\"Assets/Prefab.prefab\",\"type\":\"GameObject\"}",
            durationMs: 42);

        var text = ResponseFormatter.Format(OutputMode.Compact, response);

        Assert.Equal("{\"path\":\"Assets/Prefab.prefab\",\"type\":\"GameObject\"}", text);
    }

    [Fact]
    public void Format_CompactOutput_WithoutPayload_ReturnsEmptyObject()
    {
        var response = ResponseEnvelope.Success(
            requestId: "req-1",
            target: "target-1",
            dataJson: null,
            durationMs: 42);

        var text = ResponseFormatter.Format(OutputMode.Compact, response);

        Assert.Equal("{}", text);
    }

    [Fact]
    public void Format_CompactError_ReturnsReducedErrorJson()
    {
        var response = ResponseEnvelope.Failure(
            requestId: "req-1",
            target: "target-1",
            code: "LIVE_UNAVAILABLE",
            message: "Bridge가 아직 준비되지 않았습니다.",
            retryable: true,
            details: "{\"hint\":\"retry\"}");

        var text = ResponseFormatter.Format(OutputMode.Compact, response);

        Assert.Equal("{\"error\":\"LIVE_UNAVAILABLE\",\"message\":\"Bridge가 아직 준비되지 않았습니다.\"}", text);
    }
}
