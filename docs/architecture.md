# 일곱 걸음 Unity 아키텍처

담당: 아키텍트 (`feat/architecture`). 기준 코드: v0 (`7lucky` 0568bfb).
이 문서에 적힌 공용 타입·규칙은 고지 없이 바꾸지 않는다. 바꿀 때는 맨 아래 **변경 기록**에 날짜와 내용을 적고 리드에게 보고한다 (`agents/README.md`).

---

## 1. 결론 요약

| 항목 | 결정 |
|---|---|
| 어셈블리 | `Core` ← `Contracts` ← `Game` ← `Editor`. 테스트는 `Tests.EditMode` (Core, Contracts만 참조). 순환 금지 |
| 상태 변경 | `Rules.Dispatch(State, Command)` 하나. 게임 쪽에서는 `GameSession.Send(Command)`만 호출 |
| 상태 읽기 | `State`는 불변 스냅샷. 화면은 `State` + `StepResult`만 읽는다 |
| 입력 락 | `GameSession`이 소유. 연출 중 이동/KEEP/SWITCH는 최대 2개 큐, Undo/Restart/스테이지 이동은 즉시 끊고 스냅샷으로 다시 그림 |
| Undo | **스냅샷 재그리기 필수.** Undo는 이전 `State` 객체를 그대로 돌려받고, 보드는 `Snap(state)`로 처음부터 다시 그린다. 역재생 연출 금지 |
| 레벨 데이터 | `levels.json`(TextAsset) 유지. ScriptableObject로 바꾸지 않는다 |
| 타일 → 프리팹 | `TileSet` ScriptableObject + `TileVisual` 구조체. 보드 코드는 프리팹을 `TileSet`으로만 얻는다 |
| 좌표 | `BoardGrid`: 셀 (x, y) → 월드 (x, 0, -y), 보드 중심 원점, 1칸 = `cellSize`(기본 1). 탑다운과 쿼터뷰는 같은 격자, 카메라만 다름 |
| 씬 | `Boot` → `Title` → `Game`. 세 씬 모두 게임플레이/UI 소유, 빌드 목록은 리드 소유 |

---

## 2. 어셈블리와 폴더

```
SevenSteps.Core        Scripts/Runtime/Core        noEngineReferences  (코어)
   ▲
SevenSteps.Contracts   Scripts/Runtime/Contracts   UnityEngine 사용     (아키텍트)  공용 인터페이스·타입
   ▲
SevenSteps.Game        Scripts/Runtime/Game        + InputSystem, uGUI  (게임플레이/UI)  화면·입력·사운드·HUD
   ▲
SevenSteps.Editor      Scripts/Editor              Editor 전용          (여러 소유자, 파일 단위)

SevenSteps.Tests.EditMode  Tests/EditMode          Editor 전용, UNITY_INCLUDE_TESTS → Core, Contracts
```

- 화살표 방향으로만 참조한다. `Core`는 아무것도 참조하지 않는다. `Contracts`는 `Game`을 모른다.
- UI(HUD, 패널)는 별도 어셈블리로 나누지 않고 `SevenSteps.Game` 안의 `Game/UI/` 폴더, 네임스페이스 `SevenSteps.Game.UI`로 둔다. 잼 규모에서는 컴파일 시간 이득이 없고, `GameController` ↔ HUD 순환을 막으려면 어차피 같은 어셈블리여야 한다. HUD는 `GameSession.Updated`와 `State`만 읽는다.
- 새 asmdef가 필요하면 아키텍트에게 요청한다. asmdef 파일은 아키텍트 소유다 (위치한 폴더의 소유자와 무관).
- 테스트 프레임워크: `com.unity.test-framework` 1.8.0 (manifest에 명시).

### 파일 소유권 (이 문서가 지정하는 공용 파일)

| 경로 (`unity/` 기준) | 소유자 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Contracts/**` | 아키텍트 (공용) |
| `Assets/_Project/Scripts/Editor/Architecture/**` | 아키텍트 |
| `Assets/_Project/Tests/EditMode/Contracts/**` | 아키텍트 |
| `Assets/_Project/Tests/EditMode/**` (위 제외), `Tests/Golden/**` | 코어 |
| `Assets/_Project/Data/TileSets/TopDown_Placeholder.asset` | 아키텍트 (`TileSetBuilder`가 생성) |
| `Assets/_Project/Data/TileSets/**` (위 제외) | 아트 |
| `**/*.asmdef`, `Packages/manifest.json`, `Packages/packages-lock.json` | 아키텍트 |

나머지는 `agents/README.md` 표를 따른다.

---

## 3. 코어 공개 API (코어 소유, 형태 고정)

### 3.1 명령

```csharp
public readonly struct Command { CommandType Type; Dir Dir; }
Command.Move(Dir.Up|Down|Left|Right), Command.Keep, Command.Switch, Command.Undo, Command.Restart
Command.FromAct(Act)        // 리플레이 글자 U D L R K X
```

- 상태를 바꾸는 진입점은 `Rules.Dispatch(State, Command) → StepResult` 하나뿐이다. `Rules.Step`/`Choose`/`Apply`는 솔버·테스트용이다.
- 게임 쪽 코드는 `Rules.Dispatch`도 직접 부르지 않고 `GameSession.Send`를 쓴다 (입력 락 때문).
- 규칙이 거부한 명령(벽에 부딪힘, 승리 후 이동 등)은 **같은 State 객체**를 돌려준다. `ReferenceEquals(result.State, prev)`로 판단한다.

### 3.2 상태 읽기

- `State`는 불변이다. 속성은 `internal set`이라 Core 밖에서 바꿀 수 없다. 규칙은 바꾸기 전에 복제한다.
- 화면이 읽어도 되는 것: `State`의 속성 전부, `Level`(W, H, Tile, X, Y, Name, Hint), 그리고 **읽기 전용 질의** `Rules.TileAt`, `Rules.DoorOpen`, `Rules.Passable`, `Rules.Steps`.
- 읽기 질의를 부르는 것은 규칙 계산이 아니다. 화면에서 금지하는 것은 **질의 결과를 직접 다시 계산하는 것**(예: 문이 열렸는지 발판·석상을 뒤져 판단, 7번째 걸음 여부를 직접 세서 승패 판단)이다.
- 셀 종류 → 시각 종류 변환은 `TileId.At(state, i)` 하나만 쓴다. 화면 코드에서 `'#'`, `'o'` 같은 글자로 분기하지 않는다.

### 3.3 이벤트

`StepResult.Events`는 한 명령이 일으킨 일을 **재생 순서대로** 담는다. `StepResult.Path`는 이번 명령으로 지나간 칸들(얼음 미끄러짐 포함, 이동이 아니면 빈 배열)이다.

| 이벤트 | At | StepIdx | 의미 (기획 용어) |
|---|---|---|---|
| `Bump` | 막힌 칸 (-1 가능) | 0 | 막힘. 상태 변화 없음 |
| `Step` | 도착 칸 | 1~6 | 이동 |
| `Crumble` | 떠난 칸 | 0 | 금 간 바닥 붕괴 |
| `Fall` | 구덩이 | 1~6 | 낙하 (몸 하나 잃음) |
| `Bridge` | 구덩이 | 7 | 7번째 걸음에 구덩이 → 다리 |
| `Win` | 출구 | 7 | 클리어 |
| `Seal` | 금 간 바닥 | 7 | 금 간 바닥 위 석화 → 다리 |
| `Vanish` | 출발점 | 7 | 출발점에서는 석화 불가, 몸 잃음 |
| `Choose` | 현재 칸 | 7 | KEEP/SWITCH 선택 대기 (`Status.Choose`) |
| `Stone` | 석상 칸 | 7 또는 0 | 석화 (KEEP 또는 마지막 몸) |
| `Switch` | 현재 칸 | 0 | SWITCH, 이 칸이 새 출발점 |
| `Respawn` | 새 출발점 | 0 | 다음 몸이 깨어남 |
| `Lose` | -1 | 0 | 실패 (몸 없음) |
| `Undo` / `Restart` | -1 | 0 | 스냅샷 교체 |

**순서 규칙** (v0 `Rules.Step`/`Choose` 기준, 바뀌면 안 됨):

```
Move  (Status != Play)   → []                                   상태 그대로
Move  (막힘)              → [Bump]                               상태 그대로
Move  (통과)              → [Crumble?] + 다음 중 하나
        1~6번째, 구덩이     → Fall,   Respawn | Lose
        7번째, 구덩이       → Bridge, Respawn | Lose
        1~6번째            → Step
        7번째, 출구         → Win
        7번째, 금 간 바닥   → Seal,   Respawn | Lose
        7번째, 출발점       → Vanish, Respawn | Lose
        7번째, 몸 2개 이상  → Choose
        7번째, 마지막 몸    → Stone,  Lose
Keep                      → Stone,  Respawn | Lose
Switch                    → Switch, Respawn | Lose
Undo                      → [Undo]  (되돌릴 것이 없으면 [] 이고 상태 그대로)
Restart                   → [Restart]
```

- 한 명령의 결과에서 `Respawn`/`Lose`는 항상 마지막이다.
- `Crumble`은 목록 맨 앞에 오지만, 화면은 **경로 이동을 먼저 재생한 뒤** 이벤트를 목록 순서로 재생한다 (붕괴는 플레이어가 떠난 뒤에 보인다).
- `StepIdx == 7`인 이벤트가 행운의 7번째 걸음 연출(링, 효과음)의 기준이다.
- 이벤트 enum은 **끝에만 추가**한다 (숫자 값이 저장·로그에 쓰일 수 있음).

**코어에 요청 (2단계, 코어 담당)** — 기획의 "미끄러짐", "문 열림"이 아직 이벤트로 없다. 다음을 enum 끝에 추가한다.
- `Slide` (At = 첫 얼음 칸): `Path.Length > 1`일 때, `Crumble` 뒤·주 이벤트 앞.
- `DoorOpen` (At = 문 칸, 문마다 하나): `Stone`/`Seal` 직후, `Respawn`/`Lose` 앞. 석상은 사라지지 않으므로 `DoorClose`는 없다 (Undo/Restart는 스냅샷이라 이벤트 불필요).
- 추가 전까지 화면은 `Path.Length > 1`과 `Rules.DoorOpen(prev, d) != Rules.DoorOpen(next, d)`로 대신한다. 이것은 Core 질의 결과 비교라 허용한다.

---

## 4. 게임 세션, 입력 락, 이벤트 시퀀싱 (필수)

공용 클래스: `Contracts/GameSession.cs`, `Contracts/IBoardPresenter.cs`. 테스트: `Tests/EditMode/Contracts/GameSessionTests.cs`.

```csharp
var session = new GameSession(boardPresenter);   // boardPresenter: IBoardPresenter (BoardView)
session.Updated += OnUpdate;                     // HUD, 사운드, 메시지
session.Load(level);
session.Send(Command.Move(Dir.Right));           // 입력은 전부 여기로
session.Replay("LRRRRDDK");                      // ?replay= / -replay
```

### 4.1 입력 락 정책

| 상황 | 이동 / KEEP / SWITCH | Undo / Restart | 스테이지 이동 (`Load`) | 힌트, 메뉴 |
|---|---|---|---|---|
| 연출 없음 | 즉시 적용 | 즉시 적용 | 즉시 | 허용 |
| `Present` 연출 중 (`IsLocked`) | **큐에 넣음** (FIFO, 최대 2개, 넘치면 버림) | **연출을 끊고** 큐를 비우고 즉시 적용 → `Snap` | 연출을 끊고 큐를 비우고 `Rebuild` | 허용 (상태를 안 바꿈) |
| 규칙이 거부 (`Bump` 등) | `Feedback` 업데이트만, 잠그지 않음 | — | — | — |

- 큐를 쓰는 이유: 걸음 수를 세는 퍼즐이라 빠르게 누른 입력이 사라지면 안 된다. 2개 제한은 연출이 길게 밀리는 것을 막는다.
- 큐에 입력이 남아 있으면(`QueuedCount > 0`) 보드는 연출을 빠르게(권장 2배) 재생해도 된다.
- 잠금은 `IBoardPresenter.Present`가 `done`을 부를 때 풀린다. **보드는 `done`을 반드시 정확히 한 번 부른다** (`Snap`/`Rebuild`로 취소된 경우는 부르지 않음. 늦게 불러도 세션이 토큰으로 무시한다).
- 입력 처리(키보드, 클릭, UI 버튼)는 `IsLocked`를 직접 보지 않는다. 그냥 `Send`한다. 잠금 판단은 세션만 한다.
- `Status.Choose`(7번째 걸음 후 선택 대기)는 락이 아니라 상태다. 이동은 규칙이 거부하고(`Feedback`), K/X만 통한다.

### 4.2 업데이트 종류와 순서

`GameSession.Updated(SessionUpdate)`는 상태가 바뀐 **직후, 보드 연출 시작 전**에 한 번 발생한다.

| Kind | 언제 | 보드 | HUD/사운드 |
|---|---|---|---|
| `Rebuild` | `Load` | `Rebuild(state)` (세션이 호출) | 전체 갱신 |
| `Snap` | Undo, Restart, Replay | `Snap(state)` (세션이 호출) | 전체 갱신, Undo/Restart 효과음 |
| `Present` | 상태를 바꾼 이동/KEEP/SWITCH | `Present(prev, result, done)` (세션이 호출) | 메시지·효과음은 `result.Events`로 |
| `Feedback` | 규칙이 거부한 명령 | 호출 없음 (원하면 짧은 흔들기, 잠금 없음) | Bump 효과음 |

- 보드 호출은 세션이 한다. `Updated` 구독자는 보드를 다시 부르지 않는다.
- 효과음을 이벤트 시점에 맞추고 싶으면 보드가 이벤트 재생 시점에 콜백을 내는 방식으로 2단계에서 확장한다(게임 어셈블리 내부 일). v0처럼 `Updated`에서 한꺼번에 재생해도 된다.

### 4.3 `IBoardPresenter` 계약

```csharp
void Rebuild(State state);                               // 새 스테이지. 연출 없음
void Snap(State state);                                  // 진행 중 연출 중단, 이 스냅샷을 그대로 그림
void Present(State prev, StepResult result, Action done); // 경로 → 이벤트 순서대로 재생 → Snap(result.State)와 같은 그림으로 끝
```

- `Present`가 끝난 그림과 `Snap(result.State)`의 그림은 같아야 한다. 연출 중 만든 임시 오브젝트(링, 파편)는 `Snap`이 모두 정리한다.

---

## 5. Undo: 상태 스냅샷 재그리기 (필수)

1. `State`는 불변이고 `Rules.Dispatch`가 새 State의 `Prev`에 이전 State를 연결한다. Undo는 `Prev` 객체를 그대로 돌려준다 (다시 계산하지 않음).
2. 보드는 Undo/Restart 때 `Snap(state)`만 한다. **역재생 연출, 차분 되돌리기 금지.**
3. 보드의 모든 표시(타일, 다리, 부서진 바닥, 석상, 출발점 표시, 발자국 숫자, 문 창살, 게이트 색, 플레이어 숫자)는 `State` 하나로 다시 계산할 수 있어야 한다. `State`로 다시 만들 수 없는 화면 상태를 보드에 두지 않는다.
4. `Snap`은 같은 State로 두 번 불러도 결과가 같아야 한다 (멱등). 구현은 v0 `BoardView.ApplyState`처럼 셀마다 `TileId`를 비교해 바뀐 것만 다시 만들어도 된다.
5. HUD도 같은 원칙: `Updated`를 받을 때마다 `State`에서 전부 다시 그린다.

---

## 6. 레벨 데이터 형식

- 원본: `prototypes/seven-steps/src/levels.js` (레벨 디자이너). `unity/Tools/unity.sh levels`가 JS 솔버 답과 함께 `Assets/_Project/Data/levels.json`으로 내보낸다.
- 런타임: `TextAsset` → `JsonUtility.FromJson<LevelFileDto>` → `LevelDto.ToLevel()` → `Level.Parse`. 형식은 `Core/Level.cs`의 `LevelFileDto`/`LevelDto`/`ExpectedDto`.
- **ScriptableObject로 바꾸지 않는다.** 이유:
  1. 기준은 웹 프로토타입이고 레벨 디자이너는 JS로 작업한다. JSON은 생성물이라 SO로 바꾸면 원본이 둘이 된다.
  2. 텍스트라 git diff와 리뷰가 쉽고, `expected`(JS 솔버 답)를 함께 실어 `LevelVerifier`가 C#과 JS를 비교할 수 있다.
  3. `Core`가 엔진을 모르므로 SO를 읽을 수 없다. JSON은 Core 테스트에서 그대로 쓸 수 있다.
  4. WebGL에서 TextAsset은 빌드에 포함되므로 파일 IO가 필요 없다.
- 레벨에 기믹 글자를 추가하면: 코어가 `Level.Parse` 범례와 규칙을 고치고, 아키텍트가 `TileKind`/`TileId.FromChar`/`TileSet`에 항목을 추가한다 (enum 끝에 추가).

---

## 7. 타일 → 프리팹 매핑: `TileSet`

공용 타입: `Contracts/TileId.cs`, `Contracts/TileVisual.cs`, `Contracts/TileSet.cs`, `Contracts/BoardGrid.cs`.

```
State ──TileId.At(state, i)──▶ TileId(Kind, Variant) ──tileSet.Get(id)──▶ TileVisual ──▶ Instantiate
                                                       tileSet.Get(ActorKind.Player) ...
```

### 7.1 `TileId`

| Kind | 글자 | Variant |
|---|---|---|
| `Floor` | `.` `S` | 0/1 체커 (`(x+y)&1`) |
| `Wall` | `#` | 0 |
| `Pit` | `o`, 부서진 `x` | 0 |
| `Ice` | `~` | 0 |
| `Cracked` | `x` | 0 |
| `Bridge` | (석상으로 메운 구덩이) | 0 |
| `Exit` | `E` | 0 |
| `Gate` | `1`~`7` | 걸음 번호 1~7 |
| `Plate` | `a`~`c` | 0~2 |
| `Door` | `A`~`C` | 0~2 |

`ActorKind`: `Player`, `Statue`, `StartMarker`, `OldStartMarker`, `Footprint`, `LuckyRing`.

### 7.2 `TileVisual` (타일·액터 하나의 표시 규격)

| 필드 | 뜻 | 탑다운 ↔ 쿼터뷰 차이를 흡수하는 방법 |
|---|---|---|
| `prefab` | 프리팹. null이면 그리지 않음 | 세트마다 다른 프리팹 |
| `offset` | 셀 앵커에서의 위치(셀 단위) | **피벗 보정**. 프리팹 피벗이 바닥 중앙이면 `(0,-h,0)` 등 |
| `rotation` | 오일러 각 | 2D 스프라이트는 눕히고(`(90,0,0)`), 3D 모델은 0 |
| `scale` | (0,0,0)은 1 | 에셋 단위가 1칸과 다를 때 |
| `randomYaw90` | 셀마다 0/90/180/270도 (결정적) | 3D 바닥·벽의 반복 무늬 감추기 |
| `activeChild` / `inactiveChild` | 활성/비활성일 때만 켜는 자식 이름 | 문 창살 `"Bars"` 등. 애니메이터 없이도 상태 표시 |
| `materialTarget`, `activeMaterial`, `inactiveMaterial` | 상태별 머티리얼 교체 | 게이트 `"Arch"` 금색/기본 |
| `appearTrigger`, `vanishTrigger`, `activateTrigger`, `deactivateTrigger` | Animator 트리거 이름. 비우면 보드의 코드 트윈 | 3D 모델 애니메이션 |
| `labelOffset`, `activeLabelOffset` | 숫자/글자 라벨 위치(셀 단위) | 쿼터뷰에서 기둥 높이가 다를 때 |

**활성(active)의 뜻**: 문 = 열림, 게이트 = 다음 걸음에 통과 가능, 발판 = 눌림, 출구 = 다음 걸음이 7번째. 이 값은 보드가 `Rules.DoorOpen`, `state.Used`, `state.Statues`에서 읽는다.

**셀 앵커**: 셀 윗면 중앙 (`BoardGrid.CellToWorld(i)`, y = 0). 인스턴스 배치는
`position = anchor + offset * cellSize`, `rotation = visual.RotationFor(i)`, `localScale = visual.Scale * cellSize`.

### 7.3 `TileSet` (ScriptableObject, `Create > Seven Steps > Tile Set`)

- 필드: `projection`(TopDown/Quarter), `cellSize`, `cameraRig`, 타일별 `TileVisual`, `plates[3]`, `doors[3]`, 액터별 `TileVisual`.
- `CameraRig`: `orthographic`, `pitch`, `yaw`, `fieldOfView`, `distance`, `padding`(셀). 기본값 `CameraRig.TopDown`(v0와 같은 58°), `CameraRig.Quarter`(35°, yaw 45°).
- **격자는 투영과 무관하다.** 쿼터뷰 전환 = 다른 `TileSet` 에셋을 지정. 클릭 판정은 `BoardGrid.RayToCell(camera.ScreenPointToRay(p))`라 카메라 각도와 상관없이 맞다.
- 자산 위치: `Assets/_Project/Data/TileSets/`. 임시 아트 세트 `TopDown_Placeholder.asset`은 `Seven Steps > Architecture > Build Placeholder TileSet`(배치: `SevenSteps.EditorTools.TileSetBuilder.BatchBuild`)이 v0 `GameAssets` 프리팹과 v0 오프셋으로 만든다.
- 아트 프리팹 규격: 1칸 = 1유닛, 바닥 윗면 y = 0, 피벗은 가능하면 윗면 중앙. 다르면 프리팹을 고치지 말고 `TileVisual.offset/scale`로 맞춘다.
- `GameAssets`의 타일·액터 프리팹 필드는 2단계에서 `TileSet` 참조 하나로 대체한다 (게임플레이/UI). 폰트·효과음·레벨 JSON·팔레트는 `GameAssets`에 남는다.

---

## 8. 씬 구성과 소유권

| 씬 | 빌드 인덱스 | 내용 | 소유자 |
|---|---|---|---|
| `Scenes/Boot.unity` | 0 | 프레임레이트, 저장 데이터 로드, URL/커맨드라인 인자(`level`, `replay`) 읽기. 인자가 있으면 `Game`, 없으면 `Title`로 이동 | 게임플레이/UI |
| `Scenes/Title.unity` | 1 | 제목, 스테이지 선택 | 게임플레이/UI |
| `Scenes/Game.unity` | 2 | 보드, HUD, `GameController` | 게임플레이/UI |

- 씬 사이 전달값(시작 스테이지, 리플레이)은 `SevenSteps.Game` 안의 정적 클래스 하나로 넘긴다. 영구 싱글턴(`DontDestroyOnLoad`)은 오디오가 필요할 때만 Boot에서 하나 만든다.
- 씬 안의 오브젝트는 최소로(카메라, 라이트, 루트 하나) 두고 내용은 프리팹으로 나눈다. 한 씬은 한 에이전트만 고친다.
- 빌드 씬 목록(`ProjectSettings/EditorBuildSettings.asset`)은 리드가 바꾼다. 씬 분리는 2단계 게임플레이/UI 작업이고, 그때까지 v0의 `Game.unity` 하나로 동작한다.
- 프리팹: 타일·액터 `Prefabs/Tiles/**`(아트), UI 프리팹 `UI/**`(게임플레이/UI), `Prefabs/Generated/**`(v0 임시, 리드의 `ProjectSetup`이 생성).

---

## 9. 코드 규칙

- **네임스페이스 = 어셈블리 + 폴더**: `SevenSteps.Core`, `SevenSteps.Contracts`, `SevenSteps.Game`, `SevenSteps.Game.UI`, `SevenSteps.EditorTools`, `SevenSteps.Tests`.
- **이름**: 타입·공개 멤버 PascalCase, 비공개 필드 camelCase(접두사 없음), 상수 PascalCase, 인스펙터 노출 필드는 `public` camelCase(v0 관례). 파일 하나에 주 타입 하나, 파일 이름 = 타입 이름.
- **규칙 로직은 Core에만.** MonoBehaviour에 규칙(통과 가능, 승패, 석화, 문 열림 판단)을 넣지 않는다. MonoBehaviour는 입력을 `Command`로 바꾸고, 결과를 그리고, 소리를 낸다.
- **Core 제약**: `UnityEngine` 금지(`noEngineReferences`), 시간·난수·IO·정적 가변 상태 금지. 같은 입력은 같은 결과.
- **WebGL 호환**: `System.Threading`(Thread, Task.Run, 동기 대기), `System.IO` 파일 쓰기/읽기, `Process`, 소켓, `Application.persistentDataPath` 직접 쓰기 금지. 저장은 `PlayerPrefs`. 코루틴/`async`는 메인 스레드에서만. 리플렉션으로 타입 찾기 금지(코드 스트리핑).
- **매 프레임 할당 금지**: `Update`/`LateUpdate`에서 LINQ, `params` 배열, 문자열 결합, 람다 캡처, `GetComponent*` 금지. 문자열은 상태가 바뀔 때만 만든다. (v0 `GameController.Pressed(params ...)`+`Any`는 2단계에서 고칠 것)
- **입력**: Input System만 쓴다(`Keyboard.current` 또는 `InputSystem_Actions`). 레거시 `Input` 클래스 금지.
- **에디터 코드**는 `Scripts/Editor`의 `SevenSteps.Editor` 어셈블리에만. 런타임 코드에서 `#if UNITY_EDITOR`는 최소화.
- **테스트**: Core와 Contracts는 EditMode 테스트로 검증한다. 실행: `Unity.exe -batchmode -projectPath unity -runTests -testPlatform EditMode -testResults reports/tests-editmode.xml -logFile -` (`-quit` 넣지 않음).

---

## 10. 2단계 작업 지시 (이 문서 기준)

| 담당 | 할 일 |
|---|---|
| 코어 | `Slide`, `DoorOpen` 이벤트 추가(3.3). 공개 API 형태 유지. EditMode 테스트를 `Tests/EditMode/`(Contracts 폴더 제외)에 |
| 게임플레이/UI | `GameController`가 `GameSession`을 쓰도록 변경(직접 `Rules.Dispatch` 호출 제거). `BoardView`가 `IBoardPresenter` 구현 + `TileSet`/`BoardGrid`/`TileId` 사용(글자 분기 제거). `GameAssets`에 `TileSet` 참조 추가. HUD를 `Game/UI/`로. 씬 분리(8장) |
| 아트 | `Data/TileSets/`에 실제 아트 `TileSet` 에셋(탑다운, 쿼터뷰). 프리팹 규격은 7.3 |
| 리드 | `unity.sh`에 `tileset`(TileSetBuilder.BatchBuild), `test`(EditMode 테스트) 명령 추가 검토. 씬 분리 시 빌드 목록 갱신 |

---

## 변경 기록

| 날짜 | 누가 | 내용 |
|---|---|---|
| 2026-11-07 | 아키텍트 | 최초 작성. `SevenSteps.Contracts` 어셈블리(`TileId`, `TileVisual`, `TileSet`, `CameraRig`, `BoardGrid`, `IBoardPresenter`, `GameSession`), `SevenSteps.Tests.EditMode` 어셈블리, test-framework 1.8.0 명시, `TileSetBuilder`와 `TopDown_Placeholder.asset`. Core·Game 동작 변경 없음 (v0 그대로) |
