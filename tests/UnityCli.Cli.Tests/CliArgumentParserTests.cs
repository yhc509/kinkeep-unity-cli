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
    public void Parse_MaterialInfo_RequiresPath()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["material", "info"]));

        Assert.Contains("--path", ex.Message);
    }

    [Fact]
    public void Parse_MaterialInfo_AcceptsPath()
    {
        var parsed = CliArgumentParser.Parse([
            "material", "info",
            "--path", "Assets/Materials/Wood.mat"
        ]);

        Assert.Equal(CommandKind.MaterialInfo, parsed.Kind);
        Assert.Equal("Assets/Materials/Wood.mat", parsed.MaterialPath);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_MaterialSet_RequiresPropertyOrTexture()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse([
                "material", "set",
                "--path", "Assets/Materials/Wood.mat"
            ]));

        Assert.Contains("--property", ex.Message);
    }

    [Fact]
    public void Parse_MaterialSet_AcceptsPropertyAndValue()
    {
        var parsed = CliArgumentParser.Parse([
            "material", "set",
            "--path", "Assets/Materials/Wood.mat",
            "--property", "_Color",
            "--value", "1,0,0,1"
        ]);

        Assert.Equal(CommandKind.MaterialSet, parsed.Kind);
        Assert.Equal("Assets/Materials/Wood.mat", parsed.MaterialPath);
        Assert.Equal("_Color", parsed.MaterialProperty);
        Assert.Equal("1,0,0,1", parsed.MaterialValue);
    }

    [Fact]
    public void Parse_MaterialSet_AcceptsTextureAndAsset()
    {
        var parsed = CliArgumentParser.Parse([
            "material", "set",
            "--path", "Assets/Materials/Wood.mat",
            "--texture", "_MainTex",
            "--asset", "Assets/Textures/wood.png"
        ]);

        Assert.Equal(CommandKind.MaterialSet, parsed.Kind);
        Assert.Equal("_MainTex", parsed.MaterialTexture);
        Assert.Equal("Assets/Textures/wood.png", parsed.MaterialTextureAsset);
    }

    [Fact]
    public void Parse_MaterialSet_UsesBatchTimeout()
    {
        var parsed = CliArgumentParser.Parse([
            "material", "set",
            "--path", "Assets/Materials/Wood.mat",
            "--property", "_Metallic",
            "--value", "0.8"
        ]);

        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_Material_UnknownSubcommandThrows()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["material", "delete"]));

        Assert.Contains("delete", ex.Message);
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

    [Fact]
    public void Parse_SceneOpen_UsesBatchTimeout()
    {
        var parsed = CliArgumentParser.Parse([
            "scene",
            "open",
            "--path", "Assets/Scenes/System"
        ]);

        Assert.Equal(CommandKind.SceneOpen, parsed.Kind);
        Assert.Equal("Assets/Scenes/System", parsed.ScenePath);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_SceneInspect_AcceptsWithValues()
    {
        var parsed = CliArgumentParser.Parse([
            "scene",
            "inspect",
            "--path", "Assets/Scenes/System.unity",
            "--with-values"
        ]);

        Assert.Equal(CommandKind.SceneInspect, parsed.Kind);
        Assert.Equal("Assets/Scenes/System.unity", parsed.ScenePath);
        Assert.True(parsed.SceneWithValues);
    }

    [Fact]
    public void Parse_ScenePatch_RequiresExactlyOneSpecSource()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse([
                "scene",
                "patch",
                "--path", "Assets/Scenes/System.unity"
            ]));

        Assert.Contains("--spec-file", ex.Message);
        Assert.Contains("--spec-json", ex.Message);
    }

    [Fact]
    public void Parse_ScenePatch_DeleteOperationRequiresForce()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse([
                "scene",
                "patch",
                "--path", "Assets/Scenes/System.unity",
                "--spec-json", "{\"version\":1,\"operations\":[{\"op\":\"delete-gameobject\",\"target\":\"/Enemy[0]\"}]}"
            ]));

        Assert.Contains("--force", ex.Message);
    }

    [Fact]
    public void Parse_ScenePatch_AcceptsSpecJsonAndForce()
    {
        var parsed = CliArgumentParser.Parse([
            "scene",
            "patch",
            "--path", "Assets/Scenes/System.unity",
            "--spec-json", "{\"version\":1,\"operations\":[{\"op\":\"add-gameobject\",\"parent\":\"/\",\"node\":{\"name\":\"SpawnPoint\"}}]}",
            "--force"
        ]);

        Assert.Equal(CommandKind.ScenePatch, parsed.Kind);
        Assert.Equal("Assets/Scenes/System.unity", parsed.ScenePath);
        Assert.Equal("{\"version\":1,\"operations\":[{\"op\":\"add-gameobject\",\"parent\":\"/\",\"node\":{\"name\":\"SpawnPoint\"}}]}", parsed.SceneSpecJson);
        Assert.True(parsed.Force);
    }

    [Fact]
    public void Parse_ScenePatch_ToEnvelope_UsesScenePatchArgs()
    {
        var parsed = CliArgumentParser.Parse([
            "scene",
            "patch",
            "--path", "Assets/Scenes/System.unity",
            "--spec-json", "{\"version\":1,\"operations\":[{\"op\":\"add-gameobject\",\"parent\":\"/\",\"node\":{\"name\":\"SpawnPoint\"}}]}",
            "--force"
        ]);

        var args = ProtocolJson.Deserialize<ScenePatchArgs>(parsed.ToEnvelope().argumentsJson);
        Assert.NotNull(args);
        Assert.Equal("Assets/Scenes/System.unity", args.path);
        Assert.True(args.force);
        Assert.Contains("add-gameobject", args.specJson);
    }

    [Fact]
    public void Parse_SceneAddObject_RequiresPathAndName()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["scene", "add-object", "--path", "Assets/Scenes/Test.unity"]));

        Assert.Contains("--name", ex.Message);
    }

    [Fact]
    public void Parse_SceneAddObject_AcceptsOptions()
    {
        var parsed = CliArgumentParser.Parse([
            "scene", "add-object",
            "--path", "Assets/Scenes/Test.unity",
            "--parent", "/Root[0]",
            "--name", "SpawnPoint",
            "--components", "Rigidbody,BoxCollider"
        ]);

        Assert.Equal(CommandKind.SceneAddObject, parsed.Kind);
        Assert.Equal("Assets/Scenes/Test.unity", parsed.ScenePath);
        Assert.Equal("/Root[0]", parsed.SceneParent);
        Assert.Equal("SpawnPoint", parsed.SceneObjectName);
        Assert.Equal("Rigidbody,BoxCollider", parsed.SceneComponents);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_SceneAddObject_ToEnvelope_UsesScenePatch()
    {
        var parsed = CliArgumentParser.Parse([
            "scene", "add-object",
            "--path", "Assets/Scenes/Test.unity",
            "--name", "SpawnPoint"
        ]);

        var envelope = parsed.ToEnvelope();
        Assert.Equal(ProtocolConstants.CommandScenePatch, envelope.command);
        Assert.Contains("add-gameobject", envelope.argumentsJson);
        Assert.Contains("SpawnPoint", envelope.argumentsJson);
        var args = ProtocolJson.Deserialize<ScenePatchArgs>(envelope.argumentsJson);
        Assert.NotNull(args);
        Assert.Contains("\"parent\":\"/\"", args.specJson);
    }

    [Fact]
    public void Parse_SceneSetTransform_RequiresTarget()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["scene", "set-transform", "--path", "Assets/Scenes/Test.unity"]));

        Assert.Contains("--target", ex.Message);
    }

    [Fact]
    public void Parse_SceneSetTransform_RequiresAtLeastOneTransformProp()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["scene", "set-transform", "--path", "Assets/Scenes/Test.unity", "--target", "/Root[0]"]));

        Assert.Contains("--position", ex.Message);
    }

    [Fact]
    public void Parse_SceneSetTransform_AcceptsPosition()
    {
        var parsed = CliArgumentParser.Parse([
            "scene", "set-transform",
            "--path", "Assets/Scenes/Test.unity",
            "--target", "/Root[0]/Player[0]",
            "--position", "1,2,3"
        ]);

        Assert.Equal(CommandKind.SceneSetTransform, parsed.Kind);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
        var envelope = parsed.ToEnvelope();
        Assert.Equal(ProtocolConstants.CommandScenePatch, envelope.command);
        Assert.Contains("modify-gameobject", envelope.argumentsJson);
        var args = ProtocolJson.Deserialize<ScenePatchArgs>(envelope.argumentsJson);
        Assert.NotNull(args);
        Assert.Contains("localPosition", args.specJson);
    }

    [Fact]
    public void Parse_SceneAddComponent_RequiresType()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["scene", "add-component", "--path", "Assets/Scenes/Test.unity", "--target", "/Root[0]"]));

        Assert.Contains("--type", ex.Message);
    }

    [Fact]
    public void Parse_SceneAddComponent_AcceptsTypeAndValues()
    {
        var parsed = CliArgumentParser.Parse([
            "scene", "add-component",
            "--path", "Assets/Scenes/Test.unity",
            "--target", "/Root[0]",
            "--type", "BoxCollider",
            "--values", "{\"center\":{\"x\":0,\"y\":0.5,\"z\":0}}"
        ]);

        Assert.Equal(CommandKind.SceneAddComponent, parsed.Kind);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
        var envelope = parsed.ToEnvelope();
        Assert.Contains("add-component", envelope.argumentsJson);
        Assert.Contains("BoxCollider", envelope.argumentsJson);
        var args = ProtocolJson.Deserialize<ScenePatchArgs>(envelope.argumentsJson);
        Assert.NotNull(args);
        Assert.Contains("\"component\":", args.specJson);
    }

    [Fact]
    public void Parse_SceneRemoveComponent_RequiresForce()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse([
                "scene", "remove-component",
                "--path", "Assets/Scenes/Test.unity",
                "--target", "/Root[0]",
                "--type", "BoxCollider"
            ]));

        Assert.Contains("--force", ex.Message);
    }

    [Fact]
    public void Parse_SceneRemoveComponent_AcceptsForce()
    {
        var parsed = CliArgumentParser.Parse([
            "scene", "remove-component",
            "--path", "Assets/Scenes/Test.unity",
            "--target", "/Root[0]",
            "--type", "BoxCollider",
            "--force"
        ]);

        Assert.Equal(CommandKind.SceneRemoveComponent, parsed.Kind);
        Assert.True(parsed.Force);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
        var envelope = parsed.ToEnvelope();
        Assert.Contains("remove-component", envelope.argumentsJson);
        var args = ProtocolJson.Deserialize<ScenePatchArgs>(envelope.argumentsJson);
        Assert.NotNull(args);
        Assert.Contains("componentType", args.specJson);
    }

    [Fact]
    public void Parse_SceneSetTransform_InvalidPositionFormatThrows()
    {
        var parsed = CliArgumentParser.Parse([
            "scene", "set-transform",
            "--path", "Assets/Scenes/Test.unity",
            "--target", "/Root[0]",
            "--position", "1,2"
        ]);

        var ex = Assert.Throws<CliUsageException>(() => parsed.ToEnvelope());
        Assert.Contains("x,y,z", ex.Message);
    }

    [Fact]
    public void Parse_Screenshot_RequiresViewOrCamera()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["screenshot"]));

        Assert.Contains("--view", ex.Message);
        Assert.Contains("--camera", ex.Message);
    }

    [Fact]
    public void Parse_Screenshot_AcceptsGameView()
    {
        var parsed = CliArgumentParser.Parse(["screenshot", "--view", "game"]);

        Assert.Equal(CommandKind.Screenshot, parsed.Kind);
        Assert.Equal("game", parsed.ScreenshotView);
        Assert.Equal(ProtocolConstants.DefaultLiveTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_Screenshot_AcceptsSceneViewWithSize()
    {
        var parsed = CliArgumentParser.Parse([
            "screenshot", "--view", "scene",
            "--width", "1920", "--height", "1080",
            "--path", "/tmp/test.png"
        ]);

        Assert.Equal(CommandKind.Screenshot, parsed.Kind);
        Assert.Equal("scene", parsed.ScreenshotView);
        Assert.Equal(1920, parsed.ScreenshotWidth);
        Assert.Equal(1080, parsed.ScreenshotHeight);
        Assert.Equal("/tmp/test.png", parsed.ScreenshotPath);
    }

    [Fact]
    public void Parse_Screenshot_AcceptsCameraName()
    {
        var parsed = CliArgumentParser.Parse([
            "screenshot", "--camera", "Main Camera",
            "--path", "/tmp/render.png"
        ]);

        Assert.Equal(CommandKind.Screenshot, parsed.Kind);
        Assert.Equal("Main Camera", parsed.ScreenshotCamera);
        Assert.Equal("/tmp/render.png", parsed.ScreenshotPath);
    }

    [Fact]
    public void Parse_Screenshot_RejectsInvalidView()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["screenshot", "--view", "invalid"]));

        Assert.Contains("game", ex.Message);
        Assert.Contains("scene", ex.Message);
    }

    [Fact]
    public void Parse_Screenshot_RejectsBothViewAndCamera()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["screenshot", "--view", "game", "--camera", "Main Camera"]));

        Assert.Contains("--view", ex.Message);
        Assert.Contains("--camera", ex.Message);
    }

    [Fact]
    public void Parse_Screenshot_UsesLiveTimeout()
    {
        var parsed = CliArgumentParser.Parse(["screenshot", "--view", "game"]);

        Assert.Equal(ProtocolConstants.DefaultLiveTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_Execute_RequiresCodeOrFile()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["execute", "--force"]));

        Assert.Contains("--code", ex.Message);
        Assert.Contains("--file", ex.Message);
    }

    [Fact]
    public void Parse_Execute_RequiresForce()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["execute", "--code", "Debug.Log(42);"]));

        Assert.Contains("--force", ex.Message);
    }

    [Fact]
    public void Parse_Execute_AcceptsCodeAndForce()
    {
        var parsed = CliArgumentParser.Parse([
            "execute",
            "--code", "Debug.Log(42);",
            "--force"
        ]);

        Assert.Equal(CommandKind.ExecuteCode, parsed.Kind);
        Assert.Equal("Debug.Log(42);", parsed.ExecuteCodeSnippet);
        Assert.True(parsed.Force);
        Assert.Equal(ProtocolConstants.DefaultLiveTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_Execute_AcceptsFileAndForce()
    {
        var parsed = CliArgumentParser.Parse([
            "execute",
            "--file", "./scripts/setup.cs",
            "--force"
        ]);

        Assert.Equal(CommandKind.ExecuteCode, parsed.Kind);
        Assert.Equal("./scripts/setup.cs", parsed.ExecuteCodeFile);
        Assert.True(parsed.Force);
    }

    [Fact]
    public void Parse_Execute_RejectsBothCodeAndFile()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse([
                "execute",
                "--code", "Debug.Log(42);",
                "--file", "./scripts/setup.cs",
                "--force"
            ]));

        Assert.Contains("--code", ex.Message);
        Assert.Contains("--file", ex.Message);
    }

    [Fact]
    public void Parse_Execute_UsesLiveTimeout()
    {
        var parsed = CliArgumentParser.Parse([
            "execute", "--code", "var x = 1;", "--force"
        ]);

        Assert.Equal(ProtocolConstants.DefaultLiveTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_Custom_RequiresCommandName()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["custom"]));

        Assert.Contains("명령 이름", ex.Message);
    }

    [Fact]
    public void Parse_Custom_AcceptsCommandName()
    {
        var parsed = CliArgumentParser.Parse(["custom", "terrain-setup"]);

        Assert.Equal(CommandKind.Custom, parsed.Kind);
        Assert.Equal("terrain-setup", parsed.CustomCommandName);
        Assert.Equal(ProtocolConstants.DefaultLiveTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_Custom_AcceptsJson()
    {
        var parsed = CliArgumentParser.Parse([
            "custom", "terrain-setup",
            "--json", "{\"size\": 1000}"
        ]);

        Assert.Equal(CommandKind.Custom, parsed.Kind);
        Assert.Equal("terrain-setup", parsed.CustomCommandName);
        Assert.Equal("{\"size\": 1000}", parsed.CustomArgsJson);
    }

    [Fact]
    public void Parse_Custom_ToEnvelope_UsesCustomProtocol()
    {
        var parsed = CliArgumentParser.Parse([
            "custom", "terrain-setup",
            "--json", "{\"size\": 1000}"
        ]);

        var envelope = parsed.ToEnvelope();
        Assert.Equal(ProtocolConstants.CommandCustom, envelope.command);
        Assert.Contains("terrain-setup", envelope.argumentsJson);
    }

    [Fact]
    public void Parse_Custom_UsesLiveTimeout()
    {
        var parsed = CliArgumentParser.Parse(["custom", "my-tool"]);

        Assert.Equal(ProtocolConstants.DefaultLiveTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_Custom_PreservesGlobalJsonOutputAndCommandJsonArgs()
    {
        var parsed = CliArgumentParser.Parse([
            "--json",
            "custom", "terrain-setup",
            "--json", "{\"size\": 1000}"
        ]);

        Assert.True(parsed.JsonOutput);
        Assert.Equal(CommandKind.Custom, parsed.Kind);
        Assert.Equal("{\"size\": 1000}", parsed.CustomArgsJson);
    }

    [Fact]
    public void Parse_PackageList_UsesBatchTimeout()
    {
        var parsed = CliArgumentParser.Parse(["package", "list"]);

        Assert.Equal(CommandKind.PackageList, parsed.Kind);
        Assert.Equal(ProtocolConstants.DefaultBatchTimeoutMs, parsed.TimeoutMs);
    }

    [Fact]
    public void Parse_PackageAdd_RequiresName()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["package", "add"]));

        Assert.Contains("--name", ex.Message);
    }

    [Fact]
    public void Parse_PackageAdd_AcceptsNameAndVersion()
    {
        var parsed = CliArgumentParser.Parse([
            "package", "add",
            "--name", "com.unity.textmeshpro",
            "--version", "3.0.6"
        ]);

        Assert.Equal(CommandKind.PackageAdd, parsed.Kind);
        Assert.Equal("com.unity.textmeshpro", parsed.PackageName);
        Assert.Equal("3.0.6", parsed.PackageVersion);
    }

    [Fact]
    public void Parse_PackageRemove_RequiresForce()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["package", "remove", "--name", "com.unity.textmeshpro"]));

        Assert.Contains("--force", ex.Message);
    }

    [Fact]
    public void Parse_PackageRemove_AcceptsNameAndForce()
    {
        var parsed = CliArgumentParser.Parse([
            "package", "remove",
            "--name", "com.unity.textmeshpro",
            "--force"
        ]);

        Assert.Equal(CommandKind.PackageRemove, parsed.Kind);
        Assert.Equal("com.unity.textmeshpro", parsed.PackageName);
        Assert.True(parsed.Force);
    }

    [Fact]
    public void Parse_PackageSearch_RequiresQuery()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["package", "search"]));

        Assert.Contains("--query", ex.Message);
    }

    [Fact]
    public void Parse_PackageSearch_AcceptsQuery()
    {
        var parsed = CliArgumentParser.Parse([
            "package", "search",
            "--query", "physics"
        ]);

        Assert.Equal(CommandKind.PackageSearch, parsed.Kind);
        Assert.Equal("physics", parsed.PackageQuery);
    }

    [Fact]
    public void Parse_Package_UnknownSubcommandThrows()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliArgumentParser.Parse(["package", "upgrade"]));

        Assert.Contains("upgrade", ex.Message);
    }
}
