# PUC

`PUC` stands for **Portless Unity CLI**. The internal executable name is still `unity-cli`, but the public-facing name is simply `PUC`. Its real value is not just "Unity from the command line," but "no manual server startup, no per-project ports, and automatic attachment to the correct project."

## Why PUC Is Different

- No manual server startup. The bridge starts automatically when the Editor opens.
- No per-project port management. The correct Editor instance is selected from the project path and registry.
- One command surface for live and batch. If the Editor is running, commands go through live IPC. If it is not, supported commands fall back to batchmode.
- Asset and prefab workflows are first-class. This goes beyond `status` and `refresh` into `asset create` and `prefab create/inspect/patch`.
- The Codex skill keeps the workflow consistent: choose the command, perform the work, then verify the logs.

This repository is organized into three parts.

- `cli/`: the `unity-cli` executable and shared protocol
- `unity-package/com.puc.bridge/`: the Unity package that starts the bridge automatically inside the Editor
- `tools/skills/unity-cli-operator/`: the Codex skill that keeps `unity-cli` usage consistent

## Current Status

- Live IPC and batch fallback are both working.
- Asset find/create/move/delete and prefab create/inspect/patch are supported.
- Release validation is currently focused on `macOS arm64`.

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

For local mono-repo use, add a file reference to `unity-package/com.puc.bridge` in the Unity project's `Packages/manifest.json`. The exact relative path depends on where your Unity project lives.

For Git-based installation, use the package path inside this repository.

```json
{
  "dependencies": {
    "com.puc.bridge": "https://github.com/<org>/<repo>.git?path=/unity-package/com.puc.bridge#main"
  }
}
```

The package already includes `Editor/Plugins/Newtonsoft.Json.dll`, so you do not need to install a separate JSON package.

### 3. Basic Smoke Test

```bash
PROJECT_ROOT="/absolute/path/to/your-unity-project"
./dist/unity-cli/UnityCli.Cli status --project "$PROJECT_ROOT" --json
./dist/unity-cli/UnityCli.Cli refresh --project "$PROJECT_ROOT" --json
./dist/unity-cli/UnityCli.Cli asset info --project "$PROJECT_ROOT" --path Assets/Scenes/SampleScene.unity --json
```

Prefab authoring smoke test:

```bash
./dist/unity-cli/UnityCli.Cli prefab create \
  --project "$PROJECT_ROOT" \
  --path Assets/Smoke.prefab \
  --spec-file ./tools/skills/unity-cli-operator/assets/prefab-create-basic.json \
  --force \
  --json
```

## Documentation

- Architecture: [`docs/architecture.md`](docs/architecture.md)
- Prefab spec: [`docs/prefab-spec.md`](docs/prefab-spec.md)
- Unity package usage: [`unity-package/com.puc.bridge/README.md`](unity-package/com.puc.bridge/README.md)
- Third-party notices: [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)

## Codex Skill

The `unity-cli-operator` skill bundles command selection, live-vs-batch decisions, prefab patch authoring, and post-task `read-console` verification into one repeatable workflow.

The skill is part of the mono-repo, not part of the Unity package payload. Installing the package through a Git URL gives Unity only `unity-package/com.puc.bridge`, so the Codex skill still needs the repository itself.

Recommended flow:

- clone the mono-repo locally
- install the Unity package from `?path=/unity-package/com.puc.bridge`
- symlink `tools/skills/unity-cli-operator` into `~/.codex/skills`

Example symlink into the local Codex skills directory:

```bash
ln -sfn "$(pwd -P)/tools/skills/unity-cli-operator" ~/.codex/skills/unity-cli-operator
```

## Verification Commands

CLI and tests:

```bash
dotnet build PUC.sln -c Debug
/opt/homebrew/Cellar/dotnet/9.0.112/libexec/dotnet test PUC.sln
```

Unity integration:

- Live: `status`, `refresh`, `asset create`, `prefab create`, `prefab inspect`, `prefab patch`
- Batch: `refresh`, `asset info`, `asset create`, `prefab create`, `prefab inspect`, `prefab patch`
- After live work, always check `read-console --type error` and `read-console --type warning`.

## Current Limits

- Release automation is still centered on `macOS arm64`.
- Advanced editing for prefab-internal object references and nested prefab variants is still out of scope.
- The root prefab object name is normalized to the prefab file name when saved by Unity.
