---
name: unity-cli-operator
description: "Use when the user wants to operate Unity through `unity-cli`, including live command selection, asset commands, scene/prefab inspect/patch flows, console-log verification, or Unity CLI Bridge troubleshooting."
---

# Unity CLI Operator

`unity-cli`를 실제 작업에 안전하게 쓰기 위한 운용 스킬이다. 목적은 명령어 목록을 길게 나열하는 것이 아니라, 현재 프로젝트를 올바르게 잡고, 맞는 명령을 고르고, 작업 뒤 로그까지 확인하는 흐름을 고정하는 것이다.

## Quick Workflow

1. 실행 파일을 찾는다.
- 우선순위는 `UNITY_CLI_BIN` 환경 변수, `ucli` (PATH 상의 symlink), 현재 repo의 `dist/unity-cli/UnityCli.Cli` 순서다.
- `ucli`가 PATH에 있으면 그대로 사용한다. 매 호출마다 변수를 재정의하지 않는다.
- 셋 다 없으면 `ln -s ~/dev/unity/unity-cli/dist/unity-cli/UnityCli.Cli ~/bin/ucli`로 symlink을 만든다.

2. 프로젝트 루트를 실제 경로로 맞춘다.
- macOS에서는 항상 `pwd -P`를 우선한다.
- 필요하면 `--project "<real-path>"`를 명시한다.

3. 쓰기 작업 전에는 상태를 본다.
- 먼저 `status --json`으로 live 연결, busy 상태, 현재 프로젝트가 맞는지 확인한다.

4. 작업 종류에 맞는 흐름을 고른다.
- 일반 명령, 에셋 작업, scene inspect/patch는 `references/command-flows.md`
- 프리팹 조립과 patch spec은 `references/prefab-workflows.md`
- 문제 해결은 `references/troubleshooting.md`

5. 작업 뒤에는 반드시 검증한다.
- live 작업 뒤에는 `read-console --type error --limit N`
- live 작업 뒤에는 `read-console --type warning --limit N`

## Operating Rules

- 모든 asset 경로는 `Assets/...` 형식으로 다룬다.
- 파괴 연산과 덮어쓰기는 `--force`가 있을 때만 허용된다고 가정한다.
- `scene patch` 전에는 가능하면 `scene inspect --with-values`를 먼저 실행해서 GameObject path와 field 이름을 확인한다.
- `prefab patch` 전에는 가능하면 `prefab inspect --with-values`를 먼저 실행해서 path와 field 이름을 확인한다.
- `SerializedProperty.propertyPath`는 추측하지 말고 inspect 결과를 기준으로 쓴다.
- live 편집 명령이 compile/update 중이면 읽기 전용 명령만 남기고 나머지는 재시도 흐름으로 본다.
- scene path는 `/Root[0]/Child[0]` 형식으로 쓰고 `/`는 virtual scene root로 본다.
- root prefab 이름은 Unity 저장 규칙 때문에 파일 이름으로 정규화된다고 가정한다.

## What To Read Next

- 일반 운용, 에셋 생성, scene inspect/patch: [references/command-flows.md](references/command-flows.md)
- prefab create/inspect/patch와 spec 작성: [references/prefab-workflows.md](references/prefab-workflows.md)
- stale instance, busy, liveReachable, 로그 확인: [references/troubleshooting.md](references/troubleshooting.md)
- 빠르게 시작할 JSON 템플릿: `assets/`
