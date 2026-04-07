# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Changed
- Improved error message for unsupported SerializedPropertyType to include the actual type name

### Documentation
- ExposedReference and FixedBufferSize are intentionally unsupported (extremely rare in typical scene/prefab workflows)

## [0.1.2] - 2026-04-07

### Added
- Component operations for scenes and prefabs: list, add, and remove components from the CLI (`scene list-components`, `scene add-component`, `scene remove-component`, `prefab list-components`, `prefab add-component`, and `prefab remove-component`) (#6).
- An AI skill installer in `KinKeep > CLI Manager` for Claude Code and Codex (#8).
- Latest GitHub release version display in `KinKeep > CLI Manager` to simplify update checks (#9).
- `SerializedValueApplier` support for `AnimationCurve` (with `preWrapMode`/`postWrapMode`), `Gradient`, `ManagedReference` (`[SerializeReference]`), and `Hash128` property types — enabling inspect/patch for virtually all Unity built-in components.

### Changed
- **Breaking:** Unified `--target` to `--node` in scene/prefab `add-component` and `remove-component` commands for consistency with other node-targeting commands.
- Split `InspectorUtility` into focused utility classes (`InspectorJsonWriterUtility`, `InspectorPathParserUtility`, `InspectorMutationReaderUtility`, `InspectorDefaultPruningUtility`) for maintainability.
- Reduced GC allocations across bridge handlers: single-parse `argumentsJson`, `ComponentEntry` struct conversion, cached protocol commands and asset descriptors, closure and LINQ elimination.
- Moved `Socket.Bind`/`Listen` to a background thread in `BridgeHost` to avoid editor startup hitch.

### Fixed
- A null check after `AddComponent` to prevent a null reference exception during component creation (#7).
- CLI Manager now preserves the last known release version on network failure instead of clearing the cache.
- Added `UnityWebRequest` timeouts (15 s for version checks, 60 s for downloads).
- Cached `IsUpdateAvailable()` result to avoid repeated version parsing in `OnGUI`.

## [0.1.1] - 2026-04-05

### Added
- `CLI Manager`, an EditorWindow for one-click `unity-cli` install and update from the Unity Editor (#5).
- CI status checks for pull requests and pushes to `main`.

### Changed
- Renamed the CLI binary from `puc` to `unity-cli` (#5).

## [0.1.0] - 2026-04-05

### Added
- Initial release of the KinKeep mono-repo with a .NET 9 CLI, a Unity UPM package, and shared protocol models.
- Live IPC control for a running Unity Editor over local transports, with no manual bridge startup required.
- Scene and prefab inspect and patch workflows for structured hierarchy edits and serialized value changes.
- Asset workflows for search, metadata inspection, creation, move, rename, and delete operations.
- Commands for screenshots, materials, packages, custom execution, and Play Mode QA automation.
- Token-saving output modes for AI workflows, including `--output compact`, `--max-depth`, and `--omit-defaults`.
- Cross-platform distribution for macOS arm64 and Windows x64, plus a GitHub Actions release workflow (#3).

### Changed
- Rebranded the project from `PUC` to `KinKeep`.

### Fixed
- Prevented the Unity "modified externally" dialog after scene and prefab saves (#1).
- Preserved missing `.meta` files during relevant asset operations (#2).
- Improved Windows pipe reconnect behavior and reduced instance registry file contention (#4).

### Removed
- Removed batch mode support; the CLI is now live IPC only and requires a running Unity Editor.
