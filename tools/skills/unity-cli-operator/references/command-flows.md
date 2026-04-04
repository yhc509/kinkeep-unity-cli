# Command Flows

## 실행 파일 찾기

- `UNITY_CLI_BIN`이 있으면 그것을 우선 사용한다.
- 현재 작업 디렉터리나 상위 디렉터리에 `dist/unity-cli/UnityCli.Cli`가 있으면 그 경로를 사용한다.
- 둘 다 없으면 `command -v unity-cli` 결과를 사용한다.
- 셋 다 없으면 빌드나 설치가 필요하다고 보고 진행을 멈춘다.

## 프로젝트 결정

**모든 명령에 `--project`를 반드시 붙인다.** 생략하면 CLI가 임의의 live 인스턴스에 연결하여 잘못된 프로젝트에 명령이 실행될 수 있다.

```bash
# 현재 디렉터리가 Unity 프로젝트라면
PROJECT="$(pwd -P)"

# unity-cli 레포에서 개발/테스트 중이라면
PROJECT="kinkeep-unity-cli-sample"

# 특정 프로젝트를 지정하려면 (이름 또는 경로)
PROJECT="kinkeep-hd2d-tilemap-sample"
```

여러 프로젝트가 동시에 열려 있을 때 확인:

```bash
ucli instances list
```

## 상태 확인

가장 먼저 아래 흐름을 기준으로 본다.

```bash
"$UNITY_CLI_BIN" status --project "$PROJECT" --json
```

중요하게 볼 값:

- `transport`
- `projectName` — **의도한 프로젝트가 맞는지 반드시 확인**
- `projectRoot`
- `isCompiling`
- `isUpdating`

## 기본 운용

- 연결 확인: `status`, `refresh`
- 플레이 상태: `play`, `pause`, `stop`
- 로그 확인: `read-console`
- 메뉴 실행: `execute-menu`
- 메뉴 조회: `execute-menu --list`
- scene 전환: `scene open`

live 편집이 필요한 명령은 에디터가 켜져 있고 busy 상태가 아닐 때 우선 실행한다.

## 메뉴 실행 (execute-menu)

### 언제 메뉴 조회를 먼저 하는가

메뉴 경로는 Unity 버전마다 다르다. **경로를 추측하지 말고** 반드시 `--list`로 먼저 확인한다.

권장 사례:
- **UI 요소 추가 전**: Unity 6에서 `GameObject/UI/Button`은 없고 `GameObject/UI (Canvas)/Button - TextMeshPro`가 있다. 항상 조회 먼저.
- **첫 메뉴 실행 전**: 프로젝트에 연결한 뒤 한 번은 `--list "GameObject"`로 전체 메뉴 구조를 파악한다.
- **`executed: false` 반환 시**: 경로가 틀렸거나 validation 실패. `--list`로 올바른 경로를 확인한다.
- **커스텀 패키지 메뉴**: 패키지가 등록한 메뉴는 표준 경로와 다를 수 있으므로 항상 조회.

```bash
# 전체 GameObject 하위 메뉴 조회
ucli execute-menu --list "GameObject" --project "$PROJECT_ROOT" --json

# UI 관련 메뉴만 조회
ucli execute-menu --list "GameObject/UI" --project "$PROJECT_ROOT" --json

# 조회 결과에서 정확한 경로로 실행
ucli execute-menu --path "GameObject/UI (Canvas)/Button - TextMeshPro" --project "$PROJECT_ROOT" --json
```

### execute-menu로 충분한 경우

3D Object, UI, Audio, Light, Effects 등 Unity 기본 메뉴에서 오브젝트를 추가하는 경우 별도 템플릿 커맨드 없이 `execute-menu`로 충분하다. 새 커맨드를 만들기 전에 메뉴로 해결 가능한지 먼저 확인한다.

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
