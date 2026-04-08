# Changelog

## [Unreleased]

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
