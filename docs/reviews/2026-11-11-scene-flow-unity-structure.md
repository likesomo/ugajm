# 검토: feat/scene-flow 21ea3d8 (docs/scene-flow.md를 Unity 씬 구조로 다시 씀, docs/scene-flow-2026-11-08.md 보관)

검토자: 아키텍트, 2026-11-11. 기준 7lucky 83fd385. 문서 검토다. 코드 변경은 없다(21ea3d8은 문서 두 개만 바꿈).
- 문서 내용을 다음과 대조했다:
  - 코드: `EditorBuildSettings.asset`, `EditorSettings.asset`, `ProjectSettings.asset`(스플래시), `AppFlow.cs`(`AppScenes`), `SceneLoader`, `Bootstrap`, `TelemetryRunner`, `SceneFlowValidator`, WebGL 템플릿, `manifest.json`
  - Unity 6 매뉴얼·스크립팅 API 페이지 (URL을 직접 열어 확인)
- 사용자 브리프: `C:/unityjam/shared-assets/seven-steps/briefs/scene-flow-unity-2026-11-11.md`. 로딩 애니메이션은 지금 만들지 않는다는 갱신 내용 포함.
- 플레이어 빌드는 만들지 않았다.

**결론: 조건부 승인.**
- 구조 결정은 승인한다: Single + `DontDestroyOnLoad`, 씬 세 개, `Loading`은 보류 자리만 둔다.
- M1~M3은 문서만 고치면 된다. 고친 뒤 P1에 들어간다. M3은 늦어도 P2 전에 고친다.

## 확인한 것 (맞음)

- **씬 목록 (2장)**
  - `EditorBuildSettings`는 Boot·Title·Game 순서다. guid가 4.4의 값과 같다.
  - `AppScenes`의 Boot/Title/Game 상수, 그리고 `SceneFlowValidator`의 루트 타입(BootScreen/TitleScreen/GameController)과 일치한다.
  - 빌드 목록 변경 요청이 없다는 점도 맞다.
- **Single + DDOL 결정 (3장)**: 승인한다.
  - 근거 1: Single 로드는 `Resources.UnloadUnusedAssets`를 자동으로 부른다. `SceneManager.LoadScene` 페이지에 "If a single mode scene is loaded, Unity calls Resources.UnloadUnusedAssets automatically"라고 나온다.
  - 근거 2: `UnloadSceneAsync`는 에셋을 놓지 않는다. 같은 페이지 메모에 나온다.
  - 근거 3: WebGL(wasm) 힙은 늘기만 하고 줄지 않는다. 두 씬이 겹치는 순간이 없는 B안이 맞다.
  - 근거 4: A안은 활성 씬을 따로 관리해야 한다. 이 게임은 `ChapterStage`가 행성마다 `RenderSettings`를 바꾸므로 A안의 이 비용이 실제로 생긴다. 이 판단도 맞다.
- **`allowSceneActivation`**: 1장 설명이 API 페이지와 맞다.
  - false면 `progress`가 0.9에서 멈추고 `isDone`은 거짓이다.
  - 그동안 뒤의 비동기 작업은 대기한다.
  - `SceneLoader`의 `progress / 0.9`와 `-loadDelay` 보류 코드도 문서 설명대로다.
- **에디터 직접 Play·도메인 리로드 (1장, 5장)**
  - `m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 1`은 DisableDomainReload만 켠 값이다. 씬 리로드는 한다.
  - `SubsystemRegistration` 초기화는 `Bootstrap`·`Services`·`LevelCatalog`·`Ui`·`TelemetryTracker`에 있다. EditMode `PlayResetTests`가 이를 고정하고 있다.
  - `TitleScreen`·`GameController`의 `HasRunThisPlay` 분기도 문서대로다.
- **`Loading`은 보류 자리만 있다 (4장, 0장, 8.1, 10장)**: 사용자 정정과 맞다.
  - 씬 파일·애니메이션·아트·코드를 만들지 않는다.
  - `mayActivate` 고리는 쓰는 곳이 없으므로 미리 넣지 않는다.
  - `AppState`에는 끝에 더한다(append-only). 다시 시작할 때 리드 승인을 다시 받는다.
  - 4.2·4.3·4.4는 "그때 할 일"로만 적혀 있다. 승인한다.
- **WebGL 로더 기록 (4.3)**: 템플릿 값과 일치한다. 배경 `#101C32`, 막대 `#C9AB6B`.
- **스플래시 (4.3)**: `m_ShowUnitySplashScreen: 1`, 배경 `#231F20`(0.137/0.122/0.125)과 일치한다.
- **timelinefoundation (1장)**: `manifest.json`에 `com.unity.modules.timelinefoundation`만 있다는 설명이 맞다.
- **씬 안 전환 (7장)**: 6장 그림과 분리되어 있고 `docs/stage-select-transitions.md`를 가리킨다. 다이브·복귀에서 "씬 경계는 Single 로드, 앞뒤 카메라만 씬 안 전환"이라는 구분도 맞다.
- **텔레메트리 시점 (8.3)**: 바뀌지 않는다. `session_start`는 Boot에서 나간다.
- **확인 방법 (9장, 10장)**: P1~P4는 새 검증 규칙에 맞다. 에디터 Play Mode로 확인하고, 플레이어 빌드 smoke는 리드 전용으로 둔다. CLAUDE.md "검증 규칙"과도 같다.
- **보관 파일**: `docs/scene-flow-2026-11-08.md`가 이전 문서를 그대로 보관한다. 코드 주석의 S·절 번호 참조가 이 파일을 가리킨다고 0장 머리에 적혀 있다.

## 고칠 것 (P1 전, 문서만)

### M1. DDOL 루트 수가 "둘"로 고정되어 있지만 실제로는 0~2개다 (0장, 1장, 2장, 9장)

- `TelemetryRunner.CreateTelemetry`는 엔드포인트가 없거나 `telemetry=off`면 `NullTelemetry`를 돌려준다. 이때 러너 오브젝트를 만들지 않는다(architecture.md 8장 "텔레메트리가 켜졌을 때만").
- `SceneFader`는 첫 `SceneLoader.Load`에서 생긴다. Game을 바로 Play하고 아직 씬을 바꾸지 않았다면 없다.
- 그래서 9장의 PlayMode 항목 "DDOL 루트가 둘"은 테스트 환경에서 깨진다. 테스트는 네트워크를 쓰지 않도록 텔레메트리를 꺼야 하므로, 테스트에서는 러너가 0개다.
- **고치는 법**: "DDOL 루트는 최대 둘(`TelemetryRunner`는 텔레메트리가 켜졌을 때만, `SceneFader`는 첫 씬 로드 뒤)"로 쓴다. 테스트 항목은 다음처럼 바꾼다.
  - 각 이름이 1개를 넘지 않는다.
  - 씬을 한 번이라도 로드한 뒤에는 `SceneFader`가 정확히 1개다.
  - 왕복 뒤에도 수가 늘지 않는다.

### M2. 도메인 리로드 링크가 Unity 6 공식 매뉴얼이 아니다 (1장, 12장)

- `https://docs.unity.cn/Manual/domain-reloading.html`는 중국 미러이고 버전이 정해져 있지 않다.
- 브리프는 Unity 6 매뉴얼 링크를 요구한다.
- **고치는 법**: `https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html`로 바꾼다. 이 주소는 `docs.unity.com/en-us/engine/6000.0/manual/.../domain-reloading`("Enter Play mode with domain reload disabled")로 넘어간다.
- 이 링크만 바꾸면 된다. 나머지 링크는 열어서 확인했다. 어떤 링크는 6.0이고 어떤 링크는 6000.5/6000.6이지만 내용은 같으므로 둔다.

### M3. Play Mode 캡처(P2)는 batchmode에서 찍는 방법과 빈 화면 대조를 정해 둬야 한다 (8.2, 9장, 10장)

- 문제 1: `-batchmode`에는 Game 뷰 창이 없다. 그래서 `ScreenCapture`가 빈 화면을 낼 수 있다.
- 문제 2: 기존 `stageselect-capture` 방식(카메라 → `RenderTexture` → `ReadPixels`)은 Screen Space - Overlay uGUI를 찍지 못한다. HUD·타이틀·결과가 빠진다.
- 문제 3: `-nographics`면 아무것도 그려지지 않는다.
- **문서에 정할 것**:
  - (a) 찍는 방법. 둘 중 하나다.
    - `-batchmode` 없이 에디터를 열어 Play Mode에 들어간다. 프레임 끝(`WaitForEndOfFrame`)에서 `ScreenCapture.CaptureScreenshotAsTexture`로 찍고 `EditorApplication.Exit`으로 닫는다.
    - batchmode에서도 오버레이 UI까지 찍히는 것을 P2에서 먼저 보인다.
  - (b) 해상도를 고정한다. Game 뷰 크기 또는 `Screen.SetResolution` 대응.
  - (c) smoke-webgl의 `?smoke=empty`와 같은 대조를 한다. 빈 화면(보드·UI 숨김)이 기준 미만이어야 통과한다. 그래야 "찍었는데 비어 있음"을 성공으로 잘못 받지 않는다.

## 메모 (막지 않음)

- **N1 빌드 목록 페이지 (1장)**: Build Profiles Scene List 페이지는 "목록 순서대로 빌드한다"까지만 말한다. build index, 0번이 시작 씬, Override Global Scene List는 이 페이지에 없다.
  - index의 근거는 이미 같이 단 `Scene.buildIndex` 링크로 충분하다.
  - 덮어쓰기 설명은 이슈 트래커 링크의 내용이라고 문장에서 구분하면 된다.
- **N2 스플래시 (4.3)**: Splash Image 페이지는 스플래시를 끌 수 있다고 하지만 라이선스 등급별 조건은 말하지 않는다. "가능하면"은 그대로 두고, 판단할 때 리드가 Player Settings에서 직접 확인한다.
- **N3 검증기 순서 검사 (8.2, P3)**
  - 지금 검증기는 Boot가 0번인지, 이름이 한 번씩 있는지만 본다.
  - Title 1, Game 2 순서까지 검사하려면 기대 순서를 `AppScenes`의 선언 순서(또는 검증기의 명시 목록) 하나에서 가져온다.
  - 그러면 나중에 `Loading`을 1번에 넣을 때 `AppScenes`와 그 목록만 바꾸면 된다. "코드는 이름으로 로드하고 index에 기대지 않는다"(2장)와도 맞다.
- **N4 `Tests/PlayMode` asmdef는 아키텍트 소유다 (`**/*.asmdef`)**: P1에서 scene-flow가 요청하면 내가 만든다. 같은 커밋에 테스트를 넣어도 되고, 그 경우 내가 asmdef를 검토한다. 규격:
  - `name`: `SevenSteps.Tests.PlayMode`
  - `references`: `SevenSteps.Core`·`SevenSteps.Contracts`·`SevenSteps.Game`, `UnityEngine.TestRunner`. `UnityEditor.TestRunner`는 넣지 않는다.
  - `includePlatforms: []`, `overrideReferences` + `nunit.framework.dll`
  - `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. 이 제약 때문에 플레이어 빌드에는 들어가지 않는다.
- **N5 PlayMode 테스트의 실행 환경 (P1)**
  - 에디터의 `LaunchOptions`는 명령줄에서 온다. `-runTests` 실행에서는 `-level`·`-telemetry off`·`-savekey`를 테스트마다 줄 수 없다. 테스트가 `LaunchOptions`를 넣을 길이 필요하다. 예: 부트스트랩 전에 `Services`에 미리 등록하는 테스트용 진입점.
  - 저장도 테스트 전용 키를 쓴다. `SaveStores.For` + 테스트 접미사, 또는 메모리 저장소. 개발자의 PlayerPrefs 진행을 덮지 않게 한다.
  - Unity Test Framework는 프로젝트의 Enter Play Mode 설정(도메인 리로드 꺼짐)을 그대로 쓴다. 그래서 "두 번 연속 Play" 항목이 실제 설정을 검사한다. 좋다.
- **N6 `Boot` 배경 카메라 (P3)**: 검증기는 루트 타입(`BootScreen`)만 하나로 세므로 카메라 오브젝트를 더해도 규칙에 걸리지 않는다. 스플래시 → Boot 한 프레임 → Title 사이의 색만 맞추는 변경이니 승인한다.
- **N7 architecture.md 참조**: 9장 "구조 원칙"은 `docs/scene-flow.md` 1장·5장을 가리키고 있었다. 이번 커밋에서 내가 11장(코드 규칙)과 `docs/scene-flow-2026-11-08.md` 5장으로 고쳤다.

## 다음

- scene-flow가 M1~M3을 문서에 반영하고 커밋을 알리면 그 수정만 다시 본다.
- 리드가 구현(P1~P4)을 승인하면 P1 시작 때 N4의 asmdef를 요청받는다.
