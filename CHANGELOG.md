# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Component operations for scenes and prefabs: list, add, and remove components from the CLI (`scene list-components`, `scene add-component`, `scene remove-component`, `prefab list-components`, `prefab add-component`, and `prefab remove-component`) (#6).
- An AI skill installer in `KinKeep > CLI Manager` for Claude Code and Codex (#8).
- Latest GitHub release version display in `KinKeep > CLI Manager` to simplify update checks (#9).

### Fixed
- A null check after `AddComponent` to prevent a null reference exception during component creation (#7).

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
