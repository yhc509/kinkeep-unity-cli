# kinkeep-unity-cli

`kinkeep-unity-cli` packages the `unity-cli` executable, the `com.kinkeep.unity-cli-bridge` Unity package, and the supporting Codex skill into one project-aware workflow. Its real value is not just "Unity from the command line," but "no manual server startup, no per-project ports, and automatic attachment to the correct project."

## Why kinkeep-unity-cli Is Different

- No manual server startup. The bridge starts automatically when the Editor opens.
- No per-project port management. The correct Editor instance is selected from the project path, registered project name, and registry.
- One live command surface. Commands run through local IPC against a running Unity Editor with the bridge active.
- Scene, asset, material, package, and prefab workflows are first-class. This goes beyond `status` and `refresh` into `asset create`, `material info/set`, `package list/add/remove/search`, `scene open/inspect/patch`, scene shortcuts like `scene add-object`, `scene set-transform`, and `scene assign-material`, plus `prefab create/inspect/patch`.
- Project-defined live commands can be exposed through a lightweight `[PucCommand]` extension API instead of adding one-off transport code.
- The Codex skill keeps the workflow consistent: choose the command, perform the work, then verify the logs.
- Unity menu workflows support both direct execution and prefix listing through `execute-menu --path ...` and `execute-menu --list ...`.

## What You Can Do With kinkeep-unity-cli

- Check editor state from the terminal with `status`, `refresh`, and `compile`.
- Drive live editor behavior with `play`, `pause`, `stop`, `execute-menu`, `execute`, `custom`, `screenshot`, and `read-console`.
- Drive Play Mode QA workflows with `qa click`, `qa tap`, `qa swipe`, `qa key`, `qa wait`, and `qa wait-until`.
- Query and manage assets with `asset find`, `asset info`, `asset create`, `asset move`, `asset rename`, `asset reimport`, and `asset delete`.
- Inspect and update material shader properties with `material info` and `material set`.
- Inspect and manage Unity packages with `package list`, `package add`, `package remove`, and `package search`.
- Generate common Unity assets directly from the CLI, including materials, scenes, prefabs, animation clips, controllers, render textures, volume profiles, and ScriptableObjects.
- Open saved scenes, inspect scene hierarchies, and apply deterministic scene edits with `scene open`, `scene inspect`, `scene patch`, `scene add-object`, `scene set-transform`, `scene add-component`, and `scene remove-component`, then use `scene assign-material` to update the active scene's MeshRenderer material slot directly.
- Create project-specific assets through extension providers instead of routing everything through one-off editor scripts.
- Author prefabs structurally from JSON with `prefab create`, inspect serialized paths with `prefab inspect`, and apply deterministic changes with `prefab patch`.

## Typical Workflows

- AI-assisted editor automation: let an agent create assets or patch scenes/prefabs, then verify the console through the same CLI.
- Local multi-project work: keep multiple Unity projects open and target the right editor without port juggling.
- Live editor automation: keep the Editor open and drive asset, material, package, scene, and prefab workflows through the same IPC surface.
- Data-heavy gameplay work: generate ScriptableObjects and custom asset types from repeatable command flows instead of custom menus.

This repository is organized into three parts.

- `cli/`: the `unity-cli` executable and shared protocol
- `unity-package/com.kinkeep.unity-cli-bridge/`: the Unity package that starts the bridge automatically inside the Editor
- `tools/skills/unity-cli-operator/`: the Codex skill that keeps `unity-cli` usage consistent

## Current Status

- Live IPC is the only supported editor transport, and a small set of CLI utilities such as registry commands, diagnostics, and `qa wait` are handled locally.
- Asset find/create/move/delete, material info/set, package list/add/remove/search, Play Mode QA commands, scene open/inspect/patch plus scene convenience shortcuts, and prefab create/inspect/patch are supported.
- Release validation is currently focused on `macOS arm64`.

Current command surface, at a glance:

- Editor control: `status`, `refresh`, `compile`, `play`, `pause`, `stop`, `execute-menu`, `execute`, `custom`, `screenshot`, `read-console`
- Asset workflows: `asset find`, `asset types`, `asset info`, `asset reimport`, `asset mkdir`, `asset move`, `asset rename`, `asset delete`, `asset create`
- Material workflows: `material info`, `material set`
- QA workflows: `qa click`, `qa tap`, `qa swipe`, `qa key`, `qa wait`, `qa wait-until`
- Package management: `package list`, `package add`, `package remove`, `package search`
- Scene workflows: `scene open`, `scene inspect`, `scene patch`, `scene add-object`, `scene set-transform`, `scene add-component`, `scene remove-component`, `scene assign-material`
- Prefab workflows: `prefab inspect`, `prefab create`, `prefab patch`

Any command that accepts `--project` can target either an actual Unity project path or a registered project name from the local instance registry. Registered project-name matching is case-insensitive. Existing directory paths take precedence when the same token could match both a real path and a registered name, and invalid values fail fast with a usage error instead of hashing the current working directory. If multiple registered projects collapse to the same case-insensitive name, use the full project path.

## Quick Start

### 1. Build the CLI

```bash
./scripts/publish-osx-arm64.sh
```

Output:

```text
dist/unity-cli/UnityCli.Cli
```

### 2. Add the Unity Package

For local mono-repo use, add a file reference to `unity-package/com.kinkeep.unity-cli-bridge` in the Unity project's `Packages/manifest.json`. The exact relative path depends on where your Unity project lives.

For Git-based installation, use the package path inside this repository.

```json
{
  "dependencies": {
    "com.kinkeep.unity-cli-bridge": "https://github.com/yhc509/kinkeep-unity-cli.git?path=/unity-package/com.kinkeep.unity-cli-bridge#main"
  }
}
```

If you are migrating from the old package, update `Packages/manifest.json` so the dependency key changes from `com.puc.bridge` to `com.kinkeep.unity-cli-bridge`, and make sure any explicit asmdef references move from `PUC.Editor` / `PUC.Runtime` to `KinKeep.UnityCli.Bridge.Editor` / `KinKeep.UnityCli.Bridge.Runtime`.

The package already includes `Editor/Plugins/Newtonsoft.Json.dll`, so you do not need to install a separate JSON package.

### 3. Basic Smoke Test

```bash
PROJECT_ROOT="/absolute/path/to/your-unity-project"
./dist/unity-cli/UnityCli.Cli status --project "$PROJECT_ROOT" --json
./dist/unity-cli/UnityCli.Cli refresh --project "$PROJECT_ROOT" --json
./dist/unity-cli/UnityCli.Cli asset info --project "$PROJECT_ROOT" --path Assets/Scenes/SampleScene.unity --json
./dist/unity-cli/UnityCli.Cli asset find --project "$PROJECT_ROOT" --type Material --limit 10 --json
./dist/unity-cli/UnityCli.Cli material info --project "$PROJECT_ROOT" --path Assets/Materials/Wood.mat --json
./dist/unity-cli/UnityCli.Cli package list --project "$PROJECT_ROOT" --json
./dist/unity-cli/UnityCli.Cli scene inspect --project "$PROJECT_ROOT" --path Assets/Scenes/SampleScene.unity --with-values --json
./dist/unity-cli/UnityCli.Cli screenshot --project "$PROJECT_ROOT" --path /tmp/game-view.png --json
./dist/unity-cli/UnityCli.Cli execute-menu --project "$PROJECT_ROOT" --list "GameObject" --json
./dist/unity-cli/UnityCli.Cli execute --project "$PROJECT_ROOT" --code "Debug.Log(\"KinKeep smoke\");" --force --json
```

`asset find` accepts `--name`, `--type`, or both. For example, `asset find --type Scene` runs a type-only `FindAssets("t:Scene")` query, while `asset find --name Player --type Prefab` combines both filters.
Unity can return matching `Packages/...` assets from `asset find`; those records are reported as-is, and `asset info --path Packages/...` can inspect them. Asset creation and mutation commands remain `Assets/...`-only.

Use `scene inspect`, `prefab inspect`, or `material info` with `--omit-defaults` to strip identity/default fields from the response when you want smaller IPC payloads.

```bash
./dist/unity-cli/UnityCli.Cli scene inspect --project "$PROJECT_ROOT" --path Assets/Scenes/SampleScene.unity --with-values --max-depth 2 --omit-defaults --json
./dist/unity-cli/UnityCli.Cli material info --project "$PROJECT_ROOT" --path Assets/Materials/Wood.mat --omit-defaults --json
```

If you want only the `data` payload as compact JSON without the envelope metadata, use `--output compact`. Keep using `--json` when you need the full response envelope. Compact-mode failures are reduced to `{"error":"CODE","message":"..."}`.

```bash
./dist/unity-cli/UnityCli.Cli asset info --project "$PROJECT_ROOT" --path Assets/Scenes/SampleScene.unity --output compact
```

QA smoke tests:

```bash
./dist/unity-cli/UnityCli.Cli qa wait --ms 250 --json
./dist/unity-cli/UnityCli.Cli qa click --project "$PROJECT_ROOT" --qa-id StartButton --json
./dist/unity-cli/UnityCli.Cli qa wait-until --project "$PROJECT_ROOT" --scene SampleScene --timeout 5000 --json
```

Custom command smoke test:

```csharp
using UnityCli.Protocol;

public static class TerrainCommands
{
    [PucCommand("terrain-setup")]
    public static string SetupTerrain(string argumentsJson)
    {
        return "{\"ok\":true}";
    }
}
```

```bash
./dist/unity-cli/UnityCli.Cli --json \
  --project "$PROJECT_ROOT" \
  custom terrain-setup \
  --json '{"size":1000}'
```

Scene patch smoke test:

```bash
./dist/unity-cli/UnityCli.Cli scene patch \
  --project "$PROJECT_ROOT" \
  --path Assets/Scenes/SampleScene.unity \
  --spec-file ./tools/skills/unity-cli-operator/assets/scene-patch-basic.json \
  --json
```

Scene convenience shortcut smoke tests:

```bash
./dist/unity-cli/UnityCli.Cli scene add-object \
  --project "$PROJECT_ROOT" \
  --path Assets/Scenes/SampleScene.unity \
  --parent /Environment[0] \
  --name SpawnPoint \
  --primitive Cube \
  --position 3,0,0 \
  --json

./dist/unity-cli/UnityCli.Cli scene open \
  --project "$PROJECT_ROOT" \
  --path Assets/Scenes/SampleScene.unity \
  --json

./dist/unity-cli/UnityCli.Cli scene set-transform \
  --project "$PROJECT_ROOT" \
  --node /Environment[0]/SpawnPoint[0] \
  --position 0,1,0 \
  --json

./dist/unity-cli/UnityCli.Cli scene assign-material \
  --project "$PROJECT_ROOT" \
  --node /Environment[0]/SpawnPoint[0] \
  --material Assets/Materials/Wood.mat \
  --json
```

Successful `scene add-object --json` output includes `createdPath`, so follow-up scene commands can target the new node without a separate inspect round-trip.
`scene set-transform` and `scene assign-material` use the currently active loaded scene and save it immediately, so they do not take `--path`.

Prefab authoring smoke test:

```bash
./dist/unity-cli/UnityCli.Cli prefab create \
  --project "$PROJECT_ROOT" \
  --path Assets/Smoke.prefab \
  --spec-file ./tools/skills/unity-cli-operator/assets/prefab-create-basic.json \
  --force \
  --json
```

Material mutation smoke tests:

```bash
./dist/unity-cli/UnityCli.Cli material set \
  --project "$PROJECT_ROOT" \
  --path Assets/Materials/Wood.mat \
  --property _Color \
  --value 1,0,0,1 \
  --json

./dist/unity-cli/UnityCli.Cli material set \
  --project "$PROJECT_ROOT" \
  --path Assets/Materials/Wood.mat \
  --texture _MainTex \
  --asset Assets/Textures/wood.png \
  --json
```

## Documentation

- CLI reference (generated): [`docs/cli-reference.md`](docs/cli-reference.md)
- Architecture: [`docs/architecture.md`](docs/architecture.md)
- Scene spec: [`docs/scene-spec.md`](docs/scene-spec.md)
- Prefab spec: [`docs/prefab-spec.md`](docs/prefab-spec.md)
- Unity package usage: [`unity-package/com.kinkeep.unity-cli-bridge/README.md`](unity-package/com.kinkeep.unity-cli-bridge/README.md)
- Third-party notices: [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)

Regenerate the CLI reference after command-surface changes:

```bash
dotnet run --project cli/UnityCli.DocGen -- --write
```

## Codex Skill

The `unity-cli-operator` skill bundles command selection, scene/prefab patch authoring, and post-task `read-console` verification into one repeatable workflow.

The skill is part of the mono-repo, not part of the Unity package payload. Installing the package through a Git URL gives Unity only `unity-package/com.kinkeep.unity-cli-bridge`, so the Codex skill still needs the repository itself.

Recommended flow:

- clone the mono-repo locally
- install the Unity package from `?path=/unity-package/com.kinkeep.unity-cli-bridge`
- symlink `tools/skills/unity-cli-operator` into `~/.codex/skills`

Example symlink into the local Codex skills directory:

```bash
ln -sfn "$(pwd -P)/tools/skills/unity-cli-operator" ~/.codex/skills/unity-cli-operator
```

## Verification Commands

CLI and tests:

```bash
dotnet build KinKeepUnityCli.sln -c Debug
/opt/homebrew/Cellar/dotnet/9.0.112/libexec/dotnet test KinKeepUnityCli.sln
dotnet run --project cli/UnityCli.DocGen -- --check
```

Unity integration:

- Live: `status`, `refresh`, `compile`, `execute`, `custom`, `screenshot`, `asset find`, `asset info`, `asset create`, `material info`, `material set`, `qa click`, `qa tap`, `qa swipe`, `qa key`, `qa wait-until`, `package list`, `package add`, `package remove`, `package search`, `scene open`, `scene inspect`, `scene patch`, `scene add-object`, `scene set-transform`, `scene add-component`, `scene remove-component`, `scene assign-material`, `prefab create`, `prefab inspect`, `prefab patch`
- Local: `qa wait`, `instances list`, `instances use`, `doctor`
- Commands require a running Unity Editor with the bridge active. If live IPC is unavailable, the CLI returns an error instead of trying any editor-off fallback.
- After live work, always check `read-console --type error` and `read-console --type warning`.

## Current Limits

- Release automation is still centered on `macOS arm64`.
- When `--camera` is omitted, `screenshot` defaults to `--view game`. In Play Mode, Game View capture uses `ScreenCapture.CaptureScreenshotAsTexture()`. `--width` and `--height` can downscale the native capture, but larger requests log a warning and save it without upscaling.
- Scene patching currently targets saved `Assets/...unity` scenes. Multi-scene orchestration and generalized scene object references are still out of scope.
- Advanced editing for prefab-internal object references and nested prefab variants is still out of scope.
- The root prefab object name is normalized to the prefab file name when saved by Unity.
