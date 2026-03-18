using UnityCli.Cli.Models;
using UnityCli.Cli.Services;
using UnityCli.Protocol;

namespace UnityCli.Cli.Tests;

public sealed class CliArgumentParserTests
{
    [Fact]
    public void Parse_RunTestsModeAndGlobalJson()
    {
        var parsed = CliArgumentParser.Parse(["--json", "run-tests", "--mode", "play"]);

        Assert.Equal(CommandKind.RunTests, parsed.Kind);
        Assert.True(parsed.JsonOutput);
        Assert.Equal("play", parsed.TestMode);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_AcceptsJsonFlagAfterCommand()
    {
        var parsed = CliArgumentParser.Parse(["status", "--json"]);

        Assert.Equal(CommandKind.Status, parsed.Kind);
        Assert.True(parsed.JsonOutput);
    }

    [Fact]
    public void Parse_RawRequiresPayload()
    {
        var ex = Assert.Throws<CliUsageException>(() => CliArgumentParser.Parse(["raw"]));
        Assert.Contains("payload", ex.Message);
    }

    [Fact]
    public void Parse_CompileUsesBatchDefaultTimeout()
    {
        var parsed = CliArgumentParser.Parse(["compile"]);

        Assert.Equal(CommandKind.Compile, parsed.Kind);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_RunTestsKeepsExplicitTimeout()
    {
        var parsed = CliArgumentParser.Parse(["run-tests", "--mode", "edit", "--timeout-ms", "45000"]);

        Assert.Equal(CommandKind.RunTests, parsed.Kind);
        Assert.Equal(45_000, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_AssetDeleteRequiresForce()
    {
        var ex = Assert.Throws<CliUsageException>(() => CliArgumentParser.Parse(["asset", "delete", "--path", "Assets/Test.asset"]));

        Assert.Contains("--force", ex.Message);
    }

    [Fact]
    public void Parse_AssetFindUsesBatchDefaultTimeout()
    {
        var parsed = CliArgumentParser.Parse(["asset", "find", "--name", "Sample", "--folder", "Assets"]);

        Assert.Equal(CommandKind.AssetFind, parsed.Kind);
        Assert.Equal("Sample", parsed.AssetName);
        Assert.Equal("Assets", parsed.AssetFolder);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_AssetTypes_UsesBatchDefaultTimeout()
    {
        var parsed = CliArgumentParser.Parse(["asset", "types"]);

        Assert.Equal(CommandKind.AssetTypes, parsed.Kind);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_RawJsonOptionStillAcceptsPayload()
    {
        var parsed = CliArgumentParser.Parse(["raw", "--json", "{\"command\":\"status\",\"arguments\":{}}"]);

        Assert.Equal(CommandKind.Raw, parsed.Kind);
        Assert.Equal("{\"command\":\"status\",\"arguments\":{}}", parsed.RawJson);
    }

    [Fact]
    public void Parse_AssetCreateMaterial_UsesBatchTimeout()
    {
        var parsed = CliArgumentParser.Parse(["asset", "create", "--type", "material", "--path", "Assets/Test/Example"]);

        Assert.Equal(CommandKind.AssetCreate, parsed.Kind);
        Assert.Equal("material", parsed.AssetCreateType);
        Assert.Equal("Assets/Test/Example", parsed.AssetPath);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_AssetCreateScriptableObject_RequiresScriptOrTypeName()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["asset", "create", "--type", "scriptable-object", "--path", "Assets/Test/Example.asset"]));

        Assert.Contains("--script", ex.Message);
        Assert.Contains("--type-name", ex.Message);
    }

    [Fact]
    public void Parse_AssetCreate_AcceptsInitialMapAndForce()
    {
        var parsed = CliArgumentParser.Parse([
            "asset",
            "create",
            "--type", "input-actions",
            "--path", "Assets/Input/NewActions",
            "--initial-map", "Gameplay",
            "--force"
        ]);

        Assert.Equal(CommandKind.AssetCreate, parsed.Kind);
        Assert.Equal("input-actions", parsed.AssetCreateType);
        Assert.Equal("Gameplay", parsed.AssetInitialMap);
        Assert.True(parsed.Force);
    }

    [Fact]
    public void Parse_AssetCreateScriptableObject_AcceptsTypeNameAndDataJson()
    {
        var parsed = CliArgumentParser.Parse([
            "asset",
            "create",
            "--type", "scriptable-object",
            "--path", "Assets/Data/Config",
            "--type-name", "MyNamespace.ConfigAsset",
            "--data-json", "{\"title\":\"Hello\"}"
        ]);

        Assert.Equal("scriptable-object", parsed.AssetCreateType);
        Assert.Equal("MyNamespace.ConfigAsset", parsed.AssetTypeName);
        Assert.Equal("{\"title\":\"Hello\"}", parsed.AssetDataJson);
    }

    [Fact]
    public void Parse_AssetCreateAnimatorOverrideController_RequiresBaseController()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["asset", "create", "--type", "animator-override-controller", "--path", "Assets/Test/Override"]));

        Assert.Contains("--base-controller", ex.Message);
    }

    [Fact]
    public void Parse_AssetCreateRenderTexture_AcceptsSizeOptions()
    {
        var parsed = CliArgumentParser.Parse([
            "asset",
            "create",
            "--type", "render-texture",
            "--path", "Assets/Textures/Target",
            "--width", "2048",
            "--height", "1024",
            "--depth", "0"
        ]);

        Assert.Equal("render-texture", parsed.AssetCreateType);
        Assert.Equal(2048, parsed.AssetWidth);
        Assert.Equal(1024, parsed.AssetHeight);
        Assert.Equal(0, parsed.AssetDepth);
    }

    [Fact]
    public void Parse_AssetCreate_AcceptsExtensionTypeId()
    {
        var parsed = CliArgumentParser.Parse([
            "asset",
            "create",
            "--type", "batch-note",
            "--path", "Assets/Test/BatchNote"
        ]);

        Assert.Equal("batch-note", parsed.AssetCreateType);
    }

    [Fact]
    public void Parse_AssetCreate_AcceptsExtensionCustomOptions()
    {
        var parsed = CliArgumentParser.Parse([
            "asset",
            "create",
            "--type", "batch-note",
            "--path", "Assets/Test/BatchNote",
            "--title", "Hello",
            "--count", "7",
            "--enabled"
        ]);

        var args = ProtocolJson.Deserialize<AssetCreateArgs>(parsed.ToEnvelope().argumentsJson);
        Assert.NotNull(args);
        Assert.Equal("batch-note", args.type);
        Assert.Equal("{\"title\":\"Hello\",\"count\":7,\"enabled\":true}", args.optionsJson);
    }

    [Fact]
    public void Parse_AssetCreate_BuiltInRejectsUnknownCustomOption()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse([
                "asset",
                "create",
                "--type", "material",
                "--path", "Assets/Test/Material",
                "--widht", "512"
            ]));

        Assert.Contains("--widht", ex.Message);
    }

    [Fact]
    public void Parse_PrefabInspect_UsesBatchTimeout()
    {
        var parsed = CliArgumentParser.Parse([
            "prefab",
            "inspect",
            "--path", "Assets/Prefabs/Enemy"
        ]);

        Assert.Equal(CommandKind.PrefabInspect, parsed.Kind);
        Assert.Equal("Assets/Prefabs/Enemy", parsed.PrefabPath);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_PrefabCreate_RequiresExactlyOneSpecSource()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse([
                "prefab",
                "create",
                "--path", "Assets/Prefabs/Enemy"
            ]));

        Assert.Contains("--spec-file", ex.Message);
        Assert.Contains("--spec-json", ex.Message);
    }

    [Fact]
    public void Parse_PrefabCreate_AcceptsSpecJsonAndForce()
    {
        var parsed = CliArgumentParser.Parse([
            "prefab",
            "create",
            "--path", "Assets/Prefabs/Enemy",
            "--spec-json", "{\"version\":1}",
            "--force"
        ]);

        Assert.Equal(CommandKind.PrefabCreate, parsed.Kind);
        Assert.Equal("Assets/Prefabs/Enemy", parsed.PrefabPath);
        Assert.Equal("{\"version\":1}", parsed.PrefabSpecJson);
        Assert.True(parsed.Force);
    }

    [Fact]
    public void Parse_PrefabPatch_AcceptsSpecFile()
    {
        var parsed = CliArgumentParser.Parse([
            "prefab",
            "patch",
            "--path", "Assets/Prefabs/Enemy.prefab",
            "--spec-file", "Specs/enemy.json"
        ]);

        Assert.Equal(CommandKind.PrefabPatch, parsed.Kind);
        Assert.Equal("Assets/Prefabs/Enemy.prefab", parsed.PrefabPath);
        Assert.Equal("Specs/enemy.json", parsed.PrefabSpecFile);
    }
}
