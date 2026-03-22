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
- scene 전환: `scene open`

live 편집이 필요한 명령은 에디터가 켜져 있고 busy 상태가 아닐 때 우선 실행한다.

## live 연결 전제

`instances list`, `instances use`, `doctor`를 제외한 명령은 실행 중인 Unity Editor와 bridge 연결이 필요하다.

- `status --json`에서 `liveReachable`이 false면 먼저 에디터를 열고 bridge import/compile 완료를 기다린다.
- `compile`, `refresh`, asset/material/package/scene/prefab 명령은 모두 live IPC로 실행한다.
- `play`, `pause`, `stop`, `read-console`, `execute-menu`, `execute`, `custom`, `screenshot`도 live 전용으로 본다.

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

## scene 작업 흐름

### 조회

```bash
"$UNITY_CLI_BIN" scene inspect --project "$PROJECT_ROOT" --path Assets/Scenes/SampleScene.unity --with-values --json
```

### 수정

```bash
"$UNITY_CLI_BIN" scene patch --project "$PROJECT_ROOT" --path Assets/Scenes/SampleScene.unity --spec-file ./tools/skills/unity-cli-operator/assets/scene-patch-basic.json --json
```

### 안전 규칙

- 먼저 `scene inspect --with-values`로 GameObject path와 component field를 확인한다
- node path는 `/Root[0]/Child[0]` 형식으로 쓴다
- `/`는 virtual scene root이고, root GameObject 추가의 parent로만 쓴다
- 대상 scene이 이미 열려 있다면 먼저 저장하거나 변경을 버려서 clean 상태로 맞춘다
- `delete-gameobject`, `remove-component`, dirty scene을 버리는 `scene open`에는 `--force`가 필요하다

## 검증 루틴

live 작업 뒤 기본 검증:

```bash
"$UNITY_CLI_BIN" read-console --project "$PROJECT_ROOT" --type error --limit 10 --json
"$UNITY_CLI_BIN" read-console --project "$PROJECT_ROOT" --type warning --limit 10 --json
```

에러나 경고가 있으면 성공으로 바로 닫지 않는다.
