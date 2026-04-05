# kinkeep-unity-cli

Control the Unity Editor from the command line. No manual server startup, no port management — the bridge starts when the Editor opens, and the CLI finds the right project automatically.

## Design Philosophy

Most Unity automation tools fall into two camps:

**MCP mega-tools** wrap dozens of actions behind a Python/Node intermediary. Every call hops through AI → MCP server → HTTP → Unity plugin. More moving parts, more latency, more failure modes.

**Dynamic code execution** sends raw C# to Unity and reads results from Debug.Log. Infinitely flexible, but the LLM must generate correct C# every time, and reading results requires a second call.

**kinkeep-unity-cli takes a third path: declarative commands over direct IPC.**

```
CLI ──── local IPC ──── Unity Editor (bridge)
         (Named Pipe on Windows, Unix socket on macOS/Linux)
```

- **One hop, no intermediary.** CLI talks directly to the Editor over local IPC. Average response: 265 ms.
- **Declarative, not imperative.** `scene add-object --primitive Cube --position 3,0,0` instead of writing C# code. The LLM picks options, not APIs.
- **Token-aware responses.** `--output compact` strips envelope metadata. `--omit-defaults` cuts material info by 71% and scene inspect by 41%.
- **Structured results in stdout.** No polling, no log scraping, no file reads. Every command returns JSON immediately.
- **Fail fast, fail loud.** Invalid options get a clear error. Unrecognized patch keys return warnings instead of silent success.

> See [Benchmark: 3-Way Comparison](https://github.com/yhc509/kinkeep-unity-cli/wiki/Benchmark-Unity-Editor-CLI-Tool-Comparison) for measured results against two other approaches on the same scenario.

## Quick Start

### 1. Install the CLI

**Option A: From Unity Editor** (recommended)

After adding the Unity package (step 2), open `KinKeep > CLI Manager` in the Editor menu. Click **Install CLI** — the correct binary is downloaded automatically to `~/.kinkeep/unity-cli/`.

**Option B: Manual download**

Download from [GitHub Releases](https://github.com/yhc509/kinkeep-unity-cli/releases):

| Platform | File |
|----------|------|
| macOS (Apple Silicon) | `unity-cli-osx-arm64.tar.gz` |
| Windows (x64) | `unity-cli-win-x64.zip` |

Extract and add the binary to your PATH.

> **Tip:** A short, fixed install path (`~/.kinkeep/unity-cli/`) saves tokens when AI agents invoke the CLI repeatedly — every character in the path is repeated on each call.

### 2. Add the Unity Package

Add the following to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.kinkeep.unity-cli-bridge": "https://github.com/yhc509/kinkeep-unity-cli.git?path=/unity-package/com.kinkeep.unity-cli-bridge#main"
  }
}
```

Or paste this URL in Unity Package Manager → **Add package from git URL**:

```
https://github.com/yhc509/kinkeep-unity-cli.git?path=/unity-package/com.kinkeep.unity-cli-bridge#main
```

The bridge starts automatically when the Editor opens. No configuration needed.

### 3. Verify

Check that the CLI can reach the running Editor:

```bash
unity-cli status --project /path/to/your-project --json
```

## What You Can Do

### Editor Control

```bash
unity-cli status --project MyGame          # Editor state, Unity version, current scene
unity-cli play / pause / stop              # Play Mode control
unity-cli compile                          # Trigger recompile
unity-cli screenshot --path /tmp/shot.png  # Game View capture (default), or --view scene
unity-cli read-console --type error        # Check for errors after any operation
unity-cli execute-menu --list "GameObject" # Browse Unity menus
unity-cli execute --code "Debug.Log(1);" --force  # Run arbitrary C# (escape hatch)
```

### Assets

```bash
unity-cli asset find --type Material                    # Find by type
unity-cli asset find --name Player --type Prefab        # Find by name + type
unity-cli asset info --path Assets/Scenes/Main.unity    # Asset metadata
unity-cli asset create --type material --path Assets/Materials/Red  # Create
unity-cli asset mkdir --path Assets/NewFolder            # Create folder
unity-cli asset move --from ... --to ... --force         # Move/rename
unity-cli asset delete --path ... --force                # Delete
```

### Scenes

```bash
# Open and inspect
unity-cli scene open --path Assets/Scenes/Main.unity
unity-cli scene inspect --path ... --with-values --omit-defaults

# Build scenes with convenience commands
unity-cli scene add-object --name Cube --primitive Cube \
  --parent "/Environment[0]" --position 3,0,0
# → Response includes createdPath — no follow-up inspect needed

unity-cli scene set-transform --node "/Cube[0]" --position 0,1,0 --scale 2,2,2
unity-cli scene assign-material --node "/Cube[0]" --material Assets/Materials/Red.mat

# Component operations
unity-cli scene list-components --node "/Cube[0]"
unity-cli scene add-component --path Assets/Scenes/Main.unity --target "/Player[0]" --type Rigidbody --values '{"mass":5}'
unity-cli scene remove-component --path Assets/Scenes/Main.unity --target "/Player[0]" --type BoxCollider --index 0 --force

# Or use spec-based patching for complex edits
unity-cli scene patch --path ... --spec-file patch.json
```

### Prefabs

```bash
unity-cli prefab create --path Assets/Prefabs/Enemy.prefab \
  --spec-json '{"root":{"name":"Enemy","children":[...]}}'
unity-cli prefab inspect --path ... --with-values
unity-cli prefab patch --path ... --spec-json '{"operations":[...]}'

# Component operations
unity-cli prefab list-components --path Assets/Prefabs/Player.prefab --node "/Root[0]"
unity-cli prefab add-component --path Assets/Prefabs/Player.prefab --target "/Root[0]" --type Rigidbody --values '{"mass":5}'
unity-cli prefab remove-component --path Assets/Prefabs/Player.prefab --target "/Root[0]" --type BoxCollider --force
```

Patch ops: `add-child`, `remove-node`, `set-node`, `add-component`, `remove-component`, `set-component-values`

### Materials

```bash
unity-cli material info --path Assets/Materials/Red.mat --omit-defaults
unity-cli material set --path ... --property _BaseColor --value 1,0,0,1
unity-cli material set --path ... --texture _MainTex --asset Assets/Textures/wood.png
```

### Packages

```bash
unity-cli package list
unity-cli package add --name com.unity.inputsystem
unity-cli package remove --name ... --force
unity-cli package search --query "input"
```

### Play Mode QA

```bash
unity-cli qa click --qa-id StartButton
unity-cli qa tap --x 400 --y 300
unity-cli qa swipe --target ... --from 0,0 --to 100,0 --duration 500
unity-cli qa key --key space
unity-cli qa wait-until --scene GameScene --timeout 5000
```

## Token Optimization

For AI agent workflows, minimize token consumption:

```bash
# --output compact: strip envelope metadata, return data payload only
unity-cli asset info --path ... --output compact

# --omit-defaults: strip default/identity values from inspect and material responses
unity-cli scene inspect --path ... --with-values --omit-defaults    # 41% smaller
unity-cli material info --path ... --omit-defaults                  # 71% smaller

# --max-depth: limit hierarchy traversal depth
unity-cli scene inspect --path ... --max-depth 2
```

## Repository Structure

```
cli/UnityCli.Cli/              CLI executable (.NET 9, macOS arm64 + Windows x64)
cli/UnityCli.Protocol/         Shared protocol (linked from Unity package)
unity-package/                 com.kinkeep.unity-cli-bridge (UPM package)
tools/skills/unity-cli-operator/   AI agent skill for consistent CLI usage
tests/                         xUnit CLI tests + live IPC test scenarios
docs/                          Generated CLI reference, specs
```

## Safety Rules

- **Destructive ops require `--force`:** `asset delete`, overwrites, `package remove`, scene `delete-gameobject` / `remove-component`
- **Asset paths:** Write operations are `Assets/...` only. Reads allow `Packages/...` too.
- **Scene paths:** `/Root[0]/Child[0]` format with sibling indices. `/` is the virtual scene root.
- **Inspect before patch:** Always `scene inspect --with-values` or `prefab inspect --with-values` before writing patch specs.
- **set-node warnings:** Unrecognized keys now return warnings instead of silent success.

## Documentation

- [CLI Reference (generated)](docs/cli-reference.md)
- [Architecture](docs/architecture.md)
- [Scene Spec](docs/scene-spec.md)
- [Prefab Spec](docs/prefab-spec.md)
- [Benchmark](https://github.com/yhc509/kinkeep-unity-cli/wiki/Benchmark-Unity-Editor-CLI-Tool-Comparison)
- [Unity Package](unity-package/com.kinkeep.unity-cli-bridge/README.md)

## AI Agent Skill

The `unity-cli-operator` skill keeps agent workflows consistent: pick the right command, do the work, verify with `read-console`.

```bash
# Symlink into your skills directory
ln -sfn "$(pwd -P)/tools/skills/unity-cli-operator" ~/.claude/skills/unity-cli-operator
```

## Development

```bash
dotnet build KinKeepUnityCli.sln -c Debug
dotnet test KinKeepUnityCli.sln
dotnet run --project cli/UnityCli.DocGen -- --check   # Verify docs match code
```

## Current Limits

- macOS arm64 and Windows x64 supported
- Live IPC required — commands fail fast when no Editor is running
- Scene patching targets saved `Assets/...unity` scenes; multi-scene orchestration is out of scope
- Prefab-internal object references and nested variants are not yet supported
