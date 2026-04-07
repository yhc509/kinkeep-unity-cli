# QA Test Automation

Play Mode에서 입력을 시뮬레이션하고 결과를 검증하는 흐름이다.

## 테스트 흐름

```
status → play → (입력 시뮬레이션) → 검증 (로그 + 스크린샷) → stop
```

1. `status --json`으로 `isCompiling: false` 확인
2. `play`로 Play Mode 진입 (자동으로 `runInBackground = true` 설정됨)
3. QA 커맨드로 입력 시뮬레이션
4. **로그 검증**: `read-console --type error --limit N` + `read-console --type log --limit N`
5. **시각 검증**: `screenshot --view game --path /tmp/qa-check.png` 후 이미지 확인
6. `stop`으로 Play Mode 종료 (`runInBackground` 원래값 복원됨)

## 입력 방식 선택

| 커맨드 | 동작 방식 | 사용 시점 |
|--------|-----------|-----------|
| `qa click --target <path>` | EventSystem.Execute | UI 버튼, 토글 등 클릭 |
| `qa click --qa-id <id>` | EventSystem.Execute | [QaTarget] 어트리뷰트로 마킹된 대상 |
| `qa tap --x N --y N` | Input System 좌표 | 화면 특정 좌표 탭 (비-UI 포함) |
| `qa swipe --target <path> --from x,y --to x,y` | Input System target-relative swipe | UI 슬라이더, ScrollRect 드래그 |
| `qa swipe --from x,y --to x,y` | Input System 좌표 | 화면 좌표 기반 스와이프 |
| `qa key --key <name>` | Input System 키보드 | 키 입력 시뮬레이션 |
| `qa wait --ms N` | CLI-side 대기 | 명령 사이 간격 |
| `qa wait-until --scene <name>` | Bridge 폴링 | 씬 전환 대기 |
| `qa wait-until --log-contains <text>` | Bridge 폴링 | 특정 로그 출력 대기 |
| `qa wait-until --object-exists <path>` | Bridge 폴링 | 오브젝트 존재 대기 |

## 입력 방식 판단 기준

### UI 클릭 → EventSystem 경로 (click)
- 버튼 클릭, 토글 등 EventSystem을 통해 즉시 처리하는 대상
- `--target`으로 hierarchy path 지정: `/Canvas[0]/Button[0]`
- EventSystem이 씬에 있어야 함 (없으면 `QA_NO_EVENT_SYSTEM` 에러)

### UI 드래그 → target-relative Input System 경로 (swipe --target)
- `--target`으로 hierarchy path 지정: `/Canvas[0]/Scroll View[0]/Viewport[0]`
- target은 `RectTransform`이 있어야 함 (없으면 `QA_TARGET_NOT_RECT_TRANSFORM` 에러)
- `--from/--to`는 target `RectTransform` 중심 기준 **픽셀 오프셋**
- Bridge가 target-relative 오프셋을 화면 좌표로 변환한 뒤 multi-frame deferred swipe로 실행
- UGUI가 입력을 소비하려면 씬에 EventSystem이 있어야 함

### 비-UI 또는 좌표 기반 → Input System 경로 (tap, swipe, key)
- 3D 오브젝트 터치, 화면 스와이프 제스처, 키보드 입력
- Input System 패키지 필요 (없으면 `QA_INPUT_SYSTEM_REQUIRED` 에러)
- `tap`, 모든 `swipe`, `key`는 deferred 또는 frame-based 입력 경로를 사용

## 사전 조건

### EventSystem
`qa click`과 UGUI 대상 `qa swipe --target`은 씬에 EventSystem이 필요하다.
없으면 메뉴로 추가:

```bash
# 먼저 메뉴 경로 확인
ucli execute-menu --list "GameObject/UI" --project "$P" --json

# EventSystem 추가
ucli execute-menu --path "GameObject/UI (Canvas)/Event System" --project "$P" --json
```

### Input System
`qa tap`, `qa key`, 모든 `qa swipe`는 Input System 패키지가 필요하다.
미설치 시 Bridge가 정상 동작하되, 해당 커맨드만 `QA_INPUT_SYSTEM_REQUIRED` 에러를 반환한다.

### Play Mode
모든 QA 커맨드는 Play Mode에서만 실행 가능하다. Play Mode가 아니면 `QA_NOT_PLAYING` 에러.

## 테스트 UI 구성

`execute-menu`로 Unity 기본 메뉴를 사용해 UI를 추가한다. 커스텀 커맨드 불필요.

```bash
P="$(pwd -P)"

# 메뉴 경로 조회 (Unity 버전마다 다름)
ucli execute-menu --list "GameObject/UI (Canvas)" --project "$P" --json

# Canvas + EventSystem + Button 추가
ucli execute-menu --path "GameObject/UI (Canvas)/Canvas" --project "$P" --json
ucli execute-menu --path "GameObject/UI (Canvas)/Event System" --project "$P" --json
ucli execute-menu --path "GameObject/UI (Canvas)/Button - TextMeshPro" --project "$P" --json
```

커스텀 스크립트가 필요하면 `execute --code`로 C# 코드를 실행한다. `using` 지시문을 자유롭게 사용 가능:

```bash
ucli execute --force --project "$P" --code '
using UnityEngine.UI;
// 코드 본문
'
```

## 검증 방법

### 로그 검증
`read-console`로 Debug.Log 출력과 에러를 확인한다.

```bash
ucli read-console --type log --limit 5 --project "$P" --json
ucli read-console --type error --limit 5 --project "$P" --json
```

### 시각 검증 (스크린샷)
`screenshot --view game`으로 Game View를 캡처해서 시각적 변화를 확인한다.

```bash
ucli screenshot --view game --path /tmp/qa-check.png --project "$P" --json
# 이후 이미지를 Read 도구로 확인
```

**제한사항**: Play Mode에서는 `ScreenCapture.CaptureScreenshotAsTexture()`를 사용하므로 ScreenSpaceOverlay Canvas(UGUI)까지 포함해 캡처된다. 비 Play Mode에서는 `Camera.Render()` fallback을 사용하므로 ScreenSpaceOverlay Canvas는 포함되지 않는다.

검증 순서는 **로그 먼저, 스크린샷은 보조**로 사용한다. 로그가 이벤트 발생을 확인하는 1차 수단이고, 스크린샷은 시각적 상태를 보조적으로 확인한다.

## 예시: 버튼 클릭 테스트

```bash
P="$(pwd -P)"

ucli play --project "$P" --json
sleep 1

ucli qa click --target "/Canvas/Button" --project "$P" --json

# 로그 검증
ucli read-console --type log --limit 5 --project "$P" --json

# 시각 검증
ucli screenshot --view game --path /tmp/qa-click.png --project "$P" --json

ucli stop --project "$P" --json
```

## 예시: 조건 대기 후 검증

```bash
ucli play --project "$P" --json
sleep 1

# 특정 씬으로 전환될 때까지 대기 (최대 10초)
ucli qa wait-until --scene GameScene --timeout 10000 --project "$P" --json

# 전환 후 입력 시뮬레이션
ucli qa click --target "/Canvas/StartButton" --project "$P" --json
ucli qa wait-until --log-contains "Game started" --timeout 5000 --project "$P" --json

ucli stop --project "$P" --json
```

## 알려진 제한사항

- **좌표 기준**: `qa swipe --target`의 `--from/--to`는 절대 화면 좌표가 아니라 target `RectTransform` 중심 기준 픽셀 오프셋이다.
- **스크린샷**: Play Mode에서는 `ScreenCapture` 기반으로 Overlay Canvas까지 캡처된다. 비 Play Mode에서는 `Camera.Render()` fallback이라 ScreenSpaceOverlay Canvas는 포함되지 않는다.
- **using 제한**: `execute --code`의 using 추출은 `using Namespace;`, `using static Namespace.Type;`, `using Alias = Namespace.Type;`를 지원한다. 단, `using Dict = Dictionary<string, int>;` 같은 generic type alias는 지원하지 않는다.
- **Game View 갱신**: `play` 커맨드가 `runInBackground = true`를 자동 설정하므로, Unity가 포커스를 잃어도 Game View가 갱신된다. `stop` 시 원래값으로 복원된다.

## 트러블슈팅

| 에러 코드 | 원인 | 해결 |
|-----------|------|------|
| `QA_NOT_PLAYING` | Play Mode가 아님 | `play` 먼저 실행 |
| `QA_NO_EVENT_SYSTEM` | EventSystem 없음 | `execute-menu`로 추가 |
| `QA_INPUT_SYSTEM_REQUIRED` | Input System 미설치 | `package add --name com.unity.inputsystem` |
| `QA_TARGET_NOT_FOUND` | hierarchy path 틀림 | `scene inspect`로 정확한 path 확인 |
| `QA_TARGET_NOT_RECT_TRANSFORM` | `qa swipe --target` 대상이 UI `RectTransform`이 아님 | UI hierarchy path로 다시 지정 |
| `QA_NO_POINTER_DEVICE` | Mouse/Touchscreen 없음 | Input System 디바이스 확인 |
| `QA_WAIT_TIMEOUT` | 조건 미충족 타임아웃 | 조건 확인, timeout 늘리기 |
| `BUSY` | Unity 컴파일/업데이트 중 | 컴파일 완료 후 재시도 |
