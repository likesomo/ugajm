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
| 씬·앱 흐름 | 상태 `Boot → Title → StageSelect → Game ⇄ Result` (`AppFlow`), 씬은 `Boot`, `Title`(+StageSelect), `Game`(+Result). 씬은 게임플레이/UI 소유, 빌드 목록은 리드 소유 |
| 기본 틀 | Boot 부트스트랩이 `Services`에 저장(`SaveSystem`, PlayerPrefs)·설정·`ITelemetry`·`AppFlow`·`LaunchOptions`·`AppConfig`를 등록 (11장) |
| 텔레메트리 | `ITelemetry` + `NullTelemetry`. Core는 모름. Game 계층이 `GameSession.Updated`를 받아 기록. 스키마는 `docs/telemetry.md`(백엔드/분석)를 따름 |

---

## 2. 어셈블리와 폴더

```
SevenSteps.Core        Scripts/Runtime/Core        noEngineReferences  (코어)
   ▲
SevenSteps.Contracts   Scripts/Runtime/Contracts   UnityEngine 사용     (아키텍트)  공용 인터페이스·타입, 앱 기본 틀(App/, Save/, Telemetry/)
   ▲
SevenSteps.Game        Scripts/Runtime/Game        + InputSystem, uGUI  (게임플레이/UI)  화면·입력·사운드·HUD
   ▲
SevenSteps.Editor      Scripts/Editor              Editor 전용          (여러 소유자, 파일 단위)

SevenSteps.Tests.EditMode  Tests/EditMode          Editor 전용, UNITY_INCLUDE_TESTS → Core, Contracts, Game
```

- 화살표 방향으로만 참조한다. `Core`는 아무것도 참조하지 않는다. `Contracts`는 `Game`을 모른다. 테스트 어셈블리는 Core·Contracts·Game을 참조할 수 있다(반대 방향 참조는 없음).
- UI(HUD, 패널)는 별도 어셈블리로 나누지 않고 `SevenSteps.Game` 안의 `Game/UI/` 폴더, 네임스페이스 `SevenSteps.Game.UI`로 둔다. 잼 규모에서는 컴파일 시간 이득이 없고, `GameController` ↔ HUD 순환을 막으려면 어차피 같은 어셈블리여야 한다. HUD는 `GameSession.Updated`와 `State`만 읽는다.
- 새 asmdef가 필요하면 아키텍트에게 요청한다. asmdef 파일은 아키텍트 소유다 (위치한 폴더의 소유자와 무관).
- 테스트 프레임워크: `com.unity.test-framework` 1.8.0 (manifest에 명시).

### 파일 소유권 (이 문서가 지정하는 공용 파일)

| 경로 (`unity/` 기준) | 소유자 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Contracts/**` | 아키텍트 (공용) |
| `Assets/_Project/Scripts/Editor/Architecture/**` | 아키텍트 |
| `Assets/_Project/Tests/EditMode/Contracts/**` | 아키텍트 |
| `Assets/_Project/Tests/EditMode/Game/**`, `Assets/_Project/Plugins/WebGL/**` | 게임플레이/UI |
| `Assets/_Project/Tests/EditMode/**` (위 두 줄 제외), `Tests/Golden/**` | 코어 |
| `Assets/_Project/Data/TileSets/TopDown_Placeholder.asset` | 아키텍트 (`TileSetBuilder`가 생성) |
| `Assets/_Project/Data/TileSets/**` (위 제외) | 아트 |
| `**/*.asmdef`, `Packages/manifest.json`, `Packages/packages-lock.json` | 아키텍트 |
| `docs/reviews/**` (코드 검토 기록, 저장소 루트 기준) | 아키텍트 |

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
| `Slide` | 첫 얼음 칸 (`Path[0]`) | 0 | 미끄러짐. `Path.Length > 1`일 때만 |
| `DoorOpen` | 문 칸 (문 칸마다 하나, 칸 번호 순) | 0 | 문 열림 (새 석상이 발판을 누름) |

**순서 규칙** (v0 `Rules.Step`/`Choose` 기준, 바뀌면 안 됨):

```
Move  (Status != Play)   → []                                   상태 그대로
Move  (막힘)              → [Bump]                               상태 그대로
Move  (통과)              → [Crumble?] + [Slide?] + 다음 중 하나
        1~6번째, 구덩이     → Fall,   Respawn | Lose
        7번째, 구덩이       → Bridge, Respawn | Lose
        1~6번째            → Step
        7번째, 출구         → Win
        7번째, 금 간 바닥   → Seal,   Respawn | Lose
        7번째, 출발점       → Vanish, Respawn | Lose
        7번째, 몸 2개 이상  → Choose
        7번째, 마지막 몸    → Stone,  DoorOpen*, Lose
Keep                      → Stone,  DoorOpen*, Respawn | Lose
Switch                    → Switch, Respawn | Lose
Undo                      → [Undo]  (되돌릴 것이 없으면 [] 이고 상태 그대로)
Restart                   → [Restart]
```

- 한 명령의 결과에서 `Respawn`/`Lose`는 항상 마지막이다.
- `Crumble`은 목록 맨 앞에 오지만, 화면은 **경로 이동을 먼저 재생한 뒤** 이벤트를 목록 순서로 재생한다 (붕괴는 플레이어가 떠난 뒤에 보인다).
- `StepIdx == 7`인 이벤트가 행운의 7번째 걸음 연출(링, 효과음)의 기준이다.
- 이벤트 enum은 **끝에만 추가**한다 (숫자 값이 저장·로그에 쓰일 수 있음).

**`Slide`·`DoorOpen`·`DoorClose` (2단계, 코어가 추가함)** — enum 끝(15, 16, 17)에 있고 `core.js`도 같은 이벤트(`slide`, `doorOpen`, `doorClose`)를 낸다. 셋 다 `StepIdx`는 0 (행운 연출이 두 번 나지 않도록 주 이벤트만 7을 가짐).
- `Slide` (At = 첫 얼음 칸 = `Path[0]`): `Path.Length > 1`일 때만, `Crumble` 뒤·주 이벤트 앞.
- `DoorOpen` (At = 문 칸): 닫혀 있다 열린 문 칸마다 하나.
- `DoorClose` (At = 문 칸): 열려 있다 닫힌 문 칸마다 하나. 석상 수 문(`Q`, 6.1장)은 석상이 늘면 닫힐 수 있어서 필요하다 (2026-11-07 기믹 승인, 7lucky 37c99f7). 알파벳 문(`A`~`C`)은 석상이 사라지지 않으므로 닫히지 않는다.
- 순서: `DoorOpen`/`DoorClose`는 `Stone` 직후, 칸 번호 오름차순(열림·닫힘 섞어서 칸 번호 순), `Respawn`/`Lose` 앞. 석상이 생기는 것은 `Stone`뿐이다. `Seal`은 석상을 남기지 않으므로(다리만 생김) 문 상태를 바꾸지 않는다. Undo/Restart는 스냅샷이라 이벤트가 필요 없다.
- 열린 석상 수 문 칸 위에서 KEEP하면 석상이 문 칸에 서고 그 문이 닫힐 수 있다 (골든 트레이스 `edge-count-door-keep-on-door`: `Stone` → `DoorClose`@문 칸 → `Respawn`). 보드는 닫힌 문과 석상을 같은 칸에 함께 그릴 수 있어야 한다.
- `Flip`(18, At = 레버 칸, StepIdx 0): 레버를 당긴 7번째 걸음에서 주 이벤트(`Choose`/`Stone`) 바로 앞. 자세한 규칙은 6.2장.
- 이 규칙은 EditMode 테스트(`Tests/EditMode/Core/RulesTests`)가 모든 골든 트레이스에서 검사한다.

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
| `Present` 연출 중 (`IsLocked`) | **큐에 넣음** (FIFO, 최대 2개, 넘치면 버림) | Undo: 큐에 입력이 있으면 **가장 최근 큐 입력만 취소**(연출 계속). 큐가 비었으면, 또는 Restart면 **연출을 끊고** 큐를 비우고 즉시 적용 → `Snap` | 연출을 끊고 큐를 비우고 `Rebuild` | 허용 (상태를 안 바꿈) |
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
- `GameSession.Presented(State)`: `Present` 연출이 끝났을 때(큐의 다음 명령 실행 전) 한 번. 취소된 연출·`Snap`·`Rebuild`에는 없음. 클리어 후 결과 패널처럼 **연출이 끝난 뒤** 해야 하는 일은 여기서 한다(`Updated`에서 하면 걷는 도중에 패널이 뜬다).
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
- `expected`의 런타임 사용: 클리어 화면이 `expected.minBodies`를 `LevelCatalog.MinBodies(stage)`로 읽어 "최소 N개"로 보여 준다(feat/view 5a2180a). 0(없음)이면 표시하지 않는다. 그래서 스테이지를 바꾸면 반드시 `unity.sh levels`로 다시 내보낸다(손으로 고친 `levels.json`은 최소 수가 틀릴 수 있다). `expected.path`는 런타임에서 쓰지 않는다(H 풀이는 C# `Solver`가 계산).
- 레벨에 기믹 글자를 추가하면: 코어가 `Level.Parse` 범례와 규칙을 고치고, 아키텍트가 `TileKind`/`TileId.FromChar`/`TileSet`에 항목을 추가한다 (enum 끝에 추가).

### 6.1 석상 수 문 (`Q`, 2026-11-07 승인)

- 맵 글자 `Q` = 석상 수 문. 지금 서 있는 석상 수가 레벨의 N과 **같을 때만** 열린다 (다리·봉인은 세지 않음).
- N은 레벨 필드 `statueCount`(정수, 기본 0) 하나다. 한 스테이지의 모든 `Q`가 같은 N을 쓴다. 문마다 다른 N은 잼 범위 밖.
- `levels.js` 레벨 객체와 `levels.json` `LevelDto`에 `statueCount` 추가, `Level.StatueCount`. `Q`가 없는 레벨은 필드를 생략해도 된다(0).
- Core 질의: `Rules.DoorOpen(s, 'Q')`가 석상 수 문도 처리, `Rules.StatueCount(s)`. 화면은 이 질의만 쓴다.
- 표시: `TileKind.CountDoor`(variant 0), 문 위 숫자 라벨은 `Level.StatueCount`. 활성 = 열림.
- 담당: 코어(규칙·파서·테스트·JS), 레벨 디자인(`statueCount` 채우기·스테이지), 아키텍트(`TileId`·`TileSet`, 이 문서), 게임플레이/UI(라벨·`DoorClose` 연출), 아트(문 프리팹), 백엔드/분석(텔레메트리 `tile` 값: `door`로 볼지 새 값을 둘지).

### 6.2 레버와 뒤집힘 벽 (`L`, `R`, `r`, 2026-11-07 규칙 확정, 코어 제안 채택)

기준: GDD 75·79행, 레벨 디자이너 실험 `prototypes/seven-steps/experiments/lever/core.js` (feat/levels). 코어가 `core.js`와 C#에 같은 규칙으로 옮긴다.

- 맵 글자: `L` 레버, `R` 올라온 벽(처음 막힘), `r` 내려간 벽(처음 통과). 레벨 필드 추가 없음. (리플레이 글자 `R`=오른쪽과는 별개 범례)
- 상태: `State.Flipped`(bool, 처음 false, `core.js` `flip`). 레버가 당겨질 때마다 반전. 스냅샷·솔버 `Rules.Key`에 포함. 벽마다 따로 저장하지 않는다(모든 뒤집힘 벽이 함께 뒤집힘).
- 통과: `R`은 `Flipped`일 때만, `r`은 `!Flipped`일 때만 통과. 1~6번째 걸음으로 `L`을 지나가거나 멈추는 것은 바닥과 같다. 레버는 얼음이 아니므로 미끄러짐은 레버에서 멈춘다.
- 당김: 7번째 걸음이 `L`에서 끝나고 그 칸이 **현재 출발점이 아닐 때만** `Flipped`가 반전된다(얼음으로 지나가기만 하면 안 당김). KEEP하면 석상이 레버를 막아 다시 못 당긴다. SWITCH하면 레버 칸이 출발점이 되어 그 뒤로는 거기서 7번째를 마쳐도 Vanish만 난다. 출발점이면 기존 규칙대로 `Vanish`만 일어나고 레버는 당겨지지 않는다(출발점 7번째에는 어떤 칸 효과도 없음).
- 이벤트: `GameEventType` 끝에 `Flip`(18) 하나. At = 레버 칸, StepIdx 0. 벽 칸마다의 이벤트는 없다 — 화면은 `Flip`을 받으면 모든 `FlipWall` 칸을 질의 결과대로 바꾼다.
- 순서: `[Crumble?] [Slide?] Flip` 다음에 `Choose` 또는 `Stone`(+`DoorOpen`/`DoorClose`*)+`Respawn`|`Lose`. 즉 `Flip`은 주 이벤트 바로 앞이다.
- Core 질의: `Rules.TileAt`이 `Flipped`일 때 `R`↔`r`을 바꿔 돌려준다 → `'R'` = 지금 올라감(막힘), `'r'` = 지금 내려감(통과). 그래서 `TileId.At`이 그대로 현재 모양을 주고, 화면은 `Flipped`를 몰라도 된다(레버 표시만 `State.Flipped`를 읽음). `Rules.IsFlipWall(char)` 추가.
- 표시: `TileKind.Lever`(활성 = `Flipped`), `TileKind.FlipWall`(variant 0 = 지금 올라감, 1 = 지금 내려감 — 모양이 바뀌면 `TileId`가 바뀌므로 v0처럼 셀을 다시 만들거나 `Flip` 때 전환 연출). `TileSet.lever`, `TileSet.flipWalls[2]`. 임시 아트는 레버 = 발판 B, 올라감 = 벽, 내려감 = 바닥 프리팹.
- 경계 사례(코어가 테스트·골든 트레이스로 고정): (1) 벽이 올라올 때 그 칸에 석상이나 출발점이 있어도 그대로 뒤집힌다(실험과 같음, 잼 범위에서 겹침 허용). 화면은 올라온 벽과 석상/출발점 표시를 한 칸에 함께 그릴 수 있어야 한다. (2) 출발점인 레버 칸에서 7번째 → `Vanish`, `Flip` 없음.
- 텔레메트리 `tile` 값(`lever`, 뒤집힘 벽 이름)과 `flip` 이벤트 여부는 백엔드/분석이 `docs/telemetry.md`에 정한다.

### 6.3 진흙 (`m`, 2026-11-09 승인, 코어 구현 feat/core 6ae58e2)

레벨 디자인 제안(`design/levels/stage-plan-50.md` 5장, 실험 `prototypes/seven-steps/experiments/mud/`). 7장(원형 제단)의 새 기믹.

- 맵 글자 `m`. 레벨 필드 추가 없음. 리플레이 글자와 겹치지 않음.
- 규칙(실험 `core.js` 기준, 코어가 `core.js`·C#·골든으로 고정): 진흙 칸에 **들어가는 걸음이 2걸음**이다(`stepIdx = used + 2`). 남은 걸음이 1이면(6걸음 쓴 뒤) 들어갈 수 없다 → `Bump`. 얼음에서 미끄러지다 진흙에 닿으면 거기서 멈춘다(추가 걸음 없음). 7번째 걸음이 진흙 위에서 끝나면 보통 바닥과 같다(`Choose`/`Stone`/`Vanish`, 석상 가능). SWITCH로 진흙 칸이 출발점이 될 수 있다. Core 질의 `Rules.IsMud(char)`(공개 API 추가는 이것 하나). 숫자 문(`1`~`7`)은 그 걸음의 `stepIdx`로 판단하므로 진흙을 지나면 숫자 하나를 건너뛴다.
- 이벤트: 새 종류 없음(실험과 같음, 코어 구현도 같음). 막힐 때는 `Bump`(At = 진흙 칸). 진흙으로 들어간 걸음은 `Step`(또는 마지막 칸 이벤트)의 `StepIdx`가 2 오른다. 화면은 `StepIdx`로 걸음 표시를 2칸 채우고, 칸이 진흙인지는 `TileId.At`으로 안다. 진흙 진입 전용 이벤트(`Mud` = 19)는 넣지 않는다(코어 제안 보류): 화면이 `TileId`로 알 수 있다. 연출에 꼭 필요하면 게임플레이/UI가 요청한다.
- 표시: `TileKind.Mud`(끝에 추가, variant 0), `TileId.FromChar('m')`, `TileSet.mud`(바닥처럼 윗면 y = 0). 프리팹이 없으면 바닥으로 그린다(검사기 경고). 아트: 이끼 늪 진흙 바닥 1종.
- 담당: 코어(규칙·파서·`Rules.Key` 영향 없음 확인·테스트·JS), 레벨 디자인(43~50 스테이지), 아키텍트(`TileKind`/`TileId`/`TileSet`/검사기, 이 절), 게임플레이/UI(걸음 표시 2칸, 진흙 연출), 아트(진흙 프리팹), 백엔드/분석(텔레메트리 `tile` 값 `mud`).

### 6.4 챕터 필드와 `chapters.json` (2026-11-09 결정)

리드 결정: 레벨 디자인 제안(정수 `chapter`, `stage-plan-50.md` 7장)을 **문자열 id**로 바꾼다. 챕터 순서는 `chapters.json`의 배열 순서다.

- `levels.js` 레벨 객체 / `levels.json` `LevelDto`: `chapter`(문자열 id, 예 `"moss"`), `story`(한 줄 서사, 세쌍둥이 시점, 힌트와 별개, 선택). Core `Level`에는 넣지 않는다(규칙과 무관). `LevelDto` 필드 추가는 코어.
- `design/levels/chapters.json`(레벨 디자인 원본) → `unity.sh levels`가 `Assets/_Project/Data/chapters.json`으로 복사(텍스트, 커밋).
  ```json
  { "chapters": [
    { "id": "moss", "planet": "이끼의 궤도", "concept": "...", "intro": "진입 문구", "artPreset": "moss" }
  ] }
  ```
  `id`: 소문자·하이픈, 바꾸지 않는다(텔레메트리·저장이 기댈 수 있음). `artPreset`: `ChapterPreset.id`(12.8). 비면 `id`와 같다. 여러 챕터가 한 프리셋을 같이 쓸 수 있다.
- 런타임 형식: `Contracts/Chapters/ChapterInfo.cs`의 `ChapterFileDto`/`ChapterInfo`(아키텍트). 읽기는 `GameAssets.chaptersJson`(TextAsset) → `LevelCatalog`: `ChapterIdOf(stage)`(구현됨, 7lucky 9f3d109, 텔레메트리 `level_start` 챕터용) → `ChapterFileDto.Get(id)` → `ChapterInfo`(chapters.json이 생기면), 아트는 `AppConfig.Chapter(info.ArtPreset)`. `LevelDto.chapter`/`story`는 코어 b5c4464에서 추가됨.
- export 단정 검증(`unity/Tools/export-levels.mjs`, 실패하면 파일을 쓰지 않고 exit 1, 레벨 디자인 제안 그대로): 모든 스테이지에 `chapter`가 있고 `chapters.json`에 있다, 스테이지 순서대로 챕터가 `chapters.json` 순서를 거스르지 않고 이어진다, 모든 `bodies <= 3`, 모두 솔버로 풀리고 최소 몸 수 <= 3, 이름 중복 없음. **지금 17스테이지(챕터 없음, 14~17 몸 4~5)는 플레이테스트 1차까지 그대로**이므로 검증은 형식 도입 커밋(첫 묶음)부터 켠다. 그 전까지 런타임은 `chapter`가 없으면 첫 챕터, 몸 4~5는 외형 순환(A, B, C, A)으로 임시 허용(12.6).
- Unity 검사: `TileSetValidator`가 `Data/chapters.json`의 `artPreset`이 모두 `AppConfig.chapters`에 있는지 본다(파일과 챕터 에셋이 생기면 아키텍트가 켬).
- 담당: 레벨 디자인(`chapters.json`, `chapter`/`story` 채우기, export 검증), 코어(`LevelDto` 필드), 아키텍트(`ChapterInfo`, 이 절, 검사기), 게임플레이/UI(`LevelCatalog`·진입 문구 표시), 씬 플로우(챕터가 바뀔 때의 흐름·`ResultModel.nextChapterChanges`), 백엔드/분석(이벤트에 챕터 id를 실을지).

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
| `CountDoor` | `Q` | 0 (N은 `Level.StatueCount`) |
| `Lever` | `L` | 0 |
| `FlipWall` | `R` `r` (`Rules.TileAt` 기준, 뒤집힘 반영) | 0 = 지금 올라감(막힘), 1 = 지금 내려감(통과) |

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

**활성(active)의 뜻**: 문·석상 수 문 = 열림(`Rules.DoorOpen`), 레버 = 당겨짐(`State.Flipped`), (뒤집힘 벽은 활성 대신 variant가 현재 모양), 게이트 = 다음 걸음에 통과 가능, 발판 = 눌림, 출구 = 다음 걸음이 7번째. 이 값은 보드가 `Rules.DoorOpen`, `state.Used`, `state.Statues`에서 읽는다.

**셀 앵커**: 셀 윗면 중앙 (`BoardGrid.CellToWorld(i)`, y = 0). 인스턴스 배치는
`position = anchor + offset * cellSize`, `rotation = visual.RotationFor(i)`, `localScale = visual.Scale * cellSize`.

### 7.3 `TileSet` (ScriptableObject, `Create > Seven Steps > Tile Set`)

- 필드: `projection`(TopDown/Quarter), `cellSize`, `cameraRig`, 타일별 `TileVisual`, `plates[3]`, `doors[3]`, `countDoor`, `lever`, `flipWalls[2]`, 액터별 `TileVisual`.
- `CameraRig`: `orthographic`, `pitch`, `yaw`, `fieldOfView`, `distance`, `padding`(셀). 기본값 `CameraRig.TopDown`(v0와 같은 58°), `CameraRig.Quarter`(35°, yaw 45°).
- **격자는 투영과 무관하다.** 쿼터뷰 전환 = 다른 `TileSet` 에셋을 지정. 클릭 판정은 `BoardGrid.RayToCell(camera.ScreenPointToRay(p))`라 카메라 각도와 상관없이 맞다.
- 자산 위치: 아트 세트는 `Assets/_Project/Art/<세트>/`, 임시 세트는 `Assets/_Project/Data/TileSets/`(12.7). 임시 아트 세트 `TopDown_Placeholder.asset`은 `Seven Steps > Architecture > Build Placeholder TileSet`(배치: `SevenSteps.EditorTools.TileSetBuilder.BatchBuild`)이 v0 `GameAssets` 프리팹과 v0 오프셋으로 만든다.
- 아트 프리팹 규격: 1칸 = 1유닛, 바닥 윗면 y = 0, 피벗은 가능하면 윗면 중앙. 다르면 프리팹을 고치지 말고 `TileVisual.offset/scale`로 맞춘다.
- `GameAssets`의 타일·액터 프리팹 필드는 2단계에서 `TileSet` 참조 하나로 대체한다 (게임플레이/UI). 폰트·효과음·레벨 JSON·팔레트는 `GameAssets`에 남는다.

---

## 8. 씬 구성과 소유권

| 씬 | 빌드 인덱스 | 내용 | 소유자 |
|---|---|---|---|
| `Scenes/Boot.unity` | 0 | `Bootstrap`: 서비스 등록(11장), `LaunchOptions` 읽기. `DirectToGame`이면 `Game`, 아니면 `Title`로. 화면 없음 | 씬 플로우 |
| `Scenes/Title.unity` | 1 | 상태 `Title`(제목, 설정 패널), `StageSelect`(스테이지 목록, 클리어 표시). 패널 전환만, 씬 이동 없음 | 씬 플로우 |
| `Scenes/Game.unity` | 2 | 상태 `Game`(보드, HUD, `GameController`), `Result`(클리어/실패 패널, 다음·재시도·스테이지 선택). 같은 씬 안 오버레이 | 씬 플로우 (화면 내용은 게임플레이/UI) |

- 씬 사이 전달값은 `Services`에 등록된 `AppFlow.Stage`(시작 스테이지)와 `LaunchOptions`(리플레이 등)로 넘긴다. 정적 전역 변수를 따로 만들지 않는다. 영구 오브젝트(`DontDestroyOnLoad`)는 두 개뿐이다: 텔레메트리 러너 `TelemetryRunner`(텔레메트리가 켜졌을 때만, 이름 고정 — jslib가 이름으로 찾음, 11.5)와 씬 전환 페이드 `SceneFader`(첫 씬 로드 때 `SceneLoader`가 만듦, 씬 플로우 S5). 더 늘리려면 아키텍트와 정한다. 둘 다 Play가 끝나면 사라지고, 부트스트랩이 Play마다 한 번이라 중복 생성되지 않는다.
- 에디터에서 `Game.unity`를 바로 Play하면 Boot를 거치지 않는다. 그래서 각 씬의 진입 코드는 **이번 Play에서 부트스트랩이 돌지 않았으면** 같은 부트스트랩 함수를 직접 부른다(테스트·디버그 편의, v0의 `startLevel`/`replay` 유지). `Services.Has<AppFlow>()`로 판단하지 않는다: 이 프로젝트는 에디터 도메인 리로드가 꺼져 있어(`EditorSettings` `m_EnterPlayModeOptions: 1`) 정적 상태가 Play 사이에 남는다(9장 "정적 가변 상태").
- 씬 안의 오브젝트는 최소로(카메라, 라이트, 루트 하나) 두고 내용은 프리팹으로 나눈다. 한 씬은 한 에이전트만 고친다.
- 빌드 씬 목록(`ProjectSettings/EditorBuildSettings.asset`)은 리드가 바꾼다. 씬 분리는 2단계 게임플레이/UI 작업이고, 그때까지 v0의 `Game.unity` 하나로 동작한다.
- 프리팹: 타일·액터 `Prefabs/Tiles/**`(아트), UI 프리팹 `UI/**`(게임플레이/UI), `Prefabs/Generated/**`(v0 임시, 리드의 `ProjectSetup`이 생성).

---

## 9. 코드 규칙

- **네임스페이스 = 어셈블리 + 폴더**: `SevenSteps.Core`, `SevenSteps.Contracts`, `SevenSteps.Game`, `SevenSteps.Game.UI`, `SevenSteps.EditorTools`, `SevenSteps.Tests`.
- **이름**: 타입·공개 멤버 PascalCase, 비공개 필드 camelCase(접두사 없음), 상수 PascalCase, 인스펙터 노출 필드는 `public` camelCase(v0 관례). 파일 하나에 주 타입 하나, 파일 이름 = 타입 이름.
- **규칙 로직은 Core에만.** MonoBehaviour에 규칙(통과 가능, 승패, 석화, 문 열림 판단)을 넣지 않는다. MonoBehaviour는 입력을 `Command`로 바꾸고, 결과를 그리고, 소리를 낸다.
- **Core 제약**: `UnityEngine` 금지(`noEngineReferences`), 시간·난수·IO·정적 가변 상태 금지. 같은 입력은 같은 결과.
- **WebGL 호환** (예외 하나: 데스크톱 종료 때 텔레메트리 마지막 전송을 최대 1초 기다리는 `Thread.Sleep` 루프, `#if !UNITY_WEBGL || UNITY_EDITOR` 안에서만 — telemetry.md 4장): `System.Threading`(Thread, Task.Run, 동기 대기), `System.IO` 파일 쓰기/읽기, `Process`, 소켓, `Application.persistentDataPath` 직접 쓰기 금지. 저장은 `PlayerPrefs`. 코루틴/`async`는 메인 스레드에서만. 리플렉션으로 타입 찾기 금지(코드 스트리핑).
- **매 프레임 할당 금지**: `Update`/`LateUpdate`에서 LINQ, `params` 배열, 문자열 결합, 람다 캡처, `GetComponent*` 금지. 문자열은 상태가 바뀔 때만 만든다. (v0 `GameController.Pressed(params ...)`+`Any`는 2단계에서 고칠 것)
- **입력**: Input System만 쓴다(`Keyboard.current` 또는 `InputSystem_Actions`). 레거시 `Input` 클래스 금지.
- **에디터 코드**는 `Scripts/Editor`의 `SevenSteps.Editor` 어셈블리에만. 런타임 코드에서 `#if UNITY_EDITOR`는 최소화.
- **구조 원칙 (사용자 지시 2026-11-08)**: 객체지향, MVC, 단정적 프로그래밍. 근거·사례는 `docs/scene-flow.md` 1장. 새 코드와 이번에 손대는 코드에 적용하고, 기존 코드는 `scene-flow.md` 5장 단계 계획대로 옮긴다(한꺼번에 고치지 않는다).
- **MVC**:
  - Model: Core, `GameSession`, `AppFlow`, `SaveSystem`. 순수 C#, 변경은 C# 이벤트로 알린다. View 구체 타입을 모른다. 예외 하나: `GameSession`은 Contracts의 `IBoardPresenter` 인터페이스로 연출을 순서대로 부른다(4장, 입력 락·시퀀싱 계약).
  - View: 표시 전용 MonoBehaviour·프리팹. 사용자 조작은 `event Action...`으로 낸다. Model을 바꾸지 않고 `Services`를 읽지 않는다. Model 데이터(`State`, `ProgressData` 등)는 읽기 전용으로 받아 그린다.
  - Controller: 입력·View 이벤트 → Model 명령, Model 이벤트 → View 갱신. 보드 진행 컨트롤러와 화면 흐름 컨트롤러는 다른 클래스다.
  - 참조 방향: Controller → View, Controller → Model. View → Model은 읽기 전용 데이터만. Model → 아무것도(위 예외 제외).
- **조립**: 씬마다 루트 `MonoBehaviour` 하나(`*SceneRoot`)가 `Services`에서 한 번 꺼내 컨트롤러·뷰를 생성자(또는 `Bind`)로 연결한다. 그 밖의 클래스는 `Services`를 부르지 않는다.
- **단정적 프로그래밍(fail fast)**:
  - 공개 메서드는 인자·상태를 먼저 검사하고 `ArgumentNullException`/`ArgumentOutOfRangeException`/`ArgumentException` 또는 `InvalidOperationException`을 던진다. 메시지에 값과 이유를 넣는다.
  - **게임 규칙상 안 되는 플레이어 명령은 예외가 아니다.** 벽으로 이동, 쓸 수 없는 KEEP 등은 `Rules.Dispatch`가 지금처럼 결과로 처리한다(`core.js`와 같은 결과, 골든 테스트). 흐름에서 지금 할 수 없는 키 입력도 컨트롤러가 조건을 드러내 무시한다(`if (!flow.CanGo(...)) return;`).
  - 내부 불변식: Game 어셈블리는 `UnityEngine.Assertions.Assert`(릴리스에서 빠짐). **Core에서는 `Debug.Assert`를 쓰지 않는다**(Unity 플레이어에서 실패해도 아무 일이 없을 수 있다). 꼭 지켜야 하는 것은 예외로 쓰고, `Solver`의 탐색 루프 같은 뜨거운 경로에는 검사를 넣지 않는다. 플레이어에서도 막아야 하는 것은 Assert가 아니라 예외로 쓴다.
  - 필수 직렬화 참조는 `Awake`에서 검사해 필드 이름을 넣어 예외, 에디터는 `OnValidate`에서 `Debug.LogError(..., this)`. null을 보고 조용히 `return`하거나 기본 인스턴스로 바꾸지 않는다.
  - 예외(복구 + 경고 로그)는 외부 입력뿐이다: 실행 인자·URL(원래 값을 로그에 넣고 가까운 값으로 고침), 손상된 저장 데이터(기본값), 네트워크(텔레메트리 재시도 규칙, telemetry.md 4장).
  - WebGL은 `webGLExceptionSupport: 1`(명시적으로 던진 예외만)이다. **NullReferenceException 같은 암묵적 예외는 WebGL에서 잡히지 않고 실행이 멈출 수 있다.** 그래서 null을 그냥 쓰지 말고 먼저 검사해서 던진다.
- **정적 가변 상태**: 런타임 정적 필드(캐시, `Services`, 플래그)는 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`에서 비운다. 에디터 도메인 리로드가 꺼져 있다. 정적 이벤트도 같다. Core는 정적 가변 상태 금지(위).
- **빌드 전 검증**: 씬·프리팹·`AppConfig`·`TileSet` 필수 연결은 에디터 검증기(`TileSetValidator`, `SceneFlowValidator`, 메뉴 + 배치, 오류면 exit 1)로 잡는다.
- **테스트**: Core와 Contracts는 EditMode 테스트로 검증한다. 실행: `Unity.exe -batchmode -projectPath unity -runTests -testPlatform EditMode -testResults reports/tests-editmode.xml -logFile -` (`-quit` 넣지 않음).

---

## 10. 2단계 작업 지시 (이 문서 기준)

| 담당 | 할 일 |
|---|---|
| 코어 | `Slide`, `DoorOpen` 이벤트 추가(3.3). 공개 API 형태 유지. EditMode 테스트를 `Tests/EditMode/`(Contracts 폴더 제외)에 |
| 게임플레이/UI | `GameController`가 `GameSession`을 쓰도록 변경(직접 `Rules.Dispatch` 호출 제거). `BoardView`가 `IBoardPresenter` 구현 + `TileSet`/`BoardGrid`/`TileId` 사용(글자 분기 제거). `GameAssets`에 `TileSet` 참조 추가. HUD를 `Game/UI/`로. 씬 분리와 `Bootstrap`(8장, 11장). 저장을 `SaveSystem`으로 교체(v0 키 자동 이전). 설정 패널(음량, 텔레메트리 동의). `TelemetryRecorder`와 HTTP 전송 `ITelemetry` 구현(11.4) |
| 아트 | `Data/TileSets/`에 실제 아트 `TileSet` 에셋(탑다운, 쿼터뷰). 프리팹 규격은 7.3 |
| 백엔드/분석 | `docs/telemetry.md`에 이벤트 스키마·전송 형식 고정 후 리드·아키텍트에게 알림 → 아키텍트가 `TelemetryEvent`/`ITelemetry`를 맞춤 |
| 리드 | `unity.sh`에 `tileset`(TileSetBuilder.BatchBuild), `test`(EditMode 테스트) 명령 추가 검토. 씬 분리 시 빌드 목록 갱신(Boot, Title, Game) |

---

## 11. 프로그램 기본 틀

공용 코드: `Contracts/App/`(`AppFlow`, `AppScenes`, `ISceneLoader`, `Services`, `LaunchOptions`, `AppConfig`), `Contracts/Save/SaveSystem.cs`, `Contracts/Telemetry/ITelemetry.cs`. 테스트: `Tests/EditMode/Contracts/AppFrameworkTests.cs`.

### 11.1 앱 흐름 (`AppFlow`)

```
Boot ──▶ Title ⇄ StageSelect ──▶ Game ⇄ Result
  └───────(?level= / -level)──────▶ Game        Game/Result ──▶ StageSelect
```

- 상태: `AppState { Boot, Title, StageSelect, Game, Result }` (끝에만 추가). 허용 전이는 `AppFlow.CanGo`가 정한다. 허용되지 않은 전이는 예외.
- 상태 → 씬: `AppScenes.For(state)`. 같은 씬 안의 상태 변경(Title⇄StageSelect, Game⇄Result, Game→Game 스테이지 변경)은 씬을 다시 읽지 않고 `AppFlow.Changed(from, to)`로 패널만 바꾼다.
- 스테이지 선택: `flow.GoToGame(stage)` (0부터). 클리어 시 `GameController`가 `flow.Go(AppState.Result)`, 결과 패널의 다음/재시도는 `flow.GoToGame(...)`.
- 씬 로드: `ISceneLoader`(`Current`, `Load(name)`, `event Action<string> Loaded`)를 Game 어셈블리 `SceneLoader`가 `SceneManager.LoadScene`으로 구현(씬 플로우). `Loaded`는 `SceneManager.sceneLoaded`에서 낸다(새 씬의 Awake 뒤, Start 전). 로드 대기 중에만 구독한다(도메인 리로드 꺼짐에서 누수 없음). 테스트는 가짜 로더(`Load` 안에서 `Loaded`를 내도 된다).
- **단정적 계약 (2026-11-09, 씬 플로우 S2)**:
  - 생성자 `AppFlow(ISceneLoader scenes, int stageCount)`: `scenes` null → `ArgumentNullException`, `stageCount < 1` → `ArgumentOutOfRangeException`. `StageCount` 공개.
  - `GoToGame(stage)`: `0 <= stage < StageCount`가 아니면 `ArgumentOutOfRangeException`(상태·`Stage` 그대로). 허용되지 않은 전이는 `InvalidOperationException`.
  - `IsLoading`: 다른 씬 로드를 요청하면 참, 그 씬의 `Loaded`에서 거짓. **로드 중 전이는 `InvalidOperationException`.** 다른 씬 이름의 `Loaded`는 무시. 로드가 끝나지 않으면(빌드 목록에 없는 씬) 흐름이 멈추므로 `SceneFlowValidator`가 빌드 전에 막는다.
  - 뒤로: `static TryGetBackTarget(from, out to)`/인스턴스 `TryGetBackTarget(out to)` — StageSelect→Title, Game→StageSelect, Result→StageSelect, Boot·Title은 없음. 모두 `CanGo` 안(새 전이 없음). `CanBack` = 로드 중 아님 + 뒤 대상 있음.
  - 입력 처리는 먼저 묻고 안 되면 무시한다: `IsLoading`, `CanBack`, `CanGoToGame(stage)`.
  - `Changed(from, to)`는 로드 요청 **뒤**(이전 씬이 아직 있고 `IsLoading` = 참)에 난다. 그 안에서 `Go`를 부르면 예외. 진입 경로는 하나: 새 씬은 루트 `Start`에서 `flow.State`로, 같은 씬 안 전이는 `Changed`로. `Entered` 이벤트는 없다.
- **로드 실패(S5)**: `ISceneLoader.Load`가 예외를 던지면 `AppFlow.Go`는 `IsLoading`·`State`를 되돌리고(`GoToGame`은 `Stage`도) 다시 던진다. `Changed`는 나가지 않는다. `SceneLoader`는 빌드에 없는 씬(`Application.CanStreamedLevelBeLoaded` 거짓)을 `Load`에서 바로 거부하므로 흐름이 '로드 중'에 멈추지 않는다. 정상 경로: 페이드 아웃(0.15초, Boot에서 나갈 때는 생략) → `LoadSceneAsync` → `sceneLoaded`에서 `Loaded` → 한 프레임(새 씬 `Start`) → 페이드 인. 어두운 동안 클릭 차단, 키 입력은 `IsLoading`으로 차단.
- 진입 경로(`level_start.via`): `EntryTracker`(Game `Flow/`, 부트스트랩이 `Services`에 등록). 첫 진입은 `url`(실행 인자·URL 레벨) 또는 `boot`, 그 뒤는 `Announce(next|again|select)` 후 `TakeVia()`, 알림 없으면 `select`. 정적 `GameController.FirstEntryPending`을 대체했다.
- `GameSession`(보드 안의 입력 락)과 `AppFlow`(화면 흐름)는 별개다. 스테이지 바꾸기 = `flow.GoToGame(k)` → `GameController`가 `session.Load(level)`.

### 11.2 서비스 등록 (`Services` + Boot 부트스트랩)

간단한 서비스 로케이터 하나. **등록은 Boot의 부트스트랩만**(그리고 테스트), 나머지는 `Awake/Start`에서 한 번 꺼내 필드에 둔다(매 프레임 조회 금지).

- 같은 타입을 두 번 `Register`하면 `InvalidOperationException`(조용히 덮어쓰지 않는다). 테스트는 `[SetUp]`에서 `Services.Clear()`.
- `Services.ResetForPlay()`가 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`로 Play마다(플레이어 시작 때도) 비운다(도메인 리로드 꺼짐, 9장).
- `Bootstrap.Run(config, assets, launch, enterFirstScreen, levelFromEditor)`: `config`·`assets`가 null이면 `ArgumentNullException`(Boot 씬 루트 `BootScreen`에 둘 다 연결, 검증기 필수), 같은 Play에서 두 번째 호출이면 `InvalidOperationException`. 직접 Play 판단은 `Bootstrap.HasRunThisPlay`(8장). `launch.Warnings`를 `Debug.LogWarning`으로 남긴다.

부트스트랩 순서 (게임플레이/UI가 `SevenSteps.Game`의 `Bootstrap`으로 구현):

```csharp
if (config == null) throw new ArgumentNullException(nameof(config));   // Boot 씬이 참조하는 Data/AppConfig.asset
if (HasRunThisPlay) throw new InvalidOperationException(...);
var launch  = LaunchOptions.Parse(Environment.GetCommandLineArgs(), Application.absoluteURL);
foreach (var w in launch.Warnings) Debug.LogWarning("[SevenSteps] Launch option " + w);
Services.Clear();
var save    = new SaveSystem(new PlayerPrefsStore());
ITelemetry telemetry = string.IsNullOrEmpty(config.telemetryEndpoint) || launch.TelemetryOff
                       ? new NullTelemetry() : new HttpTelemetry(/* Game 어셈블리 */);
telemetry.SetEnabled(save.TelemetryAllowed(config) && !launch.TelemetryOff);
var flow    = new AppFlow(new SceneLoader(), LevelCatalog.Load(assets).Length);   // Run(config, assets, ...)
Services.Register(config); Services.Register(launch); Services.Register<ISaveStore>(...); Services.Register(save);
Services.Register(telemetry); Services.Register(flow); Services.Register(EntryTracker.ForLaunch(...));
if (launch.DirectToGame) flow.GoToGame(FirstStage(launch, stageCount));   // 마지막 스테이지를 넘으면 경고 후 마지막
else flow.Go(AppState.Title);
```

| 서비스 | 타입 | 수명 |
|---|---|---|
| 빌드 설정 | `AppConfig` (ScriptableObject, `Data/AppConfig.asset`) | 앱 전체 |
| 실행 인자 | `LaunchOptions` | 앱 전체 |
| 저장소 | `ISaveStore` (`PlayerPrefsStore`, 테스트 `MemoryStore`) | 앱 전체 |
| 진행·설정 | `SaveSystem` (`Progress`, `Settings`, `SettingsChanged`) | 앱 전체 |
| 텔레메트리 | `ITelemetry` | 앱 전체 |
| 앱 흐름 | `AppFlow` | 앱 전체 |
| 진입 경로 | `EntryTracker` (Game 어셈블리) | 앱 전체 |

**`LaunchOptions` (2026-11-09 캡슐화)**: 읽기 전용 속성 `Level`(1부터, 0 = 없음), `Replay`, `Screenshot`, `TelemetryOff`, `TelemetryUrl`, `DirectToGame`, `OpenStageSelect`(`-stageselect`), `OpenCredits`(`-credits`), `Warnings`.
- 바꿀 수 있는 곳은 두 메서드뿐: `TakeReplay()`(리플레이를 한 번만 꺼내고 비움), `UseEditorStart(level, replay)`(에디터에서 Game 씬 직접 Play: 실행 인자·URL이 없을 때만 인스펙터 값을 씀, 인스펙터 레벨을 썼으면 true, `level < 1`이면 예외).
- 잘못된 값은 외부 입력이라 예외가 아니다: 숫자가 아니거나 1 미만인 `level`, 값이 없는 플래그, `off`/`0`/http(s)가 아닌 `telemetry`는 버리고 `Warnings`에 한 줄씩 남긴다. Contracts는 로그하지 않고 `Bootstrap`이 로그한다. 알 수 없는 인자·URL 키는 조용히 무시한다(Unity 자체 인자, `artfx` 등). 스테이지 수를 넘는 `level`은 S2에서 부트스트랩이 경고 후 보정한다.

- `GameSession`, `BoardView`, HUD는 서비스가 아니다. Game 씬의 `GameController`가 만들고 소유한다.
- Core는 `Services`를 모른다 (참조 자체가 불가능).

### 11.3 저장과 설정 (`SaveSystem`)

- 저장소는 모든 플랫폼에서 `PlayerPrefs` (WebGL에서는 브라우저 IndexedDB). 파일 IO 금지(9장).
- 키 두 개, 값은 `JsonUtility` JSON: `seven-steps.progress` → `ProgressData`, `seven-steps.settings` → `SettingsData`. 각각 `version` 필드가 있어 형식이 바뀌면 읽을 때 이전한다.
- 값이 없거나 깨졌으면 기본값으로 시작한다(예외로 멈추지 않음). v0 키 `seven-steps-cleared`(쉼표 목록)는 처음 읽을 때 `ProgressData.cleared`로 옮긴다.
- `ProgressData`: `cleared`(클리어한 스테이지, 0부터), `bestStage`/`bestBodies`(스테이지별 최소 몸 수), `lastStage`. `MarkCleared(stage, bodiesUsed)`.
- `SettingsData`: `masterVolume`, `sfxVolume`, `musicVolume`(0~1), `telemetryChoice`(0 미선택=기본값 따름, 1 동의, 2 거부), `playerId`(익명 무작위 ID, 거부하면 지움), `language`.
- 저장 시점: 클리어할 때, 스테이지 선택으로 나갈 때, **설정 값을 바꿀 때마다**(씬 플로우 S4: 패널이 Esc·버튼·씬 전환 어느 쪽으로 닫혀도 값이 남는다). 매 걸음 저장하지 않는다. `SaveSettings()`는 `SettingsChanged`를 발생시켜 오디오 음량과 텔레메트리 사용 여부를 바로 반영한다.
- **설정 명령(2026-11-09)**: 설정 패널 컨트롤러는 필드를 직접 바꾸지 않고 이것을 부른다(9장 MVC). 모두 바로 저장하고 `SettingsChanged`를 낸다.
  - `SetMasterVolume(v)`, `SetSfxVolume(v)`: 0.1 단위로 반올림, 0~1로 자름(+를 1에서 눌러도 1). NaN은 예외.
  - `SetTelemetryChoice(on)`: 켬 = 1, 끔 = 2 + `playerId` 지움(다시 켜면 텔레메트리 시작 때 새 ID).
  - `TelemetryOn(onByDefault)`: 토글 표시값(명시 선택, 없으면 빌드 기본값). `TelemetryAllowed(config)`와 같은 판단.
- `SaveSystem(store)`: `store`가 null이면 `ArgumentNullException`.

#### 11.3.1 진행 기록 v2: 스테이지 이름 기준 (2026-11-09 결정·구현, `Contracts/Save/Progress.cs`)

문제(`design/levels/rollout-50.md` 4장 "저장 데이터"): `ProgressData`의 `cleared`·`bestStage`·`starts`·`lastStage`가 **스테이지 번호(0부터)**라 50스테이지 반영(삽입·순서 변경) 때 옛 기록이 다른 스테이지에 붙는다.

**결정: 기록 키를 스테이지 이름(`Level.Name`)으로 바꾸고, 옛 기록은 이름으로 옮긴다(초기화하지 않는다).**
- 이름은 이미 고유하다(레벨 디자인 export 검증 "이름 중복 없음", 텔레메트리 분석도 이름으로 묶음). 번호가 바뀌어도, 스테이지가 끼어들어도 기록이 따라간다. 레벨 세트 버전으로 비우는 방식은 반영 묶음(R1~R5)마다 플레이테스터 기록을 다섯 번 지우게 되어 채택하지 않는다.
- 맵이 바뀐 같은 이름 스테이지(`edit`)는 `levelKey`(`TelemetryJson.LevelKey`, 맵·몸 수 해시)로 구분한다: **클리어 표시는 유지, 최소 몸 기록은 버린다**(다른 퍼즐의 기록이므로).

형식 (`seven-steps.progress`, `version = 2`):
```json
{ "version": 2, "lastStage": "새 출발점",
  "stages": [ { "name": "일곱 걸음", "levelKey": "a1b2c3d4", "cleared": true, "bestBodies": 1, "starts": 3 } ] }
```
- `StageRecord { name, levelKey, cleared, bestBodies(0 = 없음), starts, bestStars(0 = 없음, 3별 B안 대비 자리만 — 계산·기록 없음, 결정되면 규칙과 함께 채움) }`, `ProgressData { version, stages, lastStage(이름, "" = 없음) }`.
- 질의·명령(Contracts, `Level`을 받음): `IsCleared(level)`, `BestBodies(level)`(기록 없음·`levelKey` 다름·몸 수 초과면 0), `MarkCleared(level, bodiesUsed)`(기록 갱신, `levelKey` 갱신), `CountStart(level)`(텔레메트리 `attempt`), `ClearedCount`, `LastStage`(이름)와 `LastStageIndex(levels)`(없거나 지금 목록에 없으면 0).
- 지금 목록에 없는 이름의 기록은 지우지 않는다(나중에 다시 들어올 수 있음, 크기 작음).

이전(읽을 때 한 번, 바로 v2로 저장):
- **v1**(`version` 1, 번호 목록)과 **v0**(`seven-steps-cleared` 쉼표 목록): 번호 k → `ProgressMigration.V1StageNames[k]`(**2026-11-09 7lucky의 17스테이지 이름을 고정한 표**, 코드에 상수). 표 밖 번호는 버린다. `levelKey`는 비워 둔다("모름" = 최소 몸 기록은 몸 수 범위 안일 때만 표시).
- 이전 전 원본은 `seven-steps.progress.v1` 키에 복사해 둔다(되돌릴 수 있게).
- `version`이 2보다 크면(더 새 빌드의 저장) 덮어쓰지 않고 기본값으로 시작 + 경고(외부 입력 규칙).
- 같은 이름이 `levels.json`에 둘 이상이면 `LevelCatalog.Load`가 예외(이름 = 저장 키).
- 호출부: 마지막 스테이지는 `GameFlowController`가 보드 로드 뒤 `SetLastStage(level)`, 클리어는 `MarkCleared(s.Level, ...)`, 결과 화면 최고 기록은 `BestBodies(level)`. `TitleFlowController(flow, save, panels, levels)`의 `PlayLastStage`는 `LastStageIndex`. `HudView(coroutines, tileSet, levels)`(stage 수 대신 목록). 텔레메트리 `attempt`는 `CountStart(LevelCatalog.At(level - 1))`, `session_start.cleared`는 `ClearedCount`. 테스트 `ProgressV2Tests`(이름 따라감, 맵 변경, v1 이전·백업, 미래 버전).

**구현 담당 제안**: 아키텍트가 Contracts(`ProgressData` v2, `StageRecord`, `ProgressMigration`, EditMode 테스트: v0·v1 이전, `levelKey` 불일치, 미래 버전)와 **호출부 기계적 수정을 한 커밋에** 넣는다(공용 API가 바뀌어 나눠 넣으면 7lucky 컴파일이 깨짐). 호출부: `GameFlowController`·`TitleFlowController`(씬 플로우), `TitleViews`·`HudView`·`TelemetryRunner`(게임플레이/UI), `LevelCatalog` 이름 중복 검사(씬 플로우). 각 소유자가 자기 파일을 검토한다. **R1(첫 50스테이지 묶음) 합류 전에** 들어가야 한다.

### 11.4 텔레메트리 (`ITelemetry`, 스키마 v1)

스키마·전송 사양의 기준은 **`docs/telemetry.md` v1**(백엔드/분석)이고, 이벤트 생성 기준 구현은 `prototypes/seven-steps/src/telemetry.js`의 `Tracker`다. Unity는 같은 입력에 같은 이벤트를 낸다. 공용 타입(`Contracts/Telemetry/`):

| 타입 | 역할 |
|---|---|
| `ITelemetry` | `Enabled`, `SetEnabled`, `Track(TelemetryEvent)`, `Flush()`. 막지 않고 예외를 던지지 않는다 |
| `TelemetryEvent` | `Type` + 순서 있는 필드(`With(key, string/int/long/double/bool)`, `WithCell(key, cell, width)` → `[x,y]` 또는 `null`). `seq`·`t`는 전송 구현이 `Stamp`로 붙인다 |
| `TelemetryContext` | 배치 머리: `playerId`, `sessionId`, `client`(`unity-webgl`/`unity-desktop`), `build`(≤40자), `Endpoint` → `EventsUrl` = `<Endpoint>/v1/events`. `SchemaVersion = 1` |
| `TelemetryNames` | 이벤트 이름(`Types.Move` 등)과 값(`Via`, `Dir`, `MoveResult`, `Effect`, `LostReason`, `EndReason`, `Status`, `Tile`), `IdleMs = 30000`. 문자열을 직접 쓰지 않는다 |
| `TelemetryJson` | 리플렉션 없는 JSON: `WriteEvent`, `Batch(ctx, events, start, count)`, `LevelKey(Level)`(FNV-1a, `telemetry.js`와 같은 값), `Fnv1a` |
| `NullTelemetry` / `MemoryTelemetry` | 끔 / 테스트용(메모리에 쌓고 seq 부여) |

- **Core는 텔레메트리를 모른다.** 게임플레이/UI가 `SevenSteps.Game`에 `TelemetryTracker`(telemetry.js `Tracker` 이식)를 만든다. `GameSession.Updated`의 `Prev`·`State`·`Result.Events`와 `AppFlow` 전이를 받아 이벤트를 만들고, 규칙은 다시 계산하지 않는다(타일 종류 등은 `Rules.TileAt` 질의). 시계는 주입받는다(테스트에서 0 고정).
- 이벤트 순서(telemetry.md 3장): `idle` → `move` → `seventh_step` → `body_lost` → `level_fail`/`level_clear`. 리플레이(`GameSession.Replay`)는 기록하지 않는다 — `SessionUpdate.Command`가 기본값이고 `Result`가 null인 `Snap`이 리플레이다.
- 필요한 저장값: `SettingsData.playerId`(UUID v4, 없으면 생성, 옵트아웃 시 지움), `ProgressData.CountStart(stage)` → `level_start.attempt`, `ProgressData.cleared.Count` → `session_start.cleared`, `firstTime` = 처음 실행(`playerId`를 새로 만든 경우).
- 서버 주소 우선순위: `LaunchOptions.TelemetryUrl`(`?telemetry=<url>` / `-telemetry <url>`) → `AppConfig.telemetryEndpoint` → 없으면 `NullTelemetry`. 끄기: `?telemetry=off`(또는 `0`), `-telemetry off`, 설정 `telemetryChoice = 2`. 꺼져 있으면 큐도 만들지 않는다.
- `build` = `Application.version`(BuildTools가 `0.1.0+<커밋>[-dirty]`로 넣음). `client` = WebGL이면 `unity-webgl`, 아니면 `unity-desktop`.
- 전송 사양은 telemetry.md 4장 그대로: 5초 또는 20건마다, 배치 최대 100건·64KB, 네트워크 오류·429·5xx만 재시도(5s→…→최대 5분, `Retry-After` 우선), 2xx·400·403·413은 큐에서 삭제, 요청 10초 제한, 남은 큐는 종료 때 PlayerPrefs `seven-steps.telemetry-queue`에 최대 1000건.
- **골든 검증**: `server/fixtures/telemetry.golden.json`(feat/server 134dfbc)을 `Tests/EditMode/Game/`에 복사해 `TelemetryTracker` + `MemoryTelemetry`로 같은 명령을 재생하고 비교한다 (`TelemetryJson.WriteEvent(..., includeTime: false)`). 테스트 어셈블리는 `SevenSteps.Game`을 참조한다.

### 11.5 텔레메트리 구현 파일 위치 (게임플레이/UI 소유)

| 파일 | 위치 | 어셈블리 |
|---|---|---|
| `TelemetryTracker`(순수 C#), `HttpTelemetry : ITelemetry`, `TelemetryRunner`(MonoBehaviour) | `Assets/_Project/Scripts/Runtime/Game/Telemetry/`, 네임스페이스 `SevenSteps.Game.Telemetry` | `SevenSteps.Game` (새 asmdef 만들지 않음) |
| WebGL 플러그인 `SevenStepsTelemetry.jslib` | `Assets/_Project/Plugins/WebGL/` | 없음. `.jslib`는 asmdef에 속하지 않는 플러그인 에셋이다. 임포트 설정에서 **WebGL 플랫폼만** 켠다 (`.meta`에 기록되므로 함께 커밋) |
| 골든 테스트 | `Assets/_Project/Tests/EditMode/Game/` | `SevenSteps.Tests.EditMode` |

- 이 폴더들은 게임플레이/UI 소유다. `Assets/Plugins/`(루트)는 쓰지 않는다.
- jslib 함수 이름은 **코드 기준** `SevenSteps_` 접두사(전역 이름 충돌 방지): `SevenSteps_SendBeacon(url, json)` → `navigator.sendBeacon(url, new Blob([body], {type:'text/plain'}))`(성공 1/실패 0), `SevenSteps_RegisterVisibility(objectName)` → `visibilitychange`·`pagehide` 등록. telemetry.md 4장의 `TelemetryBeacon`과 같은 동작이고 이름만 다르다. 이름을 바꾸면 jslib, C# `DllImport`(`HttpTelemetry`, `TelemetryRunner`), 이 줄을 함께 고친다. C# 선언은 `[DllImport("__Internal")]`을 `#if UNITY_WEBGL && !UNITY_EDITOR`로 감싸고, 그 밖에서는 `UnityWebRequest`만 쓴다.
- 페이지 숨김 → jslib가 `SendMessage("TelemetryRunner", "OnPageHidden")`. 그래서 Boot가 만드는 `DontDestroyOnLoad` 오브젝트 이름을 **`TelemetryRunner`로 고정**한다. `OnPageHidden`은 `session_end`(`reason: hidden`)를 넣고 큐를 100건씩 잘라 `TelemetryBeacon`으로 보낸다. 다시 보이면 `session_start`(`resumed: true`), 같은 `sessionId`.
- 데스크톱 종료: `Application.quitting`에서 `session_end`(`reason: unload`) 후 최대 1초 전송 대기.
- 서버 주소는 11.4의 우선순위로만 정한다. 코드에 주소를 적지 않는다.

---

## 12. 3단계 준비: 쿼터뷰 전환 계획

목표: 아트의 실제 `TileSet`이 나오면 **에셋 지정만으로** 3D 보드가 뜨게 한다. 코드 변경은 아래 "필요할 때만" 항목으로 한정한다.

### 12.1 이미 되어 있는 것 (2단계 결과)
- 보드는 `TileSet`/`TileVisual`/`BoardGrid`/`TileId`만 쓴다. 격자는 투영과 무관하고, 클릭은 `BoardGrid.RayToCell`이라 카메라 각도와 상관없이 맞다.
- `BoardView.FitCamera`가 `TileSet.cameraRig`(pitch, yaw, 직교/원근, 거리, 여백)로 보드 전체를 HUD 빈 띠 안에 맞춘다. 숫자·글자는 화면 라벨(uGUI)이라 각도와 상관없이 보인다.
- 연출은 Animator 트리거가 있으면 그것을, 없으면 코드 트윈을 쓴다(`TileVisual.*Trigger`).
- 사용할 세트: `AppConfig.tileSet`(없으면 `GameAssets.tileSet`). 세트 교체 = 이 참조 하나.

### 12.2 카메라 결정 (권장)
| 안 | pitch / yaw | 장점 | 비용 |
|---|---|---|---|
| **A. 정면 3/4 뷰 (권장 기본)** | 45~55° / 0° | 방향키 = 화면 방향 그대로. 입력 코드 변경 없음 | 벽 뒤 칸이 조금 가려짐 → 벽 높이로 해결 |
| B. 아이소메트릭 | 30~35° / 45° | 전형적인 쿼터뷰 느낌 | 방향키와 화면 방향이 45° 어긋남 → 입력 회전 필요, 튜토리얼 문구 수정 |

- 기본은 **A**. `CameraRig`에 `pitch 50, yaw 0, orthographic`을 넣은 세트로 시작한다. B로 가려면 사용자·리드 결정 후 게임플레이/UI가 `GameController`에서 화면 기준 입력을 지도 방향으로 돌린다(`yaw`를 90° 단위로 반올림해 `Dir`을 회전). `TileSetValidator`가 yaw ≠ 0이면 경고한다.
- Game 씬 `GameController`의 `overrideCamera`/`cameraRig`(27cb609, 기본 꺼짐, pitch 50)는 **미리 보기용**이다. 기준은 세트의 `cameraRig`이고, 실제 세트를 받을 때 씬 설정은 꺼 둔다(켜져 있으면 세트의 카메라가 무시된다).
- 직교 투영 유지(퍼즐은 칸 수를 세는 게임이라 원근 왜곡이 불리). 원근은 연출용으로만.

### 12.3 프리팹 규격 (아트, 7.3 보강)
- 1칸 = `cellSize`(기본 1) 유닛, 바닥 윗면 y = 0, 칸 중앙 피벗. 어긋나면 프리팹 대신 `TileVisual.offset/scale`로 맞춘다.
- **벽 높이 ≤ 0.6칸**(안 A에서 뒤 칸을 가리지 않는 한도). 문·뒤집힘 벽(올라감)도 같은 높이.
- 상태 표시는 자식 이름 훅으로: 문 창살(`inactiveChild`), 게이트 색(`materialTarget` + 머티리얼 2개), 뒤집힘 벽은 variant 0/1 프리팹 두 개.
- 바닥 무늬 반복은 `randomYaw90`.
- 플레이어·석상: 발밑이 y = 0, 높이 1칸 이하. 플레이어 머리 위 숫자는 `player.labelOffset`.
- 세트 자산: `Art/<세트>/<세트>.asset`(아트 소유, 12.7). 임시 세트 `TopDown_Placeholder`는 대체될 때까지 남긴다.
- 파일 위치: 모델·텍스처는 공유 폴더 `Assets/Shared/**`(git 밖, `.meta` 포함), 프리팹은 `Prefabs/Tiles/**`, 머티리얼은 `Art/**`(둘 다 git). 프리팹·머티리얼이 공유 폴더의 GUID를 참조하므로 공유 폴더 파일을 지우거나 GUID를 바꾸면 모든 작업 공간에서 메시가 빈다.
- WebGL은 **Mobile 품질(`Mobile_RPAsset`)** 로 돈다. 그림자·포스트 효과·셰이더는 Mobile 렌더러 기준으로 확인한다(Windows의 PC 품질에서만 보면 놓친다).

### 12.4 받는 절차 (아트 세트가 오면)
1. 아트: `Data/TileSets/`에 세트를 만들고 `unity.sh` 배치 또는 메뉴 **Seven Steps > Architecture > Validate TileSets**로 검사(오류 0). 검사: 빠진 프리팹(오류), 없는 자식 훅(오류), Animator에 없는 트리거(오류, Animator 자체가 없으면 경고: 코드 트윈 사용), 한 칸보다 큰 크기·중심 어긋남·바닥 윗면 y≠0(경고), 쿼터뷰 yaw(경고). 배치: `-executeMethod SevenSteps.EditorTools.TileSetValidator.BatchValidate`(오류면 exit 1).
2. 게임플레이/UI: `AppConfig.tileSet`에 지정 → `unity.sh build-win` + `smoke`(리플레이 7개 스크린샷, 17스테이지 전부는 아님. 기믹이 빠진 스테이지는 에디터에서 `[` `]`로 직접 본다) → `unity.sh build-webgl` + **`smoke-webgl`**(보드 픽셀 수·콘솔 예외. 픽셀 기준 500은 임시 아트 색으로 잡은 값이라 실제 세트에서는 다시 잡아야 할 수 있음).
3. 아키텍트: 스크린샷과 검사 결과로 검토. 가림·라벨 위치 문제는 세트 값(`labelOffset`, 벽 높이)으로 먼저 해결한다.

### 12.5 필요할 때만 하는 코드 작업
| 상황 | 할 일 | 담당 |
|---|---|---|
| 안 B(yaw 45°) 채택 | 입력 회전, 안내 문구 | 게임플레이/UI |
| 벽이 뒤 칸을 가림(높이 줄여도) | 카메라 쪽 벽 반투명(셀 단위 머티리얼 교체, 플레이어보다 앞 행만) | 게임플레이/UI |
| 탑다운/쿼터뷰 실행 중 전환(디버그) | `BoardView.Init(새 세트)` + `session` 상태로 `Rebuild` | 게임플레이/UI |
| 그림자·조명 | Game 씬의 방향광(그림자 켬), 품질 설정은 그대로(WebGL = Mobile). 그림자는 `Mobile_RPAsset`에서 켠다. 예전의 "PC 품질 유지"는 틀렸다: 보드 누락의 원인은 빌드 시작 때의 활성 타깃이었다(CLAUDE.md) | 게임플레이/UI(씬), 아트(RPAsset) |
| 세트 필드 추가(새 기믹) | `TileKind`/`TileSet` 끝에 추가, 검사 목록 갱신 | 아키텍트 |

### 12.6 몸별 외형 (세쌍둥이, 2026-11-09 계약)

사용자 결정: 세쌍둥이 A안. 몸마다 외형이 다르고(A/B/C), KEEP으로 남은 석상은 그 몸의 색 흔적이 있는 돌이다.

- **몸 인덱스**: 0 = 첫 몸(A), 1 = B, 2 = C. `BodyIndex.Current(state)`(Contracts). 정의: 첫 상태의 `Bodies` - 지금 `Bodies`. 실패(`Lose`) 뒤에는 마지막 몸.
- **석상의 주인**: `BodyIndex.OfStatue(state, cell)`. 석상이 없으면 -1. Core는 석상을 정렬된 칸 목록으로만 들고 있어 주인을 모른다. 그래서 **Undo 사슬(`State.Prev`)을 거슬러** 그 석상이 처음 생긴 직전 상태의 몸 인덱스를 쓴다. Core·`core.js`·골든은 바꾸지 않는다. Undo/Restart 뒤에도 상태만으로 다시 계산되므로 5장(스냅샷 재그리기)을 지킨다. 비용은 사슬 길이만큼이라 **상태가 바뀔 때만** 부른다(매 프레임 금지).
- **TileSet 필드(끝에 추가)**: `playerBodies[]`, `statueBodies[]`. 조회는 `tileSet.PlayerFor(bodyIndex)`, `tileSet.StatueFor(bodyIndex)`. 배열이 비었거나 그 칸에 프리팹이 없으면 기존 `player`/`statue`로 돌아간다(임시 세트는 그대로 동작). 인덱스는 **배열 길이로 나머지 순환**한다(몸이 4~5개인 스테이지: A, B, C, A, ...).
- 다리(구덩이를 메운 석상)·봉인(금 간 바닥)은 타일(`Bridge`)이라 몸별 외형이 없다. 필요하면 아트가 요청한다.
- **보드(게임플레이/UI 구현)**: 플레이어 인스턴스는 `BodyIndex.Current`가 바뀌면(`Respawn`, Undo, Restart) 프리팹을 바꿔 다시 만든다. 석상은 `Draw`에서 칸마다 `StatueFor(BodyIndex.OfStatue(s, i))`로 만들고, 같은 칸의 주인이 바뀌면(Undo 후 다른 몸이 KEEP) 다시 만든다. 플레이어 머리 위 숫자 위치는 `PlayerFor(k).labelOffset`.
- **검사**: `TileSetValidator`가 두 배열의 각 항목에 프리팹이 있는지 보고(오류), 길이가 3이 아니면 경고한다. `bodyColors`가 3개가 아니면 경고. EditMode `BodyIndexTests`(KEEP 주인, Undo, Restart, 대체·순환).
- **아트**: `playerBodies` = PlayerA/B/C, `statueBodies` = 형제 색 흔적이 있는 석상 3개(지금 세트에는 중성 석상 하나뿐이라 새로 필요).
- **몸 수 상한 3**(사용자 결정 2026-11-09). 지금 4~5개인 14~17스테이지는 레벨 디자인이 고친다. `levels.js`·`LevelVerifier`에 `bodies <= 3` 검사를 넣는다(레벨 디자인·코어). 순환은 안전장치로만 남는다.
- **HUD 몸 아이콘 3색**: `TileSet.bodyColors[3]`(기본값 청록 `#568C88`, 황토 `#B99A57`, 적갈 `#B96C59`, 아트 바이블 머플러 색), 조회 `tileSet.BodyColor(k)`. HUD는 남은 몸·쓴 몸을 이 색 아이콘으로 그린다(쓴 몸은 흐리게, 지금 몸은 강조). 색은 캐릭터 고유라 챕터가 바뀌어도 같은 값을 쓴다(세트마다 같은 값을 넣는다).

### 12.7 TileSet 위치와 검사 범위

- **정식 위치: 아트 세트는 `Assets/_Project/Art/<세트>/<세트>.asset`**(머티리얼·메시·셰이더와 같은 폴더, 아트 소유). `Data/TileSets/`에는 임시 세트 `TopDown_Placeholder`만 둔다.
- `TileSetValidator`는 `Assets/_Project` 전체에서 TileSet을 찾는다. 위 두 곳 밖에 있으면 오류. 새 검사: 메시·머티리얼 슬롯이 비었거나 셰이더가 없으면 **오류**(공유 폴더 미연결·GUID 깨짐), 벽·문·석상 수 문·올라간 뒤집힘 벽이 0.6칸보다 높으면 경고.
- 프리뷰 씬은 `Art/Test/`에 둔다. `Art/Test/Game.unity`는 아트 프리뷰 빌드용 별칭(공용 `SceneLoader`가 이름 `Game`으로 로드하므로)이라 **빌드 목록(`EditorBuildSettings`)에 넣지 않는 조건으로** 둔다. `SceneFlowValidator`의 이름 중복 검사는 빌드 목록 기준. 정식 Game 씬이 챕터 프리셋을 쓰게 되면(12.8) 별칭과 프리뷰 씬을 지운다. 프리뷰용 런타임 스크립트는 `Assets/_Project/Art/**`에 asmdef 없이 두면 플레이어 빌드(Assembly-CSharp)에 들어간다. 정식 환경은 12.8의 `IStageEnvironment`로 옮긴다.

### 12.8 챕터 프리셋 (2026-11-09 타입 추가, 적용은 게임플레이/UI)

사용자 결정: 이끼 유적 × 선돌 × 동양 석재·목재, 챕터 = 작은 행성, 전체 50스테이지. 챕터 = 스테이지 묶음 + 그 묶음의 보드 세트·머티리얼·조명·하늘. 타입은 Contracts `Chapters/`(아키텍트), 에셋은 아트(`Art/<세트>/`).

| 타입 | 필드 | 뜻 |
|---|---|---|
| `ChapterPreset` (SO) | `id`, `displayName`, `tileSet`, `materialOverrides[]`(`from`→`to`), `lighting`, `sky` | 한 챕터. `Override(Material)`로 교체 머티리얼 조회. 여러 챕터가 같은 `tileSet`을 쓰고 머티리얼만 바꿀 수 있다(행성마다 이끼·돌 색) |
| `LightingPreset` (SO) | `key`/`fill`/`rim`(`LightRig`: `enabled`, `color`, `intensity`, `euler`, `shadows`), `ambient` | 조명 값. 그림자는 키 라이트만(WebGL Mobile 예산) |
| `SkyPreset` (SO) | `background`, `skyPrefab`, `planetPrefab` | 카메라 배경색, 카메라를 따라가는 하늘(성운·별·먼 행성, 후처리 `Volume` 가능), 보드 아래 행성 정상부·장식 |
| `IStageEnvironment` | `Fit(BoardGrid grid)` | `planetPrefab` 루트가 구현. 스테이지 진입 때 한 번 불려 정상부를 W×H에 맞춘다. 게임 오브젝트 찾기·폴링·명령줄 읽기 금지 |
| `AppConfig.chapters[]` | 첫 항목이 기본 | `AppConfig.Chapter(id)`: 빈 id = 첫 챕터, 챕터 없음 = null, 모르는 id = 예외 |
| `levels.json` `chapter` + `chapters.json` | 문자열 id, 챕터 정보·`artPreset`은 `chapters.json`(6.4) | `LevelDto`에만 둔다(Core `Level`은 모름). `LevelCatalog.ChapterOf(stage)` → `ChapterInfo` → `AppConfig.Chapter(info.ArtPreset)` |

**UI 테마는 챕터에 넣지 않는다(2026-11-09 결정)**: `UiTheme`(Game 어셈블리, 씬 플로우 S6)은 `GameAssets.uiTheme` 하나로 게임 전체에 쓴다. `ChapterPreset`(Contracts)은 Game 타입을 참조할 수 없고(2장), UI 모양은 게임 전체의 정체성이다. 챕터 분위기는 보드·조명·하늘과 챕터 전환 카드(`GameFlowController.ChapterEntering`)가 맡는다. 챕터별 강조색이 필요해지면 `ChapterPreset.uiAccent`(Color) 하나만 추가한다.

**보드 세트 결정 순서**: `AppConfig.tileSet`(설정되어 있으면 디버그·전환용 덮어쓰기) → 스테이지 챕터의 `tileSet` → `GameAssets.tileSet`. 지금(챕터 에셋 전)은 `AppConfig.tileSet = RuinsHenge_Chapter01`. 챕터 에셋이 생기면 `chapters[0]`에 유적 챕터를 넣고 `AppConfig.tileSet`은 비운다.

**적용 (게임플레이/UI, Game 씬 연결은 씬 플로우)**:
- 스테이지 진입 때 챕터가 바뀌었으면: 하늘·행성 인스턴스 교체(이전 것 파괴), `LightingPreset` 값을 씬의 방향광 3개(키·필·림)에 복사, `RenderSettings.ambientLight`, 카메라 배경색, `BoardView.Init(새 세트)` 후 다시 그리기. 같은 챕터면 `IStageEnvironment.Fit`만.
- 머티리얼 교체는 보드가 프리팹을 만들 때 한 번(`renderer.sharedMaterials`를 `chapter.Override`로 바꾼 배열). 매 프레임·`material`(인스턴스 복제) 금지.
- Game 씬의 조명은 방향광 3개만 두고 값은 프리셋이 정한다. 챕터가 없을 때(임시 세트)는 씬의 현재 값 그대로.
- S5 페이드가 있으면 챕터 교체를 그 뒤에 숨긴다.

**아트 프리뷰 이전**: `RuinsHengeEnvironment`(지금 `FindFirstObjectByType<GameController>` + `LateUpdate` 폴링 + 명령줄 직접 파싱 + 하늘을 카메라에 맞춰 매 프레임 크기 조정)를 `SkyPreset.skyPrefab`과 `planetPrefab`(`IStageEnvironment.Fit`)으로 나눈다. `artfx=off`/`-artfxoff`는 `LaunchOptions`가 흡수(씬 플로우 S1). 프리뷰 조명 값(키 `#FFE2B9` 2.0, 필 `#A8CFFA` 0.55, 림 `#E4CEFF` 0.28)은 `LightingPreset` 에셋으로.

**검사**: `TileSetValidator`가 `ChapterPreset`도 본다: id 비었거나 중복이면 오류, `tileSet` 없으면 오류, 빈 머티리얼 교체 오류, `lighting`·`sky` 없음 경고, `planetPrefab` 루트에 `IStageEnvironment` 없으면 경고. `levels.json` `chapter` id가 `AppConfig.chapters`에 다 있는지는 필드가 생기면 추가.

**WebGL 예산**: 챕터 에셋은 `AppConfig.chapters` 참조로 모두 빌드에 들어간다(Addressables 안 씀). 행성을 늘리면 빌드 크기를 잰다. 1280×720 draw call 400 안팎(유적 레벨 8: 403). 바닥·벽은 SRP Batcher, 하늘·장식은 결합 메시. 후처리는 Bloom·톤매핑까지(Mobile 렌더러).

### 12.9 smoke-webgl 판정 (팔레트 무관, 리드가 구현)

리드가 `unity.sh`·`board-pixels.ps1`을 고친다. 아래는 아키텍트 의견이다.

지금 `board-pixels.ps1`은 옛 보라색 바닥만 센다. 유적 세트에서는 보드가 보여도 실패한다(아트 측정: 0~90픽셀).

- **안 A (권장): 게임 안에서 보드가 그려졌는지 잰다.** 스모크일 때만(`replay`가 있거나 `smoke=1`) 리플레이가 끝나면 두 프레임을 캡처한다: 보드 루트를 끈 프레임, 켠 프레임(`WaitForEndOfFrame` + `ScreenCapture.CaptureScreenshotAsTexture`). 두 장에서 달라진 픽셀 수를 콘솔에 남긴다: `[SevenSteps] BoardProbe changed=<n> total=<w*h>`. `smoke-webgl`은 이 줄을 읽어 `changed/total >= 1%`면 통과. 팔레트·조명·환경과 무관하고, 2026-11-07 문제(셰이더 스트리핑으로 보드만 안 그려짐)를 그대로 잡는다. 구현: 게임플레이/UI(프로브), 리드(`unity.sh` 판정). `board-pixels.ps1`은 프로브가 들어오면 지운다.
- 안 B: 스크린샷만으로 판정. 게임이 보드 화면 영역을 콘솔에 남기고, 검사기는 그 영역의 색 다양성(서로 다른 색 수·표준편차)을 본다. 게임 코드가 덜 들지만, 행성·하늘 배경이 영역 안에 들어오면 보드가 없어도 통과할 수 있다.
- 전환 기간: 유적 세트로 바꾸는 합류 한 번은 아트의 `design/art/check-ruins-pixels.ps1` 결과 + 스크린샷 3장 + 콘솔 예외 0으로 대신 확인한다(리드 판단).

---

## 변경 기록

| 날짜 | 누가 | 내용 |
|---|---|---|
| 2026-11-07 | 아키텍트 | 최초 작성. `SevenSteps.Contracts` 어셈블리(`TileId`, `TileVisual`, `TileSet`, `CameraRig`, `BoardGrid`, `IBoardPresenter`, `GameSession`), `SevenSteps.Tests.EditMode` 어셈블리, test-framework 1.8.0 명시, `TileSetBuilder`와 `TopDown_Placeholder.asset`. Core·Game 동작 변경 없음 (v0 그대로) |
| 2026-11-07 | 아키텍트 | 1단계 범위 확장(사용자 요청): 프로그램 기본 틀 11장. `AppFlow`/`AppState`/`AppScenes`/`ISceneLoader`, `Services`, `LaunchOptions`, `AppConfig`, `SaveSystem`(`ISaveStore`, `PlayerPrefsStore`, `MemoryStore`, `ProgressData`, `SettingsData`), `ITelemetry`/`NullTelemetry`/`TelemetryEvent`(임시)/`TelemetryContext`, 11.5 텔레메트리 구현·jslib 위치. 8장 씬 표를 상태 5개 기준으로 수정. `GameSession`: 연출 중 Undo는 가장 최근 큐 입력을 먼저 취소 |
| 2026-11-07 | 코어 | `GameEventType`에 `Slide`(15), `DoorOpen`(16) 추가(3.3). `core.js`도 `slide`/`doorOpen` 이벤트를 냄. 기존 이벤트·순서·공개 API 형태는 그대로. 게임플레이/UI는 `Path.Length > 1`/`DoorOpen` 비교 대신 이 이벤트를 쓸 수 있음. EditMode 테스트 `Tests/EditMode/Core/` 추가(골든 대조) |
| 2026-11-07 | 아키텍트 | 텔레메트리를 `docs/telemetry.md` v1에 맞춤: `TelemetryEvent`를 순서 있는 필드 + `WithCell`([x,y]/null) + `Stamp(seq,t)`로 변경, `TelemetryContext`(배치 머리, client 상수, `EventsUrl`), `TelemetryNames`(이벤트·값 상수), `TelemetryJson`(배치 JSON, `LevelKey` FNV-1a), `MemoryTelemetry`. `LaunchOptions.TelemetryUrl`(`telemetry=<url>`), `off` 지원. `ProgressData.starts`/`CountStart`(attempt). 테스트 어셈블리가 `SevenSteps.Game` 참조. 11.4·11.5 갱신(jslib 함수 `TelemetryBeacon`) |
| 2026-11-07 | 아키텍트 | 석상 수 문 기믹(7lucky 37c99f7) 표기 확정(코어 제안 채택): 맵 글자 `Q`, 레벨 필드 `statueCount`(스테이지당 N 하나), `Rules.DoorOpen(s,'Q')`/`Rules.StatueCount`, 이벤트 `DoorClose` 추가(3.3 순서 규칙 수정), 6.1장. `TileKind.CountDoor`(끝에 추가), `TileId.FromChar('Q')`, `TileSet.countDoor`, 임시 아트는 문 A 프리팹. 코어 구현은 feat/core f6147f1 (`DoorClose`=17, `Level.StatueCount`, `Rules.StatueCount`/`IsDoor`). 7lucky 머지 때 3.3을 코어 문단과 합쳐 정리 |
| 2026-11-07 | 아키텍트 | 레버 기믹 표기 확정(6.2, 코어 제안 채택): 맵 `L`/`R`/`r`, `State.Flipped`, `Rules.TileAt`이 뒤집힘 반영(R↔r), `Rules.IsFlipWall`, 이벤트 `Flip`(18, 주 이벤트 바로 앞), 출발점 7번째는 Vanish만, 벽·석상 겹침 허용. `TileKind.Lever`/`FlipWall`(끝에 추가), `TileSet.lever`/`flipWalls[2]`, 임시 아트 |
| 2026-11-07 | 아키텍트 | `GameSession.Presented(State)` 이벤트 추가(4.2, 연출이 끝난 뒤 할 일용). 11.5 jslib 함수 이름을 게임플레이/UI 구현에 맞춤(`SevenSteps_SendBeacon`, `SevenSteps_RegisterVisibility`). 9장 WebGL 규칙에 데스크톱 종료 1초 대기 예외. 검토 기록 `docs/reviews/` 추가 |
| 2026-11-07 | 아키텍트 | 11.5 jslib 함수 이름을 게임플레이/UI 57907b9 구현에 다시 맞춤(`TelemetryBeacon`, `TelemetryRegisterVisibility`, telemetry.md 이름). feat/view 재검토 기록 |
| 2026-11-07 | 아키텍트 | 11.5 jslib 이름을 실제 코드(7lucky c12fd42: jslib·`DllImport`)에 맞춰 `SevenSteps_SendBeacon`/`SevenSteps_RegisterVisibility`로 확정(직전 정정 되돌림). 이름을 바꿀 때 함께 고칠 파일 명시. feat/view 2a9e801·43f3f38 사후 검토 기록 |
| 2026-11-07 | 아키텍트 | 12장 쿼터뷰 전환 계획(카메라 안 A 3/4 뷰 권장, 프리팹 규격 보강, 받는 절차, 필요할 때만 하는 작업). `TileSetValidator`(에디터 메뉴·배치 검사) 추가 |
| 2026-11-08 | 아키텍트 | 12장을 현재 코드에 맞춤: 씬 카메라 설정은 미리 보기용(12.2), 파일 위치·공유 폴더 GUID·WebGL=Mobile 품질(12.3), 검사기 트리거 오류 기준, smoke 7개·smoke-webgl 절차(12.4), "PC 품질 유지" 삭제(12.5). API 변경 없음 |
| 2026-11-08 | 아키텍트 | 6장: `expected.minBodies` 런타임 사용(클리어 화면 최소 몸 수, feat/view 5a2180a) 명시. 형식 변경 없음 |
| 2026-11-08 | 아키텍트 | 9장에 구조 원칙(OOP·MVC·단정적, 사용자 지시) 추가: scene-flow.md 1.4 문안을 조정해 반영(GameSession→IBoardPresenter 예외, 규칙상 안 되는 명령은 예외 아님, Core에 Debug.Assert 금지, WebGL 예외 설정, 참조 방향 정정). 8장 직접 Play 판단을 "이번 Play에서 부트스트랩이 돌았는가"로 수정 |
| 2026-11-08 | 아키텍트 | 8장 씬 소유자를 README(7lucky 29b1f4a)에 맞춰 씬 플로우로 변경 |
| 2026-11-09 | 아키텍트 | 12.6 몸별 외형 계약: `BodyIndex.Current`/`OfStatue`(Contracts, Undo 사슬에서 계산, Core 변경 없음), `TileSet.playerBodies[]`/`statueBodies[]`(끝에 추가)·`PlayerFor`/`StatueFor`(비면 기존 필드, 인덱스 순환). 12.7 TileSet 정식 위치 `Art/<세트>/`, 검사기 범위 `Assets/_Project`·메시/머티리얼 누락 오류·벽 높이 경고. 12.8 챕터 프리셋 설계(`ChapterPreset`, `IStageEnvironment`, `AppConfig.chapters`, `levels.json` `chapter`). 12.9 smoke-webgl 팔레트 무관 판정 제안. 게임플레이/UI가 보드에 적용 |
| 2026-11-09 | 아키텍트 | 12.6 몸 수 상한 3(사용자 결정), `TileSet.bodyColors[3]`/`BodyColor(k)`(HUD 몸 아이콘, 머플러 3색), `BodyIndexTests`. 12.7 `Art/Test/Game.unity` 별칭은 빌드 목록 밖 조건으로 허용. 12.8 챕터 프리셋 타입 추가: `ChapterPreset`(`materialOverrides`), `LightingPreset`(`LightRig`), `SkyPreset`, `IStageEnvironment.Fit(BoardGrid)`, `AppConfig.chapters`/`Chapter(id)`, 세트 결정 순서, 검사기 챕터 검사. 12.9 리드 구현. 적용은 게임플레이/UI |
| 2026-11-09 | 아키텍트 | 11.2 씬 플로우 S0·S1 반영(feat/scene-flow 5fdaf49·923a1a6, 아키텍트 검토 승인): `Services.ResetForPlay`(SubsystemRegistration), `Register` 중복 예외, `Bootstrap.Run` config null·두 번째 실행 예외·`HasRunThisPlay`. **`LaunchOptions` 공개 필드 → 읽기 전용 속성**(읽기 호환), `TakeReplay()`, `UseEditorStart(level, replay)`, `OpenStageSelect`/`OpenCredits`, `Warnings`. 필드에 값을 대입하던 코드는 컴파일 오류가 나므로 두 메서드로 바꿀 것 |
| 2026-11-09 | 아키텍트 | 6.3 진흙 `m`(2걸음, 남은 1걸음이면 막힘, 얼음 미끄러짐 멈춤, 새 이벤트 없음) 고지, `TileKind.Mud`(끝에 추가)·`TileId.FromChar('m')`·`TileSet.mud`(없으면 바닥, 검사기 경고). 6.4 `chapter` = 문자열 id + `chapters.json`(`id`, `planet`, `concept`, `intro`, `artPreset`), `story`, export 검증은 첫 묶음부터, `ChapterInfo`/`ChapterFileDto`(Contracts). 12.8 표 갱신 |
| 2026-11-09 | 코어·아키텍트 | 6.3 진흙 코어 구현(feat/core 6ae58e2): `Rules.IsMud(char)`, SWITCH 출발점 가능, 막히면 `Bump`(진흙 칸). `Mud` 이벤트는 넣지 않음. 골든 트레이스 id가 스테이지 이름 기준으로 바뀜(`server/fixtures` 재생성) |
| 2026-11-09 | 아키텍트 | 11.1·11.2 씬 플로우 S2 반영(feat/scene-flow f08f4d6, 검토 승인): **`AppFlow(ISceneLoader, int stageCount)` 생성자 변경**, `StageCount`, `GoToGame` 범위 예외, `TryGetBackTarget`/`CanBack`/`CanGoToGame`, `IsLoading` + **`ISceneLoader.Loaded` 추가**(로드 중 전이 예외), `Changed` 시점 명시, `EntryTracker`(Game, `Services` 등록), `Bootstrap.Run(config, assets, ...)`, 범위 밖 `-level`은 경고 후 마지막 스테이지. `AppFlow`를 직접 만들거나 `ISceneLoader`를 구현하던 코드(테스트 가짜 로더 포함)는 고쳐야 한다 |
| 2026-11-09 | 아키텍트 | 11.3 `SaveSystem` 설정 명령 `SetMasterVolume`/`SetSfxVolume`/`SetTelemetryChoice`/`TelemetryOn` 추가(씬 플로우 S4 `SettingsController`용), 설정은 바꿀 때마다 저장, 생성자 null 검사. EditMode `SaveSettingsCommandTests` |
| 2026-11-09 | 아키텍트 | 12.8 결정: 챕터별 `UiTheme`은 `ChapterPreset`에 넣지 않음(의존 방향·일관성·비용), 필요하면 `uiAccent` 색 하나. 씬 플로우 S6 검토 승인 |
| 2026-11-09 | 아키텍트 | 씬 플로우 S5 반영(feat/scene-flow 1f9f462, 승인): 11.1 로드 실패 시 `AppFlow` 되돌림·`SceneLoader` 사전 거부·페이드 경로, 8장 DDOL 문구를 `TelemetryRunner` + `SceneFader` 두 개로 수정 |
| 2026-11-09 | 아키텍트 | 11.3.1 진행 기록 v2 결정(구현 전): 키 = 스테이지 이름, `StageRecord`(`levelKey`로 맵 변경 감지 → 최소 몸 기록만 버림), v0·v1은 고정 이름표(17스테이지)로 이전, 원본 백업 키, 미래 버전은 덮어쓰지 않음, 이름 중복 예외. 구현은 아키텍트(Contracts + 호출부 한 커밋, 소유자 검토), R1 전 |
| 2026-11-09 | 아키텍트 | 11.3.1 진행 기록 v2 구현: `ProgressData`(v2, `stages`/`lastStage` 이름), `StageRecord`, `ProgressMigration`(v0·v1 → v2, 고정 17스테이지 이름표), `SaveSystem` 이전·백업 키 `seven-steps.progress.v1`·미래 버전 읽기 전용, `LevelCatalog.At`·이름 중복 예외. **`ProgressData` 공개 API 변경**(번호 → `Level`): 호출부 함께 수정 |
| 2026-11-09 | 아키텍트 | 11.3.1 `StageRecord.bestStars`(int, 0 = 없음) 자리 추가(사용자 지시, 3별 B안 대비, 계산 없음). 형식 버전은 2 그대로(필드 추가는 JsonUtility가 기본값 0으로 읽음) |
