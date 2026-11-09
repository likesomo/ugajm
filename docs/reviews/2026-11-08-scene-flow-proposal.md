# 검토: docs/scene-flow.md 제안서 (feat/scene-flow 84ab80b)

검토자: 아키텍트, 2026-11-08. 문서만 검토(구현 없음). 진단 근거 중 핵심은 코드로 확인했다:
`EditorSettings` `m_EnterPlayModeOptions: 1`(도메인 리로드 꺼짐), `RuntimeInitializeOnLoadMethod` 0건, `AppFlow` 생성자 null 허용·`GoToGame` 범위 검사 없음,
`Changed`가 로드 요청 직후 발생, `Services.Register` 덮어쓰기, `LaunchOptions` 공개 필드(`GameController.cs:155`가 `Replay = ""`로 고침).

**결론: 아키텍트 관점에서 방향 승인, S0~S2 계약 변경은 아래 조건으로 승인.** 구현 착수는 사용자 결정 후(리드). 착수하면 S0(정적 초기화)가 실제 버그라 가장 먼저 한다.

## (1) 1.4 문안 → 9장: 조정해서 반영함 (feat/architecture)
고친 점:
- 참조 방향: "View → Controller → Model"은 "View는 컨트롤러를 모른다"와 어긋난다. **Controller → View, Controller → Model, View → Model은 읽기 전용 데이터만**으로 바꿨다.
- `GameSession`은 Model인데 `IBoardPresenter`(Contracts 인터페이스)로 연출을 부른다(4장 입력 락 계약). 이것을 허용된 예외로 적었다.
- **게임 규칙상 안 되는 명령(벽으로 이동 등)은 예외가 아니다.** `Rules.Dispatch`는 지금처럼 결과로 처리한다(core.js 골든). 이 줄이 없으면 "전제 조건 예외"가 규칙 코드로 번질 수 있다.
- Core에서 `System.Diagnostics.Debug.Assert`는 쓰지 않는다(플레이어에서 실패해도 아무 일이 없을 수 있음). Solver 같은 뜨거운 경로에는 검사를 넣지 않는다.
- WebGL은 `webGLExceptionSupport: 1`(명시적 예외만)이다. NullReferenceException 같은 암묵적 예외는 WebGL에서 잡히지 않는다. 단정적 규칙(먼저 검사하고 던지기)의 근거로 적었다.
- 적용 범위: 새 코드와 손대는 코드. 기존 코드는 5장 단계대로.

## (2) S1·S2 계약 변경: 승인 (조건)
| 항목 | 판정 | 조건 |
|---|---|---|
| `AppFlow(ISceneLoader, int stageCount)` null 금지 | 승인 | `stageCount >= 1` 검사. 기존 테스트는 가짜 로더로 |
| `GoToGame` 범위 검사 | 승인 | `ArgumentOutOfRangeException`. 실행 인자 `-level 99`는 부트스트랩이 경고 로그 + 보정 후 부름 |
| `Back()`/`BackTarget` | 승인 | Title은 갈 곳이 없으니 `bool TryGetBackTarget(out AppState)` 또는 `CanBack`. 새 전이는 추가하지 않는다(기존 `CanGo` 표 안에서만) |
| `IsLoading` + `ISceneLoader.Loaded` | 승인 | 완료 신호는 씬 루트가 아니라 **`SceneLoader` 구현이 `SceneManager.sceneLoaded`로** 낸다(Awake 뒤, Start 전). 루트마다 신호 코드를 넣지 않는다. 같은 씬 전이는 로딩 없음 |
| 진입 이벤트(`Entered`) | **보류** | 4.4의 "새 씬은 루트 `Start`에서 `Enter(flow.State)`, 같은 씬은 `Changed`"로 진입 경로가 이미 하나다. 이벤트를 더하면 경로가 둘이 된다. 페이드(S5)에서 필요해지면 그때 추가 |
| `Services.Register` 중복 예외 | 승인 | `Bootstrap`은 이미 `Clear` 후 등록. 테스트는 `[SetUp]`에서 `Clear` |
| `Services` Play 시 초기화 | 승인 | `Services` 안에 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] static void ResetForPlay() => map.Clear();` (Contracts는 엔진 참조 가능). 부트스트랩 생략 판단은 `Bootstrap`의 "이번 Play에서 돌았는가" 플래그(같은 방식으로 초기화) |
| `LaunchOptions` 캡슐화 | 승인 | `{ get; private set; }`, `TakeReplay()`(한 번만), `-stageselect`/`-credits` 흡수(스모크 전용이라 주석에 명시). 파싱 실패 경고는 Contracts에서 바로 로그하지 말고 `Warnings` 목록으로 내고 부트스트랩이 로그(테스트 가능). 텔레메트리 쪽 `TelemetryOff`/`TelemetryUrl` 읽기 호환 유지 |

- 구현: 이 변경에 한해 `Contracts/App/`(`AppFlow`, `Services`, `LaunchOptions`) 수정을 씬 플로우에 맡긴다. 커밋마다 아키텍트 검토, `docs/architecture.md` 11장·변경 기록은 아키텍트가 고친다. 공용 API 변경이므로 리드가 영향받는 담당(게임플레이/UI, 백엔드)에게 알린다.

## (3) `OverlayStack`/`EntryTracker` 위치: **Game** (`Scripts/Runtime/Game/Flow/`)
Contracts는 여러 담당이 기대는 안정된 공용 API만 둔다. 둘 다 씬 플로우·게임플레이/UI(Game 어셈블리) 안에서만 쓰고, 테스트 어셈블리는 이미 `SevenSteps.Game`을 참조하므로 Contracts에 둘 이유가 없다. 순수 C#(MonoBehaviour 아님)으로 둔다.

## (4) PlayMode 테스트 asmdef: **지금은 만들지 않는다**
EditMode + 가짜 로더 + `smoke`/`smoke-webgl`로 S0~S4를 덮을 수 있다. PlayMode는 씬 빌드 목록·배치 시간이 들고, 지금은 라이선스 문제로 배치도 못 돈다. S3 뒤에 EditMode로 못 잡는 회귀가 나오면 아키텍트가 `SevenSteps.Tests.PlayMode`를 만든다.

## (5) 8장 문장: 수정함
"`Services.Has<AppFlow>()`로 판단" → "이번 Play에서 부트스트랩이 돌지 않았으면", 도메인 리로드 꺼짐 근거 추가.

## 기타
- 5장 공통 완료 조건의 "smoke(리플레이 4개)"는 실제 7개다.
- 씬 소유: 7lucky 29b1f4a README 소유권 표에서 `Scenes/**`·`Game/App/**`가 씬 플로우로 정해졌다. 아키텍처 8장 표의 소유자 열은 아키텍트가 이에 맞춰 고쳤다.
- S3의 `GameController` 분리는 두 담당이 같은 파일을 고치게 된다. 먼저 씬 플로우가 흐름 부분을 빼는 커밋을 합치고, 그 뒤 게임플레이/UI가 보드 부분을 정리하는 순서로(동시 작업 금지).
- 텔레메트리 시점 보존(5.1) 좋다. `level_start.via` 골든 외에 씬을 넘는 `level_leave`는 S3 EditMode 테스트로 고정하는 안 그대로 진행.

---

## S0-1 검토: feat/scene-flow 5fdaf49 (Services.cs, 2026-11-09)

**결론: 승인.** 8장·9장 계약과 위 승인 조건 그대로다.
- `Services.ResetForPlay()`: `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, `map.Clear()`. `Clear()`는 테스트용으로 남김. 공개 API 추가뿐이고 기존 호출 영향 없음.
- 직접 Play 판단: `Bootstrap.HasRunThisPlay`(Run 끝에서 true, 같은 훅으로 false). `GameController`·`TitleScreen`이 `Services.Has<AppFlow>()` 대신 이것을 본다(8장 문장과 일치). `Run`이 중간에 던지면 플래그가 false로 남아 다음 진입에서 다시 시도한다(맞는 동작).
- 런타임 정적 필드 전수 확인: `Services.map`, `Bootstrap.HasRunThisPlay`, `LevelCatalog`(cached/source/minBodies), `GameController.FirstEntryPending`, `Ui.Font`/`circle` 모두 초기화됨. 나머지는 `static readonly` 상수 표(`Messages`)와 매번 비우는 임시 목록(`BoardView` Ints)이라 문제 없음.
- 테스트: `PlayResetTests`(5개 타입에 훅이 정확히 하나)는 리플렉션이지만 에디터 전용 테스트라 9장 "리플렉션 금지"(런타임·스트리핑)와 무관. 새 정적 보유 타입을 만들면 이 목록에 추가할 것.

## S1 검토: feat/scene-flow 923a1a6 (LaunchOptions, Services, 2026-11-09)

**결론: 승인.** 조건(읽기 전용 속성, `TakeReplay`, `-stageselect`/`-credits` 흡수, `Warnings` 목록 + 부트스트랩 로그, 텔레메트리 필드 읽기 호환, 중복 등록 예외) 모두 지켰다. 11장·변경 기록 반영함.
- `LaunchOptions.Parse`: 알 수 없는 인자는 `continue`라 Unity 자체 인자(`-batchmode`, `-logFile -`, `-screen-width 1280`)와 섞여도 안전. 값이 `-`로 시작하면 "값 없음" 경고 — 예전에는 `-replay`만 그렇게 했고 `-level -3` 같은 값도 이제 경고로 버려진다(맞음).
- `UseEditorStart`: 공유 객체를 바꾸는 예외적 메서드지만 에디터 직접 Play 한 곳만 쓰고, 실행 인자 우선이 명시돼 있다. 인자 오류(`level < 1`)는 코드 쪽 실수라 예외 — 9장과 맞다.
- `Bootstrap.Run`: 경고 로그 → `Services.Clear()` → 등록 순서. 중간에 던진 Run 뒤 재시도도 `Clear`로 깨끗하다.
- 사소: `Warnings`가 내부 `List`를 `IReadOnlyList`로 그대로 내보낸다(형 변환하면 수정 가능). 지금은 문제 없음.
- 다른 에이전트 영향: `LaunchOptions` 필드에 대입하던 코드는 컴파일 오류가 난다. 7lucky 기준 대입은 `GameController` 두 곳뿐이고 이 커밋이 고쳤다. 다른 브랜치(feat/view 등)에서 새로 대입하면 `UseEditorStart`/`TakeReplay`로.

## S2 검토: feat/scene-flow f08f4d6 (AppFlow, ISceneLoader, 2026-11-09)

**결론: 승인.** 승인 조건(로더 null 금지, `stageCount >= 1`, 범위 예외, 기존 전이 표 안의 Back, `Loaded`는 `SceneLoader`가 `sceneLoaded`로, `Entered` 없음) 모두 지켰다. 11.1·11.2·변경 기록 반영.
- `AppFlow`: 검사 순서가 맞다(범위 → 로드 중 → 전이 표, 실패 시 상태·`Stage` 그대로). `loadingScene`을 `Load` 전에 세워 가짜 로더의 동기 `Loaded`도 처리. 다른 씬 이름의 `Loaded` 무시.
- `SceneLoader`: 대기 중에만 `sceneLoaded` 구독, 받으면 해제 → Play 사이 누수 없음. `Current`는 활성 씬 이름이라 로드 요청 직후에는 이전 씬이지만 그동안 `Go`가 막히므로 문제 없음.
- 위험(문서화함): 씬 로드가 끝나지 않으면 `IsLoading`이 계속 참이라 흐름이 멈춘다. 빌드 목록 누락은 `SceneFlowValidator`가 막는다. S5 비동기 로더에서도 실패 시 `Loaded`를 못 내는 경로가 없게 할 것.
- `Changed`가 `IsLoading` = 참일 때 나가므로 떠나는 씬의 구독자가 그 안에서 `Go`를 부르면 예외 — 의도대로(진입 경로 하나).
- `EntryTracker`: 정적 플래그 제거, 인자 검사 단정적. `Services`에 Game 타입을 등록하는 것은 로케이터 규칙(조회는 씬 루트만)과 맞다.
- `Bootstrap.FirstStage`: 실행 인자·URL은 외부 입력이라 경고 + 마지막 스테이지(9장 규칙과 같음).
- 공용 API 영향: `AppFlow` 생성자·`ISceneLoader` 멤버 추가로 다른 브랜치의 가짜 로더·직접 생성 코드는 컴파일 오류. 리드 고지.

## S3 검토: feat/scene-flow f8c875e (83a55d6, 70ccc03, 0fc35a3, 18029f4, e83bb64; 2026-11-09)

**결론: 승인.** 9장 MVC·단정 규칙, 텔레메트리 시점, 씬 참조 모두 문제 없음. 코드 읽기(테스트·스모크는 씬 플로우 보고).

- **GUID 유지**: `GameController.cs`·`TitleScreen.cs`는 같은 파일·같은 `.meta`로 **씬 루트 클래스로 남는다**(이름·GUID 그대로라 `Game.unity`/`Title.unity`의 컴포넌트 참조와 인스펙터 값 `assets`/`config`/`startLevel`/`replay`/`overrideCamera` 유지). `BoardController`는 루트가 `AddComponent`로 붙이므로 씬 파일 변경 없음. 새 파일은 모두 `.meta` 동반, 씬 파일 변경 없음.
- **MVC**: `GameController` = 조립(Services는 여기서만), `BoardController` = 보드 진행(`AppFlow`를 모름, `IStageBoard`로 `Won`/`WinPresented`/`Lost`/`Recovered`만 냄), `GameFlowController`/`TitleFlowController` = 순수 C# 흐름. 뷰(`HudView`, `ResultView`, `TitleView`/`StageSelectView`)는 이벤트만 내고 `Services`·`AppFlow`·컨트롤러를 모른다. `HudView`가 받는 것은 코루틴용 `MonoBehaviour`와 `TileSet`(지난 권장 `SetBodyLook` 대신 생성자 주입으로 해결).
- **단정적**: 루트 `Awake`에서 `assets`/`config`/MainCamera/TileSet 없으면 예외, 컨트롤러 생성자 null 검사, `LoadStage` 범위·via 검사, 결과 화면 `ResultModel` 생성자 검사. 지금 할 수 없는 흐름 입력은 `CanGoToGame`/`CanBack`/`IsLoading`으로 드러내 무시. 저장 데이터·솔버 값이 범위 밖이면 0(모름)으로(외부 입력 예외 규칙).
- **텔레메트리(5.1)**: `StageLeaving` → `session.Load` → `StageEntered(via)` 순서가 예전 `GameController.EnterStage`와 같다. via는 `EntryTracker.TakeVia()`가 `Enter`에서 한 번. 씬을 넘는 `level_leave`는 예전처럼 `BoardController.OnDestroy` → `TelemetryRecorder.Dispose` → `PendingLeaveState` 경로 그대로. 클리어 저장은 `Won`(HUD 갱신 전), 결과 패널은 `WinPresented`에서 `Go(Result)` — 7lucky 동작과 같다.
- **흐름**: 다른 씬으로 가는 `Changed`는 `IsLoading`이라 무시하고 새 씬 루트의 `Start`가 진입(진입 경로 하나, 11.1). Result에서 R = 다시 하기(`TakesCommand`), 로드 중 보드 입력 차단.

### 남은 것 (막지 않음)
- `GameController.LevelIndex`/`Levels` 공개 속성은 아트 프리뷰 `RuinsHengeEnvironment`가 `FindFirstObjectByType<GameController>`로 읽기 때문에 남았다. 12.8 `IStageEnvironment.Fit`으로 옮기면 지운다.
- Esc·설정 패널 처리가 아직 `GameController.Update`/`TitleScreen`에 있다 → S4(`OverlayStack` + `BackNavigator`).
- `BoardController`의 문구 표·힌트·사운드 정리는 게임플레이/UI 몫(리드 배분, 동시 수정 금지 순서대로).

## S4 검토: feat/scene-flow 614236b (753fc60, 2026-11-09)

**결론: 승인.** 코드 읽기(테스트는 씬 플로우 보고).
- `OverlayStack`(순수 C#, `Game/Flow/` — 위치 결정대로): 열린 것 다시 열기·맨 위가 아닌 것 닫기는 예외, 닫기 버튼 구독을 스택이 관리해 해제 누락 없음. "열려 있나"의 기준이 스택 하나뿐(`Count`).
- `BackNavigator`: Esc = 맨 위 오버레이 닫기, 없으면 흐름 Back(`TryGetBackTarget`). Esc 처리가 루트 `Update` 한 곳. 로드 중에는 루트가 먼저 돌아간다.
- 오버레이 중 보드 입력 차단(`InputBlocked = IsLoading || overlays.Count > 0`), `Time.timeScale` 안 씀.
- `SettingsPanel` = `IOverlay` 뷰(이벤트만, `Services` 조회 제거), `SettingsController`가 바꾸고 저장. 바꿀 때마다 저장은 PlayerPrefs 쓰기가 버튼 클릭당 한 번이라 문제 없음.
- `ChapterEntering(from, to, stage)`: 보드 로드 전에 한 번, 첫 진입은 `from` = null. 12.8 챕터 프리셋 적용과 전환 카드가 여기 붙는다.

### 후속
- **아키텍트 할 일 끝냄**: `SaveSystem` 명령 `SetMasterVolume`/`SetSfxVolume`/`SetTelemetryChoice`/`TelemetryOn`(11.3, feat/architecture). `SettingsController`는 필드 직접 수정 대신 이것을 쓰도록 다음 커밋에서 바꿀 것(`Step`의 반올림·자르기·playerId 지우기가 그대로 들어 있다).
- `ChapterEntering`의 챕터 비교는 문자열 그대로라 `""`(챕터 없음 = 첫 챕터)와 첫 챕터 id(`"moss"`)가 다르게 보인다. `chapters.json`이 생기면 `ChapterFileDto.Get(id)`로 정규화한 id를 비교할 것(6.4).

## S6 검토: feat/scene-flow 09a07b3 (07927a9 UiTheme, 0b3d260 패널 프리팹, 2026-11-09)

**결론: 승인.** 코드 읽기(테스트는 씬 플로우 보고).
- `UiTheme`(SO, Game 어셈블리): 글꼴·색·패널 프리팹. `Ui.Theme` 세터가 null·글꼴 없음을 바로 예외로, 게터도 미설정이면 예외(fail fast). 정적 `theme`은 기존 `Ui` 리셋 훅이 비운다(`PlayResetTests` 대상 유지). `Ui.Ink` 등 기존 호출 모양은 그대로라 코드 생성 UI 영향 없음.
- 패널 프리팹(`UI/Prefabs/SettingsPanel`, `ResultPanel`): 뷰가 직렬화 참조를 `Validate`로 `OnValidate`(에디터 오류)·`Awake`(예외)·`SceneFlowValidator`(빌드 전)에서 검사 — 9장 그대로. `Create`는 테마에 프리팹이 없으면 예외.
- `UiBuilder`: 없을 때만 만든다(있으면 그대로 둠) → 첫 생성 후 프리팹 손편집이 덮이지 않는다. 좋다.
- `SettingsController`가 아키텍트 `SaveSystem` 명령(`SetMasterVolume`/`SetSfxVolume`/`SetTelemetryChoice`/`TelemetryOn`)을 쓴다 — S4 후속 해결.
- 사소: `SettingsController.Saved`의 `AudioListener.volume = ...`은 `GameAudio.ApplyMaster`와 같은 일 — 한 곳(`ApplyMaster`)으로.
- 소유권: `UI/Prefabs/**`는 README상 게임플레이/UI(`UI/**`). 이번 첫 생성은 씬 플로우가 했으니 이후 내용 편집은 게임플레이/UI, 테마 값은 아트로(리드 확인).

### 결정: 챕터별 UiTheme는 `ChapterPreset`에 넣지 않는다
1. **의존 방향**: `ChapterPreset`은 Contracts, `UiTheme`은 Game(패널 프리팹 타입 `SettingsPanel`/`ResultView`를 참조)이다. Contracts가 Game 타입을 참조할 수 없고, `UiTheme`을 Contracts로 옮기면 뷰 타입까지 끌려온다(2장 순환 금지).
2. **일관성**: 글꼴·버튼·패널 모양은 게임 전체의 정체성이다. 행성마다 바뀌면 조작 UI가 낯설어진다. 아트 바이블의 테마는 하나다.
3. **비용**: 코드 생성 HUD는 테마를 생성 시점에 읽어 챕터 전환 때 다시 만들어야 한다.

대신: 챕터 분위기는 보드·조명·하늘(12.8)과 챕터 전환 카드(`ChapterEntering` 오버레이, 문구는 `ChapterInfo`)가 맡는다. 챕터마다 UI 강조색이 꼭 필요해지면 `ChapterPreset`에 `Color uiAccent` 하나를 추가하고(Contracts 타입만), HUD가 `ChapterEntering`에서 그 색만 바꾸는 방식으로 한다(아키텍트에게 요청).

## S5 검토: feat/scene-flow 1f9f462 (AppFlow 로드 실패 되돌림, 페이드 SceneLoader, 2026-11-09)

**결론: 승인.** S2 메모(로드 실패 시 `IsLoading` 고착) 해결. 11.1·8장·변경 기록 반영.
- `AppFlow.Go`: `Load` 예외 시 `loadingScene`·`State` 되돌리고 다시 던짐, `Changed` 없음. `GoToGame`은 `Stage`도 되돌림. 테스트 있음.
- `SceneLoader`: `CanStreamedLevelBeLoaded`로 `Load`에서 사전 거부(코루틴 시작 전 → 되돌림 경로가 맞게 동작). 정상 경로 페이드 아웃 → `LoadSceneAsync` → `sceneLoaded` → `Loaded` → 한 프레임 → 페이드 인. `sceneLoaded`는 대기 중에만 구독. `unscaledDeltaTime` 사용.
- DDOL `SceneFader`: 텔레메트리 러너는 텔레메트리가 꺼지면 없으므로 별도 오브젝트가 맞다. 8장 "DDOL은 러너 하나" 문구를 두 개로 고쳤다. `SceneLoader`는 부트스트랩마다 새로 만들어지고 Play 끝에 DDOL도 사라져 도메인 리로드 꺼짐에서도 중복 없음.
- 사소: `LoadSceneAsync`가 null인 비정상 경로는 `Loaded`를 내서 흐름은 풀리지만 `AppFlow.State`는 새 상태, 씬은 이전 씬으로 어긋난다. 오류 로그가 크게 남고 사전 검사 뒤라 사실상 안 일어나므로 둔다.
- 텔레메트리: 씬을 넘는 `level_leave`가 페이드 아웃(0.15초)만큼 늦게 나간다. 순서·값은 같고 시간만 조금 늘어남(골든은 씬을 안 쓰므로 영향 없음).

## GlyphCheck 검토: feat/scene-flow 942995f (2026-11-09)

**결론: 승인.** 에디터 전용(`Scripts/Editor/SceneFlow`), `SceneFlowValidator` 안에서 빌드 전에 돈다.
- 기준이 `Font.HasCharacter`가 아니라 TTF cmap(형식 4·12)이라 동적 글꼴의 "있다고 하는데 WebGL에서 안 그려짐"을 잡는다. 예외 허용은 WebGL에서 확인한 `U+00B7` 하나뿐(근거 주석).
- 검사 대상: 테마 글꼴을 쓰는 프리팹 `Text`, `levels.json` 이름·힌트, `UI/Credits.txt`, `Game` 런타임 코드와 `UiBuilder`의 비ASCII 문자열 리터럴(주석 제외). 글꼴 파일을 못 읽으면(공유 폴더 미연결) 오류 — fail fast.
- **추가 필요(데이터가 생기면)**: `levels.json`의 `story`(6.4), `Data/chapters.json`의 `planet`·`concept`·`intro`. 챕터 전환 카드·결과 문구에 나온다.

## 검토: feat/scene-flow 4d6d373 (9eab620 모든 UiTheme 검사, 58b311f 챕터 진입 카드, 2026-11-09)

**결론: 승인.**
- `SceneFlowValidator`가 `Assets/_Project`의 모든 `UiTheme`을 검사 — 아트 테마(`RuinsHengeTheme`) 권장 반영.
- `LevelCatalog`: `chaptersJson`을 읽고 로드 때 `levels.json`의 모든 `chapter` id를 `ChapterFileDto.Get`으로 확인(모르는 id면 예외, fail fast). `ChapterKeyOf`가 `""`와 첫 챕터 id를 같은 키로 정규화 → S4 후속(챕터 비교) 해결. `GameFlowController`가 이 키로 `ChapterEntering`을 낸다.
- `ChapterIntroController`(순수 C#): 챕터가 바뀌면 카드를 오버레이로 띄움(보드 입력 대기, Esc·클릭·타이머로 닫힘). 카드·데이터가 없으면 아무 일도 안 함(테마에 카드가 없는 것은 선택 사항으로 명시). `ChapterCardModel` 생성자 검사.
- `ChapterCardView`: 직렬화 참조 `Validate`, 타이머는 `unscaledTime`, 닫히면 비활성이라 `CloseRequested`가 한 번만 나간다.
- **`OverlayStack.Close` 규칙 변경(S4 단정 완화)**: 맨 위가 아니어도 열린 것이면 닫을 수 있게 됨. 근거가 맞다 — 설정이 카드 위에 열린 동안 카드 타이머가 끝나면 카드가 스스로 닫혀야 한다. 열려 있지 않은 것을 닫으면 여전히 예외. `Pop`/Esc는 계속 맨 위만. 테스트 갱신됨.
- 남은 것(R1 데이터 때, 씬 플로우): GlyphCheck에 `story`·`chapters.json`(`planet`/`concept`/`intro`) 추가. 13장(다국어) 뒤에는 언어별로.

## 검토: feat/scene-flow 481e953 (12.8 챕터 연결: 방향광 3개, UseChapters, chapters.json, GlyphCheck story·chapters, 2026-11-09)

**결론: 승인.**
- `Game.unity`: 방향광 3개(Key 그림자 Soft, Fill·Rim 그림자 없음), 예전 방향광 없음. `GameController` 필드 `keyLight`/`fillLight`/`rimLight`, 없으면 `Awake`에서 예외 + 검증기. 생성기 `GameSceneLights`(씬 플로우).
- `board.UseChapters(new ChapterStage(cam, key, fill, rim, transform), stage → chapters.json artPreset → AppConfig.Chapter, cfg.tileSet)` — 12.8 순서 그대로. `AppConfig.chapters`가 비면 null이라 지금 모습 유지.
- `GameAssets.chaptersJson` 연결, GlyphCheck가 `story`·`planet`·`intro`까지(4d6d373 남은 것 해결). `concept`는 화면에 안 나오므로 제외 — 나중에 쓰면 추가.
- 주의(아키텍트가 검사 켬): 아트 `Chapter01.asset`의 id는 `moss-ruins`인데 `chapters.json`의 `artPreset`은 `moss`다. `AppConfig.chapters`에 넣는 순간 `TileSetValidator`가 오류를 낸다 → 아트가 id를 `moss`로 하거나 레벨 디자인이 `artPreset`을 맞춘다.

## 검토: feat/scene-flow 7028a00 (3별 판정·저장, ResultModel.Stars/BestStars, e173f86 위, 2026-11-10)

**결론: 승인.** 6.5 저장 담당 부분 완료.
- `OnWon`: `Stars.Of(Stars.BodiesUsed(s), MinBodies(s), attempt.Hints, Undos, Restarts)` → 1..3이면 `MarkCleared(level, bodies, stars)`, 0(최소 몸 모름)이면 별 없이 `MarkCleared(level, bodies)`. `minBodies`가 이 스테이지 몸 수를 넘으면 0(외부 데이터 불일치 = 모름) — 예전 결과 화면 규칙과 같다.
- `ResultModel`: `Stars`·`BestStars` 0..3 검사, 실패에는 별 없음, `BestStars >= Stars`(클리어를 먼저 저장하므로 성립) 단정. 기본값 0이라 기존 호출 호환.
- `BodiesUsed`가 코어 `Stars.BodiesUsed`로 통일.
- 사소: `clearStars`는 `OnWon`에서만 갱신된다. 지금 흐름(Won → WinPresented → Result)에서는 맞다. `Enter`에서 0으로 돌려 두면 다른 경로가 생겨도 이전 스테이지 별이 보이지 않는다.
- 남은 것: restarts 기준 통일(telemetry.js + `TelemetryTracker` + 골든)은 리드 결정대로 다음 묶음.

## 검토: feat/scene-flow 8481e5c (f4ce5cc 포함: Localizer 등록, 설정 언어 토글, UiTheme 13.4 글꼴, 2026-11-10)

**결론: 승인.** 13.3·13.4 그대로.
- `Bootstrap`: `Localizer.FromAssets(assets.strings, launch.Language ?? save.Language)`를 텔레메트리보다 먼저 등록(텔레메트리가 `Current`·`Changed`로 `lang`을 따름). `-lang`은 저장하지 않음.
- `SettingsController`: 토글 = 표가 있는 다음 언어(Ko→En→Ko), `save.SetLanguage` 후 `localizer.Set`(→ `Changed`로 뷰 갱신). `-lang` 덮어쓰기는 플레이어 선택으로 바뀐다(맞음).
- `SettingsPanel`: 언어 이름은 표 문자열이 아니라 `NativeName`(각 언어 고유 이름)을 그 언어 본문 글꼴로, `Redraw`에서도 갱신. 토글·라벨은 선택 참조(프리팹에 생길 때까지).
- `UiTheme`: `FontRole{Title, Body, Number}`, `LanguageFonts{language, title, body, 크기·줄간격(0 = 1)}`, 테마 공통 `number`, `FontsFor`/`FontFor`(없으면 예외), `Validate`가 언어 누락·중복·빈 글꼴·음수 크기를 검사. 두 테마(Default, RuinsHenge)에 값.

### 권장 (사소)
- 저장된 언어(`save.Language`)는 플레이어 데이터라 외부 입력이다. 그 언어의 표가 빌드에 없으면 지금은 `Localizer` 생성자가 예외를 던져 부트가 멈춘다. `assets.strings`에 없으면 ko로 + 경고(9장 외부 입력 규칙). 지금은 두 표가 다 있어 실제로는 안 일어난다.
- `NativeName`("한국어", "English")은 GlyphCheck가 테마 기본 글꼴(Jua)로만 검사한다. 언어별 GlyphCheck(13.6)에서 "각 언어 이름은 그 언어 본문 글꼴"로 검사 대상에 넣을 것.
