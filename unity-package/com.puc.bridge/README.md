# PUC

`PUC` stands for **Portless Unity CLI**. This package is the bridge that lets `unity-cli` control a running Unity Editor over local IPC. Its key advantage is simple operation: no manual server startup, no per-project ports, and project-aware attachment by default.

## Requirements

- Unity `6000.0` or newer
- the companion CLI `unity-cli`

## What This Package Provides

- A local bridge that starts automatically when the Editor opens
- Per-project instance registration and automatic selection
- Live editor control: `status`, `refresh`, `play`, `pause`, `stop`, `execute-menu`, `read-console`
- Asset commands: `find`, `types`, `info`, `reimport`, `mkdir`, `move`, `rename`, `delete`, `create`
- Prefab commands: `inspect`, `create`, `patch`
- The same command handlers reused through batch fallback when the Editor is not running

## Install

This package lives inside the PUC mono-repo.

For local development, add a file reference to `unity-package/com.puc.bridge` in your Unity project's `Packages/manifest.json`.

For Git-based installation, use the package path inside the repository.

```json
{
  "dependencies": {
    "com.puc.bridge": "https://github.com/<org>/<repo>.git?path=/unity-package/com.puc.bridge#main"
  }
}
```

The CLI executable and Codex skill are not included in the UPM package payload. If you want the full PUC workflow, clone the mono-repo locally and use:

- `cli/` for the `unity-cli` executable
- `tools/skills/unity-cli-operator/` for the Codex skill

## Notes

- This package includes `Newtonsoft.Json.dll` in `Editor/Plugins` for prefab spec parsing.
- `input-actions` assets are created as JSON files that Unity's Input System importer reads.
- `prefab patch` values are applied through `SerializedProperty.propertyPath`.
- `prefab inspect --with-values` is meant to be used as the source of truth when authoring patch specs.
- The root prefab object name is normalized to the prefab file name after save.
- This package does not include the CLI executable itself.
