# Troubleshooting

## `liveReachable: false`

- Unity가 import나 compile 중일 수 있다.
- 프로젝트 경로가 심링크 경로일 수 있다. `pwd -P`로 다시 잡는다.
- `instances list`로 다른 프로젝트에 붙은 것은 아닌지 확인한다.

## busy 상태

- `isCompiling` 또는 `isUpdating`이 true면 쓰기 명령은 재시도 흐름으로 본다.
- 이때는 `status`, `read-console`, `asset info`, `asset find`, `prefab inspect` 같은 읽기 위주 명령만 유지한다.

## stale instance

- registry는 `~/Library/Application Support/unity-cli/instances.json`에 있다.
- 오래된 인스턴스가 보이면 에디터를 모두 닫고 다시 열기 전 이 파일 상태를 확인한다.

## live 연결 문제

- 편집기 상태를 바꾸는 명령과 asset/material/package/scene/prefab 명령은 모두 live 연결이 필요하다.
- `liveReachable: false`면 에디터를 열고 bridge import/compile이 끝날 때까지 기다린 뒤 다시 시도한다.
- 프로젝트를 잘못 잡았을 수 있으니 `pwd -P`, `status --project ... --json`, `instances use`를 다시 확인한다.

## 로그 확인

- 성공 응답만 보고 닫지 않는다.
- live 작업 뒤에는 항상 `read-console --type error`와 `--type warning`을 같이 본다.
- 새 에러나 경고가 있으면 먼저 그 원인을 설명하고, 성공으로 보고하지 않는다.
