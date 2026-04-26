# Changelog

## [Unreleased]

## [0.1.5] - 2026-04-26

### Added
- `screenshot` response now includes 4 metadata fields — `screenWidth`, `screenHeight`, `coordinateOrigin`, `imageOrigin` — so callers can derive the `qa tap` coordinate system from a single screenshot response.

### Changed
- AI skills installer in `KinKeep > CLI Manager` writes to the user's global skills directory (`~/.claude/skills/`, `~/.codex/skills/`) instead of the project root.
- Bundled `unity-cli-operator` skill rewritten to actively trigger on Unity tasks and explicitly close off `Unity -batchmode`/MCP detour paths.
- Package author changed from `yhjang` to `KinKeep`.

## [0.1.4] - 2026-04-08

### Changed
- Improved error message for unsupported SerializedPropertyType to include the actual type name

### Documentation
- ExposedReference and FixedBufferSize are intentionally unsupported (extremely rare in typical scene/prefab workflows)

## [0.1.0] - 2026-03-17

- Initial package release
- Added local IPC bridge auto-start and instance registry integration
- Added live editor control for status, refresh, play state, menu execution, and console reads
- Added batch command runner support for editor-off automation
- Added asset query, mutation, and common asset creation support
- Added prefab inspect, create, and patch commands for hierarchy edits and serialized field updates
- Added editor-local `Newtonsoft.Json.dll` dependency for prefab spec parsing
