# Prefab Workflows

## 기본 원칙

- 간단한 빈 prefab 생성은 `asset create --type prefab`보다 `prefab create`를 우선한다.
- 구조 수정 전에는 `prefab inspect --with-values`를 먼저 실행한다.
- patch spec의 node path는 `/`, `/Visual[0]`, `/Visual[0]/MeshRoot[0]` 형식이다.
- component field 값은 `SerializedProperty.propertyPath` 기준으로 적는다.

## 생성 흐름

```bash
ucli prefab create --project "$PROJECT_ROOT" --path Assets/Prefabs/Enemy.prefab --spec-file /tmp/enemy-prefab.json --force --output compact
```

빠르게 시작할 때는 `assets/prefab-create-basic.json`을 복사해서 수정한다.

## 조회 흐름

```bash
# 구조 확인 (깊이 제한 + 기본값 생략)
ucli prefab inspect --project "$PROJECT_ROOT" --path Assets/Prefabs/Enemy.prefab --max-depth 2 --omit-defaults --output compact

# component 값까지 확인 (patch 전 필수)
ucli prefab inspect --project "$PROJECT_ROOT" --path Assets/Prefabs/Enemy.prefab --with-values --output compact
```

inspect 결과에서 꼭 보는 값:

- `root.children[].path`
- `components[].type`
- `components[].values`

`--omit-defaults` 결과는 read-only이다. patch input으로 그대로 쓰면 생략된 필드가 복원되지 않는다.

## 수정 흐름

```bash
ucli prefab patch --project "$PROJECT_ROOT" --path Assets/Prefabs/Enemy.prefab --spec-file /tmp/enemy-patch.json --output compact
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
