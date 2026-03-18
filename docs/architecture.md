# Architecture

## Summary

`PUC` is a mono-repo for controlling the Unity Editor from the command line without manual server startup. Its public surface has three parts.

- `cli/`: the .NET CLI that receives user commands and routes them to live IPC or batch fallback
- `unity-package/com.puc.bridge/`: the bridge package that starts automatically inside the Unity Editor
- `tools/skills/unity-cli-operator/`: the Codex skill that keeps `unity-cli` usage consistent

## Runtime Flow

### Live

1. When the Unity Editor opens, the bridge starts automatically.
2. The bridge registers the project real-path hash and instance information in the registry.
3. The CLI finds the Unity project from the current working directory or `--project`, then attaches to the correct instance through the registry.
4. Requests are executed on the Editor main thread over local IPC.

### Batch Fallback

1. If there is no live instance, or if the command supports batch execution, the CLI prepares a Unity batchmode run.
2. The CLI creates request/result files and a per-project lock.
3. Unity reads the request in batchmode and runs the same command handlers.
4. The CLI reads the JSON response and returns the same envelope shape used by live mode.

## Core Behaviors

- Multi-project selection is based on the project root hash and the active instance in the registry.
- On macOS, hashes are computed from the real path instead of a symlink path.
- Destructive operations and overwrite behavior require `--force`.
- Prefab editing is split into `prefab create`, `prefab inspect`, and `prefab patch`, and field patching is based on `SerializedProperty.propertyPath`.

## Repository Layout

- `cli/UnityCli.Cli`: user-facing CLI
- `cli/UnityCli.Protocol`: protocol models shared by the CLI and Unity package
- `unity-package/com.puc.bridge`: UPM package
- `samples/MinimalURPProject`: minimal public Unity sample project
- `tools/skills/unity-cli-operator`: Codex skill

## Current Limits

- CLI release validation is currently centered on `macOS arm64`.
- Advanced editing of prefab-internal object references and nested prefab variants is not generalized yet.
- The root prefab object name is normalized to the prefab file name after save.
