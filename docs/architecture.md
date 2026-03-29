# Architecture

## Summary

`PUC` is a mono-repo for controlling the Unity Editor from the command line without manual server startup. Its public surface has three parts.

- `cli/`: the .NET CLI that receives user commands and routes them to live IPC
- `unity-package/com.kinkeep.unity-cli-bridge/`: the bridge package that starts automatically inside the Unity Editor
- `tools/skills/unity-cli-operator/`: the Codex skill that keeps `unity-cli` usage consistent

## Runtime Flow

### Live

1. When the Unity Editor opens, the bridge starts automatically.
2. The bridge registers the project real-path hash and instance information in the registry.
3. The CLI finds the Unity project from the current working directory or `--project`, then attaches to the correct instance through the registry.
4. Requests are executed on the Editor main thread over local IPC.

### Live Target Unavailable

1. If there is no selected live instance, the CLI returns a live-unavailable error.
2. If the registry points at an instance but the bridge is still importing or compiling, the CLI returns a live-unavailable error with guidance to retry.

## Core Behaviors

- Multi-project selection is based on the project root hash and the active instance in the registry.
- On macOS, hashes are computed from the real path instead of a symlink path.
- When no running Editor with an active bridge is reachable, commands fail instead of trying any editor-off fallback.
- Destructive operations and overwrite behavior require `--force`.
- Scene editing is split into `scene open`, `scene inspect`, and `scene patch`, and scene node paths use `/Root[0]/Child[0]` with `/` as the virtual scene root.
- Prefab editing is split into `prefab create`, `prefab inspect`, and `prefab patch`, and field patching is based on `SerializedProperty.propertyPath`.

## Repository Layout

- `cli/UnityCli.Cli`: user-facing CLI
- `cli/UnityCli.Protocol`: protocol models shared by the CLI and Unity package
- `unity-package/com.kinkeep.unity-cli-bridge`: UPM package
- `tools/skills/unity-cli-operator`: Codex skill

## Current Limits

- CLI release validation is currently centered on `macOS arm64`.
- Scene editing is currently limited to saved `Assets/...unity` assets; multi-scene orchestration and generalized scene object references are not covered yet.
- Advanced editing of prefab-internal object references and nested prefab variants is not generalized yet.
- The root prefab object name is normalized to the prefab file name after save.
