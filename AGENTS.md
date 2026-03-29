# unity-cli 작업 규칙

## 목표

- 이 저장소의 목표는 `수동 서버 실행 없이 Unity Editor를 CLI로 제어`하는 것입니다.
- 문제를 막는 방향보다, 왜 이런 불편이 생겼는지 원인을 줄이는 방향을 우선합니다.

## 검증 기본값

- CLI 코드 수정 후:
  - `dotnet build KinKeepUnityCli.sln -c Debug`
- 테스트 실행 후:
  - `/opt/homebrew/Cellar/dotnet/9.0.112/libexec/dotnet test KinKeepUnityCli.sln`
- Unity 연동 수정 후:
  - 실제 Unity 프로젝트에서 `status`, `refresh`, `asset info`, `asset find`, `asset create`를 live로 확인
  - 실제 Unity 프로젝트에서 `prefab create`, `prefab inspect`, `prefab patch`를 live로 확인
  - live IPC가 끊겼을 때 CLI가 즉시 오류를 반환하는지 확인

## 문서 동기화

- CLI 명령 추가나 옵션 변경이 있으면 `README.md`의 사용 예시와 help 텍스트를 같이 갱신합니다.
- 배포 방식이 바뀌면 `scripts/publish-osx-arm64.sh`와 `README.md`를 같이 맞춥니다.

## 에셋 작업 안전 규칙

- 파괴 연산은 기본 차단합니다.
- `asset delete`는 항상 `--force`가 필요합니다.
- `asset move`, `asset rename`에서 기존 asset를 덮어쓰는 동작은 `--force`가 있을 때만 허용합니다.
- `asset create`에서 기존 asset를 덮어쓰는 동작도 `--force`가 있을 때만 허용합니다.
- 모든 asset 경로는 `Assets/...` 형식으로 다룹니다.

## 경로 규칙

- macOS에서는 심링크 경로 대신 실제 경로를 기준으로 해시와 registry를 맞춥니다.
- 테스트와 문서 예시에서도 `pwd -P`를 우선합니다.
