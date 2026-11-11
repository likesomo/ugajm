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

## 수정 확인 + P1 검토: feat/scene-flow 4515ccf + a67fac6 (2026-11-11) — **승인**

검토자: 아키텍트. 기준 7lucky 83fd385. 확인 범위는 아래와 같다.
- 코드를 읽었다.
- `unity.sh playtest`를 내 작업 공간에서 직접 돌렸다. origin/feat/scene-flow a67fac6을 detached로 받아 실행했다.
  - 결과는 **PlayMode 6/6 통과**다.
  - 로그(`unity/Logs/batch-playtest.log`)에 예외·오류가 없다.
  - 실행 뒤 작업 트리는 깨끗하다. URP 설정 파일도 되돌려졌다.
- EditMode 922/922는 scene-flow가 보고한 수치다.
- 플레이어 빌드는 만들지 않았다.

### 4515ccf (문서): M1~M3, N1·N3·N4·N5 해결
- **M1**: 0·1·2·3·6·9장에서 DDOL 루트를 "0~2개"로 썼다. 러너가 생기는 조건과 페이더가 생기는 시점도 함께 적었다. 테스트 항목은 이름마다 1개 이하, 첫 로드 뒤 `SceneFader`는 정확히 1개, 텔레메트리를 끄면 러너 0개로 바뀌었다.
- **M2**: 도메인 리로드 링크를 `docs.unity3d.com/6000.0/.../domain-reloading.html`로 바꿨다. 1장과 12장 모두 바뀌었다.
- **M3**: 9.2를 새로 썼다. 승인한다.
  - batchmode 없이 에디터를 열고 `EnterPlaymode` → `WaitForEndOfFrame` → `ScreenCapture.CaptureScreenshotAsTexture` 순서로 찍는다.
  - `PlayModeWindow.SetCustomRenderingResolution`으로 1280×720을 고정하고, 크기가 다르면 실패로 본다.
  - Board/Empty 대조를 한다. 기준은 smoke-webgl과 같은 500이다.
  - 보드가 없는 Title은 UI 켬과 숨김을 비교한다.
- **N1**: Scene List 페이지와 `Scene.buildIndex` 페이지, 이슈 트래커의 근거를 문장에서 나눠 적었다.
- **N3**: 기대 순서를 `AppScenes.BuildOrder` 한 곳에서 가져온다(P3에서 구현).
- **N4·N5**: 9.1에 적었다.

### a67fac6 (P1)
- **asmdef (소유자 검토): 승인.** `SevenSteps.Tests.PlayMode`는 N4 규격과 같다.
  - references는 Core·Contracts·Game·`UnityEngine.TestRunner`다. Editor 어셈블리는 참조하지 않는다.
  - `includePlatforms: []`, `overrideReferences` + `nunit.framework.dll`, `autoReferenced: false`.
  - `defineConstraints: ["UNITY_INCLUDE_TESTS"]` 덕분에 테스트 실행 컴파일에서만 들어간다. 플레이어 빌드에는 빠진다.
  - `BuildTools`에 `BuildOptions.IncludeTestAssemblies`가 없다. 그래서 빌드에 테스트가 섞이는 경로도 없다.
  - 런타임 어셈블리가 이 어셈블리를 참조하지 않으므로 의존 방향(Core ← Game ← Tests)이 지켜진다.
  - `.meta`가 둘 다 있다.
- **Bootstrap 테스트 주입: 런타임 경로는 그대로다.**
  - `nextRun`은 `UseLaunchOptionsForNextRun`만 채운다. 이 함수는 테스트(이후 캡처)만 부른다.
  - 그래서 플레이어와 보통 에디터 Play에서는 `nextRun == null`이고, `ReadLaunchOptions`는 이전과 같이 `LaunchOptions.Parse(명령줄, URL)`을 돌려준다.
  - 한 번 쓰면 지운다. `SubsystemRegistration`에서도 지운다(도메인 리로드 꺼짐 대비). 이미 값이 있으면 던진다. 단정적 규칙에 맞다.
  - EditMode `PlayResetTests`의 "정적 보관소마다 초기화 고리 하나" 규칙도 지켜진다. 같은 `ResetForPlay`에 넣었다.
- **저장 키 분리: 맞다.**
  - 모든 테스트가 `-savekey playtest`로 실행된다. `Bootstrap.Run`이 `SaveStores.For(launch)`를 쓰므로 키 뒤에 `#playtest`가 붙는다.
  - SetUp과 TearDown이 같은 접미사 저장소에서 `SaveSystem`의 키 5개(진행·설정·진행 백업·튜토리얼·이야기)를 지운다.
  - 텔레메트리 큐 키는 `telemetry off`라 생기지 않는다. 옛 `seven-steps-cleared` 키는 읽기 전용 이전 키다.
  - 개발자의 보통 키는 건드리지 않는다.
- **테스트 6개**: 9장 항목과 대응한다.
  - Boot→Title 단일 씬
  - 왕복 DDOL 불변
  - `-level 3` → Game 스테이지 2
  - Title 직접 Play
  - Game 직접 Play
  - 같은 Play 안 두 번째 실행
  - `WaitForState`는 상태, 로딩 끝, 활성 씬 이름을 모두 확인하고 15초 제한이 있다. 실패 메시지에 현재 상태가 들어간다.
- **`unity.sh playtest`**: `-nographics` 없이 batchmode로 실행한다. URP 설정을 되돌리고, 결과는 `reports/playmode-results.xml`에 남긴다. 실패하면 실패한 테스트 이름을 출력한다. EditMode `test`와 같은 형식이다.

### 메모 (막지 않음)
- **P1-N1 "두 번째 실행" 테스트는 진입 초기화의 일부만 흉내 낸다.**
  - `Bootstrap.ResetForTest`는 `Bootstrap`과 `Services`만 비운다. `LevelCatalog`·`Ui`·`TelemetryTracker`의 `SubsystemRegistration` 고리는 부르지 않는다.
  - 지금은 같은 데이터를 다시 읽으므로 해가 없다.
  - 다만 테스트 이름 "starts fresh"가 실제 Play 진입보다 넓게 들린다. 둘 중 하나를 고른다:
    - (a) `PlayResetTests`처럼 리플렉션으로 `SubsystemRegistration` 고리를 모두 부르는 테스트 도우미를 쓴다.
    - (b) 주석에 "Bootstrap·Services만"이라고 적는다.
  - 실제 두 번 Play(진입 초기화 전체)는 Play Mode를 두 번 들어가야 하므로 P2 캡처 도구에서 볼 수 있다.
- **P1-N2** `UseLaunchOptionsForNextRun`·`ResetForTest`는 플레이어에도 공개되어 있다. 문서 주석에 "테스트·캡처 전용"이라고 적혀 있고, 부르는 곳은 Tests와 이후 에디터 캡처뿐이다. 그대로 둔다. 게임 코드가 부르기 시작하면 다시 본다.

## P2~P4 검토: feat/scene-flow f40d292 + 975d2ce + ac632a9 (2026-11-11) — **승인**

검토자: 아키텍트. 확인 범위는 아래와 같다.
- 코드를 읽었다.
- `unity.sh playtest`를 내 작업 공간에서 직접 돌렸다. ac632a9를 detached로 받아 실행했다.
  - 결과는 **PlayMode 8/8 통과**다.
  - 로그에서 "Exception"으로 걸리는 줄은 하나뿐이다. `Debug.Log` 스택 안의 테스트 프레임워크 메서드 이름(`ExecuteEnumerableAndRecordExceptions`)이라 실제 예외가 아니다.
  - 실행 뒤 작업 트리는 깨끗하다.
- 다음은 scene-flow가 보고한 수치다: compile, EditMode 922/922, playcapture(board 73138 / empty 0), scene-validate.
- playcapture는 에디터 창을 띄우는 실행이라 돌리지 않았다.
- 플레이어 빌드는 만들지 않았다.

### Boot 배경 카메라 (975d2ce): 승인
- **DDOL이 아니다.** `Boot.unity`의 씬 루트 오브젝트다. `DontDestroyOnLoad`를 부르는 곳은 여전히 `TelemetryRunner`와 `SceneLoader`(페이더) 두 곳뿐이다. 그래서 Title을 Single로 로드할 때 Boot와 함께 사라진다.
- **렌더 비용**: 무시할 만하다.
  - 설정은 `cullingMask 0`, `SolidColor #101C32`, `depth -100`이다.
  - 살아 있는 동안은 지운 색만 그린다. Boot가 사는 몇 프레임뿐이다.
  - `AudioListener`가 없다. 그래서 Title 카메라와 리스너가 겹치지 않는다.
  - `MainCamera` 태그도 없다. 그래서 `Camera.main` 검색에 걸리지 않는다.
- **UI**: Boot에는 캔버스가 없다.
  - 페이더는 Screen Space Overlay `sortingOrder 1000`이라 카메라와 무관하게 위에 그려진다.
  - Boot는 `Start`에서 `Bootstrap.Run` → `flow.Go(Title)` → `fader.Set(1)`(`UiTheme.panelBg`, 불투명)까지 첫 렌더 전에 끝낸다. 그래서 실제 첫 프레임에 보이는 것은 페이더의 남색이고, 카메라의 지운 색은 그 아래에 깔린다.
  - 카메라가 없을 때 생기는 문제(에디터 "No cameras rendering" 표시, 플랫폼 기본색)만 막는다. 해는 없다.
- **검증기**: 루트 타입을 세는 규칙(`BootScreen` 하나)에 걸리지 않는다. scene-validate 통과는 scene-flow 보고 수치다.

### Contracts `AppFlow.cs` 변경 범위: 승인
- 바뀐 것은 둘이다.
  - `AppScenes.BuildOrder`(`IReadOnlyList<string>`, `{ Boot, Title, Game }`)를 **추가**했다.
  - `AppFlow` 문서 주석을 고쳤다.
- 동작 변경은 없다. 공용 API를 추가만 했으므로 다른 코드는 그대로 컴파일된다.
- architecture.md 변경 기록에 남겼다(N3 구현).
- `SceneLoader`·`Bootstrap`은 주석만 바뀌었다.
- `GameController.Journey`는 읽기 전용 속성 추가다(테스트용).
- 검증기 `ValidateBuildOrder`는 두 가지를 본다.
  - `BuildOrder`가 `AppScenes` 이름을 모두 한 번씩 담았는지
  - 켜진 빌드 목록이 그 순서와 정확히 같은지
- 이것으로 N3를 해결했다. 나중에 `Loading`을 넣을 때는 `AppScenes`와 `BuildOrder`만 바꾸면 된다.

### PlayCapture (f40d292): 에디터 전용 맞음, 승인
- `Scripts/Editor/SceneFlow/PlayCapture.cs`는 `SevenSteps.Editor` 어셈블리(`includePlatforms: ["Editor"]`)에 있다. 그래서 플레이어에 들어가지 않는다. 런타임 코드가 이 파일을 참조하지도 않는다.
- 방법은 9.2(M3)와 같다.
  - batchmode 없이 에디터를 연다. 장면마다 빈 씬에서 Play Mode에 들어간다.
  - 실행 옵션은 `#playcapture` 저장 키로 주입하고, 전후에 그 키를 지운다.
  - 1280×720으로 고정하고 크기를 검사한다.
  - "한 가지 색만"인 화면은 실패로 본다.
  - Board ≥ 500, Empty < 500 대조를 한다.
- `unity.sh playcapture`는 결과 줄이 OK가 아니면 실패한다.

### P4 JourneyFlowTests + PlayEnv (ac632a9): 승인
- 기존 smoke `-journeycheck advance/complete`의 확인 내용을 PlayMode 테스트로 옮겼다.
  - advance: 결과 화면 없이 2번 스테이지로 넘어가고, 카메라가 풀리고, 새 보드가 제자리에 있으며, 스냅숏이 없어진다.
  - complete: 첫 행성의 마지막 스테이지 → Title 지도로 가서 지도 자세에 안착하고, 나가는 길에 불이 켜진다.
- 풀이는 `levels.json`의 expected.path를 쓴다. 마지막 수만 실제 입력처럼 보낸다.
- `PlayEnv`가 SceneFlowTests와 실행 환경을 같이 쓴다. 이전 P1-N1(중복된 리셋·대기 코드)이 이것으로 정리됐다.

### 메모 (막지 않음)
- **P2-N1 문서와 코드가 다르다.** 9.2는 `CaptureScreenshotAsTexture`라고 적었다. 코드는 `ScreenCapture.CaptureScreenshot(file)`로 파일을 쓰고 다시 읽는다.
  - 둘 다 Game 뷰 프레임 끝을 찍는다. 그러니 문서 한 줄만 코드에 맞추면 된다.
- **P2-N2 메뉴로 실행할 때 두 가지를 조심한다.**
  - `Seven Steps > Scene Flow > Play Mode Capture`는 `EditorSceneManager.NewScene`으로 시작한다. 그래서 열린 씬의 저장하지 않은 변경이 묻지도 않고 사라진다. 메뉴 경로에서는 먼저 `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()`를 부르고, 거절하면 멈춘다.
  - Game 뷰 해상도가 1280×720 "playcapture"로 남는다. 끝날 때 원래 값으로 되돌리거나, 문서에 남는다고 적는다.
  - `unity.sh` 경로에는 해당하지 않는다.
- **P2-N3** 지울 저장 키 5개 목록이 `PlayEnv`와 `PlayCapture`에 따로 있다. 키가 늘면 둘 다 고쳐야 한다. 다음에 손댈 때 `SaveSystem`에 "모든 키" 목록을 하나 두는 것을 제안한다(Contracts라 그때 내가 넣는다).
- **P3-N1** Boot 카메라는 `allowHDR`/`allowMSAA`가 기본값(켬)이다. 지운 색만 그리므로 꺼도 결과가 같다. 끄면 URP 중간 버퍼를 만들지 않는다. 몇 프레임뿐이라 이득은 작다. `BootSceneCamera`를 다시 돌릴 때 같이 꺼도 되고, 꺼지 않아도 된다.

## 규칙 갱신에 따른 정정 (2026-11-11, CLAUDE.md b19c1ae: 헤드리스 배치 모드만)

- M3에서 권한 "batchmode 없이 에디터를 열어 Game 뷰를 `ScreenCapture`로 찍는다"(9.2, P2 `PlayCapture`)는 **이제 쓰지 않는다.** 에디터 GUI를 열지 않는다.
- 배치 방식으로 바꿀 때 지킬 것(scene-flow, 다음 P2 수정):
  - 실행: `-batchmode` + `-executeMethod`로 Play Mode에 들어간다. 카메라를 PNG로 렌더할 때만 `-nographics`를 빼고, `-batchmode`는 유지한다(`-nographics`면 아무것도 그려지지 않는다. 기존 `stageselect-capture`와 같은 방식).
  - 오버레이 UI: batchmode에는 Game 뷰가 없어서 `ScreenCapture`를 쓸 수 없다. 카메라 → `RenderTexture` 렌더는 Screen Space - Overlay 캔버스를 찍지 못한다. UI까지 보려면 캡처하는 동안만 캔버스를 Screen Space - Camera(그 카메라, 같은 크기)로 바꾸어 찍는다. 런타임 코드는 바꾸지 않고, 캡처 스크립트(Editor)에서만 바꾼다.
  - 그 밖의 M3 조건은 그대로다: 1280×720 고정·크기 검사, 한 가지 색 검사, Board ≥ 500 / Empty < 500 대조.
- 흐름 확인은 그대로 `unity.sh playtest`(`-batchmode -runTests -testPlatform PlayMode`)다.
