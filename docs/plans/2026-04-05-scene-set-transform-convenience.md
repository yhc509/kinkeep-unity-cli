# Scene Set-Transform Convenience Command Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the old patch-wrapper `scene set-transform` flow with an active-scene convenience command that accepts `--node` plus one or more transform vectors.

**Architecture:** Keep the CLI surface thin by parsing the new `--node` contract into a dedicated protocol command, then handle the mutation directly in the Unity bridge against the active saved scene. Reuse existing vector parsing and scene-node resolution patterns, and save the scene immediately after a successful transform update.

**Tech Stack:** .NET 9 CLI, Unity Editor bridge, shared protocol DTOs/catalog metadata, xUnit parser tests, DocGen-generated CLI reference.

---

### Task 1: Lock the CLI contract

**Files:**
- Modify: `cli/UnityCli.Cli/Services/CliArgumentParser.cs`
- Modify: `cli/UnityCli.Cli/Services/CliArgumentParser.Validation.cs`
- Modify: `cli/UnityCli.Cli/Models/ParsedCommand.cs`

**Step 1: Parse `scene set-transform` with active-scene semantics**

Accept `--node`, `--position`, `--rotation`, and `--scale`, keep `--project`, and stop requiring `--path` / `--target`.

**Step 2: Validate the new required inputs**

Require `--node` and at least one transform option.

**Step 3: Emit a dedicated protocol envelope**

Map `CommandKind.SceneSetTransform` to a new protocol command and serialize dedicated args instead of a `scene patch` payload.

### Task 2: Add protocol support

**Files:**
- Modify: `unity-package/com.kinkeep.unity-cli-bridge/Runtime/Protocol/ProtocolConstants.cs`
- Modify: `unity-package/com.kinkeep.unity-cli-bridge/Runtime/Protocol/CommandModels.cs`
- Modify: `unity-package/com.kinkeep.unity-cli-bridge/Runtime/Protocol/CliCommandCatalog.cs`

**Step 1: Add the protocol command constant**

Introduce `scene-set-transform`.

**Step 2: Add request and response DTOs**

Define a request model carrying `node` plus optional transform vectors, and a response model that reports the mutated node and active scene path.

**Step 3: Refresh command metadata**

Update synopsis and notes so DocGen reflects the active-scene `--node` workflow.

### Task 3: Implement the bridge handler

**Files:**
- Modify: `unity-package/com.kinkeep.unity-cli-bridge/Editor/SceneCommandHandler.cs`

**Step 1: Route the new protocol command**

Handle `scene-set-transform` alongside the other scene commands.

**Step 2: Resolve the active scene and target node**

Reuse the active-saved-scene guard and `SceneInspector.ResolveNode`.

**Step 3: Apply transform values through `SerializedObject`**

Set `m_LocalPosition`, `m_LocalRotation`, and `m_LocalScale` only when the corresponding CLI options are supplied, then save and refresh.

### Task 4: Update tests and docs

**Files:**
- Modify: `tests/UnityCli.Cli.Tests/CliArgumentParserTests.cs`
- Modify: `README.md`
- Modify: `tests/integration/LIVE_IPC_TEST_SCENARIOS.md`
- Modify: `tests/integration/run-live-ipc-tests.sh`

**Step 1: Replace parser expectations**

Cover `--node` validation and dedicated protocol-envelope generation.

**Step 2: Update examples**

Switch README and live IPC scenarios to the active-scene `scene set-transform` syntax.

### Task 5: Verify

**Files:**
- Modify if needed after failures: generated docs or touched sources

**Step 1: Build**

Run: `dotnet build KinKeepUnityCli.sln -c Debug`

**Step 2: Test**

Run: `/opt/homebrew/Cellar/dotnet/9.0.112/libexec/dotnet test KinKeepUnityCli.sln`

**Step 3: Check docs**

Run: `dotnet run --project cli/UnityCli.DocGen -- --check`
