# 일곱 걸음 씬 플로우 — Unity 씬 구조

담당: 씬 플로우 엔지니어 (`feat/scene-flow`). 브리프: `agents/scene-flow.md`, `C:/unityjam/shared-assets/seven-steps/briefs/scene-flow-unity-2026-11-11.md`.
상태: **리드 승인 (2026-11-11, 아키텍트 조건부 승인 `docs/reviews/2026-11-11-scene-flow-unity-structure.md`, M1~M3 반영). P1부터 구현 중.**

이 문서는 **Unity 씬 구조**만 다룬다:
- 어떤 씬이 빌드 목록 몇 번에 있는지
- 언제 어떤 모드로 로드·언로드되는지
- 씬을 넘어 살아남는 것은 무엇인지

씬 안에서 일어나는 화면 변화는 씬 플로우가 아니다. 예를 들어 Title ⇄ 행성 지도 패널, Game → 결과, 다이브·이어 가기·행성 완료 복귀의 카메라 이동이 그렇다. 이것들은 7장 "씬 안 전환"에 모으고 `docs/stage-select-transitions.md`를 가리킨다.

이전 문서(2026-11-08 조사·진단·S0~S8 이행 기록)는 `docs/scene-flow-2026-11-08.md`로 옮겼다. 코드 주석의 `docs/scene-flow.md S3`, `3.1`, `4.3` 같은 참조는 그 파일의 절 번호다. 코드 규칙(객체지향·MVC·단정적 프로그래밍)은 이 문서 11장에 그대로 둔다.

---

## 0. 요약

- **지금 씬 세 개**: 0 `Boot`(화면 없음, 서비스 등록) → 1 `Title`(타이틀 + 행성 지도) → 2 `Game`(보드, HUD, 결과).
- **씬 전환**: 모두 `LoadSceneMode.Single` + `SceneManager.LoadSceneAsync`다.
  - 씬을 넘어 사는 것은 `DontDestroyOnLoad` 루트 오브젝트 최대 둘이다: `TelemetryRunner`(텔레메트리가 켜졌을 때만), `SceneFader`(첫 씬 로드 뒤).
  - Boot를 상주 씬으로 두고 다른 씬을 Additive로 붙이는 방식은 쓰지 않는다. WebGL에서는 메모리 회수(`Resources.UnloadUnusedAssets` 자동 호출)와 단순함이 더 중요하다(3장).
- **첫 실행 로딩 씬(`Loading`)은 미구현·보류다** (사용자 지시 2026-11-11). 4장에 자리만 잡아 둔다: 이름, 빌드 목록 위치, `LoadSceneAsync` 연결 방식.
  - 씬 파일·애니메이션·아트는 만들지 않는다.
  - 그때 필요한 코드 고리(`ISceneLoader`의 활성화 보류)도 그 작업과 함께 넣는다.
- **에디터 직접 Play**: `Title`이나 `Game`을 바로 Play해도 지금처럼 부트스트랩한다(5장).
- **확인**: 플레이어 빌드가 아니라 에디터 Play Mode(PlayMode 테스트, Play Mode 캡처)로 한다(9장).

---

## 1. Unity 용어와 이 프로젝트에서의 뜻

| Unity 용어 (Manual / Scripting API) | 이 프로젝트 |
|---|---|
| [Scene](https://docs.unity3d.com/6/Documentation/Manual/CreatingScenes.html) | `unity/Assets/_Project/Scenes/*.unity`. 씬 이름 상수는 `AppScenes`(`Contracts/App/AppFlow.cs`) |
| [Build Profiles — Scene List](https://docs.unity3d.com/6/Documentation/Manual/build-profile-scene-list.html), [`Scene.buildIndex`](https://docs.unity3d.com/6/Documentation/ScriptReference/SceneManagement.Scene-buildIndex.html) | `ProjectSettings/EditorBuildSettings.asset`(리드 소유). Scene List 페이지: 목록 순서대로 빌드한다. `Scene.buildIndex` 페이지: index는 목록 위치이고 0부터 센다(0번이 시작 씬). 프로필별 목록 덮어쓰기(Override Global Scene List)는 쓰지 않는다. 이유는 이슈 트래커의 [알려진 문제](https://issuetracker.unity.com/issues/21328/build-profile-with-override-global-scene-list-ignored-when-using-sceneutilitygetbuildindexbyscenepath-in-onbeforeserialize-before-domain-reload)다(`SceneUtility.GetBuildIndexByScenePath`가 전역 목록만 본다) |
| [Splash Screen (Player Settings › Splash Image)](https://docs.unity.com/en-us/engine/6000.5/manual/unity-editor/editor-settings-reference/comp-manager-group/class-player-settings-splash-screen), [`PlayerSettings.SplashScreen`](https://docs.unity3d.com/ScriptReference/PlayerSettings.SplashScreen.html) | Unity 로고 스플래시다. 보류한 `Loading` 씬과는 다른 것이다. 지금 켜져 있다(`m_ShowUnitySplashScreen: 1`, 배경 `#231F20`). 4.3 참고 |
| [`SceneManager.LoadSceneAsync`](https://docs.unity3d.com/6/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html), [`AsyncOperation`](https://docs.unity3d.com/6/Documentation/ScriptReference/AsyncOperation.html) (`progress`, [`allowSceneActivation`](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/asyncoperation/allowsceneactivation)) | `SceneLoader`(`Game/App/SceneLoader.cs`)만 부른다. `allowSceneActivation = false`면 `progress`가 0.9에서 멈추고 `isDone`은 거짓이다. 그동안 다른 비동기 씬 작업(언로드 포함)도 대기한다. 지금은 `-loadDelay`(개발용)만 이 보류를 쓴다 |
| [`LoadSceneMode.Single` / `Additive`](https://docs.unity3d.com/6/Documentation/ScriptReference/SceneManagement.LoadSceneMode.html) | Single만 쓴다(3장). Single 로드는 이전 씬을 모두 닫고 `Resources.UnloadUnusedAssets`를 자동으로 부른다 |
| Active scene: [`SceneManager.SetActiveScene`](https://docs.unity3d.com/6/Documentation/ScriptReference/SceneManagement.SceneManager.SetActiveScene.html), [`GetActiveScene`](https://docs.unity3d.com/6/Documentation/ScriptReference/SceneManagement.SceneManager.GetActiveScene.html) | Single만 쓰므로 로드된 씬이 곧 활성 씬이다. `SetActiveScene`은 부르지 않는다. 라이팅·`RenderSettings`는 활성 씬의 것을 쓴다([Multi-scene editing](https://docs.unity.com/en-us/engine/6000.5/manual/working-with-scenes/multi-scene-editing/setupmultiplescenes)). `SceneLoader.Current`는 활성 씬 이름이다 |
| [`SceneManager.UnloadSceneAsync`](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/scenemanagement/scenemanager/unloadsceneasync) | 쓰지 않는다. 씬 오브젝트만 지우고 에셋은 남기므로 `Resources.UnloadUnusedAssets`를 따로 불러야 한다. 마지막 남은 씬은 언로드할 수 없다 |
| [`Resources.UnloadUnusedAssets`](https://docs.unity3d.com/6/Documentation/ScriptReference/Resources.UnloadUnusedAssets.html) | Single 로드가 자동으로 부른다. 직접 부르지 않는다 |
| [`Object.DontDestroyOnLoad`](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/object/dontdestroyonload) / 상주(bootstrap) 씬 | DDOL 루트 오브젝트는 0~2개다: `TelemetryRunner`는 텔레메트리가 켜졌을 때만(엔드포인트가 없거나 `telemetry=off`면 `NullTelemetry`, 오브젝트 없음), `SceneFader`는 첫 `SceneLoader.Load` 뒤에 생긴다. 상주 씬(항상 열린 Boot + Additive)은 쓰지 않는다 |
| [`RuntimeInitializeOnLoadMethod`](https://docs.unity3d.com/6/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html) (`SubsystemRegistration`) | 정적 상태를 Play 시작 때 비운다(`Bootstrap.ResetForPlay`, `Services`, `LevelCatalog` 등). 도메인 리로드가 꺼져 있어서 필수다 |
| [Enter Play Mode Options](https://docs.unity3d.com/6/Documentation/Manual/configurable-enter-play-mode.html), [도메인 리로드 끄기](https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html) | `ProjectSettings/EditorSettings.asset`: `m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 1`(도메인 리로드 끔, 씬 리로드는 함). 정적 필드와 정적 이벤트 구독이 Play 사이에 남는다 |
| [Animation Clip](https://docs.unity3d.com/6000.4/Documentation/Manual/AnimationOverview.html) + Animator / Timeline | 보류한 `Loading` 씬에서 쓸 방식(4.2). 전체 Timeline 패키지는 없고 `timelinefoundation` 모듈만 있다 |

---

## 2. 씬 목록 (Build Profiles Scene List)

| build index | 씬 | 로드 모드 | 루트 오브젝트 | 소유 |
|---|---|---|---|---|
| 0 | `Boot` | 시작 씬 | `Boot`(`BootScreen` → `Bootstrap.Run`). 화면 없음, 카메라 없음 | scene-flow |
| 1 | `Title` | `LoadSceneAsync` Single | `Title`(`TitleScreen`), 카메라, 이벤트 시스템. 행성 지도(v4)·패널은 루트가 코드로 만든다 | scene-flow(씬 파일), gameplay-ui(화면 내용) |
| 2 | `Game` | `LoadSceneAsync` Single | `Game`(`GameController`), 카메라, 조명 셋(key/fill/rim), 이벤트 시스템. 보드·HUD·결과는 루트가 만든다 | scene-flow(씬 파일), gameplay-ui(보드·HUD) |
| — | (DDOL, 0~2개) | — | `TelemetryRunner`(Boot의 `Bootstrap.Run`이 텔레메트리가 켜졌을 때만 생성), `SceneFader`(첫 `SceneLoader.Load`에서 생성) | scene-flow |
| (1, 보류) | `Loading` | `LoadSceneAsync` Single | **미구현·보류.** 4장 | scene-flow |

- 빌드 목록은 지금(`Boot`, `Title`, `Game`) 그대로다. 변경 요청은 없다. `Loading`을 만들 때 넣을 변경은 4.4에 적어 둔다.
- 씬은 이름으로 로드한다(`AppScenes`). 빌드 목록의 순서나 index에 기대는 코드는 없다.
- 이름 중복·누락, Boot가 0번인지는 `SceneFlowValidator`(`unity.sh scene-validate`)가 빌드 전에 잡는다.

---

## 3. 상주 씬 + Additive 대 `DontDestroyOnLoad` + Single — 결정

| | A. Boot 상주 + Additive | B. DDOL + Single (지금, **권장**) |
|---|---|---|
| 방식 | Boot가 끝까지 열려 있다. Title/Game을 `LoadSceneAsync(Additive)` → `SetActiveScene` → 이전 씬 `UnloadSceneAsync` | 각 씬을 `LoadSceneAsync(Single)`로 바꾼다. 앱 전체 오브젝트는 DDOL 루트 |
| 메모리 (WebGL) | 언로드는 에셋을 놓지 않아 매번 `Resources.UnloadUnusedAssets`를 직접 불러야 한다. 바꾸는 동안 두 씬이 동시에 올라와 최고 메모리가 높다 | Single 로드가 이전 씬을 닫고 `UnloadUnusedAssets`를 자동으로 부른다. 한 번에 한 씬만 있다 |
| 활성 씬 | 매번 `SetActiveScene`이 필요하다. 안 하면 라이팅·`RenderSettings`·새 오브젝트가 Boot에 붙는다. 이 게임은 행성마다 조명·환경광을 바꾼다(`ChapterStage`) | 로드한 씬이 곧 활성 씬이다 |
| 카메라·`AudioListener`·`EventSystem` | 씬마다 하나씩, 겹치지 않게 관리해야 한다 | 씬마다 자기 것이 있다 |
| 에디터에서 바로 Play | Boot를 강제로 같이 열어야 한다 | 지금처럼 각 루트가 `Bootstrap.Run`을 부른다(5장) |
| 얻는 것 | 상주 오브젝트를 씬 파일(인스펙터)로 편집할 수 있다 | — |

**결정: B 유지.**
- 상주 오브젝트가 최대 둘뿐이라 A의 이득(인스펙터 편집)이 작다.
- WebGL 힙은 작고, 한 번 늘면 줄지 않는다. 두 씬이 겹치는 순간이 없고 에셋 해제가 자동인 B가 안전하다.
- 잼 이후 음악·오디오 매니저가 커지면 다시 본다.

---

## 4. 첫 실행 로딩 씬 `Loading` — 미구현·보류

> **미구현·보류 (사용자 지시 2026-11-11).** 이 장은 자리만 잡아 둔다. 씬 파일·애니메이션·아트·코드는 만들지 않는다. 다시 시작할 때 이 장을 출발점으로 쓰고, 리드 승인을 다시 받는다.

### 4.1 자리

- **이름**: `Loading` (`AppScenes.Loading`, `AppState.Loading`. `AppState`는 append-only라 끝에 더한다).
- **빌드 목록 위치**: build index **1**, `Boot`(0) 바로 뒤. `Title`과 `Game`은 한 칸씩 밀린다. 코드는 이름으로 로드하므로 영향이 없다.
- **언제 거치는가**: 일반 실행에서만 Boot → `Loading` → Title. 다음 경우에는 건너뛴다.
  - `-level`/`?level=`: Boot → Game
  - `-titlecheck`, 타이틀 스크린샷
  - 에디터에서 Title/Game을 바로 Play

### 4.2 `LoadSceneAsync` 연결 방식 (그때 구현할 내용)

```
0 Boot     Bootstrap.Run → flow.Go(Loading)        LoadSceneAsync(Loading, Single), 페이드 없음
1 Loading  Start → flow.Go(Title, mayActivate)     LoadSceneAsync(Title, Single), allowSceneActivation = false
           (애니메이션·진행 표시)                     progress >= 0.9 && mayActivate() → allowSceneActivation = true
2 Title
```

- `ISceneLoader.Load(string sceneName, Func<bool> mayActivate = null)`와 `AppFlow.Go(AppState to, Func<bool> mayActivate = null)`를 더한다.
  - `null`이면 지금과 같다.
  - 지금 `-loadDelay`가 쓰는 보류 코드를 이 길로 합친다.
  - 공용 API 변경이라 고지 대상이다.
- `Loading`은 Title 로드의 `progress`(`SceneLoader`가 `progress / 0.9`로 연다)를 숫자 없이 보여 준다.
- 끝 조건은 `max(최소 시간, Title 준비)`다. 두 번째 실행부터 입력으로 건너뛰는 판단은 저장 데이터로 한다. 정적 상태는 쓰지 않는다(도메인 리로드 꺼짐).
- 애니메이션 방식은 그때 정한다(Animation Clip + Animator 권장, 4.1 표 참고). 하늘은 기존 하늘 프리셋을 쓰고, 하늘 파일 자체는 고치지 않는다.
- `session_start`는 Boot에서 나가므로 `Loading`보다 먼저다(8.3).

### 4.3 WebGL 로더 페이지와 Splash Screen (지금 상태 기록)

- `unity/Assets/WebGLTemplates/SevenSteps/index.html`: 배경 `#101C32`, 가운데 가로 40% 금색(`#C9AB6B`) 진행 막대. `createUnityInstance` 진행률로 채우고 끝나면 숨긴다.
- 그다음 Unity Splash Screen(켜짐, 배경 `#231F20`) → Boot(카메라 없음) → Title.
- `Loading`을 만들 때 이어 보이게 할 것:
  - Boot·`Loading`의 첫 배경을 `#101C32`로 맞춘다.
  - 금색은 `#C9AB6B`로 맞춘다.
  - 스플래시를 끄거나(가능하면) 배경색을 `#101C32`로 바꾼다. ProjectSettings는 리드 소유다.

### 4.4 그때 리드에게 보낼 빌드 목록 변경

`unity/ProjectSettings/EditorBuildSettings.asset`의 `m_Scenes`(guid는 `Loading.unity.meta`를 커밋한 뒤의 값):

```yaml
  m_Scenes:
  - enabled: 1
    path: Assets/_Project/Scenes/Boot.unity
    guid: ef0b755a6dbdc9e4bb34f9e00121e3bc
  - enabled: 1
    path: Assets/_Project/Scenes/Loading.unity
    guid: <Loading.unity.meta guid>
  - enabled: 1
    path: Assets/_Project/Scenes/Title.unity
    guid: 0fcee8375b24bf045a5374880e8bbadb
  - enabled: 1
    path: Assets/_Project/Scenes/Game.unity
    guid: a1f7918da179aee448693d4a3de923ce
```

---

## 5. 에디터에서 아무 씬이나 Play

| Play한 씬 | 동작 |
|---|---|
| `Boot` | 일반 실행과 같다: Boot → Title (`-level` 인자가 있으면 Game) |
| `Title` | `TitleScreen.Awake`가 `Bootstrap.HasRunThisPlay`를 보고, 없으면 `Bootstrap.Run(enterFirstScreen: false)`를 부른다. `TitleFlowController.Start`가 Boot → Title 전이를 직접 부른다 |
| `Game` | `GameController.Awake`가 같은 방식으로 부트스트랩한다(인스펙터 `startLevel`/`replay` = `-level`/`-replay`). `GameFlowController.Start`가 Boot → Game 전이를 부른다 |

- 도메인 리로드가 꺼져 있으므로 Play 사이에 남는 정적 상태는 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`로 비운다. 지금 대상은 `Bootstrap.HasRunThisPlay`, `Services`, `LevelCatalog`, `Ui.Font` 등이다.
- 새 정적 상태는 만들지 않는다. 꼭 필요하면 같은 방식으로 비우고, PlayMode 테스트(9장)의 "두 번 연속 Play" 항목으로 확인한다.

---

## 6. Unity 씬 플로우 다이어그램 (씬 로드·언로드만)

```
                (앱 시작: build index 0)
                        │
                        ▼
                 ┌─────────────┐  Bootstrap.Run → Services, DDOL TelemetryRunner, session_start
                 │  0 Boot      │
                 └─────────────┘
           -level/?level= │                 │ 일반 실행        [보류: 일반 실행은 여기서 1 Loading을 거침, 4장]
                          │                 ▼
                          │        LoadSceneAsync(Title, Single)   (Boot는 그리지 않으므로 페이드 아웃 없음)
                          │         ┌─────────────┐  ◄──────────────────────────────┐
                          │         │  1 Title     │                                 │
                          │         └─────────────┘                                 │
                          │                │ 스테이지 선택 (지도·이어하기·처음부터)   │ 스테이지 선택으로 / 행성 완료 / 엔딩 끝
                          ▼                ▼ LoadSceneAsync(Game, Single)            │ LoadSceneAsync(Title, Single)
                 ┌──────────────────────────┐                                       │
                 │  2 Game                   │ ──────────────────────────────────────┘
                 └──────────────────────────┘
   모든 Single 로드: 이전 씬 언로드 + Resources.UnloadUnusedAssets(자동). DDOL 루트(최대 둘)는 남는다.
   Title→Game, Game→Title: SceneFader 0.15초 페이드 아웃 → LoadSceneAsync → (0.3초 넘으면 로딩 화면) → 페이드 인.
   sceneLoaded(새 씬 Awake 뒤, Start 전)에서 AppFlow의 로딩 상태가 풀린다.
```

씬을 바꾸지 않는 상태 변화는 이 그림에 없다(7장). 예: `AppFlow`의 Title ⇄ StageSelect, Game ⇄ Result, Game → Game 다음 스테이지.

---

## 7. 씬 안 전환 (in-scene transitions)

씬을 로드하지 않고 한 씬 안에서 바뀌는 것들이다. Unity 씬 구조와는 관계없고, 화면 흐름 컨트롤러가 맡는다.

| 씬 | 전환 | 기준 문서 / 코드 |
|---|---|---|
| Title | Title 패널 ⇄ 행성 지도(StageSelect), 행성 표면 길, 설정·크레딧 오버레이 | `TitleFlowController`, `StageSelectController`, `OverlayStack` |
| Title | 다이브 앞부분(지도 → 행성 꼭대기), 행성 완료 복귀의 뒤로 빠지기 | `docs/stage-select-transitions.md`, `TitleJourney` |
| Game | 스테이지 진입, 결과(Result), 다음 스테이지 | `GameFlowController` |
| Game | 다이브 도착, 이어 가기(다음 보드가 오른쪽에서 솟음), 행성 완료 뒤로 빠지기 | `docs/stage-select-transitions.md`, `GameJourney` |
| Game | 챕터 카드, 메뉴, 엔딩 카드 | `ChapterIntroController`, `MenuController`, `EndingController` |

다이브와 복귀는 씬 경계를 넘는다. 이때 씬 경계 자체는 6장의 Single 로드이고, 그 앞뒤의 카메라 이동만 씬 안 전환이다. 둘 사이는 순수 C# 모델 `JourneyDive`/`JourneyReturn`(Services)이 넘겨준다.

---

## 8. 코드에서 바뀌는 것 (이번 작업)

### 8.1 바꾸지 않는 것

- 다음은 그대로다:
  - `AppState` / `AppScenes` / `AppFlow`의 공개 API
  - `ISceneLoader`
  - 빌드 목록
  - `SceneLoader`의 로드 방식(LoadSceneAsync Single + 페이드 + 로딩 화면)
- 4.2의 `mayActivate` 고리는 `Loading` 작업과 함께 넣는다. 쓰는 곳 없이 미리 넣지 않는다.

### 8.2 바꾸는 것

| 항목 | 내용 |
|---|---|
| 용어 정리 (코드 주석) | `SceneLoader`·`AppFlow`·`Bootstrap` 주석을 Unity 용어로 맞춘다(Single 로드, 활성 씬, DDOL 루트, build index). 동작은 그대로 |
| `SceneFlowValidator` | 빌드 목록이 기대 순서(Boot 0, Title 1, Game 2)인지 검사한다. 기대 순서는 `AppScenes.BuildOrder` 한 곳에서 가져온다(아키텍트 N3: `Loading`을 넣을 때 이 목록만 바꾼다). 그리고 DDOL을 만드는 코드가 두 곳뿐인지 문서로 고정한다 |
| `Boot` 카메라 | Boot에 배경 `#101C32` 카메라 하나. 카메라 없는 씬이 한 프레임 그려질 때의 기본색(WebGL 회색/검정)을 로더 페이지 색으로 맞춘다. 씬 파일 변경이라 PlayMode 캡처로 확인 |
| PlayMode 테스트 (새) | `Tests/PlayMode` 어셈블리 + `unity.sh playtest`(9장) |
| Play Mode 캡처 (새) | 에디터 스크립트 + `unity.sh playcapture`(9장) |
| journey 확인 이전 | 플레이어 빌드 `smoke`의 `-journeycheck`를 PlayMode 테스트로 옮긴다 |

### 8.3 텔레메트리 시점 (바뀌지 않음)

| 이벤트 | 내는 곳 | 이번 작업 뒤 |
|---|---|---|
| `session_start` | `TelemetryRunner.CreateTelemetry`(`Bootstrap.Run`, Boot 씬) | 그대로. 보류한 `Loading`보다 먼저 나간다 |
| `level_start` (`via`) | `GameFlowController.Enter` → `BoardController.LoadStage` → `StageEntered` | 그대로 |
| `level_leave`, `level_clear`/`level_fail`, `session_end` | 보드·러너 | 그대로. 러너는 DDOL이다 |

---

## 9. 확인 방법 (에디터 Play Mode, 2026-11-11 규칙)

플레이어 빌드(WebGL·Standalone)로 동작이나 화면을 확인하지 않는다. WebGL 빌드는 리드가 플레이테스트·출시 빌드를 만들 때만 한다.

| 무엇 | 방법 |
|---|---|
| 컴파일 | `unity.sh compile` (`BuildTools.CompileCheck`) |
| 순수 로직 | EditMode 테스트 (`unity.sh test`): `AppFlow` 전이, 가짜 `ISceneLoader`, 흐름 컨트롤러, journey 모델 |
| 씬 흐름 | **PlayMode 테스트**(새 `Tests/PlayMode` 어셈블리, `-runTests -testPlatform PlayMode`, `unity.sh playtest`). 항목은 아래 목록 |
| 화면 | Play Mode 캡처(9.2, `unity.sh playcapture`) → `reports/playcapture/` |
| 빌드 전 검사 | `unity.sh scene-validate` |

PlayMode 테스트 항목:
- Boot를 열면 Title이 활성 씬이 되고 `SceneManager.sceneCount == 1`이다.
- DDOL 루트: 이름마다 1개를 넘지 않는다. 씬을 한 번이라도 로드한 뒤에는 `SceneFader`가 정확히 1개다. 테스트는 텔레메트리를 끄므로 `TelemetryRunner`는 0개다(아키텍트 M1).
- Title → Game → Title 왕복 뒤에도 DDOL 루트 수가 늘지 않는다.
- `-level`과 같은 시작으로 Boot → Game이 된다.
- Title·Game을 바로 열어도 부트스트랩된다.
- 두 번 연속 Play(도메인 리로드 없음)에서 서비스가 새로 만들어진다.
- journey 이어 가기·행성 완료 복귀가 동작한다.

### 9.1 PlayMode 테스트의 실행 환경 (아키텍트 N4·N5)

- 어셈블리 `SevenSteps.Tests.PlayMode`(`Tests/PlayMode/`, asmdef는 아키텍트 소유):
  - references: `SevenSteps.Core`, `SevenSteps.Contracts`, `SevenSteps.Game`, `UnityEngine.TestRunner` (`UnityEditor.TestRunner`는 넣지 않는다)
  - `includePlatforms: []`, `overrideReferences` + `nunit.framework.dll`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` (플레이어 빌드에 들어가지 않는다)
- 실행 옵션 주입: `-runTests` 실행에는 테스트마다 `-level`·`-telemetry off`·`-savekey`를 줄 수 없다. 그래서 `Bootstrap.UseLaunchOptionsForNextRun(LaunchOptions)`(테스트·캡처용 진입점)을 둔다. 다음 `Bootstrap.Run`은 명령줄 대신 이 값을 쓰고 한 번 쓰면 지운다. 정적 값이므로 `SubsystemRegistration`에서도 지운다.
- 저장: 테스트는 `-savekey` 접미사 `playtest`로 저장 키를 나눈다(`SaveStores.For`). 시작할 때와 끝날 때 그 키를 지운다. 개발자의 PlayerPrefs 진행은 건드리지 않는다.
- 텔레메트리: 테스트는 `telemetry=off`. 네트워크를 쓰지 않는다.
- 도메인 리로드: Unity Test Framework는 프로젝트의 Enter Play Mode 설정을 그대로 쓴다. 그래서 "두 번 연속 Play" 항목이 실제 설정을 검사한다.

### 9.2 Play Mode 캡처 (P2, 아키텍트 M3)

- **방법**: `-batchmode` 없이 에디터를 연다. `-batchmode`에는 Game 뷰 창이 없고, `-nographics`면 아무것도 그려지지 않는다. 카메라 → `RenderTexture` → `ReadPixels`는 Screen Space - Overlay uGUI(HUD·타이틀·결과)를 찍지 못하므로 쓰지 않는다.
  - `unity.sh playcapture <이름>`이 `Unity.exe -projectPath unity -executeMethod SevenSteps.EditorTools.PlayCapture.Run -playcapture <이름>`을 실행한다(에디터 창이 잠깐 뜬다).
  - 에디터 스크립트 `PlayCapture`는 다음 순서로 동작한다:
    1. Game 뷰 해상도를 고정한다.
    2. 캡처 이름에 맞는 `LaunchOptions`를 9.1의 진입점으로 넣는다.
    3. 시작 씬(Boot)을 열고 `EditorApplication.EnterPlaymode()`.
    4. 정해진 시점(예: Title 활성 + 1초)에 `WaitForEndOfFrame` 뒤 `ScreenCapture.CaptureScreenshotAsTexture()`로 찍어 PNG로 저장한다.
    5. `EditorApplication.ExitPlaymode()`, `EditorApplication.Exit(0)`.
- **해상도**: 1280×720 고정. `PlayModeWindow.SetCustomRenderingResolution(1280, 720, "playcapture")`로 Game 뷰 크기를 정하고, 찍은 텍스처 크기가 1280×720이 아니면 실패로 본다.
- **빈 화면 대조**(smoke-webgl의 `?smoke=empty`와 같은 뜻): 캡처마다 두 장을 찍는다.
  - 본 캡처: `SmokeView.Board`(하늘·행성 끄고 카메라 배경 마젠타 `#FF00FF`, 화면 UI 숨김)로 보드 영역(가로 30~70%, 세로 33~66%)에서 마젠타와 다른 픽셀이 500 이상이어야 한다.
  - 대조: `SmokeView.Empty`(보드까지 숨김)로 같은 영역이 500 미만이어야 한다. 대조가 500 이상이면 측정이 망가진 것으로 보고 실패.
  - 보통 화면(UI 포함)은 같은 실행에서 따로 한 장 저장한다(눈으로 보는 용도). Title처럼 보드가 없는 화면은 대조를 "UI 켬 vs 숨김" 픽셀 차이로 한다.
- 결과: `reports/playcapture/<이름>.png`, `<이름>-board.png`, `<이름>-empty.png`, 로그 한 줄 `[SevenSteps] Play capture <이름>: OK|FAIL (board N, empty M)`.

기존 `smoke`(플레이어 빌드 리플레이·flowcheck·titlecheck·journey)는 리드의 빌드 확인용으로 남긴다. scene-flow는 같은 확인을 PlayMode 테스트로 옮긴다.

---

## 10. 구현 순서 (리드 승인 뒤)

각 단계가 끝나도 게임은 돈다. 단계마다 compile + EditMode, P1부터 PlayMode 테스트도 한다.

| 단계 | 내용 | 확인 |
|---|---|---|
| P1 | `Tests/PlayMode` 어셈블리 + `unity.sh playtest`. 지금 흐름(Boot→Title→Game→Title, 직접 Play, `-level` 시작, 두 번 Play)을 PlayMode 테스트로 | PlayMode |
| P2 | Play Mode 캡처 에디터 스크립트 + `unity.sh playcapture` | 캡처 |
| P3 | `SceneFlowValidator` 빌드 목록 순서 검사, `Boot` 배경 카메라, 코드 주석 용어 정리 | scene-validate, PlayMode, 캡처 |
| P4 | journey 확인(`-journeycheck advance/complete`)을 PlayMode 테스트로 옮김 | PlayMode |
| (보류) | `Loading` 씬(4장): 사용자가 다시 요청할 때 | — |

---

## 11. 코드 규칙 (구조 원칙, 사용자 지시 2026-11-08)

이전 문서 1장을 그대로 옮겼다. 기준은 `docs/architecture.md` 9장이다.

### 11.1 객체지향

- 클래스 하나는 책임 하나를 맡는다. `MonoBehaviour` 하나가 입력·흐름·사운드·저장·UI 생성을 함께 하지 않는다.
- 상태는 캡슐화한다. 다른 클래스가 읽는 값은 `{ get; private set; }`이나 메서드로 연다. 공개 가변 필드는 Unity 직렬화 필드(인스펙터 연결)와 `JsonUtility` DTO(`ProgressData`, `SettingsData`)에만 쓴다.
- 의존은 생성자로 받는다. 외부 시스템(씬 로드, 저장소, 시계, 텔레메트리)은 인터페이스로 받는다. 예: `ISceneLoader`, `ISaveStore`, `ITelemetry`.
- 상속보다 조합을 쓴다. 씬 루트 `MonoBehaviour`는 순수 C# 컨트롤러와 뷰를 만들어 연결하는 일(조립, composition root)만 한다.
- `Services`(서비스 로케이터)는 **씬 루트에서만** 읽는다. 컨트롤러와 뷰는 필요한 것을 생성자나 `Bind(...)`로 받는다.

### 11.2 MVC

| 층 | 무엇 | 규칙 |
|---|---|---|
| Model | `SevenSteps.Core`(규칙), `GameSession`, `AppFlow`(화면 상태), `SaveSystem`(진행·설정), `OverlayStack`, `EntryTracker`, `JourneyDive`/`JourneyReturn` | 순수 C#. 변경은 C# 이벤트로 알린다. View를 모른다. 허용되지 않은 명령은 예외 |
| View | `BoardView`, `HudView`, 각 화면과 패널 | 표시만 한다. 사용자 조작은 이벤트로 낸다. Model을 직접 바꾸지 않는다 |
| Controller | 보드 진행(`BoardController`), 화면 흐름(`TitleFlowController`, `GameFlowController`, `BackNavigator`, `GameJourney`, `TitleJourney`) | View 이벤트와 입력을 받아 Model 명령을 부른다. Model 이벤트를 받아 View를 갱신한다 |

- 참조 방향: Controller → View, Controller → Model, View → Model은 읽기 전용 데이터만이다.
- 예외는 하나다. `GameSession`이 Contracts 인터페이스 `IBoardPresenter`로 연출을 부른다(architecture.md 4장).
- 보드 진행 컨트롤러와 화면 흐름 컨트롤러는 다른 클래스다.

### 11.3 단정적(assertive) 프로그래밍

1. 공개 메서드의 전제 조건은 시작 부분에서 검사하고, 어기면 던진다(`ArgumentException` 계열, `InvalidOperationException`). 게임 규칙상 안 되는 명령은 예외가 아니다(`Rules.Dispatch`의 결과).
2. 내부 불변식은 `UnityEngine.Assertions.Assert`로 확인한다(Game·Contracts). Core에서는 `Debug.Assert`를 쓰지 않는다. 플레이어에서도 반드시 막아야 하는 것은 1번(예외)으로 쓴다. WebGL은 명시적 예외만 잡으므로 null은 먼저 검사해서 던진다.
3. "있어야 하는" 인스펙터 참조는 `Awake`에서 검사하고, 비었으면 필드 이름을 넣어 예외를 던진다. `OnValidate`에서는 `Debug.LogError(..., this)`로 표시한다.
4. fail fast. 예외는 외부 입력 세 가지뿐이고(실행 인자·URL, 저장 데이터 손상, 네트워크), 이때도 명시적으로 복구하고 로그를 남긴다. 지금 할 수 없는 사용자 입력은 컨트롤러가 조건을 코드에 드러내고 무시한다.
5. 씬·프리팹·`AppConfig`·`TileSet`의 필수 연결은 에디터 검증기(`SceneFlowValidator`, `TileSetValidator`)가 빌드 전에 잡는다.

---

## 12. 출처

- Build Profiles Scene List: https://docs.unity3d.com/6/Documentation/Manual/build-profile-scene-list.html
- `Scene.buildIndex`: https://docs.unity3d.com/6/Documentation/ScriptReference/SceneManagement.Scene-buildIndex.html
- 프로필 목록 덮어쓰기와 `GetBuildIndexByScenePath` 문제: https://issuetracker.unity.com/issues/21328/build-profile-with-override-global-scene-list-ignored-when-using-sceneutilitygetbuildindexbyscenepath-in-onbeforeserialize-before-domain-reload
- Splash Image Player settings: https://docs.unity.com/en-us/engine/6000.5/manual/unity-editor/editor-settings-reference/comp-manager-group/class-player-settings-splash-screen
- `PlayerSettings.SplashScreen`: https://docs.unity3d.com/ScriptReference/PlayerSettings.SplashScreen.html
- `SceneManager.LoadSceneAsync`: https://docs.unity3d.com/6/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html
- `AsyncOperation.allowSceneActivation`: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/asyncoperation/allowsceneactivation
- `LoadSceneMode`: https://docs.unity3d.com/6/Documentation/ScriptReference/SceneManagement.LoadSceneMode.html
- `SceneManager.UnloadSceneAsync`: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/scenemanagement/scenemanager/unloadsceneasync
- `SceneManager.SetActiveScene`: https://docs.unity3d.com/6/Documentation/ScriptReference/SceneManagement.SceneManager.SetActiveScene.html
- Multi-scene editing (활성 씬과 라이팅): https://docs.unity.com/en-us/engine/6000.5/manual/working-with-scenes/multi-scene-editing/setupmultiplescenes
- `Resources.UnloadUnusedAssets`: https://docs.unity3d.com/6/Documentation/ScriptReference/Resources.UnloadUnusedAssets.html
- `Object.DontDestroyOnLoad`: https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/object/dontdestroyonload
- `RuntimeInitializeOnLoadMethodAttribute`: https://docs.unity3d.com/6/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html
- Configurable Enter Play Mode: https://docs.unity3d.com/6/Documentation/Manual/configurable-enter-play-mode.html
- Enter Play Mode without domain reload: https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html
- Animation overview (Animation Clip, Animator Controller): https://docs.unity3d.com/6000.4/Documentation/Manual/AnimationOverview.html
- 이전 조사 출처(2026-11-08): `docs/scene-flow-2026-11-08.md` 7장
