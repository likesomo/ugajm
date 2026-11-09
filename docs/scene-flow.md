# 일곱 걸음 씬 구조와 화면 흐름 — 조사·진단·제안 (1단계)

담당: 씬 플로우 엔지니어 (`feat/scene-flow`). 브리프: `agents/scene-flow.md`.
상태: **사용자 승인(2026-11-08), 2단계 구현 중. 진행 상태는 5.2.** 아키텍트 검토(방향 승인, S0~S2 조건부 승인, 기록 `docs/reviews/2026-11-08-scene-flow-proposal.md`) 반영 내용은 8장.
진단 기준 코드: 로컬 `7lucky` f367750 (feat/view 5a2180a "Esc closes settings" 포함). 줄 번호는 이 커밋 기준이다. 5a2180a는 아직 origin/7lucky(29b1f4a)에 없다. 그래서 지금 브랜치에서는 `GameController.cs` 222줄 이후가 5줄, `TitleScreen.cs` 84줄 이후가 4줄, `HudView.cs`는 약 5줄 앞당겨진다.

---

## 0. 요약

- 지금 구조(Boot → Title → Game 세 씬, 순수 C# `AppFlow`, 서비스 로케이터, DDOL 오브젝트 하나)는 **방향은 맞다**. 갈아엎지 않는다.
- 문제는 구조의 경계가 코드에 강제되지 않는다는 점이다. 흐름 판단, 텔레메트리 진입 경로, 사운드, 문구, 힌트 계산, UI 생성이 `GameController` 한 클래스에 섞여 있다. 흐름 상태는 정적 변수(`FirstEntryPending`)와 씬 진입 순서에 숨어 있다.
- **가장 급한 결함**: 이 프로젝트는 에디터에서 도메인 리로드를 끈 상태다(`ProjectSettings/EditorSettings.asset:29-30`, `m_EnterPlayModeOptions: 1`). 그런데 정적 상태(`Services`, `LevelCatalog`, `GameController.FirstEntryPending`, `Ui.Font`)를 Play 시작 때 초기화하는 코드가 없다. 그래서 두 번째 Play부터 이전 실행의 `AppFlow`·`LaunchOptions`·텔레메트리가 그대로 남는다(3.1).
- 제안은 작은 단계 6개다. 각 단계가 끝나도 게임은 돌아간다. 잼 안에서 할 것은 S0~S4(정적 초기화, 검증기, `AppFlow` 단정화, 흐름 컨트롤러 분리, 오버레이·뒤로 가기 스택)이고, 페이드 전환(S5)은 여유가 있으면 한다. UI 프리팹화는 게임플레이/UI 담당과 나눠서 한다(S6, 일부는 잼 이후).
- **채택하지 않는 것**: Additive 영구 씬, UI Toolkit 전환, DI 컨테이너, ScriptableObject 이벤트 채널, Addressables. 이유는 2장.

---

## 1. 코드 규칙 (구조 원칙, 사용자 지시 2026-11-08)

씬 흐름 코드와 이 문서의 모든 제안이 따르는 규칙이다. 9장에 넣을 문안은 1.4에 있다.

### 1.1 객체지향

- 클래스 하나는 책임 하나를 맡는다. `MonoBehaviour` 하나가 입력·흐름·사운드·저장·UI 생성을 함께 하지 않는다.
- 상태는 캡슐화한다. 다른 클래스가 읽는 값은 `{ get; private set; }`이나 메서드로 연다. 공개 가변 필드는 Unity 직렬화 필드(인스펙터 연결)와 `JsonUtility` DTO(`ProgressData`, `SettingsData`)에만 쓴다.
- 의존은 생성자로 받는다. 외부 시스템(씬 로드, 저장소, 시계, 텔레메트리)은 인터페이스로 받는다. 예: `ISceneLoader`, `ISaveStore`, `ITelemetry`.
- 상속보다 조합을 쓴다. 씬 루트 `MonoBehaviour`가 순수 C# 컨트롤러와 뷰를 만들어 연결한다(조립만 하는 composition root).
- `Services`(서비스 로케이터)는 **씬 루트에서만** 읽는다. 컨트롤러와 뷰는 필요한 것을 생성자나 `Bind(...)`로 받는다. `Services.Get`을 뷰 안에서 부르지 않는다.

### 1.2 MVC

| 층 | 무엇 | 규칙 |
|---|---|---|
| Model | `SevenSteps.Core`(규칙), `GameSession`, `AppFlow`(화면 상태), `SaveSystem`(진행·설정), 새로 만드는 `OverlayStack`·`EntryTracker` | 순수 C#. 변경은 C# 이벤트로 알린다. View를 모른다. 허용되지 않은 명령은 예외 |
| View | `BoardView`, `HudView`, 각 화면과 패널(Title, StageSelect, Settings, Credits, Result) | 표시만 한다. 사용자 조작은 이벤트(`event Action ...`)로 낸다. Model을 직접 바꾸지 않는다. 흐름·규칙을 판단하지 않는다 |
| Controller | 보드 진행(`GameController` → 나눔, 게임플레이/UI 소유), 화면 흐름(`TitleFlowController`, `GameFlowController`, `BackNavigator`, 씬 플로우 소유) | View 이벤트와 입력을 받아 Model 명령을 부른다. Model 이벤트를 받아 View를 갱신한다 |

- 참조 방향(아키텍트 조정): **Controller → View, Controller → Model, View → Model은 읽기 전용 데이터만.** View는 컨트롤러를 모른다. 조작은 이벤트로만 낸다(지금 `HudView(GameController game, ...)`는 이 규칙을 어긴다, 3.2). Model은 View를 모른다. 허용된 예외는 하나다. `GameSession`(Model)은 Contracts 인터페이스 `IBoardPresenter`로 연출을 부른다(architecture.md 4장 입력 락 계약).
- 보드 진행 컨트롤러와 화면 흐름 컨트롤러는 다른 클래스다. 보드 컨트롤러는 `AppFlow`를 부르지 않고 "클리어 연출 끝남" 같은 사실만 이벤트로 낸다. 흐름 컨트롤러가 그 이벤트를 받아 `AppFlow.Go(Result)`를 부른다.
- 아트 테마를 바꾸면 View 프리팹과 `UiTheme` 에셋만 바뀐다. 컨트롤러·모델 코드는 바뀌지 않는다.

### 1.3 단정적(assertive) 프로그래밍

1. **공개 메서드의 전제 조건**: 시작 부분에서 검사하고, 어기면 던진다.
   - 인자가 틀림 → `ArgumentNullException` / `ArgumentOutOfRangeException` / `ArgumentException`
   - 지금 상태에서 할 수 없는 호출 → `InvalidOperationException` (예: 허용되지 않은 `AppFlow` 전이, 로드 중 전이, 이미 등록된 서비스 다시 등록)
   - **게임 규칙상 안 되는 명령(벽으로 이동, 선택 대기 중 이동 등)은 예외가 아니다.** `Rules.Dispatch`가 결과(`Bump`, `Feedback`)로 처리한다(core.js 골든). 전제 조건 예외를 규칙 코드로 넓히지 않는다.
   - WebGL 빌드는 `webGLExceptionSupport: 1`(명시적 예외만)이다. `NullReferenceException` 같은 암묵적 예외는 WebGL에서 잡히지 않는다. 그래서 null은 **먼저 검사해서 던진다**.
2. **내부 불변식**: `UnityEngine.Assertions.Assert`(Game·Contracts 어셈블리). **Core에서는 `Debug.Assert`를 쓰지 않는다.** 플레이어에서 실패해도 아무 일이 없을 수 있기 때문이다. Solver 같은 뜨거운 경로에는 검사를 넣지 않는다. 이 검사는 `UNITY_ASSERTIONS`가 없는 릴리스 빌드에서 빠진다. 그러니 **플레이어에서도 반드시 막아야 하는 것은 1번(예외)으로 쓴다.** Unity 6.6부터 Development Build가 자동으로 Checked 변형을 쓰지 않는다는 점에 주의한다([managed code variants](https://docs.unity3d.com/Manual/managed-code-variants.html)).
3. **직렬화 참조**: "있어야 하는" 인스펙터 필드는 `Awake`에서 검사하고, 비었으면 어떤 필드가 비었는지 이름을 넣어 예외를 던진다(`MissingReferenceException` 또는 `InvalidOperationException`). 에디터에서는 `OnValidate`에서 `Debug.LogError(..., this)`로 바로 표시한다. null을 보고 조용히 `return`하거나 기본 인스턴스를 만들지 않는다.
4. **fail fast**: 잘못된 상태를 기본값으로 덮고 진행하지 않는다. 예외는 **외부 입력** 세 가지뿐이고, 이때도 명시적으로 복구하고 로그를 남긴다.
   - 실행 인자·URL(`?level=99` 등): 범위 밖이면 경고 로그를 남기고 가까운 값으로 고친다. 경고 문구에 원래 값을 넣는다.
   - 사용자 저장 데이터 손상: 기본값으로 시작하고 `Debug.LogWarning`(지금 `SaveSystem` 동작 유지 + 로그 추가).
   - 네트워크(텔레메트리 전송 실패): 재시도 규칙대로 처리한다(`docs/telemetry.md` 4장). 게임을 멈추지 않는다.
   - 사용자 입력(키, 클릭)은 예외가 아니다. 지금 할 수 없는 입력은 컨트롤러가 **명시적으로** 무시한다(`if (!flow.CanGo(...)) return;`처럼 조건이 코드에 드러나게).
5. **빌드 전 검증**: 씬·프리팹·`AppConfig`·`TileSet`의 필수 연결은 에디터 검증기가 빌드 전에 잡는다. `TileSetValidator`와 같은 형식(메뉴 + 배치 메서드, 오류면 exit 1)으로 `SceneFlowValidator`를 만든다(5장 S0).

### 1.4 `docs/architecture.md` 9장에 넣을 문안 (아키텍트에게 요청 → 조정해서 반영됨)

아키텍트가 아래 문안을 조정해 9장에 넣었다(feat/architecture). **기준은 9장이다.** 조정한 점: 참조 방향(1.2), `IBoardPresenter` 예외, 규칙 거부는 예외가 아님, Core `Debug.Assert` 금지, WebGL 예외 지원, 적용 범위(새 코드와 손대는 코드, 기존 코드는 5장 단계대로). 아래는 제출한 원안이고 기록으로만 남긴다.

> - **구조 원칙(사용자 지시 2026-11-08)**: 객체지향, MVC, 단정적 프로그래밍. 자세한 규칙은 `docs/scene-flow.md` 1장.
> - **MVC**: Model(Core, `GameSession`, `AppFlow`, `SaveSystem`)은 순수 C#이고 C# 이벤트로 변경을 알린다. View(`MonoBehaviour`, 프리팹)는 표시만 하고 조작은 이벤트로 낸다. Model을 바꾸거나 `Services`를 읽지 않는다. Controller는 View 이벤트를 Model 명령으로 바꾸고 Model 이벤트로 View를 갱신한다. 보드 진행 컨트롤러와 화면 흐름 컨트롤러는 나눈다. 의존은 View → Controller → Model 한 방향.
> - **조립**: 씬마다 루트 `MonoBehaviour` 하나(`*SceneRoot`)가 `Services`에서 한 번 꺼내 컨트롤러·뷰를 생성자로 연결한다. 그 밖의 클래스는 `Services`를 부르지 않는다.
> - **전제 조건**: 공개 메서드는 인자와 상태를 먼저 검사하고 `ArgumentException` 계열이나 `InvalidOperationException`을 던진다. 내부 불변식은 `Assert`(릴리스에서 빠짐). 필수 직렬화 참조는 `Awake`에서 검사해 예외, `OnValidate`에서 오류 로그. null을 조용히 넘기거나 기본값으로 덮지 않는다. 예외는 외부 입력(실행 인자·URL, 저장 데이터 손상, 네트워크)뿐이고, 이때는 복구하고 경고 로그를 남긴다.
> - **정적 가변 상태**: 런타임 정적 필드는 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`로 초기화한다. 이 프로젝트는 에디터 도메인 리로드가 꺼져 있다.
> - **빌드 전 검증**: 씬·프리팹·`AppConfig`·`TileSet` 필수 연결은 에디터 검증기(`TileSetValidator`, `SceneFlowValidator`)로 확인한다.

---

## 2. 조사: 이 게임에 맞는 씬·흐름 구조

전제: 작은 격자 퍼즐, 스테이지 17개 이상(한 씬에서 데이터로 바꿈), **WebGL 우선**(itch.io), 잼 일정, 에이전트 여러 명이 병렬로 작업(씬 파일 하나는 한 명만 수정).

### 2.1 Bootstrap 씬 + `DontDestroyOnLoad` vs 영구 씬 + Additive

| | Bootstrap + DDOL (현재) | 영구(Persistent) 씬 + Additive |
|---|---|---|
| 방식 | 빌드 0번 Boot가 서비스를 만들고 `Single`로 다음 씬을 연다. 앱 전체 오브젝트는 DDOL | 영구 씬이 항상 로드돼 있고, 화면 씬을 `LoadSceneAsync(Additive)`로 붙였다 뗀다 |
| 장점 | 단순하다. 씬마다 카메라·라이트가 자기 것. 에디터에서 아무 씬이나 Play하기 쉽다(부트스트랩만 직접 호출) | 앱 전체 오브젝트를 씬 파일에 둘 수 있다(인스펙터 편집). 전환 화면을 영구 씬에 둔다 |
| 단점 | DDOL 오브젝트는 루트에만 쓸 수 있고, 다시 로드하면 중복을 직접 막아야 한다 | 활성 씬 관리(`SetActiveScene`, 라이팅 설정은 활성 씬 것을 씀), 카메라·`AudioListener`·`EventSystem` 중복 관리, 같은 씬을 두 번 Additive로 열면 인스턴스가 두 개 생긴다. 에디터 직접 Play 처리가 더 복잡 |
| 참고 | Unity의 ScriptableObject 데모(PaddleBallSO)도 Bootloader 씬을 빌드 0번에 두고, 다른 씬을 직접 Play하면 부트 씬을 강제로 먼저 연다 | Unity Open Project 1(Chop Chop)은 Initialization 씬 + `PersistentManagers` 씬 + SO 씬 데이터 + 로딩 화면 매니저 구조 |

**결론: Bootstrap + DDOL 유지.** 이 게임은 앱 전체 오브젝트가 텔레메트리 러너 하나(전환 페이드를 넣어도 둘)뿐이다. Additive로 얻는 이득(인스펙터 편집)은 작고, 활성 씬·라이팅·중복 카메라 관리라는 새 위험이 생긴다. 잼 이후 오디오 매니저·음악·로딩 화면이 커지면 다시 검토한다.

### 2.2 동기 `LoadScene` vs `LoadSceneAsync` + 전환 화면 (WebGL)

- `SceneManager.LoadScene`은 즉시 로드하지 않고 **다음 프레임에** 끝난다. Unity 문서는 이 반동기 동작이 프레임 끊김을 만들 수 있다며 `LoadSceneAsync`를 대안으로 든다([LoadScene 6000.6](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/SceneManagement.SceneManager.LoadScene.html)).
- WebGL은 스레드가 없어 비동기 로드도 메인 스레드에서 프레임을 나눠 진행한다. 동기 완료 대기(`WaitForCompletion` 등)는 WebGL에서 지원되지 않는다([Addressables 동기 로드](https://docs.unity.cn/Packages/com.unity.addressables%401.21/manual/SynchronousAddressables.html)). 그래서 코루틴으로 `AsyncOperation`을 기다리는 방식만 쓴다. 지금 씬은 작아서(카메라 + 루트 하나) 동기 로드도 끊김이 거의 보이지 않는다.
- **결론**: 지금은 동기 로드를 유지한다. 대신 `ISceneLoader` 뒤로 감춰 두었으므로 S5에서 `FadeSceneLoader`(검은 화면 0.15초 페이드 아웃 → `LoadSceneAsync` → 페이드 인)로 바꿀 수 있다. 이렇게 바꾸면 **로드 중 상태**가 생긴다. 그래서 `AppFlow`가 `IsLoading` 동안 오는 전이를 거부하도록 S2에서 먼저 계약을 단단히 한다.

### 2.3 이름 로드 vs 경로/빌드 인덱스

- `LoadScene`은 이름 또는 빌드 목록에 보이는 경로를 받는다. 이름만 주면 목록에서 **처음 맞는 씬**을 연다. 이름이 같은 씬이 여럿이면 경로를 써야 한다([LoadScene 문서](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadScene.html)). 빌드 인덱스는 목록 순서를 바꾸면 달라진다([Scene.buildIndex](https://docs.unity3d.com/kr/ScriptReference/SceneManagement.Scene-buildIndex.html)).
- **결론**: 이름 로드를 유지한다(`AppScenes` 상수). 이름 중복과 목록 누락은 실행 중에 알 수 없으므로 **빌드 전 검증기**가 잡는다: `AppScenes`의 모든 이름이 빌드 목록에 정확히 한 번 있고, Boot가 0번이며, 경로가 `Assets/_Project/Scenes/<이름>.unity`인지 확인한다(S0). 빌드 인덱스는 쓰지 않는다(빌드 목록은 리드 소유라 순서가 바뀔 수 있다).

### 2.4 상태 머신과 화면(패널) 관리, 뒤로 가기, 일시정지

- Unity의 디자인 패턴 e-book(Unity 6판)은 State 패턴, MVC/MVP, UI용 MVP를 다룬다. MVP 튜토리얼은 기존 UI 프레임워크(uGUI 또는 UI Toolkit)를 그대로 View로 쓰고, Model의 상태 변경 이벤트가 Presenter를 깨우는 구조를 권한다([Unity Learn: MVC와 MVP](https://learn.unity.com/course/design-patterns-unity-6/tutorial/build-a-modular-codebase-with-mvc-and-mvp-programming-patterns?version=6.0), [e-book PDF](https://unity3d.jp/wp-content/uploads/2025/07/Level-up-your-code-with-design-patterns-and-SOLID.pdf)).
- 이 게임의 화면은 두 종류다.
  - **화면 상태**(한 번에 하나): Title, StageSelect, Game, Result → 지금의 `AppFlow`. 허용 전이 표가 있는 작은 상태 머신이면 충분하다. 상태 클래스(State 패턴)로 쪼갤 정도로 상태별 행동이 크지 않다.
  - **오버레이**(겹쳐 쌓임): 설정, 크레딧, (나중에) 일시정지 메뉴·확인 창 → 지금은 각 화면이 `IsOpen`을 따로 보고 `Update`마다 Esc를 직접 읽는다. 이것을 **순수 C# `OverlayStack`**(Model)과 **`BackNavigator`**(Controller) 하나로 모은다.
- 뒤로 가기(Esc) 규칙 하나로 통일: 오버레이가 있으면 맨 위 오버레이를 닫는다. 없으면 `AppFlow.BackTarget(state)`로 간다(StageSelect → Title, Game → StageSelect, Result → StageSelect, Title → 없음).
- 일시정지: 턴제 퍼즐이라 시간이 흐르지 않는다. `Time.timeScale`을 쓰지 않는다. "일시정지" = 오버레이가 열려 있으면 보드 입력을 받지 않는 것이고, `OverlayStack.Count > 0`으로 판단한다. 텔레메트리 `idle`은 지금처럼 입력 사이 시간으로 잰다.
- 실패(Lose)는 `AppState`로 만들지 않는다. 되돌리기(Z)로 같은 판을 이어 가므로 보드 세션 상태(`Status.Lose`)이고, 실패 패널은 HUD 내용이다. 클리어(Result)만 화면 상태다. 이 구분을 문서로 고정한다.

### 2.5 코드 생성 UI vs uGUI 프리팹 vs UI Toolkit (아트 테마 교체)

| | 코드 생성 uGUI (현재) | uGUI 프리팹 + 테마 SO | UI Toolkit (UXML/USS) |
|---|---|---|---|
| 테마 교체 | 색·글꼴·배치가 C# 상수(`Ui.cs:11-19`, 각 화면의 `Place(...)` 좌표). 아트가 못 바꾼다 | 아트가 프리팹·스프라이트·`UiTheme` 에셋만 바꾼다. 중첩 프리팹 + 프리팹 변형으로 버튼 하나를 바꾸면 전체에 반영([Nested Prefabs](https://docs.unity3d.com/Manual/NestedPrefabs.html), [Unity Learn: UI 메뉴와 프리팹 변형](https://learn.unity.com/tutorial/challenge-ui-menu-with-prefab-variants-nested-prefabs)) | USS 스타일시트 교체로 전체 테마. 전역 스타일·전환 애니메이션·SVG 지원 |
| 병합 | 코드라 병합이 쉽다 | 프리팹 YAML. 파일 하나는 한 명만 수정하면 된다 | UXML/USS는 텍스트라 병합이 쉽다 |
| 비용 | 0 (이미 있음) | 화면별 이전 작업. 뷰 코드는 남는다(조립 → 참조로 바뀜) | 전면 재작성, 팀 경험 없음. 보드 위 월드 좌표 라벨(`HudView.LabelLayer`)은 uGUI가 맞다 |
| Unity 6 권장 | — | Unity 6.4 문서는 런타임 UI로 uGUI를 권장하고 UI Toolkit을 대안으로 둔다. 3D 공간 UI·Timeline 연동은 uGUI가 낫다([UI 시스템 비교 6000.6](https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/ui-system-compare), [6000.4](https://docs.unity3d.com/6000.4/Documentation/Manual/UI-system-compare.html)) | 화면 겹침 메뉴·HUD에는 장점이 있다 |

**결론: uGUI 프리팹 + `UiTheme` ScriptableObject.** 북두칠성 동서양 퓨전 테마가 확정되면 아트가 프리팹 변형과 테마 에셋만 바꾼다. 이전 순서: 먼저 **뷰 클래스의 공개 면(이벤트 + `Show/Hide/Render(model)`)을 고정**한다(S3). 그다음 한 화면씩 생성 코드를 프리팹으로 옮긴다(S6). 이렇게 하면 프리팹 이전 중에도 컨트롤러는 바뀌지 않는다. TextMeshPro 전환(한글 SDF 아틀라스)은 글꼴 바이너리 문제가 있어 아트와 따로 정한다.

### 2.6 ScriptableObject 설정·이벤트 채널, 서비스 로케이터 vs DI 컨테이너

- **SO 설정**(`AppConfig`, `TileSet`, `GameAssets`): 유지한다. 빌드 설정을 코드 밖에 두고 아트가 바꿀 수 있다.
- **SO 이벤트 채널**: PaddleBallSO는 SO 이벤트 채널을 오브젝트 간 메시지의 중심으로 쓴다([PaddleBallSO 소개](https://unity.com/how-to/get-started-with-scriptableobjects-demo), [6 ways ScriptableObjects can benefit your team](https://unity.com/blog/engine-platform/6-ways-scriptableobjects-can-benefit-your-team-and-your-code), [GitHub](https://github.com/UnityTechnologies/PaddleGameSO)). 디자이너가 인스펙터에서 연결을 바꿀 수 있다는 장점이 있다. 하지만 누가 이벤트를 내고 받는지가 에셋 참조에 숨는다. 그러면 "숨은 의존을 없앤다"는 이번 목표와 반대다. 또 도메인 리로드를 끈 에디터에서는 SO에 남은 구독자 목록도 직접 비워야 한다. **채택하지 않는다.** Model의 C# 이벤트와 생성자 주입으로 충분하다.
- **DI 컨테이너**(VContainer, Zenject 등): 외부 패키지가 필요하다. 리플렉션으로 해석하므로 WebGL 코드 스트리핑에 `link.xml`·`[Preserve]` 관리가 따른다(Unity는 Zenject를 직접 예로 든다: [관리 코드 스트리핑 블로그](https://unity.com/blog/engine-platform/tales-from-the-optimization-trenches-better-managed-code-stripping-with-unity-2020), [WebGL 코드 스트리핑](https://docs.unity3d.com/Manual/webgl-distributionsize-codestripping.html)). 9장도 "리플렉션으로 타입 찾기 금지"다. **채택하지 않는다.** 대신 지금의 `Services` 로케이터를 **씬 루트 전용**으로 좁히고, 그 아래는 생성자 주입(수동 DI)을 쓴다.

### 2.7 도메인 리로드를 끈 에디터 (이 프로젝트 설정)

- 도메인 리로드를 끄면 Play 진입이 빨라진다. 대신 **정적 필드와 정적 이벤트 구독이 Play 사이에 남는다.** 런타임 코드는 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 정적 메서드에서 초기화해야 한다([Domain Reloading, Unity 6](https://docs.unity.cn/Manual/domain-reloading.html), [2019.4](https://docs.unity3d.com/ru/Manual/DomainReloading.html)).
- 이 프로젝트는 `m_EnterPlayModeOptions: 1`(도메인 리로드 끔)인데 초기화 메서드가 하나도 없다(`grep RuntimeInitializeOnLoad` 결과 0건). 3.1에 영향을 적었다.

### 2.8 기타 확인한 Unity 6 사실

- `DontDestroyOnLoad`는 루트 오브젝트에만 효과가 있고, 자식 Transform도 함께 남는다. 공식 예제도 다시 로드할 때 생기는 중복을 직접 지운다([DontDestroyOnLoad 6000.6](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/object/dontdestroyonload)). 페이드 캔버스를 추가한다면 기존 `TelemetryRunner` 루트의 자식으로 두는 것이 "DDOL 루트 하나" 규칙(8장)과 맞다.
- 여러 씬이 열려 있으면 라이팅 설정은 활성 씬 것을 쓴다. 플레이 중에는 `DontDestroyOnLoad` 가상 씬이 계층 창에 보인다([Multi-scene editing 6000.5](https://docs.unity.com/en-us/engine/6000.5/manual/working-with-scenes/multi-scene-editing/setupmultiplescenes)). Additive를 고르지 않은 이유 중 하나다.
- 빌드 프로필 씬 목록의 첫 씬이 시작 씬이다([Scenes 6000.6](https://docs.unity3d.com/Manual/content-directories-scenes.html)).

---

## 3. 진단: 지금 코드

### 3.0 현재 구조

```
Boot.unity ─ BootScreen.Start → Bootstrap.Run(config)
              Services.Clear/Register(AppConfig, LaunchOptions, ISaveStore, SaveSystem, ITelemetry, AppFlow)
              TelemetryRunner (DDOL, 이름 고정) 생성
              flow.Go(Title) 또는 flow.GoToGame(level-1)
                │ AppFlow.Go → SceneLoader.Load(name)  (동기, 다음 프레임에 교체)
                │            → Changed(from,to)  ← 아직 이전 씬이 살아 있을 때 발생
                ▼
Title.unity ─ TitleScreen (MonoBehaviour, 179줄): 부트스트랩(직접 Play), UI 생성, Esc/Enter 입력,
              패널 전환(Title/StageSelect), SettingsPanel·CreditsPanel 생성, 스모크 인자 직접 파싱
                │ flow.GoToGame(k)
                ▼
Game.unity  ─ GameController (MonoBehaviour, 402줄): 부트스트랩(직접 Play), 세션·보드·HUD 생성,
              키/마우스 입력, 흐름(Result, 스테이지 이동, StageSelect), 텔레메트리 via 판단,
              문구 표, 힌트(Solver), 효과음, 음량, 저장, 스크린샷
              HudView(this, …) — Result/Lose 오버레이, 설정 패널 포함
```

좋은 점(유지): `AppFlow`가 순수 C#이고 전이 표가 있으며 허용되지 않은 전이는 예외다. `ISceneLoader`가 분리돼 있어 가짜 로더로 테스트한다(`AppFrameworkTests.FlowLoadsSceneOnlyWhenItChanges`). 씬 파일에는 카메라와 루트만 있다(병합 충돌이 적음). 씬은 `GameplayScenes.Build`로 다시 만들 수 있다. 보드 쪽 `GameSession` / `IBoardPresenter` 경계는 이미 MVC에 가깝다.

### 3.1 위험 상위 5개

| # | 위험 | 근거 (파일:줄) | 영향 |
|---|---|---|---|
| 1 | **도메인 리로드 꺼짐 + 정적 상태 초기화 없음** | `ProjectSettings/EditorSettings.asset:29-30`; 정적 상태 `Contracts/App/Services.cs:13`, `Game/App/LevelCatalog.cs:10-11`, `Game/GameController.cs:88`(`FirstEntryPending`), `Game/UI/Ui.cs:10`(`Font`); 부트스트랩 생략 조건 `GameController.cs:95`, `TitleScreen.cs:38`(`Services.Has<AppFlow>()`) | 두 번째 Play부터 `Services.Has<AppFlow>()`가 참이라 부트스트랩을 건너뛴다. 이전 실행의 `AppFlow` 상태(예: StageSelect·Game), `LaunchOptions`, `HttpTelemetry`(러너 오브젝트는 Play 종료로 사라져 `Tick`이 멈춤)를 그대로 쓴다. 예상 증상: Title.unity를 Play했는데 이전 상태 StageSelect가 보인다. Game.unity를 Play하면 인스펙터 `startLevel`이 무시되고 이전 스테이지·`via=select`가 된다. 텔레메트리가 전송되지 않는다. `LevelCatalog`는 같은 `TextAsset` 참조면 다시 파싱하지 않으므로 `levels.json`을 고쳐도 반영되지 않을 수 있다. **코드 읽기로 확인했고 에디터 재현은 S0 첫 작업에서 한다**(2.5단계 전에는 Unity를 열지 않음) |
| 2 | **`GameController` 하나에 책임 9개** | `GameController.cs`: 문구 표 25-42, 부트스트랩 92-102, UI·카메라·EventSystem·오디오 생성 108-133, 진입 경로 판단 136-155, 흐름 175-219, 입력 222-266, 힌트(Solver 호출) 283-303, 세션 이벤트·저장 307-349, 효과음·음량 363-398 | 보드 진행(게임플레이/UI 소유)과 화면 흐름(씬 플로우 소유)이 한 파일이라 두 담당이 같은 파일을 고쳐야 한다. 단위 테스트를 할 수 없다(MonoBehaviour + `Keyboard.current` + `Camera.main`) |
| 3 | **씬 진입 순서에 숨은 의존** | `AppFlow.cs:75-76`: `Load`를 요청한 직후 `Changed`를 낸다. 동기 로드는 다음 프레임에 끝나므로 이 시점의 구독자는 **떠나는 씬**이다. 그래서 새 씬은 `Start`에서 `flow.State`를 보고 스스로 진입한다(`GameController.cs:139-149`, `TitleScreen.cs:56-57`). 같은 씬 안 변경은 `OnFlowChanged`로 진입한다(`GameController.cs:177`). 진입 경로가 둘이다. 떠나는 Title은 `Changed(StageSelect→Game)`를 받아 두 패널을 모두 끈다(`TitleScreen.cs:74-80`). 로드 직전 한 프레임 동안 빈 화면이 나온다 | 비동기 로드·페이드로 바꾸면 순서가 깨진다. 텔레메트리 `level_start.via`가 정적 플래그 `FirstEntryPending`(`Bootstrap.cs:39`, `GameController.cs:147,150`)과 `levelFromInspector`(90, 99, 141)에 기대고 있다. 씬을 넘는 `level_leave`는 `TelemetryRecorder.Dispose`가 `HttpTelemetry.PendingLeaveState`에 남겨 두는 방식이다(`TelemetryRecorder.cs:32-46`). 흐름을 바꾸면 텔레메트리 골든 시점이 깨지기 쉽다 |
| 4 | **코드 생성 UI가 아트 교체를 막음** | 색·글꼴 상수 `Ui.cs:11-19`, 정적 가변 `Ui.Font`(`GameController.cs:108`, `TitleScreen.cs:41`); 좌표 하드코딩 `TitleScreen.cs:106-167`, `HudView.cs:41-154`, `SettingsPanel.cs:22-49`, `CreditsPanel.cs:18-` | 테마가 확정돼도 아트가 프리팹·스프라이트를 바꿀 곳이 없다. 화면마다 C#을 고쳐야 한다. 캔버스 설정(1280×720, match 0.5)이 `TitleScreen.cs:108-116`와 `HudView.cs:44-52`에 중복돼 있다 |
| 5 | **View가 Model·Controller를 직접 만짐, 뒤로 가기 규칙이 흩어짐** | `HudView(GameController game, …)`(`HudView.cs:41`)가 구체 컨트롤러 메서드를 직접 부른다(71, 113-116, 125-126, 146-150). 코루틴도 컨트롤러로 돌린다(166). `SettingsPanel`이 `save.Settings` 필드를 `ref`로 직접 바꾼다(`SettingsPanel.cs:35-36`, 72-77). `Services.TryGet`도 뷰 안에서 부른다(81-82). Esc 처리가 네 곳에 있다: `GameController.cs:227, 245`, `TitleScreen.cs:88, 93, 100`(settings·credits·StageSelect) | 테스트할 수 없다. 오버레이가 늘면(일시정지, 확인 창) Esc 우선순위 버그가 생긴다. 지금도 Game에서 설정이 열려 있으면 `GameController.Update`가 Esc만 보고 나머지 입력은 버리는데, 같은 규칙이 Title에서는 따로 구현돼 있다 |

### 3.2 원칙 위반 목록 (파일:줄, 기준 f367750)

경로는 `unity/Assets/_Project/Scripts/Runtime/` 기준이다. "소유"는 고칠 사람이다.

| 파일:줄 | 원칙 | 내용 | 소유 |
|---|---|---|---|
| `Game/App/Bootstrap.cs:20-24` | 단정 | `AppConfig`가 null이면 경고만 내고 빈 인스턴스로 진행한다(텔레메트리가 조용히 꺼짐). 씬 연결 누락은 예외여야 한다 | 씬 플로우 |
| `Game/App/Bootstrap.cs:39` | OOP | `GameController.FirstEntryPending`(정적 internal)을 부트스트랩이 세팅한다. 흐름 정보가 정적 전역에 있다(8장 "정적 전역 변수 금지" 위반) | 씬 플로우 |
| `Game/App/Bootstrap.cs:42` | 단정 | `Mathf.Max(0, launch.Level - 1)`로 범위를 고치면서 로그가 없다. 상한 검사는 `GameController.cs:142`에 따로 있다 | 씬 플로우 |
| `Contracts/App/AppFlow.cs:39` | 단정 | 생성자가 null 로더를 받는다(`scenes != null` 분기, 75). 테스트용 우회가 런타임 경로에 남아 있다 | 아키텍트(계약) |
| `Contracts/App/AppFlow.cs:63-67` | 단정 | `GoToGame(stage)`에 범위 검사가 없다. 음수·초과 값은 `GameController.EnterStage`의 `Clamp`(183)가 조용히 고친다 | 아키텍트(계약) |
| `Contracts/App/AppFlow.cs:75-76` | MVC | `Changed`가 "씬 로드 요청 직후"에 난다. 새 씬 진입과 맞물리지 않는다(3.1-3) | 아키텍트(계약) |
| `Contracts/App/Services.cs:18` | 단정 | 같은 타입을 다시 등록하면 조용히 덮어쓴다 | 아키텍트(계약) |
| `Contracts/App/LaunchOptions.cs:14-20` | OOP | 공개 가변 필드. `GameController.cs:155`가 `launch.Replay = ""`로 바꾼다(한 번만 재생하는 상태를 공유 객체에 씀). `-level abc`를 `TryParse` 실패로 조용히 0으로 만든다(외부 입력이므로 경고 로그 필요) | 아키텍트(계약) |
| `Game/GameController.cs:25-42` | MVC | 화면 문구(View 내용)가 컨트롤러에 있다 | 게임플레이/UI |
| `Game/GameController.cs:92-102` | OOP | 씬 루트·부트스트랩·컨트롤러가 한 클래스다 | 씬 플로우 |
| `Game/GameController.cs:111` | 단정 | `Camera.main` null 검사가 없다(나중에 NRE). Title은 null이면 건너뛴다(`TitleScreen.cs:44-45`). 같은 상황을 두 씬이 다르게 처리한다 | 씬 플로우 |
| `Game/GameController.cs:119-120`, `TitleScreen.cs:47-48` | OOP | `EventSystem`을 두 씬이 실행 중에 찾아서 만든다. 씬에 두거나 한 곳에서 보장해야 한다 | 씬 플로우 |
| `Game/GameController.cs:122` | MVC | `new HudView(this, …)`: View가 구체 Controller를 안다 | 게임플레이/UI(내용) + 씬 플로우(연결) |
| `Game/GameController.cs:124-125` | 단정 | 좋은 예: TileSet이 없으면 예외를 던진다. 다만 검사 시점이 Play다. 빌드 전 검증기로 앞당긴다 | 씬 플로우(검증기) |
| `Game/GameController.cs:132` | OOP | `is HttpTelemetry` 다운캐스트. `ITelemetry` 인터페이스에 기대지 않는다 | 게임플레이/UI |
| `Game/GameController.cs:136-155` | MVC | 진입 경로(via) 판단이 흐름 + 텔레메트리 정책인데 MonoBehaviour `Start`에 있다. 테스트할 수 없다 | 씬 플로우 |
| `Game/GameController.cs:181-190` | 단정 | `EnterStage`가 `Clamp`로 잘못된 스테이지를 덮는다 | 씬 플로우 |
| `Game/GameController.cs:193-205` | 단정 | `SelectStage`·`NextLevel`이 범위 밖이면 조용히 `return`한다. 키 입력(`[` `]`)이라 무시하는 것은 맞지만, 조건을 흐름 컨트롤러의 `CanSelect`로 드러낸다 | 씬 플로우 |
| `Game/GameController.cs:214-218` | MVC | 저장(`SaveProgress`)과 흐름 전이가 한 메서드에 섞여 있다 | 씬 플로우 |
| `Game/GameController.cs:222-266` | OOP | 입력 읽기가 흐름 키(`[` `]` Enter Esc)와 보드 키(이동, K, X, Z, R, H)를 함께 처리한다. 매 프레임 `Keyboard.current`를 직접 읽어서 테스트할 수 없다 | 게임플레이/UI(보드 키) + 씬 플로우(흐름 키) |
| `Game/GameController.cs:269-278` | MVC | Result 상태에서 Restart를 Again으로 바꾸는 흐름 규칙이 보드 입력 경로에 있다 | 씬 플로우 |
| `Game/GameController.cs:283-303` | OOP | 힌트 계산(Solver)과 캐시가 컨트롤러에 있다. 순수 C# `HintProvider`로 뺀다 | 게임플레이/UI |
| `Game/GameController.cs:340-349`, 352-355 | MVC | 클리어 저장과 Result 전이를 보드 컨트롤러가 직접 한다. 보드 컨트롤러는 "연출 끝난 클리어"만 알리고, 흐름 컨트롤러가 전이한다 | 씬 플로우 |
| `Game/GameController.cs:363-398` | OOP | 효과음·음량 처리가 컨트롤러에 있다. `TitleScreen.cs:46`도 음량을 따로 설정한다(중복). `GameAudio`로 뺀다 | 게임플레이/UI |
| `Game/UI/TitleScreen.cs:36-52` | OOP/MVC | 부트스트랩, 서비스 조회, 카메라·오디오·EventSystem, UI 생성, 흐름 구독이 한 `Awake`에 있다 | 씬 플로우 |
| `Game/UI/TitleScreen.cs:58-66` | OOP | 스모크용 `-stageselect` `-credits` 인자를 `Environment.GetCommandLineArgs()`로 직접 읽는다. `LaunchOptions`를 거치지 않는다 | 씬 플로우 + 아키텍트(`LaunchOptions`) |
| `Game/UI/TitleScreen.cs:78` | 단정 | `if (titlePanel == null) return;`: 순서 문제를 조용히 넘긴다 | 씬 플로우 |
| `Game/UI/TitleScreen.cs:84-104` | MVC | 뷰가 입력을 읽고 `flow.Go`를 직접 부른다(View → Model) | 씬 플로우 |
| `Game/UI/TitleScreen.cs:128-161` | MVC | 버튼 콜백이 `flow.Go`/`flow.GoToGame`을 직접 부른다 | 씬 플로우 |
| `Game/UI/HudView.cs:157-161` | OOP | `ShowResult(bool show, State s, int levelIdx, int levelCount)`가 `show` 말고는 인자를 쓰지 않는다. 마지막 값(`lastState` 등)을 다시 그린다. 오해하기 쉬운 API다 | 게임플레이/UI |
| `Game/UI/HudView.cs:166` | OOP | 뷰가 컨트롤러 MonoBehaviour로 코루틴을 돌린다 | 게임플레이/UI |
| `Game/UI/HudView.cs:222-250` | MVC | 결과·실패 오버레이(흐름 화면)와 HUD(보드 정보)가 한 뷰에 있다. Result는 흐름 상태 화면이므로 `ResultView`로 분리한다 | 게임플레이/UI(내용) + 씬 플로우(표시 시점) |
| `Game/UI/SettingsPanel.cs:35-36, 72-77` | MVC | 뷰가 `SaveSystem.Settings`를 직접 바꾼다. 컨트롤러가 받아서 Model(`SaveSystem`) 명령으로 바꿔야 한다(예: `SetVolume`, `SetTelemetryChoice`) | 게임플레이/UI + 아키텍트(`SaveSystem` API) |
| `Game/UI/SettingsPanel.cs:81-82` | OOP | 뷰가 `Services.TryGet<AppConfig>`를 부르고, 없으면 조용히 false로 처리한다 | 게임플레이/UI |
| `Game/App/LevelCatalog.cs:10-20` | 단정 | 정적 캐시를 `TextAsset` 참조로만 구분한다. 도메인 리로드가 꺼져 있으면 오래된 값이 남는다(3.1-1) | 씬 플로우 |
| `Game/Telemetry/TelemetryRunner.cs:30-81` | OOP | MonoBehaviour의 정적 팩토리가 정책(주소 우선순위), 동의, `playerId` 생성, DDOL 생성, `SettingsChanged` 구독(해제 없음, 66)을 모두 한다. `GameObject.Find` + `DestroyImmediate`로 중복을 막는다(32-33) | 게임플레이/UI (흐름 영향만 씬 플로우가 검토) |
| `Game/Telemetry/TelemetryRecorder.cs:32-46` | 숨은 의존 | 씬을 넘는 `level_leave`를 `HttpTelemetry.PendingLeaveState` 공유 필드로 전달한다 | 게임플레이/UI, 흐름 분리 때 `EntryTracker`로 옮기는 것을 제안 |

### 3.3 에디터에서 아무 씬이나 Play할 때

| Play한 씬 | 지금 동작 (첫 Play) | 두 번째 Play부터 (도메인 리로드 꺼짐) |
|---|---|---|
| `Boot.unity` | 정상. Title 또는 `?level` | `Services.Clear()`부터 하므로 정상 |
| `Title.unity` | `TitleScreen.Awake`가 부트스트랩(`enterFirstScreen:false`) → `Start`에서 Title로 | 부트스트랩을 건너뛴다. 이전 `AppFlow.State`(예: Game)를 보고 `Show(Game)`을 한다 → **패널이 하나도 안 보인다**(추정, S0에서 재현) |
| `Game.unity` | `GameController.Awake`가 부트스트랩(인스펙터 `startLevel`/`replay` 적용) → `Start`에서 진입 | 부트스트랩을 건너뛴다. 이전 스테이지·`via`, 텔레메트리 러너 없음(추정, S0에서 재현) |
| `Assets/Scenes/SampleScene.unity` | 템플릿 잔재. 빌드 목록에 없고 `ProjectSettings.asset:267 templateDefaultScene`이 가리킨다. 아무것도 안 된다 | 같음 → 삭제 제안(리드 결정) |

---

## 4. 목표 구조

### 4.1 씬 목록 (바뀌지 않음)

| 씬 | 빌드 | 루트 (새 이름) | 역할 |
|---|---|---|---|
| `Boot` | 0 | `BootSceneRoot` (지금 `BootScreen`) | `Bootstrap.Run` → 첫 화면. 화면 없음 |
| `Title` | 1 | `TitleSceneRoot` | `TitleView`, `StageSelectView`, 오버레이(설정·크레딧)를 연결한다. `TitleFlowController` |
| `Game` | 2 | `GameSceneRoot` | 보드·HUD·`ResultView`·오버레이(설정)를 연결한다. `GameFlowController` + 보드 진행 컨트롤러 |
| `SampleScene` | — | — | 삭제 제안(리드, `ProjectSettings.templateDefaultScene` 정리 포함) |

DDOL 루트는 `TelemetryRunner` 하나로 유지한다. 페이드 캔버스(S5)는 그 자식으로 둔다(jslib가 이름으로 찾는 루트 이름은 그대로).

### 4.2 흐름 다이어그램

```
                    ┌──────────────── Esc / 뒤로 ───────────────┐
                    ▼                                           │
 Boot ──▶ Title ──Enter/시작──▶ StageSelect ──스테이지──▶ Game ──클리어 연출 끝──▶ Result
   │        ▲                    │   ▲  ▲                 │ ▲  ▲ [ ] 스테이지        │
   │        └──────Esc───────────┘   │  └───Esc / 스테이지──┘ │  └──(Game→Game)       │
   │                                 └──────────────── 스테이지 선택 ────────────────┤
   └──(?level= / -level)──────────────────────────────────▶ Game ◀── 다음 / 다시 하기 ┘

 오버레이(어느 화면 위에나): Settings, Credits(Title만), [나중] Pause/Confirm
   Esc = OverlayStack.Pop() 우선, 비었으면 AppFlow.Back()
 Lose: Game 상태 안의 보드 상태. 실패 패널은 HUD 내용(AppState 아님)
```

### 4.3 클래스 구조 (MVC)

```
Model (순수 C#. AppFlow는 Contracts/App, OverlayStack·EntryTracker는 Game/Flow — 4.5)
  AppFlow            상태·전이 표·TryGetBackTarget/CanBack·IsLoading·StageCount. Changed(from,to)
                     (Entered 이벤트는 보류: 진입은 루트 Start + Changed 하나로)
  OverlayStack       Push(id)/Pop()/Top/Count, Changed
  EntryTracker       다음 level_start.via 계산(boot/url/next/select/again). FirstEntryPending 대체
  SaveSystem         SetVolume/SetTelemetryChoice 등 명령 메서드(아키텍트에게 요청)
  GameSession, Core  (그대로)

Controller (순수 C#, MonoBehaviour 아님. 씬 루트가 만들고 Tick/Dispose 호출)
  TitleFlowController   TitleView/StageSelectView 이벤트 → AppFlow, AppFlow → 뷰 Show/Hide
  GameFlowController    ResultView·HUD 흐름 이벤트 → AppFlow, AppFlow → session.Load·ResultView,
                        보드 컨트롤러의 StageCleared → 저장 + Go(Result)
  BackNavigator         Back 입력 → OverlayStack.Pop(), 비었으면 flow.TryGetBackTarget(out t) → Go(t)
  BoardController       (게임플레이/UI) 보드 입력 → GameSession.Send, SessionUpdate → HUD/사운드
  GameAudio, MessagePresenter, HintProvider (게임플레이/UI, GameController에서 분리)

View (MonoBehaviour / 프리팹, 표시 + 이벤트만)
  TitleView, StageSelectView, ResultView, SettingsView, CreditsView, HudView, BoardView
  IScreenView { void Show(); void Hide(); }   이벤트: StartPressed, StageChosen(int), BackPressed …

Input
  AppInput (Game 어셈블리) — Keyboard.current를 읽어 의미 이벤트로 바꾼다(Back, Confirm, PrevStage, NextStage).
  보드 키는 BoardInput(게임플레이/UI). 둘 다 OverlayStack.Count > 0이면 보드 이벤트를 내지 않는다

Scene root (MonoBehaviour 하나, 조립만)
  Awake: Bootstrap.EnsureForDirectPlay(...) → Services에서 한 번 꺼냄 → 필수 참조 검사(예외)
         → new Controller(view, flow, ...) → 구독
  Start: controller.Enter(flow.State)         ← 진입 경로 하나
  OnDestroy: controller.Dispose()
```

### 4.4 전환 방식

- **씬 진입 경로를 하나로**: 새 씬의 루트는 `Start`에서 `Enter(flow.State)`만 부른다. 같은 씬 안의 전이는 `Changed`로 받는다. 떠나는 씬의 컨트롤러는 자기 씬에 속하지 않는 상태로 가는 `Changed`를 받으면 **아무것도 그리지 않는다**(`AppScenes.For(to) != 내 씬`이면 무시). 그래서 지금처럼 빈 화면이 한 프레임 보이지 않는다.
- **로드 중 잠금**: `AppFlow.IsLoading`(로더가 완료를 알릴 때까지 참). 이 동안 `Go`는 `InvalidOperationException`을 던진다. 입력 컨트롤러는 `flow.CanGo(to)`(로딩 중이면 거짓)로 먼저 확인한다. 완료 신호는 `ISceneLoader.Loaded`(`event Action<string>`)다. **`SceneLoader` 구현이 `SceneManager.sceneLoaded`로 낸다**(새 씬의 Awake 뒤, Start 전). 씬 루트마다 신호 코드를 넣지 않는다. 같은 씬 안 전이는 로딩이 없다(아키텍트 승인 조건).
- **진입 이벤트(`Entered`)는 만들지 않는다**(보류). 진입 경로는 "새 씬 = 루트 `Start`의 `Enter(flow.State)`, 같은 씬 = `Changed`" 하나다. S5 페이드에서 꼭 필요해지면 그때 아키텍트에게 요청한다.
- **페이드(S5)**: `FadeSceneLoader : ISceneLoader`. `LoadSceneAsync(Single)` + 코루틴. DDOL 러너 자식 캔버스로 0.15초 아웃 → 로드 → 인. 같은 씬 안 전이(Title⇄StageSelect, Game→Game)는 페이드하지 않는다.

### 4.5 폴더·어셈블리 위치

| 무엇 | 위치 | 어셈블리 | 소유 |
|---|---|---|---|
| `AppFlow`, `ISceneLoader`, `Services`, `LaunchOptions` 변경 (S0~S2) | `Scripts/Runtime/Contracts/App/` | `SevenSteps.Contracts` | 아키텍트 소유. **이 변경에 한해 씬 플로우가 수정**, 커밋마다 아키텍트 검토. 11장·변경 기록은 아키텍트가 씀 |
| `OverlayStack`, `EntryTracker` (순수 C#, MonoBehaviour 아님) | `Scripts/Runtime/Game/Flow/` (아키텍트 결정: Contracts는 여러 담당이 기대는 안정 API만) | `SevenSteps.Game` | 씬 플로우 |
| 씬 루트, 흐름 컨트롤러, `BackNavigator`, `AppInput`, `SceneLoader`, `FadeSceneLoader`, `Bootstrap` | `Scripts/Runtime/Game/App/` (지금 폴더 유지) + `Game/Flow/` | `SevenSteps.Game` (새 asmdef 없음) | 씬 플로우 |
| 화면 뷰 (`TitleView` 등) | `Game/UI/` | `SevenSteps.Game` | 게임플레이/UI(내용), 공개 면(이벤트·Show/Hide)은 씬 플로우와 합의 |
| UI 프리팹, `UiTheme` | `Assets/_Project/UI/` | — | 게임플레이/UI, 테마 값은 아트 |
| `SceneFlowValidator` | `Scripts/Editor/SceneFlow/` | `SevenSteps.Editor` | 씬 플로우 |
| 테스트 | `Tests/EditMode/Flow/` (새 폴더) | `SevenSteps.Tests.EditMode` | 씬 플로우(README 소유권 표에 추가 요청) |

### 4.6 바꾸는 것 / 그대로 두는 것

| 그대로 | 바꿈 |
|---|---|
| 씬 3개, 빌드 순서, 씬 이름 상수 | 정적 상태 초기화(`SubsystemRegistration`) |
| `AppState` 5개와 전이 표(새 전이 추가 없음) | `AppFlow`: 생성자 null 금지, `stageCount >= 1`, 스테이지 범위, `TryGetBackTarget`/`CanBack`, `IsLoading` |
| `Services` 로케이터(조회는 씬 루트만) | `Services.Register` 중복 등록 예외 |
| DDOL 루트 하나(`TelemetryRunner`) | `GameController`를 흐름 / 보드 진행 / 오디오 / 문구 / 힌트로 분리 |
| `GameSession` / `IBoardPresenter` | Esc·뒤로 가기를 `BackNavigator` + `OverlayStack` 하나로 |
| 실행 옵션(`-level`, `-replay`, `-screenshot`, `?level=` …), 인스펙터 `startLevel`/`replay` | 텔레메트리 진입 경로 판단을 `EntryTracker`로(같은 값, 골든으로 확인) |
| uGUI, legacy `Text` + Jua | 화면을 뷰 클래스(이벤트 + Show/Hide)로 → 이후 프리팹 + `UiTheme` |

---

### 4.7 뷰 공개 면 (게임플레이/UI와 합의, S3 확정안 2026-11-09)

내용은 게임플레이/UI, 표시 시점과 흐름은 씬 플로우가 맡는다. S2(f08f4d6) 뒤 확정안: 아래 표대로 S3에서 만든다. 게임플레이/UI 확인 후 S3 시작. 흐름 쪽이 기대는 S2 계약: `AppFlow.IsLoading`, `CanGoToGame(stage)`, `TryGetBackTarget(out)`/`CanBack`, `StageCount`, `EntryTracker.Announce/TakeVia`. 뷰 이벤트를 받은 컨트롤러는 이것으로 먼저 묻고, 할 수 없으면 무시한다(예외 아님).

| 대상 | 공개 면 | 구독·호출하는 쪽 | 단정 조건·비고 |
|---|---|---|---|
| `ResultView` | `Show(ResultModel)`, `Hide()`. 이벤트 `NextRequested`, `RetryRequested`, `StageSelectRequested` | `GameFlowController` | 클리어와 실패(몸을 모두 잃음)를 한 뷰로 그린다. 열림 여부의 기준은 흐름 쪽: 클리어 = `AppFlow.State == Result`, 실패 = 보드 상태 Lost(`AppState`는 Game 그대로, 2.4 결정). 실패일 때는 `NextRequested`를 내지 않는다. `Show`/`Hide`를 부르는 쪽은 `GameFlowController` 하나다. 실패는 보드 컨트롤러의 이벤트 `BoardLost(State)`/`BoardRecovered`(코어 `Status.Lose`를 그대로 전달)로 받는다. 실패 결과는 **오버레이가 아니다**(`OverlayStack`에 넣지 않음). 그래서 Z(되돌리기)·R(다시 시작) 보드 입력이 살아 있고, 상태가 Lose를 벗어나면 `BoardRecovered`로 `Hide` |
| `ResultModel` (읽기 전용) | `stage`, `cleared`, `bodiesUsed`, `minBodies`, `bestBodies`, `isLast` | — | 생성자 검사: `stage >= 0`, `cleared`일 때만 `1 <= bodiesUsed <= level.Bodies`(스테이지별 2~5), `minBodies`는 0(모름) 또는 `1..Bodies`, `bestBodies`는 0(기록 없음) 또는 `1..Bodies`. chapter 필드가 들어오면 `nextChapterChanges` 추가 자리 |
| `HudView` | `Refresh(State, HudModel{stageIdx, stageCount, message, progress})`. 이벤트 `StageSelectRequested`, `SettingsRequested`, `StageClicked(int)` / `UndoRequested`, `RestartRequested`, `HintRequested` | 앞 3개는 흐름 컨트롤러, 뒤 3개는 보드 컨트롤러 | `GameController` 직접 참조 제거. `NudgeChoice()`와 보드 피드백 메서드는 보드 컨트롤러 전용. 세쌍둥이 몸 아이콘 외형(`TileSet.BodyColor`)은 `HudModel`이 아니라 `HudView`가 읽는다. **S3에서 `TileSet`을 생성자/Init으로 주입**하고, 임시 조회 `HudView.BodyLook`(`GetComponent<BoardView>().TileSet`, feat/view eafd97b)는 게임플레이/UI가 지운다 |
| `IOverlay` (설정, 크레딧, 이후 확인 창·챕터 전환) | `Show()`, `Hide()`, `event CloseRequested` | `BackNavigator` | 뷰는 스스로 닫지 않고 `CloseRequested`만 낸다. 열림 여부는 `OverlayStack`(`Contains`, `Count`) 하나가 가진다. 설정 저장은 `Hide`가 아니라 값이 바뀔 때 컨트롤러가 `SaveSystem` 명령으로 한다(Pop 경로와 무관) |
| 보드 입력 차단 | `BoardInput`이 `OverlayStack.Count > 0`이면 보드 이벤트를 내지 않음 | 보드 컨트롤러 | — |

## 5. 단계별 이행 계획

모든 단계 공통 완료 조건: `BuildTools.CompileCheck` 통과, `unity.sh test`(EditMode, 텔레메트리 골든 포함) 통과, `unity.sh build-win` + `smoke`(리플레이 7개: 스테이지 1, 3, 8, 9, 13, 15, 16) 통과. 표시된 단계는 `build-webgl` + `smoke-webgl`도 통과해야 한다. 커밋은 단계마다 작게 나눈다.

| 단계 | 내용 | 잼 안 | 테스트 | 계약 변경(아키텍트) |
|---|---|---|---|---|
| **S0 안전망** | ① 정적 초기화: `Services.ResetForPlay`, `LevelCatalog`, `FirstEntryPending`, `Ui.Font`에 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`. 부트스트랩 생략 조건을 "이번 Play에서 부트스트랩이 돌았는가"로 바꾼다. ② 에디터에서 3.3 표를 재현해 기록 ③ `SceneFlowValidator`(메뉴 + 배치): 빌드 목록 = `AppScenes`(Boot 0번, 이름 중복 없음, 경로 규칙), 각 씬에 루트 하나, 루트 필수 참조(`assets`, `config`), `AppConfig.tileSet` | 예 | EditMode: 초기화 메서드를 부른 뒤 `Services.Has` = false. 에디터 Play 두 번 수동 확인(Title, Game). 검증기 배치 실행 | 승인: `Services` 안의 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] static void ResetForPlay() => map.Clear()`. 부트스트랩 생략 판단은 `Bootstrap`의 "이번 Play에서 돌았는가" 플래그(같은 방식으로 초기화) |
| **S1 단정화** | `Bootstrap`: config null이면 예외, `LaunchOptions.Warnings`를 로그. `LaunchOptions`: `{ get; private set; }`, 소비형 `TakeReplay()`, 스모크 전용 `-stageselect`/`-credits` 흡수, 파싱 실패는 Contracts에서 로그하지 않고 `Warnings` 목록에 담음, `TelemetryOff`/`TelemetryUrl` 읽기 호환 유지. `Services.Register` 중복 예외(테스트는 `[SetUp]`에서 `Clear`). `TitleScreen`·`GameController`의 조용한 null `return` 제거 | 예 | EditMode: 잘못된 전이·중복 등록·범위 밖 스테이지가 예외인지, `Warnings` 내용. 기존 `AppFrameworkTests` 유지 | 승인(`LaunchOptions`, `Services`) |
| **S2 `AppFlow` 계약** | `AppFlow(ISceneLoader, int stageCount)`(null 금지, `stageCount >= 1`), `GoToGame` 범위 밖이면 `ArgumentOutOfRangeException`(실행 인자 `-level 99`는 부트스트랩이 경고 + 보정 후 호출), `TryGetBackTarget(out AppState)`/`CanBack`(기존 `CanGo` 표 안에서만), `IsLoading` + `ISceneLoader.Loaded`(`SceneLoader`가 `sceneLoaded`로 냄). `Entered` 이벤트 없음. `EntryTracker`(via 계산, `Game/Flow/`) | 예 | EditMode: 전이 표 전체(허용·거부), Back 표, 로드 중 전이 거부, 가짜 로더(지연 완료). `EntryTracker`가 지금과 같은 via(boot/url/next/select/again)를 내는지 + 텔레메트리 골든 | 예(API 고지 필요) |
| **S3 흐름 분리** | `BootSceneRoot`/`TitleSceneRoot`/`GameSceneRoot` + `TitleFlowController`/`GameFlowController`. `GameController`에서 흐름 코드(136-219, 269-278, 340-355)를 옮긴다. `HudView`의 Result 부분을 `ResultView`로 나누고 이벤트로 연결한다. 보드 진행 부분은 게임플레이/UI가 `BoardController`로 정리(리드 배분) | 예 | EditMode: 흐름 컨트롤러를 가짜 뷰(`IScreenView` 구현)로 테스트한다. Boot→Title→StageSelect→Game→Result→다음. 씬을 넘는 `level_leave`도 EditMode(가짜 로더)로 고정한다. PlayMode asmdef는 만들지 않는다(아키텍트 결정). EditMode로 못 잡는 회귀가 나오면 그때 아키텍트가 만든다. 그 전까지는 `smoke` + 수동. **build-webgl + smoke-webgl** | 없음(뷰 인터페이스는 Game 어셈블리 안) |
| **S4 오버레이·뒤로 가기** | `OverlayStack`(`Game/Flow/`) + `BackNavigator` + `AppInput`. Settings·Credits를 오버레이로 등록한다. Esc 처리 4곳을 하나로 모은다. 오버레이가 열리면 보드 입력을 막는다 | 예 | EditMode: Esc 순서(설정 열림 → 닫힘 → StageSelect), 크레딧 위 Esc, 오버레이 중 보드 입력 무시 | 없음(Game 어셈블리) |
| **S5 페이드 전환** | `FadeSceneLoader`(`LoadSceneAsync` + 코루틴, DDOL 러너 자식 캔버스). 같은 씬 전이는 페이드 없음. **실패 경로에서도 `Loaded`를 못 내는 일이 없게 한다**(`IsLoading`이 참으로 남으면 흐름이 멈춤, 아키텍트 S2 메모): 로드 실패는 예외나 명시적 복구 | 여유 있으면 | 가짜 로더 지연 테스트(S2) 재사용. 스모크 스크린샷 대기 시간 확인(`ScreenshotOnce` 1.5초). **build-webgl + smoke-webgl**(보드 픽셀 기준 500 이상) | 없음 |
| **S6 UI 프리팹 + 테마** | 화면별로 코드 생성 → 프리팹(`UI/**`) + 뷰 직렬화 참조(`OnValidate` 검사) + `UiTheme` SO(색·글꼴·스프라이트). 순서: Settings → Result → Title/StageSelect → HUD. `Ui.cs` 정적 색은 `UiTheme` 기본값으로 | 일부(Settings·Result), 나머지는 테마 확정 후 | `SceneFlowValidator`가 프리팹 필수 참조를 검사. 스모크 스크린샷 비교 | 없음 |

### 5.1 텔레메트리 시점 보존 (모든 단계)

흐름을 바꿔도 다음 시점은 지금과 같아야 한다(`docs/telemetry.md` 3장).

| 이벤트 | 지금 내는 곳 | 바뀐 뒤 |
|---|---|---|
| `session_start` | `TelemetryRunner.CreateTelemetry`(부트스트랩) | 그대로 |
| `level_start` (`via`) | `GameController.EnterStage` → `StageEntered` → `TelemetryRecorder` | `GameFlowController`가 `EntryTracker.Next()`로 via를 받아 같은 이벤트를 낸다 |
| `level_leave` | `StageLeaving`(같은 씬) / `PendingLeaveState`(씬을 넘을 때) | 같은 순서. 씬을 넘는 경우는 S3에서 `EntryTracker`가 "떠난 시도"를 들고 있게 바꾸는 안을 게임플레이/UI와 정한다 |
| `level_clear`/`level_fail` | `TelemetryRecorder.OnUpdated` | 그대로(보드 쪽) |
| `session_end` | `TelemetryRunner` (페이지 숨김, 종료) | 그대로. 페이드 중에도 러너는 DDOL이라 영향 없음 |

S2·S3 커밋마다 `unity.sh test`(텔레메트리 골든)를 돌린다. 골든은 씬을 쓰지 않으므로, 씬을 넘는 `level_leave`를 따로 확인한다. 스모크에서 Game → StageSelect → 다른 스테이지로 가는 리플레이 로그를 수동으로 보고, S3에서 EditMode 테스트(가짜 로더)로 고정한다.

---

### 5.2 진행 상태 (2단계 구현)

사용자 승인(2026-11-08, 코디네이터). 기준: origin/7lucky 80b74cd(feat/view 5a2180a 합류 후).

| 단계 | 커밋 | 결과 | 남은 것 |
|---|---|---|---|
| S0 ① 정적 초기화 | 5fdaf49 | compile OK, test 224/224(+3), build-win + smoke 7/7 OK | 아키텍트 승인(2026-11-09) |
| S0 ③ `SceneFlowValidator` + `unity.sh scene-validate` | 971ae2f | scene-validate OK(0 오류). 첫 실행에서 `GameAssets.font` 누락을 잡음: 이 작업 공간에 `Assets/Shared` 정션이 없었다(`tools/link-shared-assets.ps1`로 해결). 즉 정션 없는 작업 공간의 빌드는 글자가 빠진 채 smoke를 통과한다 | 리드 검토(`unity.sh` 줄). `BuildTools` 빌드 전에 검증기 호출은 제안만(소유자 결정) |
| S0 ④ `SampleScene` 삭제, `templateDefaultScene` → Boot | feb8fcb | 정션 연결 후 다시 compile·test 224/224·build-win·smoke 7/7 OK, 스크린샷에 글자 표시 확인 | `CLAUDE.md` "시작 씬" 줄 정리(리드) |
| S0 ② 에디터 Play 두 번 재현(3.3 표) | — | 배치로는 Play 모드를 재현할 수 없어 미실시. 정적 초기화 자체는 `PlayResetTests`가 고정한다 | 에디터에서 Title·Game을 두 번씩 Play해 확인(사용자 또는 리드, 수동) |
| S1 단정화 | 923a1a6 | compile OK, test 230/230(+6), build-win + smoke 7/7 OK, 타이틀 스모크(`-screenshot -stageselect -credits`) OK, 잘못된 `-level abc`는 경고 로그 | 아키텍트 승인(2026-11-09, 11.2 반영 feat/architecture 8914e53) |
| (배정) `AppConfig.tileSet` → RuinsHenge_Chapter01 | 7300d9d | tileset-validate(2세트 0오류), scene-validate, test 232/232, build-win + smoke 7/7, build-webgl + smoke-webgl 1/8/13 OK. 방향광 조정 없음 | — |
| S2 `AppFlow` 계약 + `EntryTracker` | f08f4d6 | compile OK, test 242/242(+10), scene-validate OK, build-win + smoke 7/7, 타이틀 스모크(Boot→Title 로드 후 StageSelect 전이로 `Loaded` 해제 확인), `-level 99`는 경고 후 17 | 아키텍트 승인(2026-11-09, 11.1·11.2 반영 feat/architecture 332b634). Title→Game·Game→StageSelect 실제 씬 전환은 입력이 필요해서 수동 확인(S0 ②와 함께) |
| (요청) `LevelCatalog.ChapterIdOf` | 9f3d109, e83bb64 | test +2. core b5c4464 뒤 `LevelDto.chapter`를 읽음 | — |
| S3a `ResultView`/`ResultModel` | 83a55d6 | test 242, build-win + smoke 7/7. 결과 패널은 EditMode로 확인(리플레이는 끝 상태로 바로 가서 패널을 열지 않음, 7lucky도 같음) | — |
| S3b `HudView` 이벤트화, TileSet 주입 | 70ccc03 | test 242, build-win + smoke 7/7. feat/view eafd97b(BodyLook)를 먼저 합친 뒤 주입으로 바꿈 | — |
| S3c Game 씬 분리 | 0fc35a3 | test 249, build-win + smoke 7/7. `GameController`=씬 루트(클래스·guid·필드 유지: Game.unity, Art/Test/Game.unity, 아트 빌더 그대로), `BoardController`=보드, `Flow/GameFlowController`=흐름(`IStageBoard`/`IResultPanel`) | 아키텍트 승인(2026-11-09, 42613fb). 보드 쪽 정리는 게임플레이/UI. `GameController.LevelIndex`/`Levels`(아트 프리뷰용)는 12.8 이전 때 삭제 |
| S3d Title 씬 분리 | 18029f4 | test 253, build-win + smoke 7/7, 타이틀 스모크, **build-webgl(Mobile_RPAsset) + smoke-webgl 1/8/13 OK**(33381/27627/24738, 콘솔 0). 64bb47f에서 7lucky 81262de 합친 뒤 test 253, build-win + smoke 7/7 | Title→Game 실제 클릭은 수동 확인 |
| S4 오버레이·뒤로 가기·챕터 자리 | 753fc60 | test 258/258(+5), scene-validate OK, build-win + smoke 7/7, 타이틀 스모크(크레딧을 오버레이로 띄움) OK. `AppInput`은 만들지 않음(키 읽기는 씬 루트 두 곳뿐이라 필요 없음). 챕터 전환 카드·테마 프리셋은 `GameFlowController.ChapterEntering`에 구독만 붙이면 된다. **chapters.json이 생기면** 비교 전에 `ChapterFileDto.Get(id).id`로 정규화한다(""와 첫 챕터 id를 같은 챕터로, 아키텍트 S4 메모) | 아키텍트 승인. 리드 WebGL 수동 확인(클리어→결과→다음, 설정·크레딧 Esc) 완료 |
| (후속) `SettingsController` → `SaveSystem` 설정 명령 | 625e258 | test 260, build-win + smoke 7/7 | — |
| S6a `UiTheme`(글꼴·색) 에셋 | 07927a9 | test 260, scene-validate, build-win + smoke 7/7, build-webgl + smoke-webgl OK. `Data/UiTheme_Default.asset`(v0 값), `GameAssets.uiTheme`, `Ui` 색·글꼴은 테마에서 읽음 | 챕터별 테마 프리셋 연결은 12.8(`ChapterPreset`이 Contracts라 아키텍트와 형식 결정) |
| S6b Settings·Result 프리팹 | 0b3d260 | test 262, scene-validate, build-win + smoke 7/7, 타이틀 스모크, build-webgl + smoke-webgl OK. `UI/Prefabs/SettingsPanel.prefab`, `ResultPanel.prefab`(UiBuilder가 v0 레이아웃으로 처음 생성, 이후 프리팹 편집), 직렬화 참조 + `Validate`(OnValidate·Awake·검증기) | 설정·결과 패널 실제 화면 확인은 수동(스모크 경로 없음). HUD·Title·Credits 프리팹은 게임플레이/UI(아트 테마 확정 뒤) |
| (후속) 음량을 `GameAudio.ApplyMaster`로 | 899f8c8 | test 262, build-win + smoke 7/7 | — |
| S5 페이드 전환 | 1f9f462 | test 263, build-win + smoke 7/7, 타이틀 스모크, build-webgl + smoke-webgl OK. `SceneLoader`가 LoadSceneAsync + 페이드(0.15초), DDOL `SceneFader`. 로드 실패는 Load에서 예외, `AppFlow`가 상태를 되돌림 | 아키텍트 승인(2026-11-09, DDOL 8장 문구·11.1 반영). 메모: LoadSceneAsync null 경로는 State와 씬이 어긋나지만 사전 검사 뒤라 둠, 씬 경계 level_leave가 0.15초 늦어짐(순서·값 동일). WebGL 페이드 체감은 리드가 합칠 때 확인 |

**대기 중: 12.8 챕터 연결(게임플레이/UI 요청, feat/view 6aac6e5, 2026-11-09)**. 시기는 레벨 묶음(chapters.json) 반영 때, 리드와 맞춘다. Game 씬 루트에서 할 일: (1) Game 씬에 방향광 3개(키·필·림, 그림자는 키만) (2) `GameController.Awake`에서 `board.Init` 뒤 `board.UseChapters(new ChapterStage(cam, key, fill, rim, transform), stage => 챕터 프리셋, cfg.tileSet)`. 프리셋은 `LevelCatalog.ChapterIdOf` → chapters.json `ChapterInfo.ArtPreset` → `AppConfig.Chapter`, 챕터가 없으면 null (3) `ChapterEntering`의 id를 `ChapterFileDto.Get(id).id`로 정규화 (4) S5 페이드가 생기면 `LoadStage`를 페이드 뒤에 둔다(챕터 교체가 가려짐).

챕터 방향(2026-11-08 사용자): 챕터 = 우주 속 행성, 주인공 세쌍둥이. S4에서 `GameFlowController`에 챕터 전환 연출 자리(다음 스테이지의 chapter가 바뀌면 전환 오버레이), S6에서 `UiTheme`·조명·하늘을 챕터별 프리셋으로 고를 자리를 남긴다. 레벨 데이터 chapter 필드는 레벨 담당이 추가한다.

**미루는 것(잼 이후)**: Additive 영구 씬, 오디오 매니저·배경음 DDOL, UI Toolkit, TextMeshPro 전환, 로딩 진행률 화면, Addressables, DI 컨테이너, PlayMode 자동 테스트(필요해지면 아키텍트가 `SevenSteps.Tests.PlayMode`를 만든다).

## 6. 요청과 결정이 필요한 것

| 대상 | 요청 |
|---|---|
| 아키텍트 | **완료(8장).** (1) 9장 반영 (2) S1·S2 조건부 승인 (3) 위치는 Game/Flow (4) PlayMode asmdef 지금은 없음 (5) 8장 문장 수정 |
| 리드 | (1) `GameController` 분리 중 보드 진행 부분(입력, 문구, 힌트, 오디오, `BoardController`)과 UI 프리팹화(S6)를 누가 할지 배분. 순서는 아키텍트 권고대로 **흐름 부분을 씬 플로우가 먼저 빼서 합친 뒤 게임플레이/UI가 보드 부분 정리, 동시 작업 금지** (2) 씬 파일(`Scenes/**`) 소유: README 표는 씬 플로우로 바뀜(29b1f4a). `docs/architecture.md` 8장 표의 "게임플레이/UI"와 맞출 것 (3) `Tests/EditMode/Flow/**`, `Scripts/Editor/SceneFlow/**`, `Game/Flow/**` 소유권 표 반영 (4) `SampleScene.unity` 삭제와 `templateDefaultScene` 정리 (5) `unity.sh`에 `scene-validate` 명령 추가(`tileset-validate`와 같은 형식) (6) 도메인 리로드를 끈 설정을 유지할지(유지 권장: S0로 해결) (7) feat/view 5a2180a 합류 시점: S0~S3의 `GameController`/`TitleScreen`/`HudView` 수정과 겹치므로 먼저 합쳐지길 권장 |
| 게임플레이/UI | 뷰 공개 면(이벤트 이름, `Show/Hide`) 합의. `HudView`에서 `ResultView` 분리 |

---

## 7. 조사 출처

- Unity 스크립트 레퍼런스 `SceneManager.LoadScene` (6000.6): https://docs.unity3d.com/6000.6/Documentation/ScriptReference/SceneManagement.SceneManager.LoadScene.html
- Unity 스크립트 레퍼런스 `SceneManager.LoadScene` (이름·경로 규칙): https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadScene.html , 2019.1 판 https://docs.unity3d.com/2019.1/Documentation/ScriptReference/SceneManagement.SceneManager.LoadScene.html
- `SceneManager` (6000.6): https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/scenemanagement/scenemanager
- `Scene.buildIndex`: https://docs.unity3d.com/kr/ScriptReference/SceneManagement.Scene-buildIndex.html
- 씬 (6000.6 매뉴얼, 빌드 목록 첫 씬): https://docs.unity3d.com/Manual/content-directories-scenes.html
- 다중 씬 편집 (6000.5): https://docs.unity.com/en-us/engine/6000.5/manual/working-with-scenes/multi-scene-editing/setupmultiplescenes
- `Object.DontDestroyOnLoad` (6000.6): https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/object/dontdestroyonload
- Domain Reloading (Unity 6): https://docs.unity.cn/Manual/domain-reloading.html , 2019.4: https://docs.unity3d.com/ru/Manual/DomainReloading.html
- Managed code variants (`UNITY_ASSERTIONS`, 6000.6): https://docs.unity3d.com/Manual/managed-code-variants.html
- Addressables 동기 로드(WebGL 제약): https://docs.unity.cn/Packages/com.unity.addressables%401.21/manual/SynchronousAddressables.html
- UI 시스템 비교 (6000.6): https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/ui-system-compare , 6000.4: https://docs.unity3d.com/6000.4/Documentation/Manual/UI-system-compare.html
- Nested Prefabs: https://docs.unity3d.com/Manual/NestedPrefabs.html ; Unity Learn UI 메뉴와 프리팹 변형: https://learn.unity.com/tutorial/challenge-ui-menu-with-prefab-variants-nested-prefabs
- Unity ScriptableObject 데모 PaddleBallSO: https://unity.com/how-to/get-started-with-scriptableobjects-demo , 저장소 https://github.com/UnityTechnologies/PaddleGameSO , 블로그 https://unity.com/blog/engine-platform/6-ways-scriptableobjects-can-benefit-your-team-and-your-code
- Unity Open Project 1 (Chop Chop) 씬 관리 폴더(미러): https://plastichub.unity.cn/unity-tech-cn/open-project-1/src/branch/main...UOP1_Project/Assets/Scripts/SceneManagement/ScriptableObjects
- Unity Learn, MVC와 MVP (Unity 6 디자인 패턴 과정): https://learn.unity.com/course/design-patterns-unity-6/tutorial/build-a-modular-codebase-with-mvc-and-mvp-programming-patterns?version=6.0 ; e-book "Level up your code with design patterns and SOLID": https://unity3d.jp/wp-content/uploads/2025/07/Level-up-your-code-with-design-patterns-and-SOLID.pdf
- 관리 코드 스트리핑(DI 프레임워크 주의): https://unity.com/blog/engine-platform/tales-from-the-optimization-trenches-better-managed-code-stripping-with-unity-2020 ; WebGL 코드 스트리핑: https://docs.unity3d.com/Manual/webgl-distributionsize-codestripping.html

Open Project 1은 미러의 파일 목록과 커밋 메시지만 확인했다(코드 본문은 안 읽음). PaddleBallSO의 SceneLoader 연결 방식도 소개 페이지 수준으로만 확인했다. 둘 다 "부트 씬 + 로더 + 영구 매니저" 구조의 예로만 인용한다.

---

## 8. 검토 결과 (아키텍트 2026-11-08)

기록: `docs/reviews/2026-11-08-scene-flow-proposal.md`(feat/architecture b3b15ae). **결론: 방향 승인, S0~S2 조건부 진행 가능.** 리드 결정안의 사용자 승인 전에는 구현하지 않는다. Unity 라이선스가 복구될 때까지 배치 명령도 돌리지 않는다.

| 항목 | 결정 | 문서 반영 |
|---|---|---|
| 9장 문안 | 조정해서 반영. 참조 방향은 Controller → View/Model, View → Model은 읽기 전용. `GameSession` → `IBoardPresenter`는 허용 예외. 규칙 거부 명령은 예외 아님. Core `Debug.Assert` 금지. WebGL `webGLExceptionSupport: 1`이라 암묵적 NRE를 못 잡으니 먼저 검사. 적용 범위는 새 코드와 손대는 코드 | 1.2, 1.3, 1.4 |
| `AppFlow` | 로더 null 금지, `stageCount >= 1`, `GoToGame` 범위(`ArgumentOutOfRangeException`), `TryGetBackTarget`/`CanBack`(새 전이 없음), `IsLoading` + `ISceneLoader.Loaded`(`SceneLoader`가 `sceneLoaded`로) | 4.3, 4.4, 5장 S2 |
| `Entered` 이벤트 | 보류(진입 경로 하나 유지) | 4.3, 4.4 |
| `Services` | 중복 등록 예외, `ResetForPlay`(`SubsystemRegistration`)를 `Services` 안에. 부트스트랩 생략은 `Bootstrap` 플래그 | 5장 S0, S1 |
| `LaunchOptions` | 읽기 전용 속성, `TakeReplay()`, `-stageselect`/`-credits` 흡수, `Warnings` 목록(부트스트랩이 로그), 텔레메트리 필드 읽기 호환 | 5장 S1 |
| 수정 권한 | 이 변경에 한해 `Contracts/App/`(`AppFlow`, `Services`, `LaunchOptions`)은 씬 플로우가 수정, 커밋마다 아키텍트 검토. 11장·변경 기록은 아키텍트. 리드가 영향받는 담당에게 고지 | 4.5 |
| `OverlayStack`/`EntryTracker` | `Game/Flow/`(Game 어셈블리) | 4.5 |
| PlayMode asmdef | 지금은 없음. S3 뒤 EditMode로 못 잡는 회귀가 나오면 | 5장 |
| 8장 직접 Play 판단 문장 | 아키텍트가 수정함 | — |
| 기타 | smoke는 리플레이 7개. 씬 파일 소유와 `GameController` 분리 순서(흐름 먼저, 동시 작업 금지)는 리드 결정 | 5장, 6장 |

---

## 변경 기록

| 날짜 | 내용 |
|---|---|
| 2026-11-08 | 1단계 최초 작성(조사·진단·제안). 구현 없음 |
| 2026-11-08 | 아키텍트 검토 반영(8장): 참조 방향, 규칙 거부는 예외 아님, Core `Debug.Assert` 금지, WebGL 예외 지원, S0~S2 승인 조건, `OverlayStack`/`EntryTracker`는 `Game/Flow/`, `Entered` 보류, PlayMode asmdef 없음, smoke 리플레이 7개. origin/7lucky 29b1f4a 합침 |
| 2026-11-09 | 구현 시작(사용자 승인). S0 ①③④ 커밋(5fdaf49, 971ae2f, feb8fcb), 진행 상태 5.2 추가. origin/7lucky 80b74cd 합침 |
| 2026-11-09 | 4.7 뷰 공개 면 합의 초안(게임플레이/UI), 5.1/5.2 순서 정리 |
| 2026-11-09 | 4.7: 실패 결과도 ResultView(NextRequested 없음, bodiesUsed 검사는 클리어일 때만). S0-1 아키텍트 승인, 971ae2f 확인(변경 없음) |
| 2026-11-09 | 4.7: 실패 결과는 오버레이 아님, BoardLost/BoardRecovered → GameFlowController가 ResultView 표시/숨김 |
| 2026-11-09 | S1 커밋 923a1a6, 5.2 갱신 |
| 2026-11-09 | 4.7: HudView에 TileSet 주입(S3). AppConfig.tileSet RuinsHenge_Chapter01. origin/7lucky 6cf3de4 합침 |
| 2026-11-09 | S2 커밋 f08f4d6, 4.7 확정안, 5.2 갱신 |
| 2026-11-09 | S2 아키텍트 승인, S5에 로드 실패 경로 조건 추가. origin/7lucky 0ca8ae6 합침 |
| 2026-11-09 | S3 완료(83a55d6, 70ccc03, 0fc35a3, 18029f4), 5.2 갱신. origin/7lucky 81262de 합침 |
| 2026-11-09 | S4 커밋 753fc60, 5.2 갱신 |
| 2026-11-09 | S3 아키텍트 승인(42613fb) |
| 2026-11-09 | S4 승인, SettingsController가 SaveSystem 명령 사용, ChapterEntering 정규화 메모. origin/7lucky 6b9c419 합침 |
| 2026-11-09 | S6 씬 플로우 몫 완료(07927a9, 0b3d260), 5.2 갱신 |
| 2026-11-09 | 12.8 챕터 연결 요청(게임플레이/UI)을 대기 항목으로 기록 |
| 2026-11-09 | S5 페이드, GameAudio 음량, 5.2 갱신. origin/7lucky 0c62e54 합침 |
| 2026-11-09 | S5 아키텍트 승인. 씬 플로우 S0~S6 완료, 12.8 챕터 연결 대기 |
