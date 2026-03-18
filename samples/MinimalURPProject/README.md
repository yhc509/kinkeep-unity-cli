# Minimal URP Project

This folder is the minimal Unity sample project used for public smoke tests.

Included content:

- `Assets/Scenes`
- `Assets/Settings`
- `Assets/InputSystem_Actions.inputactions`
- `ProjectSettings`
- `Packages/manifest.json`

For local testing, open the project and add the bridge package in one of these ways.

- Working inside the mono-repo: add a local file reference in `Packages/manifest.json`
- Validating the public repository: add either a Git URL or a local path as described in the package README

Recommended smoke test:

```bash
PROJECT_ROOT="$(pwd -P)"
../../dist/unity-cli/UnityCli.Cli status --project "$PROJECT_ROOT" --json
../../dist/unity-cli/UnityCli.Cli refresh --project "$PROJECT_ROOT" --json
../../dist/unity-cli/UnityCli.Cli asset create --project "$PROJECT_ROOT" --type material --path Assets/SmokeMaterial --force --json
../../dist/unity-cli/UnityCli.Cli prefab create --project "$PROJECT_ROOT" --path Assets/Smoke.prefab --spec-file ../../tools/skills/unity-cli-operator/assets/prefab-create-basic.json --force --json
```
