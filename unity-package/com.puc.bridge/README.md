# PUC

`PUC` stands for **Portless Unity CLI**. This package is the bridge that lets `unity-cli` control a running Unity Editor over local IPC. Its key advantage is simple operation: no manual server startup, no per-project ports, and project-aware attachment by default.

## Requirements

- Unity `6000.0` or newer
- the companion CLI `unity-cli`

## What This Package Provides

- A local bridge that starts automatically when the Editor opens
- Per-project instance registration and automatic selection
- Live editor control: `status`, `refresh`, `play`, `pause`, `stop`, `execute-menu`, `execute`, `custom`, `read-console`
- Asset commands: `find`, `types`, `info`, `reimport`, `mkdir`, `move`, `rename`, `delete`, `create`
- Material commands: `info`, `set`
- Package commands: `list`, `add`, `remove`, `search`
- Scene commands: `open`, `inspect`, `patch`
- Prefab commands: `inspect`, `create`, `patch`
- The same command handlers reused through batch fallback when the Editor is not running

In practice, this means the package can expose Unity as a project-aware automation surface instead of a manually managed editor plugin session.

- It removes the need to keep a custom bridge server running.
- It removes per-project port configuration when several editors are open.
- It keeps live editor work and batch fallback on the same protocol and command model.
- It gives the CLI direct access to asset, material, package, and prefab workflows instead of relying only on menu execution.

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

- This package includes `Newtonsoft.Json.dll` in `Editor/Plugins` for scene/prefab spec parsing.
- `input-actions` assets are created as JSON files that Unity's Input System importer reads.
- `scene inspect --with-values` is meant to be used as the source of truth when authoring `scene patch` specs.
- `scene patch` uses `/Root[0]/Child[0]` paths, treats `/` as the virtual scene root, and requires `--force` for destructive ops.
- `execute` is live-only and always requires `--force` because it runs arbitrary C# in the editor context.
- `custom` is live-only and invokes project-defined static methods marked with `[PucCommand("name")]`.
- if the target scene is already loaded, `scene inspect` and `scene patch` expect it to be clean before they run.
- `scene open` requires `--force` if the currently loaded scenes have unsaved changes that should be discarded.
- `prefab patch` values are applied through `SerializedProperty.propertyPath`.
- `prefab inspect --with-values` is meant to be used as the source of truth when authoring patch specs.
- The root prefab object name is normalized to the prefab file name after save.
- This package does not include the CLI executable itself.
