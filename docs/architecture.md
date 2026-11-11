| 2026-11-10 | 게임플레이/UI·아키텍트 | 14.6 `BoardView.Fit` 맞춤 식: 대칭 반폭 대신 투영 min/max 상자, 중심 +0.3 들어올림 제거(시그니처 그대로). feat/view 1f689b0 검토 승인 |
| 2026-11-10 | 아트·아키텍트 | 7.2 `TileVisual.labelScale`(선택, 0 = 기존 기본 글자 크기, 양수 = 셀 한 변 px × 값), `BoardView.SetCellLabel` 1줄. feat/art 4760464 검토 승인 |
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
SevenSteps.Art         Art/                        + uGUI, URP          (아트)  행성·하늘 환경, 벽 변형, UI 리소스
SevenSteps.Art.Preview Art/Test                    UNITY_EDITOR || SEVENSTEPS_ART_PREVIEW → Art  (아트)  프리뷰·캡처 스크립트, 정식 빌드 제외
   ▲
SevenSteps.Editor      Scripts/Editor              Editor 전용          (여러 소유자, 파일 단위)
SevenSteps.Art.Editor  Art/Editor                  Editor 전용 → Art, Art.Preview, Editor  (아트)  세트·환경·UI 생성기

SevenSteps.Tests.EditMode  Tests/EditMode          Editor 전용, UNITY_INCLUDE_TESTS → Core, Contracts, Game
```

- `SevenSteps.Art`(2026-11-09): 아트 런타임 스크립트가 정식 Game 씬 프리팹에 들어가므로 Assembly-CSharp에서 분리. Core·Contracts·Game·uGUI·URP를 참조하고, Game·Contracts는 Art를 모른다(보드는 `IStageEnvironment` 같은 Contracts 인터페이스로만 부른다). `autoReferenced: false`. 스크립트 GUID는 그대로라 프리팹 참조는 유지된다. `Art/` 아래 새 런타임 스크립트는 자동으로 이 어셈블리, `Art/Editor/` 아래는 `SevenSteps.Art.Editor`.
- `SevenSteps.Art.Preview`(2026-11-10, 93477aa 검토 P1): `Art/Test/`의 프리뷰·캡처 스크립트(`PlanetReviewCapture`, `RuinsHengeEnvironment`, `RuinsHengeProbe`). `defineConstraints: UNITY_EDITOR || SEVENSTEPS_ART_PREVIEW` → 에디터에서는 항상 컴파일, **정식 빌드(BuildTools)에는 들어가지 않는다**(파일 IO·화면 캡처·명령줄 읽기 코드가 플레이어에서 빠짐). 아트 프리뷰 플레이어 빌드(`RuinsHengeBuilder.BuildPlayer`, `SevenPlanetBuilder.ReviewWindows`)는 `BuildPlayerOptions.extraScriptingDefines = { "SEVENSTEPS_ART_PREVIEW" }`를 넣어야 이 스크립트가 들어간다(없으면 프리뷰 씬의 스크립트가 빠진다). `Art/Test/` 아래 새 스크립트는 자동으로 이 어셈블리. 정식 게임 코드(Art·Game)는 이 어셈블리를 참조하지 않는다.
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

### 4.4 KEEP/SWITCH 선택 화면 (최소 UI v3, 2026-11-11 사용자 승인, `Contracts/ChoicePreview.cs`)
기준: `production/ui-minimal-plan.md` "승인된 결정", feat/art `design/art/ui-minimal.md` UI v3, 참고 구현 feat/art 2cd08fb `Art/Test/UiV3`(리뷰 전용, 복사하지 않음). 규칙 코드는 바꾸지 않는다.
- **판단**: 선택 UI는 화면의 State가 `Status.Choose`일 때만 보인다. 다른 것(구덩이·출구·출발점·금 간 바닥·마지막 형제의 자동 선돌)은 Status가 Choose가 아니므로 표시가 없다. 판정을 따로 만들지 않는다.
  - 보이는 시점: Present로 Choose가 되었으면 `Presented(state)`(일곱째 걸음 연출 끝) 뒤. Snap(Undo로 돌아옴)·Rebuild면 그 `Updated`에서 바로.
  - 숨는 시점: Status가 Choose가 아닌 `Updated`를 받으면 즉시(확정 명령의 Present `Updated`는 보드 연출 시작 전에 온다).
- **미리보기(고스트·카드 문구)**: `ChoicePreview.TryOf(state, out p)`. `Rules.Dispatch(choose, Keep/Switch)` 순수 호출 두 번이고 세션에 보내지 않는다. 그래서 세션 State·Undo 사슬·텔레메트리·리플레이가 그대로다. 테스트 `MinimalUiContractTests`가 "미리보기 = 세션이 실제 적용한 결과"(`Rules.Key` 비교)를 고정한다.
  - KEEP 고스트: `p.Cell`의 선돌 + `p.KeepStart`의 다음 형제 + 둘 사이 점선. SWITCH 고스트: `p.SwitchStart`(= `p.Cell`)의 다음 형제 + 매듭.
  - KEEP으로 문이 바뀌면 `p.Keep.Events`·`p.Keep.State`로 보여 줄 수 있다(선택).
  - 카드 테두리 = 형제 색[`p.NextBody`](`BodyIndex`, 15.1과 같은 출처).
  - `Updated`마다 다시 만든다(Undo·언어 변경). 미리보기 State를 `StarTrailMap`·텔레메트리·저장에 쓰지 않는다.
- **입력**: 카드 클릭·탭·Enter는 `session.Send(Command.Keep / Command.Switch)`이고 K/X와 같은 경로다. 카드는 `IsLocked`를 보지 않는다(4.1). 호버·포커스는 표시만 바꾸고 명령이 아니다. 선택 중 이동은 규칙이 `Feedback`으로 거부한다. 방향 버튼을 끄는 것은 표시일 뿐이다.
- **확정 연출**: 그 Keep/Switch 명령의 `IBoardPresenter.Present` 안에서 재생한다. 경로·이벤트·새 형제 등장까지 **`ChoicePreview.ConfirmSecondsMax` 0.5초 이하**이고, 움직임 줄이기(11.3.2)면 0초(바로 `done`)다. 잠금은 4.1 그대로다: 그 사이 입력은 큐(최대 2), Undo·Restart는 연출을 끊고 `Snap`. `done`은 정확히 한 번. 카드가 사라지는 UI 애니메이션은 잠금과 무관하고 상태 반영을 늦추지 않는다.
- **Z**: 선택 중 Z = 일곱째 걸음 전(6걸음 진행). 확정 뒤 Z = 선택 화면(`p.Keep.State.Prev`가 Choose 상태)이고 카드가 Snap으로 다시 뜬다. 기존 Undo 그대로이며 새 코드가 없다.
- **확정 횟수**: `Updated`가 Present이고 `Command.Type`이 Keep/Switch일 때 세션 구독 컨트롤러(Game, 한 곳)가 `save.CountChoiceConfirm()`을 부른다. Feedback·Snap·Rebuild·Replay는 세지 않고 Undo로 줄지 않는다. 설명 밀도는 `save.ExplainChoice`(처음 3회: 두 결과 설명, 이후: 포커스한 쪽만), 11.3.2.

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
    { "id": "moss", "planet": "이끼의 궤도", "concept": "...", "intro": "진입 문구", "tagline": "행성 선택 한 줄", "artPreset": "moss" }
  ] }
  ```
  `tagline`(2026-11-10, 리드 요청): 행성 선택 화면에 행성 이름 아래 보이는 한 줄. `ChapterInfo.tagline`/`Tagline(lang)`. **전부 아니면 없음**: 한 챕터라도 ko `tagline`이 있으면 export가 모든 챕터의 ko `tagline`과 모든 번역 `text[].tagline`을 요구한다(빈 값 실패). 하나도 없으면 경고만 하고 `Tagline(lang)`은 모든 언어에서 `""`(행성 선택은 줄을 숨긴다). 채우기는 레벨 디자인, 표시는 씬 플로우.
  `id`: 소문자·하이픈, 바꾸지 않는다(텔레메트리·저장이 기댈 수 있음). `artPreset`: `ChapterPreset.id`(12.8). 비면 `id`와 같다. 여러 챕터가 한 프리셋을 같이 쓸 수 있다.
- 런타임 형식: `Contracts/Chapters/ChapterInfo.cs`의 `ChapterFileDto`/`ChapterInfo`(아키텍트). 읽기는 `GameAssets.chaptersJson`(TextAsset) → `LevelCatalog`: `ChapterIdOf(stage)`(구현됨, 7lucky 9f3d109, 텔레메트리 `level_start` 챕터용) → `ChapterFileDto.Get(id)` → `ChapterInfo`(chapters.json이 생기면), 아트는 `AppConfig.Chapter(info.ArtPreset)`. `LevelDto.chapter`/`story`는 코어 b5c4464에서 추가됨.
- export 단정 검증(`unity/Tools/export-levels.mjs`, 실패하면 파일을 쓰지 않고 exit 1, 레벨 디자인 제안 그대로): 모든 스테이지에 `chapter`가 있고 `chapters.json`에 있다, 스테이지 순서대로 챕터가 `chapters.json` 순서를 거스르지 않고 이어진다, 모든 `bodies <= 3`, 모두 솔버로 풀리고 최소 몸 수 <= 3, 이름 중복 없음. **지금 17스테이지(챕터 없음, 14~17 몸 4~5)는 플레이테스트 1차까지 그대로**이므로 검증은 형식 도입 커밋(첫 묶음)부터 켠다. 그 전까지 런타임은 `chapter`가 없으면 첫 챕터, 몸 4~5는 외형 순환(A, B, C, A)으로 임시 허용(12.6).
- Unity 검사(켜짐, 2026-11-09): `TileSetValidator`가 `GameAssets.chaptersJson`의 `artPreset`이 모두 `AppConfig.chapters`에 있는지 본다(없으면 오류). `AppConfig.chapters`가 비어 있으면 경고만(모든 스테이지가 `AppConfig.tileSet`/`GameAssets.tileSet`).
- 담당: 레벨 디자인(`chapters.json`, `chapter`/`story` 채우기, export 검증), 코어(`LevelDto` 필드), 아키텍트(`ChapterInfo`, 이 절, 검사기), 게임플레이/UI(`LevelCatalog`·진입 문구 표시), 씬 플로우(챕터가 바뀔 때의 흐름·`ResultModel.nextChapterChanges`), 백엔드/분석(이벤트에 챕터 id를 실을지).

### 6.5 3별 (2026-11-09 사용자 결정 B안, 50스테이지 반영 다음 순서)

- 규칙: ★1 = 클리어. ★2 = ★1 + 이번 시도에서 풀이 보기(H)를 쓰지 않음 + 쓴 몸 수 ≤ 최소 몸 수(`levels.json` `expected.minBodies`, JS 솔버). ★3 = ★2 + 이번 시도에서 되돌리기·재시작 없음(한 번에). 잠금 없음(별은 진행을 막지 않는다).
- **시도(attempt)** = 스테이지에 들어온 때(`LoadStage`: 선택·다음·다시 하기·`[` `]`)부터 클리어까지. 결과 화면의 "다시 하기"는 새 시도다. 시도 안의 재시작(R)은 시도를 끝내지 않는다(★3만 잃음).
- 셈: `undos` = 실제로 상태를 되돌린 Undo(`Undo` 이벤트가 난 것). `restarts` = 시작 상태가 아닐 때의 Restart(움직인 뒤 처음부터). `hintUsed` = 이번 시도에서 H를 한 번이라도. 실패(`Lose`) 뒤의 Undo도 센다.
- **판정 함수(코어 구현 feat/core b41948d, Core 어셈블리, 엔진 없음, `core.js` `stars()` 이식)**: `SevenSteps.Core.Stars`
  ```csharp
  public static class Stars {
      public const int Max = 3;
      public static int Of(int bodiesUsed, int minBodies, int hints, int undos, int restarts); // 0 = 판정 불가, 1..3
      public static int BodiesUsed(State s);   // Level.Bodies - Bodies + 1 (클리어 상태면 쓴 몸 수)
  }
  ```
  ★1 클리어, ★2 = `hints == 0 && bodiesUsed <= minBodies`, ★3 = ★2 `&& undos == 0 && restarts == 0`. `minBodies <= 0`(데이터에 최소 몸 수가 없음)이면 **0 = 판정 불가**(core.js는 null): 이때는 별 없이 `MarkCleared(level, bodies)`. levels.json export가 모든 스테이지에 `minBodies`를 채우므로 정상 빌드에서는 나오지 않는다. 클리어 안 한 시도에는 부르지 않는다. `GameFlowController.BodiesUsed`·`TelemetryTracker`의 같은 식은 `Stars.BodiesUsed`로 바꿀 수 있다.
- **셈(게임플레이/UI)**: `BoardController`가 시도마다 센다(`LoadStage`에서 0으로, `ShowHint`·`Undo`/`Restart` 결과에서 증가). `IStageBoard`에 시도 셈(`Hints`, `Undos`, `Restarts`)을 내는 읽기 전용 속성을 추가(씬 플로우 인터페이스, 씬 플로우가 모양 결정).
- **저장(씬 플로우)**: `GameFlowController.OnWon`에서 `stars = Stars.Of(...)`, 0이 아니면 → `save.Progress.MarkCleared(level, bodiesUsed, stars)`(Contracts, 구현됨: 최소 몸·최다 별 유지, 맵이 바뀌면 별도 0부터). 표시용 `save.Progress.BestStars(level)`(0 = 없음). `ProgressData.MaxStars == Stars.Max`(= 3).
- **표시(게임플레이/UI·아트)**: 결과 화면 이번 별 + 최고 별, 스테이지 선택의 별. 행성(챕터)마다 7스테이지 = 북두칠성 7별, 별 수 = 밝기, 50번(원형 제단) = 북극성. 아트 `RuinsHengeUiResources.starEmpty/starFilled`.
- 텔레메트리 `level_clear`에 `stars`를 실을지는 백엔드/분석이 `docs/telemetry.md`에 정한다(규칙 변경이 아니라 필드 추가).

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
| `labelScale` | 선택적 글자 크기 = 투영된 셀 한 변 px × 값, 0이면 기존 기본값. 0 또는 0.2~0.8(`TileVisual.IsValidLabelScale`, 밖이면 `tileset-validate` 오류) | 큰 숫자 문의 판독성 조정; 게임 규칙에는 영향 없음 |

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
           Title ──(시작 = 1스테이지 / 이어하기 = 마지막 스테이지, S7a)──▶ Game
```

- 상태: `AppState { Boot, Title, StageSelect, Game, Result }` (끝에만 추가). 허용 전이는 `AppFlow.CanGo`가 정한다. 허용되지 않은 전이는 예외. **Title → Game**(2026-11-10, feat/scene-flow 19554a3): 타이틀의 시작·이어하기. 이 전이가 없어서 버튼이 `CanGoToGame` false로 조용히 무시되던 버그(플레이테스트 리허설에서 발견). 뒤로 대상은 그대로(Game → StageSelect).
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
- 진입 경로(`level_start.via`): `EntryTracker`(Game `Flow/`, 부트스트랩이 `Services`에 등록). `TakeVia() = announced ?? (첫 진입 ? firstVia : select)` — 알린 via가 첫 진입보다 앞선다(2026-11-10 리드 결정, docs/telemetry.md 3장 via 정의, feat/scene-flow c474b03): 지도·목록 선택과 타이틀 "시작"은 `Announce(select)`(앱 첫 진입이어도 `select`), 타이틀 "이어하기"는 알리지 않음(첫 진입이면 `boot`), `-level`/`?level=`은 `url`, 결과 화면은 `next`/`again`. `TitleFlowController`가 `EntryTracker`를 생성자 필수 인자로 받는다. 정적 `GameController.FirstEntryPending`을 대체했다.
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

**`LaunchOptions` (2026-11-09 캡슐화)**: 읽기 전용 속성 `Level`(1부터, 0 = 없음), `Replay`, `Screenshot`, `TelemetryOff`, `TelemetryUrl`, `DirectToGame`, `OpenStageSelect`(`-stageselect`), `OpenCredits`(`-credits`), `Language`(`-lang`, 13.3), `SmokeView`(`-smoke board|empty` / `?smoke=`, 2026-11-10 feat/view 26d60dc: `Board` = 하늘·행성 없이 카메라 #FF00FF로 지움(board-pixels.ps1 측정용), `Empty` = 보드도 숨긴 음성 대조. 상태·리플레이·텔레메트리는 그대로, 잘못된 값은 `Warnings`), `Warnings`.
- **`NewPlayer`**(2026-11-10, feat/view 01795d3, 리드 결정 (가), 플레이테스트 진행자용): `-newplayer`(값 없는 플래그) / `?newplayer=1|true`(`0|false`는 끔, 그 밖은 `Warnings`). 부트스트랩이 텔레메트리를 만들기 **전에** `NewPlayerLink.Apply` → `SaveSystem.StartNewPlayer()`: **저장된 진행(클리어·최소 몸·별·시도 수)과 `playerId`를 실제로 지우고 바로 저장**, 볼륨·동의·언어(`languageChosen` 포함)는 유지, 새 빌드의 읽기 전용 진행도 덮어쓴다(의도). 텔레메트리는 새 id로 `session_start` firstTime. WebGL은 jslib `SevenSteps_RemoveUrlParam`(history.replaceState)으로 주소창에서 인자를 지워 새로 고침이 또 새 플레이어를 만들지 않게 한다. 큐에 남은 이전 테스터 이벤트는 그 테스터 id로 나간다(`HttpTelemetry` 큐 항목이 playerId를 가짐, 줄 형식 `player	session	json`, 예전 줄은 현재 플레이어로 읽음). 주의: itch.io 임베드는 페이지 URL 인자를 게임 iframe에 넘기지 않으므로 `?newplayer`·`?level`·`?lang`은 직접 호스팅(`unity.sh serve`·플레이테스트 서버) 주소에서만 동작한다.
- **`LoadDelay`**(2026-11-10, 씬 플로우 S7c, 개발용): `-loadDelay 1.5` / `?loadDelay=1.5`(초, 0..10, 범위 밖·숫자 아님은 `Warnings`, 0 = 끔). `SceneLoader`가 매 씬 로드를 그 시간만큼 `allowSceneActivation = false`로 붙잡아 로딩 화면을 확인할 수 있게 한다. 공개 빌드에서도 읽히지만 최대 10초 지연뿐이라 막지 않는다.
- **`FlowCheck`**(2026-11-10, 씬 플로우 S8, 개발·스모크용): `-flowcheck` / `?flowcheck=1|true`, `-level N -replay R`와 함께. `Game/App/FlowCheck`가 리플레이 마지막 수를 실제로 보내 이기고 Result 문구(`chapter_done`/`journey_done`)·Next·챕터 진입 카드·`ChapterStage.Current`를 검사, `[SevenSteps] Flow check stage N: OK|FAIL` 로그 후 종료. `unity.sh smoke`의 흐름 단계(`Tools/flow-stages.mjs`: 행성마다 마지막 스테이지 + 중간 대조 2개)가 쓴다. 일반 실행 영향 없음.
- **`OpenSettings`**(2026-11-10, feat/view 6a6056d, 리드 결정, 개발·스크린샷용): `-settings` / `?settings=1|true`(`0|false` 끔, 그 밖 `Warnings`). 첫 화면(타이틀 또는 `-level` 게임)에서 설정 패널을 연 채로 시작. 저장 변경 없음.
- **`TitleCheck`**(2026-11-10, 씬 플로우 S8, 개발·스모크용): `-titlecheck begin|continue|map` / `?titlecheck=`(그 밖 `Warnings`, "" = 꺼짐, `-level` 없이). 타이틀이 그 경로의 실제 버튼을 누르고(`onClick.Invoke`), 게임 씬이 `Title check X: arrived stage N via V`를 로그하고 종료(10초 안에 타이틀을 못 떠나면 FAIL). `unity.sh smoke`의 타이틀 단계가 쓴다. 일반 실행 영향 없음.
- **`ArtPerf`**(2026-11-10, 아트 측정용): `-artperf` / `?artperf=1`(`0`/`false` 끔, 그 밖 `Warnings`). 행성 환경(`RuinsHengePlanet`)이 `Services`의 `LaunchOptions.ArtPerf`를 보고 `RuinsFrameSample`을 붙여 스테이지가 열리고 10초 뒤 10초 동안의 `[ArtPerf]` 한 줄(fps·중앙값·p95)을 로그한다. 플레이는 그대로. 아트가 `Application.absoluteURL`을 직접 읽던 것을 대체(feat/art 6448f82 검토 A2).
- **`SaveKey`**(2026-11-10, 개발·스모크용): `-savekey <접미사>` / `?savekey=`(1~32자 `A-Z a-z 0-9 _ -`, 그 밖 `Warnings`, "" = 보통 키). 이번 실행의 모든 저장 키 뒤에 `#접미사`. Windows PlayerPrefs는 제품당 레지스트리 키 하나라 여러 작업 공간의 빌드가 동시에 smoke를 돌리면 진행·설정을 서로 덮어쓰던 문제(간헐 실패) 해결. 저장소는 **`SaveStores.For(launch)`** 하나로 만든다(`PlayerPrefsStore` 또는 `SuffixedStore`) — `Bootstrap`의 `SaveSystem`과 `TelemetryRunner`의 텔레메트리 큐가 같은 접미사를 써야 한다. 같은 접미사끼리는 실행 사이에 저장이 이어진다(smoke 타이틀 단계의 continue가 앞 단계 진행을 읽음). `unity.sh smoke`가 작업 공간 이름을 넘긴다(코어).
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
- `StageRecord { name, levelKey, cleared, bestBodies(0 = 없음), starts, bestStars(0 = 없음, 6.5: `MarkCleared(level, bodies, stars)`가 최다 별 유지, 맵이 바뀌면 0, 조회 `BestStars(level)`) }`, `ProgressData { version, stages, lastStage(이름, "" = 없음) }`.
- 질의·명령(Contracts, `Level`을 받음): `IsCleared(level)`, `BestBodies(level)`(기록 없음·`levelKey` 다름·몸 수 초과면 0), `MarkCleared(level, bodiesUsed)`(기록 갱신, `levelKey` 갱신), `CountStart(level)`(텔레메트리 `attempt`), `ClearedCount`, `LastStage`(이름)와 `LastStageIndex(levels)`(없거나 지금 목록에 없으면 0).
- 지금 목록에 없는 이름의 기록은 지우지 않는다(나중에 다시 들어올 수 있음, 크기 작음).

이전(읽을 때 한 번, 바로 v2로 저장):
- **v1**(`version` 1, 번호 목록)과 **v0**(`seven-steps-cleared` 쉼표 목록): 번호 k → `ProgressMigration.V1StageNames[k]`(**2026-11-09 7lucky의 17스테이지 이름을 고정한 표**, 코드에 상수). 표 밖 번호는 버린다. `levelKey`는 비워 둔다("모름" = 최소 몸 기록은 몸 수 범위 안일 때만 표시).
- 이전 전 원본은 `seven-steps.progress.v1` 키에 복사해 둔다(되돌릴 수 있게).
- `version`이 2보다 크면(더 새 빌드의 저장) 덮어쓰지 않고 기본값으로 시작 + 경고(외부 입력 규칙).
- 같은 이름이 `levels.json`에 둘 이상이면 `LevelCatalog.Load`가 예외(이름 = 저장 키).
- 호출부: 마지막 스테이지는 `GameFlowController`가 보드 로드 뒤 `SetLastStage(level)`, 클리어는 `MarkCleared(s.Level, ...)`, 결과 화면 최고 기록은 `BestBodies(level)`. `TitleFlowController(flow, save, panels, levels)`의 `PlayLastStage`는 `LastStageIndex`. `HudView(coroutines, tileSet, levels)`(stage 수 대신 목록). 텔레메트리 `attempt`는 `CountStart(LevelCatalog.At(level - 1))`, `session_start.cleared`는 `ClearedCount`. 테스트 `ProgressV2Tests`(이름 따라감, 맵 변경, v1 이전·백업, 미래 버전).

**구현 담당 제안**: 아키텍트가 Contracts(`ProgressData` v2, `StageRecord`, `ProgressMigration`, EditMode 테스트: v0·v1 이전, `levelKey` 불일치, 미래 버전)와 **호출부 기계적 수정을 한 커밋에** 넣는다(공용 API가 바뀌어 나눠 넣으면 7lucky 컴파일이 깨짐). 호출부: `GameFlowController`·`TitleFlowController`(씬 플로우), `TitleViews`·`HudView`·`TelemetryRunner`(게임플레이/UI), `LevelCatalog` 이름 중복 검사(씬 플로우). 각 소유자가 자기 파일을 검토한다. **R1(첫 50스테이지 묶음) 합류 전에** 들어가야 한다.

#### 11.3.2 1회 안내와 접근성 설정 (최소 UI v3, 2026-11-11, scene-flow 제안 채택, `Contracts/Save/Tutorial.cs`)
- **키 `seven-steps.tutorial`** → `TutorialData { version 1, seen: ["move@1", ...], choiceConfirms 0..3 }`. 설정 키에 두지 않는 이유: `StartNewPlayer`(`-newplayer`)가 진행 기록과 함께 지워야 다음 테스터가 안내를 다시 본다. 설정은 남는 값이다.
- **안내 ID** = `이름@버전`(이름 a-z 0-9 -, 버전 1~99, `TutorialIds.IsValid`). 안내 내용을 바꾸면 버전을 올린다. 그러면 새 ID라 한 번 더 보인다. 정해진 것: `TutorialIds.Move = "move@1"`(첫 이동), `TutorialIds.Choose = "choose@1"`(첫 선택 화면의 "걷기 잠김, 하나를 고른다"). 새 ID는 씬 플로우가 `TutorialIds`에 상수로 더한다(아키텍트 리뷰).
- **명령**(모두 바로 저장): `TutorialSeen(id)`, `MarkTutorialSeen(id)`(잘못된 ID는 `ArgumentException`), `ChoiceConfirms`, `ExplainChoice`(= 확정 3회 미만), `CountChoiceConfirm()`(3에서 멈춤).
- **seen 시점**: 안내가 가리킨 조작이 처음 **적용된** 때(`Updated` Present). 막힌 입력(Feedback)은 소비하지 않는다. Undo·Restart·언어 변경은 seen을 바꾸지 않는다. 메뉴의 조작법 다시 보기도 seen을 바꾸지 않는다.
- **리플레이·검사 실행**: `LaunchOptions.Replay`가 있거나 `SmokeView`가 켜진 실행은 안내를 띄우지 않고 세지도 않는다(스크린샷·보드 픽셀 측정 보호). Replay는 Snap이라 확정 횟수에도 저절로 안 들어간다. `Rules.Key`·QA 시도 줄·텔레메트리에는 들어가지 않는다.
- **이전·손상**: 값이 없거나 깨졌으면 기본값이다. `version`이 1보다 크면(새 빌드의 저장) 읽지 않고 덮어쓰지 않는다(progress와 같다). `choiceConfirms`는 0~3으로 자른다.
- **접근성**: `SettingsData.showStepNumbers`·`reduceMotion`(기본 false, 필드 추가만이라 `version` 1 그대로, 옛 저장은 false로 읽힘). 명령 `SetShowStepNumbers(on)`·`SetReduceMotion(on)`은 저장하고 `SettingsChanged`를 낸다. 뷰는 그 이벤트로 다시 그린다.
  - 설정이라 `StartNewPlayer`가 남긴다(음량·언어와 같음). 플레이테스트 진행자는 테스터를 바꿀 때 둘 다 꺼져 있는지 본다(playtest-guide에 한 줄).
  - 걸음 숫자 표시: 발치 별 옆 한 곳에 걸은 수/7, 숫자 발자국(gameplay-ui). `StarTrailMap` 데이터는 그대로다.
  - 움직임 줄이기: 맥동 대신 이중 테두리(`_StarTrailPulse` x·z는 0으로 고정, 뷰가 표시를 바꿈), 각성·피날레 광선은 끝 그림으로 바로, 확정 연출 0초(4.4). 규칙·입력 타이밍은 바뀌지 않는다.

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
- **아트**: `playerBodies` = PlayerA/B/C(RuinsHenge_Chapter01에 연결됨: `SS_Actor_Triplet_A/B/C`, 같은 메시·머플러 3색, `labelOffset.y` 1.04).
- **KEEP 선돌 외형 `TileSet.keepLook`(2026-11-10, 아트 바이블 "KEEP 선돌")**: `KeepLook.Neutral`(기본값 0) = 모든 몸의 석상이 `statue`(색 없는 중성 화강암 #B9BFC0 선돌, 같은 몸 실루엣). `KeepLook.SiblingTrace` = `statueBodies[k]`(머플러 윗면에 형제 색 흔적, `Menhir_A/B/C`). 흔적 채택은 가독성 검토 후 아트·리드가 세트 에셋 값만 바꿔 정한다(코드 변경 없음). Neutral이면 `statueBodies`는 무시된다(지워도 되고 남겨도 된다). 어느 쪽이든 형제 식별은 HUD 몸 아이콘(`BodyColor`)이 맡고 석상 색에 기대지 않는다.
- **검사 추가**: `playerBodies`에 같은 프리팹이 겹치면 경고(세쌍둥이는 서로 달라야 함). `keepLook = SiblingTrace`인데 `statueBodies`가 비면 **오류**, 같은 프리팹이 겹치면 경고. EditMode `BodyIndexTests.KeepLookNeutralIgnoresStatueBodies`.
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

**보드 세트 결정 순서**: `AppConfig.tileSet`(설정되어 있으면 디버그·전환용 덮어쓰기) → 스테이지 챕터의 `tileSet` → `GameAssets.tileSet`. **적용됨(2026-11-10, feat/view 5e0a1cb)**: `AppConfig.chapters` = `Art/RuinsHenge/Chapter_<id>` 7개(chapters.json 순서 moss…altar), `AppConfig.tileSet`·`GameAssets.tileSet`은 비어 있다. 보드의 첫 세트는 `BoardController.TileSetFor(override, 첫 스테이지 챕터, GameAssets.tileSet)`, 스테이지마다 챕터 세트로 바뀐다. `SceneFlowValidator`·`GameplayScenes`는 "tileSet 비고 chapters 있음"을 정상으로 본다(둘 다 비면 오류). 옛 `Chapter01`(id `moss-ruins`)은 프리뷰 전용, `AppConfig.chapters`에 넣지 않는다.

**적용 (게임플레이/UI, Game 씬 연결은 씬 플로우)**:
- 스테이지 진입 때 챕터가 바뀌었으면: 하늘·행성 인스턴스 교체(이전 것 파괴), `LightingPreset` 값을 씬의 방향광 3개(키·필·림)에 복사, `RenderSettings.ambientLight`, 카메라 배경색, `BoardView.Init(새 세트)` 후 다시 그리기. 같은 챕터면 `IStageEnvironment.Fit`만.
- 머티리얼 교체는 보드가 프리팹을 만들 때 한 번(`renderer.sharedMaterials`를 `chapter.Override`로 바꾼 배열). 매 프레임·`material`(인스턴스 복제) 금지.
- Game 씬의 조명은 방향광 3개만 두고 값은 프리셋이 정한다. 챕터가 없을 때(임시 세트)는 씬의 현재 값 그대로.
- S5 페이드가 있으면 챕터 교체를 그 뒤에 숨긴다.

**아트 프리뷰 이전**: `RuinsHengeEnvironment`(지금 `FindFirstObjectByType<GameController>` + `LateUpdate` 폴링 + 명령줄 직접 파싱 + 하늘을 카메라에 맞춰 매 프레임 크기 조정)를 `SkyPreset.skyPrefab`과 `planetPrefab`(`IStageEnvironment.Fit`)으로 나눈다. `artfx=off`/`-artfxoff`는 `LaunchOptions`가 흡수(씬 플로우 S1). 프리뷰 조명 값(키 `#FFE2B9` 2.0, 필 `#A8CFFA` 0.55, 림 `#E4CEFF` 0.28)은 `LightingPreset` 에셋으로.

**머티리얼 교체(feat/view cfde7f2)**: `ChapterMaterials.Apply(root, chapter)`가 `materialOverrides`를 보드 조각·하늘·행성 인스턴스에 한 번씩 적용하고, 행성은 `Fit` 뒤 다시 적용한다. 그래서 **환경 프리팹(`planetPrefab`)은 렌더러를 `Fit` 안에서만 만든다**(`Start`/`Awake`/`Update`에서 만들면 교체를 비켜 간다). 하늘(`RuinsHengeSky`)은 메시를 `Start`에서 카메라 밑에 만들어 교체가 닿지 않는다 — 하늘 색은 하늘 프리팹 필드로 바꾼다.

**검사**: `TileSetValidator`가 `ChapterPreset`도 본다: id 비었거나 중복이면 오류, `tileSet` 없으면 오류, 빈 머티리얼 교체 오류, `lighting`·`sky` 없음 경고, `planetPrefab` 루트에 `IStageEnvironment` 없으면 경고. `levels.json` `chapter` id가 `AppConfig.chapters`에 다 있는지는 필드가 생기면 추가.

**WebGL 예산**: 챕터 에셋은 `AppConfig.chapters` 참조로 모두 빌드에 들어간다(Addressables 안 씀). 행성을 늘리면 빌드 크기를 잰다. 1280×720 draw call 400 안팎(유적 레벨 8: 403). 바닥·벽은 SRP Batcher, 하늘·장식은 결합 메시. 후처리는 Bloom·톤매핑까지(Mobile 렌더러).
- 행성 환경 결합 메시(`BoardShapeAAssembly`)의 삼각형 상한은 런타임 가드다. 50k로 두고, 환경 드로우 1개를 유지한다(2026-11-11 e05c3c1, 실제 최대는 joinery 12×5 31,830). 예산 판단은 표준 Chrome 60 FPS이고, 가장 큰 판(14 hollow 17×3, 31 joinery 10×7, 49 altar 9×9)을 측정에 포함한다. 정적 그림자 한도는 48이다(`BoardShapeAShadowField`).
- 리뷰·프리뷰 코드 asmdef는 `defineConstraints: UNITY_EDITOR || SEVENSTEPS_ART_PREVIEW`로 게임 빌드에서 뺀다. 리뷰용 데이터는 `Resources`에 두지 않는다.

### 12.9 smoke-webgl 판정 (팔레트 무관, 리드가 구현)

리드가 `unity.sh`·`board-pixels.ps1`을 고친다. 아래는 아키텍트 의견이다.

지금 `board-pixels.ps1`은 옛 보라색 바닥만 센다. 유적 세트에서는 보드가 보여도 실패한다(아트 측정: 0~90픽셀).

- **안 A (권장): 게임 안에서 보드가 그려졌는지 잰다.** 스모크일 때만(`replay`가 있거나 `smoke=1`) 리플레이가 끝나면 두 프레임을 캡처한다: 보드 루트를 끈 프레임, 켠 프레임(`WaitForEndOfFrame` + `ScreenCapture.CaptureScreenshotAsTexture`). 두 장에서 달라진 픽셀 수를 콘솔에 남긴다: `[SevenSteps] BoardProbe changed=<n> total=<w*h>`. `smoke-webgl`은 이 줄을 읽어 `changed/total >= 1%`면 통과. 팔레트·조명·환경과 무관하고, 2026-11-07 문제(셰이더 스트리핑으로 보드만 안 그려짐)를 그대로 잡는다. 구현: 게임플레이/UI(프로브), 리드(`unity.sh` 판정). `board-pixels.ps1`은 프로브가 들어오면 지운다.
- 안 B: 스크린샷만으로 판정. 게임이 보드 화면 영역을 콘솔에 남기고, 검사기는 그 영역의 색 다양성(서로 다른 색 수·표준편차)을 본다. 게임 코드가 덜 들지만, 행성·하늘 배경이 영역 안에 들어오면 보드가 없어도 통과할 수 있다.
- 전환 기간: 유적 세트로 바꾸는 합류 한 번은 아트의 `design/art/check-ruins-pixels.ps1` 결과 + 스크린샷 3장 + 콘솔 예외 0으로 대신 확인한다(리드 판단).

## 13. 다국어 (ko/en) 설계 (2026-11-09, 사용자 지시, 구현은 50스테이지·3별 다음)

### 13.1 결정: 자체 경량 문자열 테이블 (Unity Localization 패키지 안 씀)

| | Unity Localization (`com.unity.localization`) | 자체 경량 테이블 (채택) |
|---|---|---|
| WebGL 용량 | 패키지 + 필수 의존 Addressables 런타임 코드가 들어간다(수 MB 단위 증가 예상, 지금 빌드 ~14MB). 문자열 테이블이 에셋 번들·카탈로그로 빌드된다 | JSON 텍스트 두 개(수십 KB) + 클래스 몇 개 |
| 초기화 | 로케일·테이블 비동기 로드(`LocalizationSettings.InitializationOperation`), WebGL에서 첫 화면 전에 기다려야 함 | `TextAsset` 동기 파싱(levels.json과 같은 방식) |
| 병합·리뷰 | 테이블이 ScriptableObject YAML(키 id·공유 데이터 분리) → git 충돌·리뷰 어려움, 에이전트가 텍스트로 고치기 어려움 | 키 = 값 JSON, 줄 단위 diff |
| 우리 데이터 | `levels.json`(이름·힌트·스토리)·`chapters.json`은 어차피 JSON이라 패키지 테이블과 이중 관리 | 같은 JSON 방식으로 통일 |
| 기능 | Smart String, 복수형, 에디터 도구, 이미지·오디오 현지화 | `{0}` 치환만(두 언어·잼 범위에 충분) |
| 일정 | 설정·Addressables 빌드 단계 추가, 팀 경험 없음 | 하루 안 |

언어가 늘거나 음성·이미지 현지화가 필요해지면 다시 검토한다.

### 13.2 문자열 테이블
- 파일: `Assets/_Project/Data/Strings/ko.json`, `en.json`(텍스트, 커밋). 형식 `{ "entries": [ { "key": "hud.undo", "text": "되돌리기 Z" }, ... ] }`(JsonUtility가 딕셔너리를 못 읽으므로 배열). 키: 소문자·점 구분(`화면.요소`), 바꾸지 않는다.
- 기준 언어 = ko. 모든 키가 ko에 있어야 하고, en에 없는 키는 **빌드 전 검증 오류**(13.6).
- 런타임(Contracts `Localization/`, 아키텍트, **구현됨 2026-11-10**): `Language` enum(`Ko`, `En`, 끝에 추가)과 `Languages`(`Base` = Ko, `All`, `Code`, `TryParse`, `FromSystem`). `StringTable`(`FromJson(lang, json)`, `Get(key)` 없으면 `KeyNotFoundException`, `Has`, `Format(key, args)`, 중복 키·빈 키는 예외). `Localizer`(`FromAssets(GameAssets.strings, current)`, ko 표 필수, `Current`, `Set(lang)`(없는 언어 예외), `Changed`, `Get`/`Format`). 부트스트랩이 만들어 `Services`에 등록(씬 플로우).
- **테이블 슬롯**: `GameAssets.strings` = `StringTableAsset[]`(`language`, `json`) — 언어가 늘어도 필드를 바꾸지 않는다. 지금 `Data/Strings/ko.json`·`en.json`(빈 틀 `{"entries":[]}`) 연결됨. `LocalizationCheck`는 이 배열 기준(ko 필수, 언어 중복 금지, 키 대칭). 코드에서 UI 문구 리터럴을 쓰지 않고 `loc.Get("hud.undo")`. 문구 표(`MessagePresenter` 등)는 키 표가 된다.
- 동적 문구(`몸 {0}개`)는 `{0}` 치환. 조사(을/를 등)는 문장을 키마다 통째로 둬서 피한다.

### 13.3 언어 설정
- `SettingsData.language` 값은 `"ko"`/`"en"`. **첫 실행**(설정 저장 없음): `SaveSystem`이 `Application.systemLanguage`로 정한다(Korean → ko, 그 밖 en). 읽기는 `SaveSystem.Language`(모르는 값이면 ko), 바꾸기는 `SaveSystem.SetLanguage(lang)`(바로 저장 + `SettingsChanged`). 구현됨.
- **언어를 고른 적 있음**(2026-11-10, 씬 플로우 요청, 첫 실행 언어 선택 화면): `SettingsData.languageChosen`(bool, 기본 false), 읽기 `SaveSystem.LanguageChosen`. `SetLanguage`만 true로 만든다(설정 토글·선택 화면 모두 `SetLanguage`). 시스템 언어 기본값·다른 설정 저장은 바꾸지 않는다. 이 필드 이전의 저장은 false로 읽혀 기존 플레이어에게도 선택 화면이 한 번 나온다(의도: 아직 고른 적이 없음). Title은 `!LanguageChosen && launch.Language == null`일 때 선택 화면을 먼저 띄운다(`-lang`/`?lang=` 실행은 띄우지 않고 저장도 하지 않는다). 선택 화면의 "계속"은 바꾸지 않았어도 지금 보이는 언어로 `SetLanguage`를 부른다(그래야 다음 실행에 다시 안 나온다), 그다음 `Localizer.Set`.
- 실행 인자 `-lang en`/URL `?lang=en`(플레이테스트·스모크용, `LaunchOptions.Language`(`Language?`, null = 저장된 설정), 저장하지 않음, 잘못된 값은 `Warnings`). 구현됨. 이번 실행의 언어 = `launch.Language ?? save.Language`.
- 바꾸면 즉시 반영: `Localizer.Changed` → 뷰가 다시 그린다(코드 생성 UI는 `Refresh`, 프리팹 뷰는 텍스트 갱신 메서드). 씬 다시 읽기 없음.

### 13.4 글꼴 (UiTheme 언어·역할별 세트, 2026-11-09 아트 제안 반영)

아트 확정(`design/art/fonts.md`): en 제목 **Kaushan Script**, en 본문 **Patrick Hand**, ko 제목·본문 **Gaegu**, 숫자(공통) **Jua**. 모두 OFL, 공유 폴더 `Shared/Fonts`. WebGL gzip +843 KiB(손글씨 세트 비교 빌드).

`UiTheme`(Game 어셈블리, 씬 플로우 구현) 필드 — 아트 제안 `LocalizedFontSet`을 아래로 정리:
```csharp
public enum FontRole { Title, Body, Number }
[Serializable] public struct LanguageFonts {
    public Language language;        // Contracts Language (Ko, En) — 문자열 locale 대신 13.2의 enum
    public Font title, body;
    public float titleScale, bodyScale;   // 0 = 1. 필기체의 작은 x-height 보정
    public float bodyLineSpacing;         // 0 = 1. en 0.90, ko 1.10 (아트 프리뷰 값)
}
public LanguageFonts[] languages;         // 언어당 하나
public Font number;                       // 숫자 역할: 언어와 무관하게 하나(Jua). 보드 라벨·걸음·몸·석상 수·별 수
public Font FontFor(Language lang, FontRole role);  // 없는 언어면 예외(검증기가 빌드 전에 잡음)
```
- 아트 제안과 다른 점: (1) `numberFont`는 언어별이 아니라 테마에 하나(두 언어 모두 Jua, 보드 숫자는 언어와 무관해야 함). (2) **`symbolFallbackFont`는 두지 않는다** — 레거시 uGUI `Text`는 다른 글꼴 에셋으로 글자별 대체를 하지 못하고 WebGL에는 OS 글꼴도 없다. 기호는 해당 역할 글꼴에 있어야 하고 GlyphCheck가 검사한다(없으면 문구를 바꾸거나 숫자 역할 Text로 분리). (3) locale 문자열 대신 `Language` enum.
- `Text` 하나에 글꼴을 섞지 않는다. 숫자는 별도 `Text`(역할 Number).
- 언어 선택 버튼(첫 실행·설정)의 언어 이름은 **현재 UI 언어와 무관하게 그 언어의 본문 글꼴**로 그린다("English"는 Patrick Hand, "한국어"는 Gaegu).
- 기존 `UiTheme.font`는 이전 기간 동안 기본값(`Ui.Font`)으로 남기고, 화면마다 역할 글꼴로 옮긴 뒤 지운다.
- GlyphCheck(13.6, 씬 플로우): 각 언어 문자열(테이블·레벨·챕터 텍스트)을 그 언어의 **제목·본문 글꼴 둘 다**의 cmap으로(어느 역할로 보일지 모르므로), 숫자 역할 문자(`0-9`, 쓰는 기호 `/ · × ★` 등 목록)를 `number` 글꼴로.
- 줄 높이·레이아웃: 문자 수로 높이를 계산하지 않고 `preferredHeight`, 제목·행성 이름은 2줄까지(아트 `ui-theme.md` 레이아웃 기준).

### 13.5 레벨·챕터 텍스트
- `levels.js`/`levels.json`: 기존 `name`/`hint`/`story`는 **ko 그대로**(그리고 `name`은 저장 키, 11.3.1 — 번역해도 키는 ko 이름). 번역은 `text` 배열: `"text": [ { "lang": "en", "name": "...", "hint": "...", "story": "..." } ]`. `LevelDto.text`(`LevelTextDto[]`)·`LevelDto.TextFor(code)`(코어 구현 feat/core 6398ca5: ko는 기본 필드, 번역 없으면 `KeyNotFoundException`. Core는 Contracts를 모르므로 언어는 코드 문자열 — 호출부가 `Languages.Code(lang)`로 바꿔 넘긴다). `LevelCatalog`의 언어별 조회는 게임플레이/UI·씬 플로우. export가 `chapters.json`의 `text`도 `Data/chapters.json`으로 옮긴다.
- `chapters.json`: 같은 방식 `"text": [ { "lang": "en", "planet": "...", "concept": "...", "intro": "...", "tagline": "..." } ]`. `ChapterInfo.text`(`ChapterText[]`), `Planet(lang)`/`Concept(lang)`/`Intro(lang)`/`Tagline(lang)`(ko는 기본 필드, 다른 언어 번역이 없으면 `KeyNotFoundException` — 검사가 빌드 전에 잡는다). 구현됨. `Tagline`만 예외: ko `tagline`이 비었으면 번역을 찾지 않고 `""`(6.4 전부 아니면 없음). `LocalizationCheck`(씬 플로우)의 글꼴 검사 대상에 `Tagline(lang)`을 더한다.
- 텔레메트리는 ko 이름(키)을 그대로 보낸다(분석이 이름으로 묶음). `session_start`에 `lang`을 실을지는 백엔드.

### 13.6 누락 검사
- export(`unity/Tools/export-levels.mjs`, 레벨 디자인): en이 켜져 있으면(`chapters.json`/설정 플래그) 모든 스테이지·챕터에 en `text`가 있고 빈 칸이 아님. 실패하면 파일을 쓰지 않고 exit 1.
- Unity 빌드 전(`SceneFlowValidator`에 `LocalizationCheck` 추가, 씬 플로우): ko 키 = en 키(빠진 것·남는 것 오류), 코드의 `loc.Get("...")`/`Format("...")` 리터럴 키가 테이블에 있음(GlyphCheck의 리터럴 스캐너 재사용), **GlyphCheck를 언어별로**(각 언어 테이블·레벨·챕터 텍스트를 그 언어 글꼴의 cmap으로).
- 코드에 남은 한글 리터럴(UI 문구)도 오류로(로그·주석 제외) → 문구가 테이블로 다 옮겨졌는지 확인.

### 13.7 담당·순서 (50스테이지·3별 다음)
1. 아키텍트: Contracts `Language`·`StringTable`·`Localizer`, `SettingsData.language` 값 규칙·`SetLanguage`, `LaunchOptions.Language`, `ChapterInfo.text`, 이 장.
2. 게임플레이/UI: 문구를 키로 옮김(`MessagePresenter`, HUD, 결과·설정·크레딧), `ko.json` 작성, 언어 바꿀 때 다시 그리기, `UiTheme` 언어별 글꼴 적용.
3. 씬 플로우: 타이틀·스테이지 선택 문구, `LocalizationCheck`·언어별 GlyphCheck, 설정 패널 언어 토글 흐름.
4. 코어: `LevelDto.text`.
5. 레벨 디자인: 레벨·챕터 en 텍스트, export 검사. 번역 품질 확인은 리드·사용자.
6. 아트: en 필기체 글꼴 선정·크레딧, `UiTheme.fonts`.

---

## 14. UI 레이아웃 규칙 (2026-11-10, 사용자 지시 `production/ui-layout-fix.md`)

모든 화면(Title, 언어 선택, StageSelect 지도·목록, Loading, 챕터 진입 카드, HUD, Result, Settings, Credits)에 적용한다. 검증기 `UiLayoutCheck`(씬 플로우, `SceneFlowValidator`)가 이 절을 그대로 검사하고, `scene-validate`와 빌드 전 검사에서 위반이 하나라도 있으면 **오류**다. 수치는 모두 **기준 해상도 1280×720의 캔버스 단위**(`CanvasScaler` ScaleWithScreenSize, `referenceResolution` 1280×720, `matchWidthOrHeight` 0.5 — 지금 모든 캔버스가 이 값이고 바꾸지 않는다). 1920×1080에서는 같은 배치가 1.5배로 그려진다.

### 14.1 용어
- **글자 사각형**: `Text`가 실제로 그리는 글자의 범위. `RectTransform` 전체가 아니라 `preferredWidth`·`preferredHeight`(그 `Text`의 글꼴·크기·줄간격·줄바꿈 설정으로 계산)를 정렬(`alignment`) 기준으로 `rect` 안에 놓은 사각형. 회전·스케일은 월드 모서리(`GetWorldCorners`)로 반영한다.
- **판(plate)**: 글자 뒤에 깔린 `Image`. 그리기 순서가 글자보다 앞(같은 캔버스에서 앞선 형제나 조상)이고, 글자 사각형을 사방 8단위 이상 여유를 두고 감싼다(14.4).
- **장식**: 판이 아닌 보이는 `Graphic`(`Image`·`RawImage`, 북두칠성 연결선, 테두리, 아이콘, 구분선). 3D 장면(보드·선돌·하늘·행성)은 장식이 아니라 **배경**이다.
- **보이는**: `activeInHierarchy`, `enabled`, 색 알파 × 조상 `CanvasGroup.alpha` > 0.01, 마스크 안.

### 14.2 글자 크기
| 종류 | 최소 | 대상 |
|---|---|---|
| 본문 | **22** | 버튼 글자, 힌트·이야기·설명, 행성·스테이지 이름, 별 조건, 결과 문구, 설정 항목 |
| 보조 | **18** | 별 개수, 진행 숫자 옆 단위, 조작 안내, 크레딧 본문, 상태 라벨("사용 중"·"남음") — `UiLayout.secondary`로 표시한 것만 |
| 제목 | 본문 이상 | 화면 제목, 챕터 행성 이름(카드), 스테이지 제목 |

- 실제 크기 = `fontSize` × 그 언어·역할의 `LanguageFonts.titleScale/bodyScale`(13.4, 0이면 1). 숫자 역할은 × 1. `resizeTextForBestFit`이면 `resizeTextMinSize`로 판정한다.
- 18 미만은 어디에도 없다(허용 목록으로도 안 됨). 보드 위 3D 라벨(TextMesh 등 월드 공간)은 이 절 밖이다.

### 14.3 대비
- 글자 색과 바로 뒤 판 색의 WCAG 대비가 **4.5:1 이상**(크기 무관, 큰 글자 예외 없음). 판이 불투명하지 않으면(알파 < 1) 판 색을 **검정과 흰색 위에 각각 합성한 두 값 중 낮은 쪽**으로 계산한다. 글자 알파도 같은 방식으로 판 위에 합성해 계산한다. `Outline`·`Shadow` 효과는 계산에 넣지 않는다.
- 판이 없는 글자는 대비를 계산할 수 없으므로 14.4 위반이다.
- 선·아이콘 같은 장식은 뒤 배경(판이나 화면 배경색)과 **3:1 이상**, 선 굵기 **2 이상**(1920×1080에서 3px).

### 14.4 판 (텍스트 뒤 배경)
- 3D 장면 위에 보이는 모든 글자는 판 위에 있다. 판의 알파는 **0.75 이상**. 글자를 감싸는 화면 전체 배경 `Image`(Title·Loading처럼 불투명 배경)도 판으로 친다.
- **안쪽 여백(padding) 8**: 판의 안쪽 영역이 글자 사각형을 사방 8 이상 여유를 두고 감싼다. 판이 9-slice(`Image.type == Sliced`)면 안쪽 영역은 스프라이트 `border`(× `pixelsPerUnitMultiplier` 반영 크기)를 뺀 가운데 칸이다 — 테두리·이중선 장식은 border 안에 있어야 하고, border가 0인 테두리 스프라이트는 판으로 쓸 수 없다(늘어난 장식선이 글자를 지나는 지금 문제의 원인).
- 한 판에 여러 글자(이름·번호·별 개수 = 한 카드)가 있어도 된다. 카드끼리는 겹치지 않는다(14.5).

### 14.5 겹침
- 보이는 글자 사각형끼리 겹치지 않는다(같은 판 안에서도).
- 장식(선·테두리·아이콘)은 글자 사각형 + 여백 4를 지나지 않는다. 북두칠성 선은 노드 상자 가장자리에서 끊는다. 회전한 선은 회전 사각형으로 판정한다.
- 판·카드·버튼끼리 겹치지 않는다(자기 조상·자손은 제외).
- 글자는 자기 부모 `RectTransform` 밖으로 넘치지도, 잘리지도 않는다: `horizontalOverflow`/`verticalOverflow`가 Wrap/Truncate이면 `preferredWidth/Height` ≤ `rect`, Overflow면 글자 사각형이 부모 `rect` 안. 줄 수는 13.4대로 제목·행성 이름 2줄까지.

### 14.6 안전 영역과 보드 영역
- **안전 영역**: 캔버스 가장자리에서 24 안쪽. 모든 글자·버튼은 안전 영역 안.
- **보드 영역**: 보드 칸과 액터의 월드 경계(`BoardGrid` 셀 전체, 높이 포함)를 Game 카메라로 화면에 투영한 사각형 + 여백 8. 카메라는 실제 게임과 같은 맞춤(`IStageEnvironment.Fit`·카메라 맞춤) 뒤의 값.
- HUD의 글자·판·버튼은 보드 영역과 겹치지 않는다. 3D 장식(빙산·선돌·기둥)은 보드 영역 밖에 있어도 되지만, HUD 글자 뒤로 지나가면 그 글자는 판 위에 있어야 한다(14.4가 잡는다).
- 검사 범위: 가장 큰 보드(가로·세로 칸 수 최대 스테이지 각각)와 23번처럼 힌트가 가장 긴 스테이지.
- 구현(feat/view f525128): HUD가 비워 두는 칸은 `HudView.BoardFrame`(1280×720 기준 316,222 648×398), `BoardView.FitCamera`가 보드를 그 안에 맞춘다. `UiLayoutCheck`는 이 상수를 믿지 말고 **실제 HUD 사각형과 보드 투영**을 비교한다(상수가 HUD 배치와 어긋나면 잡히도록).
- 구현(feat/scene-flow 17cbddf): 카메라 맞춤은 `BoardView.Fit(cam, grid, rig)`(Game, public static, 게임플레이/UI 소유) 하나를 게임(`FitCamera`)과 `UiLayoutCheck`가 같이 쓴다. 시그니처를 바꿀 때는 씬 플로우와 같이 바꾼다. 배우 높이 `BoardView.ActorHeight`(1.2칸).
- 맞춤 식(feat/view 1f689b0): 여백(`rig.padding` 칸) 포함 보드의 바닥(y 0)과 배우 높이 8모서리를 카메라 공간으로 돌린 **min/max 상자**를 `BoardViewport`의 결속 축에 꽉 채우고, 상자 중심을 틀 중심에 둔다. 직교 크기 하한 3.2. 원근은 같은 needH로 거리만 정한다(근사).

### 14.7 모달
- Result, Settings, 언어 선택, 챕터 진입 카드, Credits, 확인 창은 모달이다. 모달이 뜨면 **화면 전체를 덮는 막(`Image`, 캔버스 전체로 늘림, 알파 0.6 이상, `raycastTarget` 켬)**이 모달 아래·다른 UI 위에 그려진다(정렬 순서가 더 높은 캔버스나 마지막 형제). 막 아래 UI는 입력을 받지 않는다.
- 아래 UI가 모달 패널 가장자리에 잘려 보이면 안 된다: 막 알파 0.6 이상이면 아래 UI는 겹침 검사에서 빠지고, 그보다 옅게 하려면 아래 HUD를 숨긴다(`CanvasGroup.alpha = 0`).
- 모달 패널도 14.2~14.6을 그대로 따른다.
- **클리어 결과는 "보드 위 결과"다**(2026-11-11, 최소 UI "결과 화면은 별, 별자리, 다음 버튼만", gameplay-ui 제안 채택). 위 둘째 항목(옅은 막 + HUD 숨김)을 쓴다.
  - **막**: 화면 전체를 덮고 `raycastTarget`을 켠다. 알파는 0.25이고, 피날레 별자리(보드)가 보이게 한다.
  - **HUD**: `CanvasGroup.alpha = 0`, `blocksRaycasts = false`로 숨긴다.
  - **배치**: 결과 요소는 보드 영역(14.11 투영 + 8, 타일 영역 기준)과 겹치지 않는다. 행성 환경(뒷담·뒤 문·크러스트·선돌)은 대상이 아니다. 장식이고 출구는 타일이다(48ff6b4 검토).
    - 별 3개는 `BoardFrame` 위 띠(y < 80)에 판(14.4)과 함께 둔다.
    - [다음]은 우하단, 안전 영역 24 안에 둔다.
  - **별 조건 문구**: 별에 hover·focus·tap할 때만 보조 글자 판으로 보인다. 그 판도 보드 영역과 다른 결과 요소를 피한다.
  - **이야기 피날레 줄**: 결과가 열리면 사라지므로(16.8) 결과에 자리를 비워 두지 않는다.
  - **버튼**: 다시 하기와 스테이지 선택 버튼은 결과에서 뺀다(사용자 승인 범위). 대신 클리어 결과가 떠 있는 동안 두 키가 동작해야 한다.
    - **R**: 그 스테이지를 다시 한다. ★ 다시 도전 경로다.
    - **Esc**: 메뉴(14.11 5번)가 결과 위에 열리고, 스테이지 선택으로 갈 수 있다.
    - 포인터만 쓰는 플레이어는 [다음] 뒤 HUD 메뉴로 간다. 플레이테스트 관찰 항목이다.
  - **패배 결과**는 지금처럼 알파 0.6 이상 막의 모달이다. 50 결과의 [다음]은 [끝까지 보기]다(16.8).
  - **UiLayoutCheck(scene-flow)**: 'Result clear' 화면은 막 알파 검사 대신 셋을 확인한다.
    - 막이 화면 전체이고 `raycastTarget`이 켜져 있다.
    - HUD의 `CanvasGroup` 알파가 0이다.
    - 결과 요소가 보드 영역과 겹치지 않는다.
  - 별 조건 판이 보이는 상태(가장 긴 문구, ko·en)도 등록한다. 'Result lose'는 기존 막 검사를 그대로 한다.

### 14.8 허용 목록 (`UiLayout` 컴포넌트)
검사를 일부러 비켜야 하는 요소는 그 GameObject에 컴포넌트를 붙인다. 이름·경로 문자열 목록은 쓰지 않는다(코드로 만든 UI는 이름이 자주 바뀐다).
```csharp
namespace SevenSteps.Game.UI {
    [Flags] public enum UiLayoutRule { None = 0, Overlap = 1, Decoration = 2, Overflow = 4, Plate = 8, Contrast = 16, Board = 32, Safe = 64 }
    [DisallowMultipleComponent]
    public sealed class UiLayout : MonoBehaviour {
        public bool secondary;          // 14.2 보조 글자(최소 18)
        public UiLayoutRule exempt;     // 이 요소에만 빼는 규칙. 자손에 퍼지지 않는다
        public string reason = "";      // exempt != None이면 필수(비면 검사 오류)
    }
}
```
- 코드로 만드는 UI는 `Ui.Label` 등 생성 도우미에서 붙인다(`Ui.Secondary(text)`, `Ui.Exempt(go, rule, reason)` 같은 짧은 도우미 권장).
- 최소 크기 18 미만과 모달 막(14.7)은 뺄 수 없다.
- 규칙과 플래그는 하나씩 대응한다: 안전 영역(14.6 가장자리 24)은 `Safe`, 보드 영역은 `Board`. 다른 플래그로 대신 건너뛰지 않는다(2026-11-10 추가, scene-flow b4ed8b6 검토 S1).
- `UiLayoutCheck`는 끝에 허용 개수를 화면별로 출력한다(`exempt 3: StageSelect/Map/BigDipper Decoration "..."`). 허용을 늘리는 커밋은 아키텍트 리뷰에서 이유를 본다.

### 14.9 검사 방법 (`UiLayoutCheck`, 씬 플로우 구현)
- 화면: 위 목록 전부, **ko·en 각각**, 해상도 1280×720·1920×1080(16:9, WebGL 템플릿이 16:9 비율 유지로 맞추므로 이 둘이면 된다). Windows 빌드용 1280×800(16:10)과 4:3 창 1024×768(캔버스 약 1108×831 단위)은 **경고**만이고, 목표는 경고 0이다. 세로 화면(720×1280)은 범위 밖이다.
- 문자열: 각 키에 그 언어의 실제 문구, 레벨·챕터 텍스트는 언어별 **가장 긴 값**(이름·힌트·이야기·행성 이름·소개), 숫자는 가장 긴 값(`150/150`, `50`, `7/7`).
- 화면을 Play 없이 Edit Mode에서 만들 수 있어야 한다(빌더나 뷰의 `Build`를 `Localizer`·데이터로 호출 → `Canvas.ForceUpdateCanvases`·`LayoutRebuilder.ForceRebuildLayoutImmediate`). 지금 Play에서만 만들어지는 뷰는 이 경로를 추가한다(각 화면 소유자).
- 위반 출력은 `화면/언어/해상도: 규칙 — 요소 경로 (수치)` 한 줄씩. 하나라도 있으면 실패.

### 14.10 화면별 수치 (ui-layout-fix 반영)
- Loading: 북두칠성 진행 표시 너비 = 화면 너비 40~50%, 진행 숫자 본문 이상.
- StageSelect 지도: 행성 하나 = 이름·번호·별 개수 한 카드(판), 전체 별 수는 하단 정보 띠(판). 배경 보드·선돌은 노드 뒤에서 어둡게(판 규칙으로 해결되지 않는 대비 문제는 화면 소유자 판단).
- Result: 클리어는 14.7 "보드 위 결과"(막 0.25 + HUD 숨김)이고, 패배는 14.7 막이다. 별 조건 문구는 본문 크기다(hover·focus·tap 때만 보인다).
- HUD: 상태 라벨은 아이콘 옆(같은 카드), 조작 안내는 보조 18 이상 + 판, 버튼 줄과 8 이상 띄움.
- 전후 캡처: 각 작업 공간 `reports/ui-layout/<화면>-<ko|en>-<1280|webgl>-<before|after>.png`(커밋하지 않음).

### 14.11 최소 UI v3 등록 (2026-11-11 사용자 승인, `production/ui-minimal-plan.md`)
14.1~14.9 규칙은 그대로다. 바뀌는 것은 HUD 배치와 `UiLayoutCheck`에 등록할 화면이다.
- **보드 틀** `HudView.BoardFrame` 제안은 **(276, 80, 728, 496)**이다(1280×720, 왼쪽 위 원점).
  - 위 HUD 띠(메뉴·제목·형제 점, y 24~72) + 8부터, 아래 Undo·Reset(y 644~) 위까지이다. 양옆 252씩은 선택 카드와 1회 안내 자리다.
  - 8×6(pitch 50)에서 `BoardView.Fit`이 padding 0.7이면 셀 약 77px, 0.5이면 약 81px를 준다.
  - Codex 값 ortho 4.65·중심 (0,−0.75,0)은 8×6의 목표치다. 카메라를 그 값으로 고정하지 않고, Fit이 틀에서 판마다 계산한다(가장 넓은 판 17×3도 같은 틀).
  - gameplay-ui가 수치를 바꿔도 된다. `UiLayoutCheck`는 상수가 아니라 실제 HUD 사각형과 보드 투영을 비교한다(14.6).
- **상시 HUD**는 메뉴·제목·Undo·Reset·형제 점 3이다.
  - 버튼은 48×48 이상(터치)이다.
  - 제목은 본문 22 이상이다. **시안의 20px는 14.2 위반**이라 22로 올린다.
  - 형제 점은 장식이다(14.3 대비 3:1, 색 말고 모양으로도 구분).
  - 상시 조작키 줄과 방향 버튼은 1회 안내 화면에만 있다.
- **선택 화면**(4.4)
  - 카드(판·이름·그림·K/X 배지·결과 한 줄)와 설명 판은 보드 영역(14.6) 밖에 두고, 서로 겹치지 않는다. 배지 글자는 보조 18 이상, 나머지는 본문 22 이상이다. 다음 형제 색 테두리는 장식이다(3:1, 굵기 2).
  - 배치: 보드 영역의 좌·우·위·아래 후보 중 형제 투영점에 가장 가까운, 겹치지 않는 후보를 쓴다. 후보가 없으면 칸 위에 얹지 않는다. 등록 화면에서 오류로 나오므로 틀을 고친다.
  - **겹치지 않음의 대상**(2026-11-11 추가, scene-flow b619811이 찾음): 보드 영역뿐 아니라 그 순간 보이는 다른 HUD 판·버튼(메시지 줄, 이야기·이동 안내, 상시 버튼)도 포함한다. 카드와 설명 판의 높이는 실제 크기다(ExplainChoice 참 + 안내가 가장 크다).
    - 선택 중에는 카드가 우선이다. 어느 후보도 메시지 판을 피하지 못하면 메시지 줄은 카드가 보이는 동안 숨겼다가, 카드가 숨으면 다시 보인다(문구는 그대로).
    - 상시 버튼·형제 점은 숨기지 않는다. 그래서 그 자리는 처음부터 후보에서 뺀다.
  - **보드 30% 암부**: 3D 쪽(셰이더가 `_StarTrailState.y == 1` 선택 단계를 읽음, 또는 카메라 앞 quad)을 권한다.
    - UI `Image`로 한다면 HUD 맨 뒤 형제이고 `raycastTarget` 끔이다.
    - 그리고 `UiLayout.exempt = Decoration | Board`, reason "choose dim 30% (ui-minimal v3)"을 단다. 막 알파 0.6 미만이라 모달이 아니다.
    - 형제 링·연결선·고스트는 3D 표시다(UI로 그리면 장식 규칙을 따른다).
- **등록 화면**: 씬 플로우가 `UiLayoutCheck`에 등록하고, gameplay-ui는 각 뷰의 Edit Mode `Build` 경로(14.9)를 준다. 모두 ko·en, 1280×720(1920×1080은 같은 캔버스 단위), 1280×800·1024×768은 경고다.
  1. HUD 평소: 기존 넷(가장 긴 힌트·이름, 가장 넓은·높은 판)을 새 HUD로 본다.
  2. HUD + 걸음 숫자 표시 켬: 가장 넓은 판.
  3. 1회 이동 안내(`TutorialIds.Move`): 첫 스테이지.
  4. 선택 화면: 기존 chooser 스테이지와, 7수 안에 Choose에 닿는 가장 넓은 판(있으면). 각각 `ExplainChoice` 참(두 설명 + `TutorialIds.Choose` 안내)과 거짓(포커스 하나)이다.
  5. 메뉴(씬 플로우, 설정·스테이지·풀이 보기): 14.7 모달.
  6. Result(최소: 별·별자리·다음): 기존 등록을 새 배치로 본다.
- HUD·Result·선택 화면 위반은 지금처럼 오류다(`HudResultAreErrors`). 새 `exempt`는 아키텍트 리뷰에서 이유를 본다(14.8).

---

## 15. 별 흔적 셰이더 입력 (2026-11-10, 사용자 승인 `production/star-trail-plan.md`)

시안과 기술 메모는 `design/art/star-trail.md`(origin/feat/art-world 3c870b9)다. 기본안은 **추가 드로우콜 0**이다. 공유 타일 셰이더(아트, `RuinsSurface`)가 전역 보드 데이터 텍스처 하나를 읽어 별 발자국·경로·선돌 별을 그린다. 렌더러마다 머티리얼이나 `MaterialPropertyBlock`을 붙이지 않는다(URP SRP Batcher 유지). 타일 메시가 공유 에셋이라 vertex color 방식은 칸마다 메시 복사가 필요하므로 쓰지 않는다.

### 15.1 전역 프로퍼티 (`Contracts.StarTrailMap`의 상수)
| 이름 | 형 | 뜻 |
|---|---|---|
| `_StarTrailMap` | Texture2D | 15.2 배치. RGBA32, **linear**(sRGB 아님), Point, Clamp, 밉 없음, 크기 (2W)×H |
| `_StarTrailWorldToCell` | float4x4 | 월드 위치 → (cx, cy). 칸 (x, y) 중심이 정수 (x, y)이고 cy는 맵 아래로 커진다(levels.json 행). `StarTrailMap.WorldToCell(BoardGrid)` |
| `_StarTrailParams` | float4 | (W, H, 켜짐 0/1, 페이드 0..1). 설정 안 된 전역은 0이라, 뷰가 없으면(타이틀, 아트 리뷰 씬) 셰이더가 아무것도 그리지 않는다 |
| `_StarTrailPulse` | float4 | (최신 칸 맥동 0..1, 일곱째 각성 0..1, 마지막 한 걸음 고리 맥동 0..1, 0). 뷰가 프레임마다 애니메이션 |
| `_StarTrailState` | float4 | (현재 형제 0..2, 단계 0 진행 / 1 선택 / 2 승리 / 3 패배, 피날레 0..1, 쓴 걸음 0..7), 15.5·15.6 |
| `_StarTrailColors` | float4[3] | 형제 0·1·2의 linear 색. HUD 형제 색과 같은 출처(gameplay-ui) |

### 15.2 텍스처 배치 (칸마다 텍셀 2개, 행 y = 맵 행 y)
- **층 0** `(2x, y)`
  - R·G·B = 형제 0·1·2(`BodyIndex`). 비트 k−1(k = 1..7) = 그 형제의 k번째 걸음이 이 칸에 닿음. 진흙처럼 한 칸에 두 걸음이면 두 비트가 다 켜진다.
  - 비트 7 = 그 형제의 KEEP 선돌이 이 칸에 서 있음(`BodyIndex.OfStatue`).
  - A 비트 0 = 가장 최근 걸음 칸(맥동), 비트 1 = 걷는 형제가 선 칸(별 일곱 고리의 중심, 15.6). 나머지 비트는 0(예약).
- **층 1** `(2x+1, y)`
  - R·G·B = 형제 0·1·2의 경로 변. 비트 0은 x+1, 비트 1은 x−1, 비트 2는 y+1(맵 아래), 비트 3은 y−1 쪽 변이다.
  - 연속한 두 발자국 사이의 경로는 양 끝 칸의 마주 보는 변을 켜고, 사이 칸(얼음 미끄러짐)은 양쪽 변을 다 켠다(`AddPath`).
  - 나머지 비트는 0(예약). A는 형제가 끝난 방식이다(15.5).
- 셰이더 읽기(아트):
  - 표본 위치는 `cell = floor(c + 0.5)`, `local = c − cell`(−0.5..0.5, x 오른쪽, y 맵 아래), `uv = ((2·cell.x + 층 + 0.5) / (2W), (cell.y + 0.5) / H)`이다.
  - 표본은 `SAMPLE_TEXTURE2D_LOD(..., point clamp, 0)`으로 읽는다.
  - 바이트 값은 `floor(v·255 + 0.5)`이고, 비트는 `fmod(floor(b / 2^k), 2)`로 꺼낸다(GLES3 정수 연산에 기대지 않음).
  - 켜짐이 0이거나 보드 밖이면 건너뛴다.
  - 전역은 `UnityPerMaterial` 밖에 선언한다.
  - 숫자 영역(칸 중앙 40%)·기믹 실루엣 보호, 겹친 림 분할, 밝기 상한(시안 메모: 기본 0.8, 각성 1.2, 흰 HUD의 65% 이하)은 셰이더 쪽 규칙이다.

### 15.3 쓰는 쪽 (gameplay-ui `StarTrailView`, Game)
- **원칙**: `GameSession.Updated`의 Present·Snap·Rebuild마다 `State`에서 **처음부터 다시 채운다**(`Clear` 후 기록). 이벤트를 누적하지 않으므로 Undo·Restart·리플레이가 저절로 맞는다. Feedback(막힘)은 상태가 그대로라 건너뛴다. 큐에 있던 명령을 지우는 Undo는 업데이트를 내지 않으므로 흔적도 그대로다(이것이 맞다).
- **형제별 경로**:
  - 현재 형제는 `State.Trail`(`Footprint.Cell`, `Step`)이다.
  - 앞선 형제는 `State.Prev` 사슬을 거슬러 `Bodies`가 바뀌기 직전 상태의 `Trail`을 쓴다. KEEP·SWITCH로 끝난 형제는 Choose 상태에 일곱 걸음이 다 있다.
  - 형제 번호는 `BodyIndex.Current(그 상태)`다.
  - 1~6걸음에 떨어진 형제의 마지막 칸(구멍)은 어느 `State`에도 없다. 그리려면 직전 상태에서 네 방향 `Rules.Dispatch`를 해 보고 `Rules.Key`가 다음 상태와 같은 결과의 `Path`·`Fall` 이벤트를 쓴다(순수 함수, 저렴, 리플레이에도 맞음). v1에서는 그리지 않아도 된다.
- **최신 칸**은 Present일 때 결과 상태의 마지막 발자국이고, Snap·Rebuild에서는 끈다.
- **각성·맥동**은 `StepResult.Events`(StepIdx 7, Choose·Win)로 시작해 `_StarTrailPulse`만 움직인다. 입력이나 상태 반영을 연출 때문에 늦추지 않는다.
- **Restart 잔광**: 이전 텍스처를 0.25초 `페이드`로 줄인 뒤 새 상태로 다시 채운다. 상태는 이미 바뀌어 있고, 다음 업데이트가 오면 바로 덮는다.
- **텍스처 관리**: 보드 크기가 바뀔 때(Load) 만들고, 갱신은 `SetPixelData(Bytes)` + `Apply(false)`로 한다(할당 없음). 비활성·파괴 시 `켜짐 = 0`과 텍스처 `Destroy`. 프레임마다 다시 계산하지 않는다(업데이트 때만).
- 비용: 가장 큰 판 17×9는 34×9 텍셀(1.2KB)이다. `Prev` 사슬 걷기는 업데이트마다 명령 수만큼이다(`BodyIndex`와 같은 비용).

### 15.4 아트 쪽 (Codex, `Art/**`)
- `RuinsSurface`(타일·선돌)가 15.1~15.2를 읽는다. 별 모양은 아틀라스 남는 채널이나 공용 R8 마스크를 쓴다. 추가 라이트·드로우 0.
- 머티리얼 수치(밝기, 별 크기)는 머티리얼 프로퍼티(아트)에 둔다. 형제 색은 15.1 전역에서 읽는다(HUD와 한 출처).
- `TileVisual`/`TileSet` 확장이 필요하면 0/null이면 기존 동작인 선택 필드로 더하고 `tileset-validate` 검사를 붙인다(7.2 `labelScale`와 같은 방식).
- 확인: `scene-validate`(보드 대비·UiLayoutCheck 보드 영역), 표준 Chrome 1/29/50에서 60 FPS, 드로우 수 그대로.

### 15.5 상태 머신과 행성별 값 (`StarTrailPreset`, 2026-11-10 리드 추가 지시, gameplay-ui와 합의)
- **상태 판정**은 View 쪽 순수 C# `StarTrail.Of(State)`(gameplay-ui)에서 한다.
  - 입력은 `Trail`과 `Prev` 사슬이다.
  - 끝난 형제는 끝난 방식으로 나눈다: 낙하, 다리, 봉인, 사라짐, 선돌(KEEP), SWITCH, 승리.
  - 한 이동 안에서 끝난 형제의 마지막 칸은 Present의 `StepResult` 캐시에서 얻고, 캐시가 없으면 15.3의 네 방향 재실행으로 얻는다.
  - 규칙 코드는 그대로다.
- **셰이더에 넘기는 것**:
  - 층 1 A에 끝난 방식 비트를 둔다: 0 SWITCH 바통 이음매(새 출발점), 1 다리·봉인(`State.Bridges`, 가라앉은 별), 2 낙하(구멍), 3 사라짐(출발점에서 일곱째). 비트의 주인은 그 칸에 7번째(또는 마지막) 걸음이 있는 형제다.
  - 전역 `_StarTrailState`는 (현재 형제 0..2, 단계 0 진행 / 1 선택 / 2 승리 / 3 패배 = Core `Status` 순서, 피날레 0..1, 0)이다.
  - 셰이더는 현재 형제보다 앞선 형제를 "굳은 별자리"(실선, 선돌 별)로, 현재 형제를 "진행 중"(걸음마다 커지는 점)으로 그린다. 승리 단계에서는 피날레 값에 따라 세 색 별자리를 함께 밝힌다.
  - Undo·Restart는 15.3대로 다시 채우고 페이드한다.
- **행성별 값**은 `Contracts.StarTrailPreset`(ScriptableObject)에 둔다.
  - `ChapterPreset.starTrail`이 이것을 참조한다(조명·하늘과 같은 방식). 비어 있으면 `StarTrailPreset.Default`(필드 기본값 = 시안 메모 초기값)를 쓴다.
  - 아트가 `Art/<Set>/StarTrail_<id>.asset` 7개를 채운다.
  - 셰이더 값은 `StarTrailPreset.SetGlobals(preset)`이 챕터가 바뀔 때 전역 네 개로 넘긴다.
    - `_StarTrailStyleA` = (점 크기 최소, 최대, 불투명도 최소, 최대)
    - `_StarTrailStyleB` = (발광, 각성 발광, 선 굵기, 선 불투명도)
    - `_StarTrailStyleC` = (선돌 별 크기, 이음매 굵기, 가라앉음, 음각 깊이)
    - `_StarTrailTint` = (석질 색조 linear rgb, 섞는 양)
  - View만 쓰는 값: 시간(각성 0.18, Undo 0.12, 실패 0.20, Restart 0.25, 피날레 1.2초), 불꽃 입자 수(0..12, 공유 풀), 모양 마스크 번호(0..3).
  - 형제 색은 프리셋에 두지 않는다(HUD와 한 출처, 15.1).
- **검사**: `tileset-validate`가 `ChapterPreset.starTrail`이 있으면 `StarTrailPreset.Validate()` 범위를 오류로 검사한다. 비어 있으면 검사하지 않는다(기본값).
  - 범위: 크기 0.01~0.2이고 최소 ≤ 최대, 불투명도 0~1, 발광 0~1.5이고 각성 ≥ 기본, 섞는 양 ≤ 0.5, 시간 0~2초(피날레 0~5), 입자 0~12, 마스크 0~3.
- **임시 렌더**: 셰이더 마스크(아트)가 오기 전, gameplay-ui의 결합 quad 메시 1개(+1 드로우, 코드 생성 아틀라스)는 개발용으로 허용한다. 최종은 15.2 전역 텍스처(추가 드로우 0)다. 아트 v2 시안에서 배치(비트)를 바꿔야 하면 아키텍트가 15.2와 `StarTrailMap`을 함께 고친다.

### 15.6 형제 발치의 별 일곱 고리 (2026-11-10, 리드 지시 ui-minimal 예고, feat/view 535e775)
- 걷는 중(`Status.Play`)인 형제 발치에 별 일곱 개가 고리로 놓인다. k걸음을 쓰면 k번째 별이 채워진다(2026-11-11 승인 시안: 채운 별 = 걸은 수, 빈 별 = 남은 수, 숫자 없음). 6걸음 뒤에는 마지막 빈 별이 맥동한다(움직임 줄이기면 이중 테두리, 11.3.2).
- **데이터**: 위치는 층 0 A 비트 1(`StarTrailMap.SetPlayer(State.Pos)`), 쓴 걸음은 `_StarTrailState.w`(`StateVector(..., used)`), 맥동은 `_StarTrailPulse.z`다. 선택·승리·패배 단계에서는 비트를 쓰지 않는다(고리 없음).
- 같은 State에서 다시 계산하므로 Undo·Restart가 저절로 맞는다. 이동 중에는 고리가 도착 칸에 있다(Present 뒤 갱신).
- 임시 quad가 그리는 동안(`StarTrailView.UseQuad`)은 뷰가 같은 값으로 그린다. 셰이더가 그리게 되면 셰이더가 이 세 값만 읽는다.

## 16. 이야기 계약 (2026-11-11 S2, 사용자 승인 구조 `design/story/story-integration.md`, 사전 검토 A1~A13)

이야기는 규칙 코드를 바꾸지 않는다. 판정 입력은 `SessionUpdate`·`State`와 흐름 쪽 사실(클리어 기록, 챕터 경계)뿐이다. 모든 타입은 `Contracts/Story/`에 있고 네임스페이스는 평평한 `SevenSteps.Contracts`다(A1).

### 16.1 파일과 소유
| 무엇 | 위치 | 소유 |
|---|---|---|
| 원고 | `design/story/story.json` | 스토리 디렉터(S1, `check-story.mjs`가 1차 검사) |
| 런타임 입력 | `Data/story.json` | 생성물(`unity.sh levels`가 검사 통과 뒤 복사, S3) |
| `GameAssets.storyJson`(TextAsset), `GameAssets.storyImages`(`StoryImageSet`) | `Game/GameAssets.cs` | 아키텍트(필드), 게임플레이/UI(연결), 아트(이미지 값) |
| `StoryFileDto`·`StoryEntryDto`·`StoryLineDto`·`StoryBrotherDto`·`StoryDipperDto`·`StoryBeatDto` | `StoryFile.cs` | 아키텍트. 모양은 story-integration 2.2 |

### 16.2 이미지 (`StoryImageSet`, A7)
- ScriptableObject `entries: {key, Sprite}[]`, `Get(key)`(없으면 null → 글만 보인다), `required`(기본 false).
- `scene-validate`(scene-flow)가 `story.json` `images`의 키마다 Sprite가 있는지 본다. `required`가 거짓이면 경고, 참이면 오류다. 엔딩 이미지는 11/14 뒤에 오므로 그때까지 거짓이다. 아트가 다 채우고 켠다.

### 16.3 `StoryCatalog` (로드 검사 = 단정, A8)
- `FromJson(json, chapterIds)` — `chapterIds`는 `LevelCatalog.Chapters`의 id 순서다. 실패하면 `InvalidDataException`이고, 메시지에 entry id(줄이면 `id/lineId`)와 이유가 있다.
  - 검사 항목:
    - `version == 1`이고, `languages`는 `ko`로 시작하며 이 빌드의 모든 언어(`Languages.All`)를 포함한다.
    - 형제 1·2·3이 한 번씩 있고, 이름과 번역이 있다.
    - entry id는 `[a-z0-9.]+`이고 서로 다르다. line id는 entry 안에서 다르다. ko와 모든 번역이 비어 있지 않다.
    - `on`은 `StoryOns` 코드다. 한정자 범위는 chapter = chapters id, brother 0~3, stones −1~2, card 0~4(ending만), after는 `""`/`ending`/`allStars`/`allTruths`다.
    - `image`는 `images`에 있는 키다.
    - 치환은 `{brother}`만 쓴다. `{brother}`는 형제를 아는 지점(순간 문구, `seventhFirst`, 엔딩 카드 1)에서만 쓴다.
    - **모호함**: 같은 `on`에서 같은 구체성의 두 entry가 함께 맞는 cue가 있고, 그 cue를 더 구체적인 entry가 덮지 않으면 오류다.
    - **필수 갈래**(최소 UI, story-integration 0.3·0.5): `title`(`after ""`, `"ending"`), `lose`, 챕터마다 `finale`, `ending` 카드 1 형제 1~3 · 카드 2 선돌 0~2 · 카드 3 `""`와 `allTruths` · 카드 4. 갈래는 **써 있어야** 한다(아무거나 entry가 대신하면 오류).
    - `dipper`: from은 챕터 id, to는 챕터 id 또는 `circle`이다. 중복이 없고, 모든 챕터가 from에 한 번 이상 나온다(A13).
  - 글자 수, KS X 1001, 기호, 금지어, 받침 검사는 `check-story.mjs`와 GlyphCheck가 한다.
  - Boot에서 실패하면 오류 로그를 남기고 이야기만 끈다. 게임은 계속된다(호출하는 쪽 일).
- `Find(cue)`는 한정자가 모두 맞는 entry 가운데 가장 구체적인 것을 고르고, 없으면 `KeyNotFoundException`이다. `TryFind`는 선택 지점용이다.
- `Pick(entry, shownCount)` = `shownCount % lines.Length`(난수 없음), `Line(entry, i, lang)`, `BrotherName(n, lang)`.
- `Text(cue, progress, lang, out entry)`는 Find, Pick, `{brother}` 치환을 한 번에 한다. **세지 않는다.** 실제로 보였을 때 호출하는 쪽이 `progress.MarkShown(entry.id)`를 부른다.
- `ImageKeys`, `Dipper`, `Entries`, `ChapterIds`(읽기 전용).

### 16.4 `StoryCue`·`StoryRules` (순수, 상태 없음, A2·A3·A11)
- `StoryOn`(끝에만 추가)과 `StoryOns.Code/TryParse`가 있다. `StoryCue(on, chapter, brother, stones, card, after)`의 빈 값은 `""`/0/−1/0/`""`이고, 생성자가 범위를 검사한다.
- `TryMoment(SessionUpdate u, out cue)`:
  - Present만 본다. 결과가 Win·Lose면 내지 않는다(결과·실패 패널이 말한다).
  - Keep → `keep`, Switch → `switch`, Move면 Fall > Vanish > Bridge > Seal 순으로 고른다.
  - 형제는 `BodyIndex.Current(u.Prev) % 3 + 1`이다. 변환은 `StoryRules.Brother` 한 곳에서만 한다.
- `TrySeventh(u, out cue)`: 일곱째 걸음이 된 이동이면 늘 낸다. "처음 한 번" 거르기, 시도당 1회, `QueuedCount > 0` 건너뛰기는 `StoryDirector`(S4)가 한다.
- `Ending(State win, int card, int truths, int chapterCount)`:
  - 카드 1: 형제 = 출구에 닿은 몸
  - 카드 2: 선돌 = `min(2, Statues.Length)`
  - 카드 3: `truths == chapterCount`이면 `after = allTruths`
  - 카드 4: 고정
  - 이긴 State가 아니거나 범위를 벗어나면 예외다.
- `Finale(chapterId)`.
- 최소 UI에서는 순간 문구와 `seventhFirst`를 잼에서 뺐다(story-integration 0.1). 함수는 남겨 두고 데이터 entry는 선택이다.

### 16.5 저장: `SaveSystem.Story` (A5·A12)
- 키 `seven-steps.story`, 타입 `StoryProgress { version 1, counts[{id,n}], endingSeen, carvingSeen[](챕터 id), finalTruthSeen }`.
- `Count`·`MarkShown`·`EndingSeen`·`MarkEndingSeen`·`CarvingSeen(id)`·`MarkCarvingSeen(id)`·`FinalTruthSeen`·`MarkFinalTruthSeen`은 **메모리만** 바꾼다.
- `SaveSystem.SaveStory()`를 부르는 때: 결과 패널이 열린 뒤, 엔딩 끝, 별지도 새김 연출 끝, 앱 종료.
- 손상되면 기본값으로 읽고 경고한다. 새 버전이면 읽지도 덮어쓰지도 않는다. 중복·음수 count와 중복 id는 읽을 때 정리한다.
- `StartNewPlayer`(`-newplayer`)가 지운다. 리플레이 해시·QA 줄·텔레메트리에는 섞지 않는다.

### 16.6 `StoryTruths` (A4·A9·A10, 저장 없음)
- 입력은 `levels`, 스테이지마다 챕터 키(`LevelCatalog.ChapterKeyOf`), `ProgressData`다.
- `FinaleStage(keys, id)`:
  - 챕터의 마지막 스테이지다.
  - 게임의 마지막 스테이지를 가진 챕터는 그 바로 앞 스테이지다.
  - 지금 데이터로는 7·14·21·28·35·42·49다(0부터 세면 6…48).
- `IsFinaleStage`, `IsEndingStage`(마지막 스테이지 = 엔딩).
- `FinaleCleared(…, id)`는 스테이지 **이름**으로 클리어를 본다.
- `Truths(…, chapterIds)`. 다 모았는지는 `Truths == chapterIds.Count`로 본다.
- `ChapterComplete`는 별자리(`constellation`)용이다.

### 16.7 `LevelDto.startLine` (최소 UI 시작 한 줄)
- `"story"`, `"hint"`, `""`(없음) 중 하나다. 필드가 없거나 null이면 `"story"`로 읽는다(`LevelDto.StartLine`). 그 밖의 값은 `ArgumentException`이다.
- 채우기는 레벨 디자인(`levels.js`, `export-levels.mjs` 검사)이다. 코어 파일의 필드 한 줄은 아키텍트가 리드 지시로 넣었다.
- 시작 한 줄은 `Game/Story/StoryDirector`가 정하고 HUD 잠깐 줄(`ITransientLine`)에 보인다(gameplay-ui). 메뉴 다시 보기의 진입점은 `GameController.ReplayStartLine()`(bool: 결과 화면이거나 줄·스테이지가 없으면 false, 메뉴가 덮는 동안 기다림. 안에서 `StoryDirector.ReplayStartLine()`을 부름, 54fab4a)이고, 메뉴 항목과 연결은 scene-flow 몫이다(`MenuController`에 람다로 넘김).

### 16.8 흐름 쪽 약속 (씬 플로우·게임플레이/UI, 계약 아님)
- 50 결과의 [다음]은 [끝까지 보기]가 되고, 엔딩 4장으로 이어진다(9장 질문 1). 엔딩 카드 이미지는 `entry.image`에서 온다.
- `EndingModel.Truths`는 scene-flow가 `StoryTruths.Truths`로 센다. 텔레메트리 `ending_view.truths`는 백엔드가 스키마와 골든을 같이 바꾼다.
- 피날레 한 줄은 `Updated(Present, Win)`에서 피날레 스테이지인지 판정해 보류한다. 별자리 피날레 동안 잠깐 줄 자리에 보이고, 결과가 열리면 사라진다(story-integration 0.1).

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
| 2026-11-09 | 아키텍트 | 2장 어셈블리 `SevenSteps.Art`(`Art/`), `SevenSteps.Art.Editor`(`Art/Editor/`, Editor 전용) 추가. 아트 스크립트가 Assembly-CSharp에서 빠짐(GUID 유지) |
| 2026-11-09 | 아키텍트 | 6.5 3별 계약(B안): 시도 정의, 셈 규칙(undos/restarts/hintUsed), Core `Scoring.Stars(AttemptStats)`(코어), 셈은 `BoardController`(게임플레이/UI)·`IStageBoard` 노출(씬 플로우), 저장 `ProgressData.MarkCleared(level, bodies, stars)`/`BestStars`(Contracts, 구현). 13장 다국어 설계: 자체 경량 JSON 문자열 테이블(Unity Localization 기각: WebGL 용량·비동기 초기화·병합), 언어 설정·첫 실행 시스템 언어, UiTheme 언어별 글꼴, levels/chapters `text` 배열, 누락 검사 |
| 2026-11-09 | 코어·아키텍트 | 6.5 판정 API를 코어 구현에 맞춤: `Scoring.Stars(AttemptStats)` 대신 `SevenSteps.Core.Stars.Of(bodiesUsed, minBodies, hints, undos, restarts)`(0 = 판정 불가), `Stars.Max`, `Stars.BodiesUsed(State)`. 의미는 같음(★2는 최소 몸 이하·힌트 0, ★3은 Z·R 0) |
| 2026-11-09 | 아키텍트 | 13.4 UiTheme 글꼴을 아트 제안(fonts.md, ui-theme.md)과 맞춤: `LanguageFonts{language, title, body, 크기·줄간격}` + 테마 공통 `number`, `FontFor(lang, role)`, `symbolFallbackFont` 없음(레거시 Text는 에셋 간 대체 불가). 6.4 `artPreset` ↔ `AppConfig.chapters` 검사 켬 |
| 2026-11-10 | 아키텍트 | 13장 1번 구현: `Language`/`Languages`(93347db), `StringTable`/`StringTableAsset`/`Localizer`, `SaveSystem.Language`/`SetLanguage`·첫 실행 시스템 언어, `LaunchOptions.Language`(`-lang`/`?lang`), `ChapterInfo.text`/`ChapterText`, `GameAssets.strings` + `Data/Strings/ko.json`·`en.json` 빈 틀. EditMode `LocalizationContractTests` |
| 2026-11-10 | 코어·아키텍트 | 6.5 `Stars.Of` 전제 조건 예외(bodiesUsed<1, 음수 카운트; feat/core a332e43). 13.5 `LevelDto.text`/`TextFor(code)`(6398ca5), export가 chapters.json text 보존 |
| 2026-11-10 | 아키텍트 | 12.6 아트 확정 계약: `KeepLook`(Contracts enum `Neutral`/`SiblingTrace`), `TileSet.keepLook`(끝에 추가, 기본 Neutral). **`TileSet.StatueFor` 동작 변경**: Neutral이면 `statueBodies`를 보지 않고 `statue`. 지금 RuinsHenge는 `statueBodies`가 모두 중성 `Statue`라 화면 변화 없음. 검사기: 세쌍둥이 프리팹 중복 경고, SiblingTrace인데 `statueBodies` 없음 오류. `AppConfig.tileSet` = `RuinsHenge_Chapter01`은 7300d9d에서 이미 전환됨(변경 없음). 영향: 게임플레이/UI(`BoardView`는 이미 `StatueFor`만 부르므로 코드 변경 없음, HUD는 `BodyColor` 그대로), 씬 플로우(없음, `UiTheme`과 무관), 아트(흔적 채택 시 `keepLook`과 `statueBodies`=Menhir_A/B/C 설정, `RuinsHengeBuilder`가 값을 쓴다면 같이) |
| 2026-11-10 | 아키텍트 | 6.4·13.5 `chapters.json` `tagline`(행성 선택 한 줄, 리드 요청): `ChapterInfo.tagline`/`ChapterText.tagline`/`Tagline(lang)`(ko가 비면 모든 언어 `""`). export: 한 챕터라도 ko tagline이 있으면 모든 챕터 ko + 모든 번역 tagline 필수, 없으면 경고. `Data/chapters.json`에 빈 `tagline` 필드가 생김. 영향: 레벨 디자인(`design/levels/chapters.json` 값 채움, ko·en 함께), 씬 플로우(행성 선택 표시, `LocalizationCheck` 글꼴 검사에 tagline 추가), 게임플레이/UI 없음. EditMode `ChapterTaglineIsOptionalUntilFilled` |
| 2026-11-10 | 아키텍트 | 12.8 챕터 프리셋 적용 반영(feat/view 5e0a1cb 검토 승인): `AppConfig.chapters` 7개 + `AppConfig.tileSet` 비움, 첫 세트 `TileSetFor`, 검증기 조건. 공용 API 변경 없음 |
| 2026-11-10 | 아키텍트 | 2장 `SevenSteps.Art.Preview`(`Art/Test/`, defineConstraints UNITY_EDITOR 또는 SEVENSTEPS_ART_PREVIEW) 추가, `SevenSteps.Art.Editor`가 참조. 정식 Windows 빌드 Managed에 Preview dll 없음 확인. 영향: 아트(프리뷰 플레이어 빌드 두 곳에 `extraScriptingDefines` 한 줄 필요) |
| 2026-11-10 | 아키텍트 | 13.3 `SettingsData.languageChosen`·`SaveSystem.LanguageChosen`(씬 플로우 요청, 첫 실행 언어 선택 화면). `SetLanguage`가 true로 저장, 이전 저장은 false. 영향: 씬 플로우(Title 선택 화면), 게임플레이/UI 없음(설정 토글은 이미 `SetLanguage`). EditMode `LanguageChosenOnlyBySetLanguage` |
| 2026-11-10 | 게임플레이/UI·아키텍트 | 11장 `LaunchOptions.SmokeView`(Contracts enum `SmokeView` None/Board/Empty, `-smoke`/`?smoke=`; feat/view 26d60dc, 코어 합의, 아키텍트 검토 승인). 스모크 전용, 일반 실행 동작 변경 없음 |
| 2026-11-10 | 게임플레이/UI·아키텍트 | 11장 `LaunchOptions.NewPlayer`(`-newplayer`/`?newplayer=1`), `SaveSystem.StartNewPlayer()`(진행·playerId 삭제 후 저장, 설정 유지), `HttpTelemetry` 큐 항목에 playerId(feat/view 01795d3, 아키텍트 검토 승인). 일반 실행 동작 변경 없음. 영향: 씬 플로우(`Bootstrap` 한 줄), 백엔드(배치 머리 playerId가 기록 시점 기준으로 바뀜 — 같은 테스터면 동일) |
| 2026-11-10 | 씬 플로우·아키텍트 | 11장 `LaunchOptions.LoadDelay`(`-loadDelay`/`?loadDelay=`, 개발용, feat/scene-flow 3f6671f 검토 승인). 13.3 언어 선택 로직이 `LanguageController`(Title·Game 공용)로 옮겨지고 설정 언어 버튼이 선택 화면을 연다. `LocalizationCheck.KoreanLiteralsAreErrors = true` |
| 2026-11-10 | 씬 플로우·아키텍트 | 11장 `LaunchOptions.FlowCheck`(`-flowcheck`/`?flowcheck=1`, 개발·스모크용, feat/scene-flow 0bcade4 검토 승인), `unity.sh smoke` 흐름 단계 |
| 2026-11-10 | 게임플레이/UI·아키텍트 | 11장 `LaunchOptions.OpenSettings`(`-settings`/`?settings=1`, feat/view 6a6056d 검토 승인). 영향: 씬 플로우(`TitleScreen` 한 줄) |
| 2026-11-10 | 씬 플로우·아키텍트 | 11.1 `AppFlow.CanGo`에 (Title, Game) 추가(feat/scene-flow 19554a3, 검토 승인). 타이틀 시작·이어하기가 조용히 막히던 버그 수정. 뒤로 표 변경 없음 |
| 2026-11-10 | 씬 플로우·아키텍트 | 11.2 `EntryTracker.TakeVia`: 알린 via가 첫 진입보다 앞섬(지도·목록·타이틀 시작 = select, 이어하기 = 첫 진입이면 boot), `TitleFlowController(…, EntryTracker)` 생성자 인자 추가(feat/scene-flow c474b03 검토 승인, telemetry.md v1 via 정의 e953eb0). 텔레메트리 골든은 트레이스 `via`로 첫 `level_start` |
| 2026-11-10 | 씬 플로우·아키텍트 | 11장 `LaunchOptions.TitleCheck`(`-titlecheck begin, continue, map`, 개발·스모크용, feat/scene-flow 78e81ee 검토 승인), `GameFlowController.LastVia`, `unity.sh smoke` 타이틀 단계 |
| 2026-11-10 | 아키텍트 | 14장 UI 레이아웃 규칙(사용자 지시 production/ui-layout-fix.md): 기준 1280x720 캔버스 단위, 본문 22·보조 18(`UiLayout.secondary`), 대비 4.5:1(장식 3:1, 선 2 이상), 3D 위 글자는 판(알파 0.75 이상, 안쪽 여백 8, 9-slice는 border 안쪽), 장식선은 글자 사각형을 지나지 않음, 글자·판 겹침과 넘침 금지, 안전 영역 24와 보드 영역 분리, 모달 막(알파 0.6 이상), 허용 목록은 `UiLayout` 컴포넌트(exempt 규칙 + reason 필수). 검증기 `UiLayoutCheck`(씬 플로우) ko·en, 1280x720·1920x1080 오류, 1280x800 경고. 영향: 씬 플로우(검증기, `UiLayout` 타입 추가, 소유 화면), 게임플레이/UI(HUD·결과·버튼 스킨, Edit Mode에서 만들 수 있게) |
| 2026-11-10 | 아키텍트 | 11장 `LaunchOptions.ArtPerf`(`-artperf`, `?artperf=1`, 아트 측정용, 리드 승인). 영향: 아트(`RuinsHengePlanet.Awake`의 URL 직접 검사를 `Services.TryGet<LaunchOptions>`의 `ArtPerf`로 한 줄 교체, Codex 다음 커밋) |
| 2026-11-10 | 게임플레이/UI·아키텍트 | 14.6 `HudView.BoardFrame`·`BoardViewport`(보드가 들어갈 HUD 빈 칸, 공개 상수), `BoardView.FitCamera`가 그 틀에 맞춤. `Ui.FitButtonSkin`·`FrameBorder`·`Plate`·`PlateAlpha`(14.4), `UiLayout`(14.8, scene-flow 37a146b) — feat/view f525128·3216c4d 검토 승인 |
| 2026-11-10 | 아키텍트 | 14.8 `UiLayoutRule.Safe = 64` 추가(안전 영역 면제, 지금은 `Overflow`가 대신하던 것), 규칙과 플래그 1:1. 영향: 씬 플로우(`UiLayout.cs` 한 줄, `UiLayoutCheck` 안전 영역 검사가 `Safe`를 봄). feat/scene-flow b4ed8b6 검토 승인 |
| 2026-11-10 | 아키텍트 | 11장 `LaunchOptions.SaveKey`(`-savekey`, `?savekey=`), 11.3 `SuffixedStore`(키 + `#접미사`)·`SaveStores.For(LaunchOptions)`·`SaveStores.IsValidSuffix` (Contracts, 리드 제안 채택). 영향: 씬 플로우(`Bootstrap` `new PlayerPrefsStore()` → `SaveStores.For(launch)`), 게임플레이/UI(`TelemetryRunner.CreateTelemetry` 같은 교체), 코어(`unity.sh smoke`가 `-savekey <작업 공간>`) |
| 2026-11-10 | 아키텍트 | 11장 SaveKey 연결: `Bootstrap`(SaveSystem)·`TelemetryRunner.CreateTelemetry`(큐)가 `SaveStores.For(launch)` (리드 지시로 아키텍트가 소유자 대신 두 줄 수정, 리드가 씬 플로우·게임플레이/UI에 알림). `unity.sh smoke -savekey`는 코어 |
| 2026-11-10 | 씬 플로우·아키텍트 | 14.6 보드 영역 검사 구현: `BoardView.Fit` public static(게임과 검사기가 같은 맞춤 식), `BoardView.ActorHeight`, `UiLayoutRule.Board` 면제. feat/scene-flow 17cbddf 검토 승인(B1: Choose 상태·긴 메시지 HUD 변형 추가 요청) |
| 2026-11-10 | 게임플레이/UI·씬 플로우·아키텍트 | 14.9 HUD·결과 위반도 오류(`UiLayoutCheck.HudResultAreErrors = true`, HUD/결과 0건 달성, feat/view d170385 검토 승인). 이제 14장 위반은 모든 화면에서 빌드를 막는다 |
| 2026-11-10 | 씬 플로우·아키텍트 | Game: `AttemptCounts.After(SessionUpdate)`(6.5 Undo·Restart 세기, `BoardController`와 QA 리플레이가 공유), `Game.Flow.QaReplay`(attempts.jsonl 교차 검사, 재시작 글자·해시는 docs/qa-attempts.md 확정 전 임시). feat/scene-flow 332498f 검토 승인 |
| 2026-11-10 | 아키텍트 | 15장 별 흔적 셰이더 입력: 전역 `_StarTrailMap`((2W)×H RGBA32, 층 0 걸음·선돌·최신, 층 1 경로 변)·`_StarTrailWorldToCell`·`_StarTrailParams`·`_StarTrailPulse`·`_StarTrailColors`, Contracts `StarTrailMap`(배치·쓰기 도우미·`WorldToCell`). 추가 드로우 0, State에서 매번 다시 채움 |
| 2026-11-10 | 아키텍트·게임플레이/UI | 15.5 별 흔적 상태 머신(층 1 A 끝난 방식 비트, 전역 `_StarTrailState`)과 행성별 값 `StarTrailPreset`(Contracts SO, `ChapterPreset.starTrail`, 비면 Default, `SetGlobals` → `_StarTrailStyleA/B/C`·`_StarTrailTint`, `tileset-validate` 범위 검사) |
| 2026-11-10 | 아키텍트·게임플레이/UI | 15.6 별 일곱 고리: 층 0 A 비트 1(`SetPlayer`), `_StarTrailState.w` = 쓴 걸음, `_StarTrailPulse.z` = 마지막 걸음 고리 맥동. feat/view 535e775 검토 |
| 2026-11-11 | 아키텍트·씬 플로우 | 최소 UI v3 계약: 4.4 선택 화면(`ChoicePreview.TryOf` = `Rules.Dispatch` 순수 호출 미리보기, 확정 연출 0.5초 이하는 기존 Present 잠금, Z는 선택 전, 확정 횟수는 Present Keep/Switch만). 11.3.2 키 `seven-steps.tutorial`(`TutorialData` seen `이름@버전`·choiceConfirms 상한 3, `StartNewPlayer`가 지움, 리플레이·smoke 실행 제외), 접근성 `SettingsData.showStepNumbers`·`reduceMotion`(기본 끔). 14.11 BoardFrame 제안 (276,80,728,496)·상시 HUD·선택 화면 배치·등록 화면 6종. 15.6 채운 별 = 걸은 수로 문구 정정. 테스트 `MinimalUiContractTests` 7개 |
| 2026-11-11 | 아키텍트 | scene-flow c513108 검토 승인(메뉴 오버레이·`TutorialTracker`·접근성 설정 연결, 검사 실행은 모두 seen인 메모리 저장소). 메모 C1 선택 카드·안내는 `Presented` 뒤 표시(gameplay-ui), C2 메모리 저장소 상한, C3 임시 설정 버튼 연결 정리. `docs/reviews/2026-11-11-scene-flow-c513108.md` |
| 2026-11-11 | 아키텍트 | feat/view cce7783·740a516·fdd9651(최소 HUD, 선택 UI v3, 발치 별) 조건부 승인: 계약 4.4·14.11 대조 맞음(BoardFrame 8×6 ortho 4.67 셀 77px). R1 카드 Button 선택이 남아 Enter가 Submit과 `choice.Confirm` 두 경로로 갈 수 있음, R2 확정 Present 문 여러 개면 0.5초 초과·움직임 줄이기에서도 문 대기. `docs/reviews/2026-11-11-feat-view-fdd9651.md` |
| 2026-11-11 | 아키텍트 | scene-flow b619811(14.11 등록 4종, C2) 승인, 병합은 gameplay-ui `LayoutChoice` 수정 뒤. 14.11 보강: 카드 비겹침 대상에 보이는 HUD 판·버튼 포함, 피할 후보가 없으면 카드가 보이는 동안 메시지 줄을 숨김, 상시 버튼 자리는 후보에서 뺌. `docs/reviews/2026-11-11-scene-flow-b619811.md` |
| 2026-11-11 | 아키텍트 | feat/view 71a8f9c 승인(R1 Enter 한 경로, R2 확정 Present 최대 0.42초·움직임 줄이기 0, M2 고스트 Behaviour 끔, 14.11 보이는 HUD 판 회피·메시지 숨김). b619811과 함께 병합 가능 |
| 2026-11-11 | 아키텍트 | feat/art e05c3c1(행성 2~7 모델링)+2cd08fb 조건부 승인. 12.8에 환경 삼각형 런타임 가드 50k(예산은 Chrome 60 FPS, 큰 판 14·31·49 측정)와 리뷰 asmdef 빌드 제외 규칙 추가. 병합 전 B1 concepts PNG 75개·실행 로그 제외, R1 `SevenSteps.Art.Review` defineConstraints와 ReviewLevels.json Resources 밖으로. R2 altar 9×9 정적 그림자 47/48. `docs/reviews/2026-11-11-feat-art-e05c3c1.md` |
| 2026-11-11 | 아키텍트 | 16장 이야기 계약 S2(A1~A13): `Contracts/Story/` `StoryCatalog`(로드 단정, 구체성 Find, 모호함·필수 갈래 검사), `StoryCue`/`StoryOn`, `StoryRules`(TryMoment·TrySeventh·Ending·Finale), `StoryProgress` + `SaveSystem.Story`/`SaveStory`/키 `seven-steps.story`(-newplayer가 지움), `StoryTruths`(ChapterKeyOf 경계 피날레·Truths), `StoryImageSet`(required 기본 false), `GameAssets.storyJson`/`storyImages`, `LevelDto.startLine`(기본 story). 테스트 `StoryContractTests` 12개 |
| 2026-11-11 | 아키텍트 | 14.7 클리어 결과 = 보드 위 결과(막 0.25 + raycast, HUD CanvasGroup 0, 별 3은 BoardFrame 위 띠, 다음은 우하단, 별 조건은 hover/focus/tap, 다시 하기·스테이지 선택 버튼 빼고 R·Esc 동작). 패배는 0.6 모달. UiLayoutCheck Result clear는 막 raycast·HUD 숨김·보드 비겹침 검사 |
| 2026-11-11 | 아키텍트 | feat/levels-2 dc6cea2(S3) 승인: b03ce11 위 병합 EditMode 852/852(실제 Data/story.json StoryCatalog 로드 통과), startLine hint 4·8, 피날레 스테이지 7·14·…·49 데이터 경계와 일치. QA 끝까지 qa 워크트리 고정에 동의. `docs/reviews/2026-11-11-levels-2-dc6cea2.md` |
| 2026-11-11 | 아키텍트 | feat/art bcb2126 승인: R2 altar 앞 그루터기 그림자 제외(9×9 47→44, 한도 48), S1 hollow 트라이플래너 펼친 좌표 미분으로 SAMPLE_TEXTURE2D_GRAD. 메모 G1 미분이 정점 데이터 분기 안(경계 줄이 남으면 분기 밖으로). `docs/reviews/2026-11-11-feat-art-bcb2126.md` |
| 2026-11-11 | 아키텍트 | feat/scene-flow 6e8f3cd(이야기 S4 흐름) 승인: StoryLoad A8 끔, 개발 실행 메모리 진행, 저장 시점 결과·엔딩 끝, 엔딩 StoryRules.Ending 갈래·FinalTruthSeen, 50 끝까지 보기, 타이틀·별지도 새김·dipper, ValidateStory 이미지 경고. 메모 M1 ResultView 병합 순서, M2 dipper↔테마 선, M3 앱 종료 저장. `docs/reviews/2026-11-11-scene-flow-6e8f3cd.md` |
| 2026-11-11 | 아키텍트 | feat/view 48ff6b4 승인(별 흔적 v2 임시 메시 1 draw, TrailMark.End 값 비교·Knot, 움직임 줄이기, linear 꼭짓점 색 / 결과 14.7 클리어 막 0.25+HUD Conceal·패배 0.72 모달·50 result.ending 유지·R Again). 메모 M1 키보드 focus로 별 조건, M2 Result clear·lose 검사 등록(scene-flow), M4 Chrome FPS에 긴 판. `docs/reviews/2026-11-11-feat-view-48ff6b4.md` |
| 2026-11-11 | 아키텍트 | feat/view c58dfa4 승인(48ff6b4 M1 결과 별 키보드 초점 Tab·←→ 순환, M5 주석). R1 다음 커밋: 키 입력을 ResultView.Update에서 GameController로 옮김(메뉴 덮개 위에서 별 초점이 같이 움직임). `docs/reviews/2026-11-11-feat-view-c58dfa4.md` |
| 2026-11-11 | 아키텍트 | feat/scene-flow cd38eb8 조건부 승인(새김 첫 등장·CarvingSeen 저장, 진실 다시 보기 세지 않음, 북극성, 타이틀 하늘, 앱 종료 저장). R1 병합 전 CarvingTap navigation None(별지도 방향키가 숨은 버튼에 멈춤), R2 진실 다시 보기 48px·키보드 경로. 4d41d15(14.7 Result clear·lose 검사)·01f4cc6(dipper 경고) 승인. `docs/reviews/2026-11-11-scene-flow-cd38eb8.md` |
| 2026-11-11 | 아키텍트 | feat/scene-flow 6958352 승인(cd38eb8 R1·R2를 (a)로: 새김 탭 버튼 제거, 행성 카드 초점·hover로 진실 표시). 메모 N1 언어 변경 뒤 detailDefault 갱신. `docs/reviews/2026-11-11-scene-flow-cd38eb8.md` |
| 2026-11-11 | 아키텍트 | feat/scene-flow 2fe7f4d 승인(N2 단계 패널 tagline 줄에 진실 금색, N1 언어 변경 뒤 detailDefault). `docs/reviews/2026-11-11-scene-flow-cd38eb8.md` |
| 2026-11-11 | 아키텍트 | feat/view bdaa019 반려(c58dfa4 R1: 키를 GameController로 옮겼지만 ResultView.Update가 남아 키당 FocusStar 두 번, 메뉴 위에서도 초점 이동). ResultView.Update 삭제 후 병합. `docs/reviews/2026-11-11-feat-view-c58dfa4.md` |
| 2026-11-11 | 아키텍트 | feat/view 378bf38 승인(ResultView.Update 삭제로 c58dfa4 R1 종료: 별 초점 키는 GameController Result 분기 한 곳). `docs/reviews/2026-11-11-feat-view-c58dfa4.md` |
| 2026-11-11 | 아키텍트 | 14장 UiLayoutCheck 해상도에 1024x768(4:3) 경고 추가 승인(scene-flow 제안, QA 1차 1280x800 경고 정리와 4:3 창 대비, 목표 경고 0, 세로 화면은 범위 밖). 영향: 씬 플로우(Resolutions 한 줄), gameplay-ui(Result 1024x768 경고 20건) |
| 2026-11-11 | 아키텍트 | feat/view 4ea1ec0 승인(이야기 S4 보드 쪽: StoryDirector, 시작 줄 startLine, 피날레 줄 보류·표시, HUD 잠깐 줄 하나). 메뉴 다시 보기 나눔: gameplay-ui StoryDirector.ReplayStartLine, scene-flow 메뉴 항목. `docs/reviews/2026-11-11-feat-view-4ea1ec0.md` |
| 2026-11-11 | 아키텍트 | feat/view ebdb653 승인(StoryDirector.ReplayStartLine, 메뉴 다시 보기 진입점. 메뉴 항목 연결은 scene-flow). `docs/reviews/2026-11-11-feat-view-4ea1ec0.md` |
| 2026-11-11 | 아키텍트 | 16.7 시작 한 줄 담당 갱신: StoryDirector + ITransientLine(gameplay-ui), 메뉴 다시 보기 진입점 이름 StoryDirector.ReplayStartLine()(bool, ebdb653), 메뉴 항목·연결은 scene-flow |
| 2026-11-11 | 아키텍트 | feat/view 54fab4a 승인(M2 FinaleStarting 순서 무관, N1 메뉴 진입점 GameController.ReplayStartLine 결과에선 false, 16.7 이름 갱신). feat/scene-flow 3a7804e 승인(QA 1 UiLayoutCheck 경고 정리, 1024x768 경고, 별지도 CardCenters). `docs/reviews/2026-11-11-feat-view-4ea1ec0.md`, `docs/reviews/2026-11-11-scene-flow-3a7804e.md` |
