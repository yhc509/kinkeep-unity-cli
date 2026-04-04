# IPC Token Optimization Design

Reduce LLM token consumption from CLI↔Bridge IPC responses through three incremental phases.

## Problem

CLI responses are consumed by LLMs (Claude). Every byte of output costs tokens. Current issues:

1. **JSON-in-JSON wrapping** — `dataJson` is a string field, causing double escaping
2. **CLI pretty-print overhead** — envelope metadata (status, transport, durationMs) + indentation
3. **Full tree dumps** — inspect returns entire scene/prefab tree with all fields
4. **`--with-values` explosion** — includes identity values, empty arrays, redundant object references
5. **Serialization gaps** — only null is omitted; empty strings, `false`, `0`, `[]` remain

## Phase 1: CLI `--output compact` (CLI-only, no Bridge changes)

### Goal

Add `--output compact` flag that strips envelope metadata and outputs only the data payload as compact JSON.

### Changes

| File | Change |
|------|--------|
| `ResponseFormatter.cs` | Add `OutputMode` enum (`Default`, `Json`, `Compact`). Compact mode: deserialize `dataJson`, output as compact JSON. Error: `{"error":"CODE","message":"..."}` |
| `CliArgumentParser.cs` | Parse `--output compact` option |
| `ParsedCommand.cs` | Add `OutputMode` field |
| `CliApp.cs` | Pass `OutputMode` to formatter |

### Output comparison

```
# Default (current)
status: success
transport: live
target: Epoch
durationMs: 42
data:
  {
    "path": "Assets/Prefab.prefab",
    "type": "GameObject"
  }

# --output compact (new)
{"path":"Assets/Prefab.prefab","type":"GameObject"}
```

### Estimated savings

~30-40% per response (metadata lines + indentation removed).

### Compatibility

Fully backward compatible. Existing `--json` and default output unchanged.

---

## Phase 2: inspect `--max-depth` + `--omit-defaults` (Bridge changes)

### Goal

Reduce inspect payload size by limiting tree depth and omitting identity/default values.

### New options

| Option | Scope | Behavior |
|--------|-------|----------|
| `--max-depth N` | scene/prefab inspect | Traverse only N levels deep. Children beyond depth shown as `{"name":"...","childCount":N}` stub |
| `--omit-defaults` | scene/prefab inspect | Omit fields with identity values |

### Identity values to omit with `--omit-defaults`

- `active: true`
- `tag: "Untagged"`
- `layer: 0` (Default)
- `transform.position/rotation/scale` at identity `(0,0,0)/(0,0,0,1)/(1,1,1)`
- Empty `components: []`
- Empty `children: []`
- `--with-values`: skip `0`, `0.0`, `false`, `""`, empty arrays, null object references

### Changes

| File | Change |
|------|--------|
| `SceneInspector.cs` | Accept maxDepth param, emit stubs beyond limit |
| `PrefabInspector.cs` | Same as above |
| `SceneCommandHandler.cs` | Parse and forward options |
| `PrefabCommandHandler.cs` | Parse and forward options |
| `CommandModels.cs` | Add `maxDepth`/`omitDefaults` to inspect args |
| `CliArgumentParser.cs` | Parse `--max-depth`, `--omit-defaults` |
| `ParsedCommand.cs` | Add fields to inspect command variants |
| `CliCommandCatalog.cs` | Register new options |

### Estimated savings

50%+ reduction on inspect responses (additive with Phase 1).

### Compatibility

Opt-in flags. Default behavior unchanged. No breaking changes.

---

## Phase 3: Protocol v2 — `dataJson` → `data` (Breaking change)

### Goal

Eliminate JSON-in-JSON double escaping by making `data` a first-class object in the response envelope.

### Changes

**ResponseEnvelope v2:**

```csharp
// v1 (current)
public string? dataJson;    // serialized JSON string

// v2 (new)
public object? data;        // direct object, no double-serialization
```

**Migration strategy:**

1. Bridge sends v2 envelope with `data` field (object)
2. CLI attempts v2 parse first; falls back to v1 (`dataJson` string) if `data` is absent
3. Deprecation period: Bridge sends both `data` and `dataJson` for one minor version
4. Remove `dataJson` in next major version

**Pagination (asset list, inspect):**

```csharp
// Standard pagination envelope
public int? offset;
public int? limit;
public int? total;
public object? data;  // current page
```

- `asset find` and `asset list`: default limit stays 50, but `--limit`/`--offset` standardized
- `inspect`: no pagination (tree structure doesn't paginate well)

### Changes

| File | Change |
|------|--------|
| `TransportModels.cs` | Add `data` field, keep `dataJson` for compat |
| `BridgeHost.cs` | Serialize response with `data` as object |
| `LocalIpcClient.cs` | Parse v2 envelope, fallback to v1 |
| `ResponseFormatter.cs` | Handle `data` object directly |
| `ProtocolJson.cs` | Update serialization for mixed envelope |

### Phase 3 sub-phases

**Phase 3a (current): CLI-side preparation**
- Add `data` field to ResponseEnvelope with `[NonSerialized]` (Unity-invisible)
- CLI `EnsureData()` promotes `dataJson` → `data` (JsonElement) on receipt
- ResponseFormatter uses `data` for default/compact output (no double-escape)
- `--json` mode suppresses `dataJson` when `data` is present
- Bridge unchanged: still sends `dataJson` only

**Phase 3b (future): Bridge migration**
- Migrate Bridge serialization from JsonUtility to Newtonsoft
- Bridge populates `data` directly with response objects
- Remove `[NonSerialized]` from `data` field
- Deprecation period: send both `data` and `dataJson`

**Phase 3c (future): dataJson removal**
- Remove `dataJson` field after deprecation period
- Update CLI to only use `data`

### Estimated savings

Additional 10-20% (eliminates escape overhead). Cumulative: 60-80%.

### Compatibility

**Breaking.** Mitigation: dual-field deprecation period. CLI auto-detects version.

---

## Implementation order

```
Phase 1 (CLI-only)     → immediate, safe, quick win
Phase 2 (Bridge opts)  → after Phase 1 merged, opt-in flags
Phase 3 (Protocol v2)  → after Phase 2 stable, requires version bump
```

## Skill integration

Update `unity-cli-operator` skill to default `--output compact` in all LLM command templates.
Update `command-flows.md` reference with new options.
