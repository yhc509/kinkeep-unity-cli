# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PUC (Portless Unity CLI) — controls the Unity Editor from the command line without manual server startup. Mono-repo containing a .NET 9 CLI, a Unity UPM package (bridge), and shared protocol models.

The CLI is **live IPC only**. Unity commands require a running Editor with the bridge active.

## Build & Test Commands

```bash
# Build
dotnet build PUC.sln -c Debug

# Run all tests
dotnet test PUC.sln

# Run a single test
dotnet test PUC.sln --filter "FullyQualifiedName~ClassName.MethodName"

# Publish macOS arm64 binary
./scripts/publish-osx-arm64.sh    # → dist/unity-cli/UnityCli.Cli

# Doc generation (verify docs match code)
dotnet run --project cli/UnityCli.DocGen -- --check

# Doc generation (write/update docs)
dotnet run --project cli/UnityCli.DocGen -- --write
```

## Architecture

```
cli/UnityCli.Cli/           CLI executable (.NET 9, osx-arm64)
  ├── CliApp.cs              Entry point, routes to IPC
  ├── Services/
  │   ├── CliArgumentParser  Switch-based parser → ParsedCommand
  │   ├── CliCommandCatalog  CLI-side command metadata
  │   ├── LocalIpcClient     Live IPC to running Editor
  │   └── InstanceRegistryStore  Per-project instance tracking
  └── Models/ParsedCommand   CommandKind variants + envelope builder

cli/UnityCli.Protocol/       Shared protocol (symlinked from unity-package Runtime/Protocol/)

unity-package/com.puc.bridge/
  ├── Editor/
  │   ├── BridgeHost          Main bridge orchestrator
  │   ├── AssetCommandHandler Asset CRUD operations
  │   ├── SceneCommandHandler Scene open/inspect/patch
  │   ├── PrefabCommandHandler Prefab create/inspect/patch
  │   ├── SerializedValueApplier Applies values via SerializedProperty.propertyPath
  └── Runtime/Protocol/       Shared models (C# 11, nullable enabled)
      ├── CliCommandCatalog    Master command descriptor catalog
      ├── CommandModels        Request/response envelopes
      ├── ProtocolConstants    Registry paths, timeouts
      └── ProtocolHelpers      Serialization utilities

tests/UnityCli.Cli.Tests/    xUnit tests
```

**Protocol sharing:** `cli/UnityCli.Protocol/` compiles the same `.cs` files from `unity-package/.../Runtime/Protocol/` via `<Compile Include>` links in the `.csproj`. Changes to protocol files affect both CLI and Unity package.

## Key Conventions

- **Nullable references enabled** throughout (`#nullable enable`, implicit usings).
- **Asset paths** always use `Assets/...` format.
- **Destructive ops require `--force`:** `asset delete` (always), `asset move/rename/create` (when overwriting).
- **macOS paths:** Use real paths (`pwd -P`), not symlinks, for hashing and registry lookups.
- **Scene paths:** Format `/Root[0]/Child[0]` with array notation for sibling indexing; `/` is the virtual scene root.
- **Prefab editing:** Based on `SerializedProperty.propertyPath` (run `prefab inspect --with-values` to verify paths before patching).
- **Doc sync:** CLI command or option changes must update `README.md` examples and help text. Run `dotnet run --project cli/UnityCli.DocGen -- --check` to verify.

## Verification After Changes

- CLI code changes → `dotnet build PUC.sln -c Debug`
- Test changes → `dotnet test PUC.sln`
- Unity integration changes → test live IPC flows with an actual Unity project
