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
