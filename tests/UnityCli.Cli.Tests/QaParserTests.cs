using System.Text.Json;
using UnityCli.Cli.Models;
using UnityCli.Cli.Services;
using UnityCli.Protocol;

namespace UnityCli.Cli.Tests;

public sealed class QaParserTests
{
    [Fact]
    public void Parse_QaClick_WithQaId()
    {
        var parsed = CliArgumentParser.Parse(["qa", "click", "--qa-id", "test-btn"]);

        Assert.Equal(CommandKind.QaClick, parsed.Kind);
        Assert.Equal("test-btn", parsed.QaId);
        Assert.Null(parsed.QaTarget);
    }

    [Fact]
    public void Parse_QaClick_WithTarget()
    {
        var parsed = CliArgumentParser.Parse(["qa", "click", "--target", "Canvas/Panel/Button"]);

        Assert.Equal(CommandKind.QaClick, parsed.Kind);
        Assert.Null(parsed.QaId);
        Assert.Equal("Canvas/Panel/Button", parsed.QaTarget);
    }

    [Fact]
    public void Parse_QaTap_AcceptsCoordinates()
    {
        var parsed = CliArgumentParser.Parse(["qa", "tap", "--x", "100", "--y", "200"]);

        Assert.Equal(CommandKind.QaTap, parsed.Kind);
        Assert.Equal(100, parsed.QaTapX);
        Assert.Equal(200, parsed.QaTapY);
    }

    [Fact]
    public void Parse_QaSwipe_AcceptsDuration()
    {
        var parsed = CliArgumentParser.Parse(["qa", "swipe", "--from", "100,200", "--to", "300,400", "--duration", "500"]);

        Assert.Equal(CommandKind.QaSwipe, parsed.Kind);
        Assert.Equal("100,200", parsed.QaSwipeFrom);
        Assert.Equal("300,400", parsed.QaSwipeTo);
        Assert.Equal(500, parsed.QaSwipeDuration);
    }

    [Fact]
    public void Parse_QaSwipe_UsesDefaultDuration()
    {
        var parsed = CliArgumentParser.Parse(["qa", "swipe", "--from", "100,200", "--to", "300,400"]);

        Assert.Equal(CommandKind.QaSwipe, parsed.Kind);
        Assert.Equal(ProtocolConstants.DefaultQaSwipeDurationMs, parsed.QaSwipeDuration);
    }

    [Fact]
    public void Parse_QaKey_AcceptsKeyName()
    {
        var parsed = CliArgumentParser.Parse(["qa", "key", "--key", "Space"]);

        Assert.Equal(CommandKind.QaKey, parsed.Kind);
        Assert.Equal("Space", parsed.QaKeyName);
    }

    [Fact]
    public void Parse_QaWait_AcceptsMilliseconds()
    {
        var parsed = CliArgumentParser.Parse(["qa", "wait", "--ms", "2000"]);

        Assert.Equal(CommandKind.QaWait, parsed.Kind);
        Assert.Equal(2000, parsed.QaWaitMs);
    }

    [Fact]
    public void Parse_QaWaitUntil_AcceptsSceneAndTimeout()
    {
        var parsed = CliArgumentParser.Parse(["qa", "wait-until", "--scene", "GameScene", "--timeout", "5000"]);

        Assert.Equal(CommandKind.QaWaitUntil, parsed.Kind);
        Assert.Equal("GameScene", parsed.QaWaitScene);
        Assert.Equal(5000, parsed.QaWaitTimeout);
    }

    [Fact]
    public void Parse_QaWaitUntil_AcceptsLogContains()
    {
        var parsed = CliArgumentParser.Parse(["qa", "wait-until", "--log-contains", "Loading"]);

        Assert.Equal(CommandKind.QaWaitUntil, parsed.Kind);
        Assert.Equal("Loading", parsed.QaWaitLogContains);
    }

    [Fact]
    public void Parse_QaWaitUntil_AcceptsObjectExists()
    {
        var parsed = CliArgumentParser.Parse(["qa", "wait-until", "--object-exists", "start-btn"]);

        Assert.Equal(CommandKind.QaWaitUntil, parsed.Kind);
        Assert.Equal("start-btn", parsed.QaWaitObjectExists);
    }

    [Fact]
    public void Parse_Qa_WithoutSubcommand_ThrowsUsage()
    {
        Assert.Throws<CliUsageException>(() => CliArgumentParser.Parse(["qa"]));
    }

    [Fact]
    public void Parse_Qa_InvalidSubcommand_ThrowsUsage()
    {
        var ex = Assert.Throws<CliUsageException>(() => CliArgumentParser.Parse(["qa", "invalid"]));

        Assert.Contains("invalid", ex.Message);
    }

    [Fact]
    public void Parse_QaClick_ToEnvelope_UsesQaClickCommand()
    {
        var parsed = CliArgumentParser.Parse(["qa", "click", "--qa-id", "test-btn"]);

        var envelope = parsed.ToEnvelope();
        var arguments = ParseArguments(envelope.argumentsJson);

        Assert.Equal(ProtocolConstants.CommandQaClick, envelope.command);
        Assert.Equal("test-btn", arguments.GetProperty("qaId").GetString());
    }

    [Fact]
    public void Parse_QaTap_ToEnvelope_UsesQaTapCommand()
    {
        var parsed = CliArgumentParser.Parse(["qa", "tap", "--x", "100", "--y", "200"]);

        var envelope = parsed.ToEnvelope();
        var arguments = ParseArguments(envelope.argumentsJson);

        Assert.Equal(ProtocolConstants.CommandQaTap, envelope.command);
        Assert.Equal(100, arguments.GetProperty("x").GetInt32());
        Assert.Equal(200, arguments.GetProperty("y").GetInt32());
    }

    [Fact]
    public void Parse_QaSwipe_ToEnvelope_UsesQaSwipeCommand()
    {
        var parsed = CliArgumentParser.Parse(["qa", "swipe", "--from", "100,200", "--to", "300,400", "--duration", "500"]);

        var envelope = parsed.ToEnvelope();
        var arguments = ParseArguments(envelope.argumentsJson);

        Assert.Equal(ProtocolConstants.CommandQaSwipe, envelope.command);
        Assert.Equal(100, arguments.GetProperty("fromX").GetInt32());
        Assert.Equal(200, arguments.GetProperty("fromY").GetInt32());
        Assert.Equal(300, arguments.GetProperty("toX").GetInt32());
        Assert.Equal(400, arguments.GetProperty("toY").GetInt32());
        Assert.Equal(500, arguments.GetProperty("durationMs").GetInt32());
    }

    [Fact]
    public void Parse_QaKey_ToEnvelope_UsesQaKeyCommand()
    {
        var parsed = CliArgumentParser.Parse(["qa", "key", "--key", "Space"]);

        var envelope = parsed.ToEnvelope();
        var arguments = ParseArguments(envelope.argumentsJson);

        Assert.Equal(ProtocolConstants.CommandQaKey, envelope.command);
        Assert.Equal("Space", arguments.GetProperty("key").GetString());
    }

    [Fact]
    public void Parse_QaWaitUntil_ToEnvelope_UsesQaWaitUntilCommand()
    {
        var parsed = CliArgumentParser.Parse([
            "qa", "wait-until",
            "--scene", "GameScene",
            "--log-contains", "Loading",
            "--object-exists", "start-btn",
            "--timeout", "5000"
        ]);

        var envelope = parsed.ToEnvelope();
        var arguments = ParseArguments(envelope.argumentsJson);

        Assert.Equal(ProtocolConstants.CommandQaWaitUntil, envelope.command);
        Assert.Equal("GameScene", arguments.GetProperty("scene").GetString());
        Assert.Equal("Loading", arguments.GetProperty("logContains").GetString());
        Assert.Equal("start-btn", arguments.GetProperty("objectExists").GetString());
        Assert.Equal(5000, arguments.GetProperty("timeoutMs").GetInt32());
    }

    [Fact]
    public void BuildHelpText_IncludesQaCommands()
    {
        var helpText = CliCommandMetadata.BuildHelpText();

        Assert.Contains("qa click", helpText);
        Assert.Contains("qa tap", helpText);
        Assert.Contains("qa swipe", helpText);
        Assert.Contains("qa key", helpText);
        Assert.Contains("qa wait --ms <int>", helpText);
        Assert.Contains("qa wait-until", helpText);
    }

    private static JsonElement ParseArguments(string argumentsJson)
    {
        using var document = JsonDocument.Parse(argumentsJson);
        return document.RootElement.Clone();
    }
}
