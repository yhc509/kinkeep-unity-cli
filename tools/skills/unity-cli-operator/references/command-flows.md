# Command Flows

## 실행 파일 찾기

- `UNITY_CLI_BIN`이 있으면 그것을 우선 사용한다.
- 현재 작업 디렉터리나 상위 디렉터리에 `dist/unity-cli/UnityCli.Cli`가 있으면 그 경로를 사용한다.
- 둘 다 없으면 `command -v unity-cli` 결과를 사용한다.
- 셋 다 없으면 빌드나 설치가 필요하다고 보고 진행을 멈춘다.

## 상태 확인

가장 먼저 아래 흐름을 기준으로 본다.

```bash
PROJECT_ROOT="$(pwd -P)"
"$UNITY_CLI_BIN" status --project "$PROJECT_ROOT" --json
```

중요하게 볼 값:

- `transport`
- `projectRoot`
- `isCompiling`
- `isUpdating`

## 기본 운용

- 연결 확인: `status`, `refresh`
- 플레이 상태: `play`, `pause`, `stop`
- 로그 확인: `read-console`
- 메뉴 실행: `execute-menu`

live 편집이 필요한 명령은 에디터가 켜져 있고 busy 상태가 아닐 때 우선 실행한다.

## batch 우선 명령

에디터가 없어도 아래 명령은 batch fallback을 먼저 고려한다.

- `compile`
- `refresh`
- `run-tests`
- `asset info`
- `asset mkdir`
- `asset create`
- `prefab create`
- `prefab inspect`
- `prefab patch`

`play`, `pause`, `stop`, `read-console`, `execute-menu`는 live 전용으로 본다.

## 에셋 작업 흐름

### 조회

```bash
"$UNITY_CLI_BIN" asset find --project "$PROJECT_ROOT" --name Sample --folder Assets --limit 10
"$UNITY_CLI_BIN" asset types --project "$PROJECT_ROOT" --json
"$UNITY_CLI_BIN" asset info --project "$PROJECT_ROOT" --path Assets/Scenes/SampleScene.unity
```

### 생성

```bash
"$UNITY_CLI_BIN" asset create --project "$PROJECT_ROOT" --type material --path Assets/Materials/NewMaterial
"$UNITY_CLI_BIN" asset create --project "$PROJECT_ROOT" --type scriptable-object --path Assets/Data/NewData --type-name MyNamespace.MyData --data-json '{"title":"hello"}'
```

### 안전 규칙

- 삭제는 항상 `--force`
- 이름 변경, 이동, 생성 덮어쓰기는 `--force`
- 먼저 `asset info`나 `asset find`로 대상이 맞는지 본다

## 검증 루틴

live 작업 뒤 기본 검증:

```bash
"$UNITY_CLI_BIN" read-console --project "$PROJECT_ROOT" --type error --limit 10 --json
"$UNITY_CLI_BIN" read-console --project "$PROJECT_ROOT" --type warning --limit 10 --json
```

에러나 경고가 있으면 성공으로 바로 닫지 않는다.
