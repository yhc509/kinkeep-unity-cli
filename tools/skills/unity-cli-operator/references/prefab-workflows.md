# Prefab Workflows

## 기본 원칙

- 간단한 빈 prefab 생성은 `asset create --type prefab`보다 `prefab create`를 우선한다.
- 구조 수정 전에는 `prefab inspect --with-values`를 먼저 실행한다.
- patch spec의 node path는 `/`, `/Visual[0]`, `/Visual[0]/MeshRoot[0]` 형식이다.
- component field 값은 `SerializedProperty.propertyPath` 기준으로 적는다.

## 생성 흐름

```bash
"$UNITY_CLI_BIN" prefab create --project "$PROJECT_ROOT" --path Assets/Prefabs/Enemy.prefab --spec-file /tmp/enemy-prefab.json --force --json
```

빠르게 시작할 때는 `assets/prefab-create-basic.json`을 복사해서 수정한다.

## 조회 흐름

```bash
"$UNITY_CLI_BIN" prefab inspect --project "$PROJECT_ROOT" --path Assets/Prefabs/Enemy.prefab --with-values --json
```

inspect 결과에서 꼭 보는 값:

- `root.children[].path`
- `components[].type`
- `components[].values`

## 수정 흐름

```bash
"$UNITY_CLI_BIN" prefab patch --project "$PROJECT_ROOT" --path Assets/Prefabs/Enemy.prefab --spec-file /tmp/enemy-patch.json --json
```

지원 op:

- `add-child`
- `remove-node`
- `set-node`
- `add-component`
- `remove-component`
- `set-component-values`

빠르게 시작할 때는 `assets/prefab-patch-boxcollider.json`을 기준으로 잡는다.

## 자주 쓰는 값 형식

Vector3:

```json
{ "x": 0, "y": 1, "z": 0 }
```

asset reference:

```json
{ "assetPath": "Assets/Physics/EnemyHit.physicMaterial" }
```

또는

```json
{ "guid": "..." }
```

## 제한사항

- root prefab 이름은 저장 후 파일 이름으로 정규화된다.
- prefab 내부 object reference와 scene object reference는 아직 일반화되어 있지 않다.
- field 이름은 추측하지 말고 inspect 결과를 기준으로 쓴다.
