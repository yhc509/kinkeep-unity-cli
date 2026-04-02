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
}
