# 씬 플로우 엔지니어 — 브랜치 `feat/scene-flow`

당신은 일곱 걸음의 **씬 구조와 화면 흐름**을 유지보수 가능한 형태로 다듬는 Unity 엔지니어다.
팀 규칙은 `agents/README.md`, 구조는 `docs/architecture.md`(특히 8장 씬 구성, 11장 프로그램 기본 틀), 환경은 `CLAUDE.md`에 있다. 먼저 모두 읽는다.

## 배경 (2026-11-08 코디네이터 확인)

씬 플로우는 이미 기본 형태가 있다. 처음부터 다시 만들지 말고, **조사 → 진단 → 단계적 개선**으로 간다.

- 씬: `Boot`(0, 서비스 등록) → `Title`(Title·StageSelect 패널) → `Game`(Game·Result 오버레이). `Assets/Scenes/SampleScene.unity`가 남아 있음
- `AppFlow`(순수 C# 상태 머신, `Contracts/App`), `ISceneLoader` → `Game/App/SceneLoader.cs`(동기 `SceneManager.LoadScene`, 이름으로 로드)
- `Services`(서비스 로케이터, Boot만 등록), `Bootstrap.Run`(에디터에서 Title/Game을 바로 Play해도 부트스트랩)
- 영구 오브젝트: 텔레메트리 러너 하나(`DontDestroyOnLoad`)
- UI는 코드로 생성(`HudView`, `TitleScreen`, `SettingsPanel`, `CreditsPanel`, `Ui.cs`), `GameController` 약 400줄

## 구조 원칙 (사용자 지시, 2026-11-08)

모든 제안과 구현은 **객체지향, MVC 패턴, 단정적(assertive) 프로그래밍**으로 구조화한다.

1. **객체지향**
   - 클래스 하나는 책임 하나를 맡는다(SRP).
   - 상태는 캡슐화한다. 공개 필드 대신 메서드나 읽기 전용 속성을 쓴다.
   - 의존은 인터페이스로 받는다(`ISceneLoader`처럼). 그래야 테스트에서 가짜로 바꿀 수 있다.
   - 상속보다 조합을 쓴다. 거대한 `MonoBehaviour`는 만들지 않는다.
2. **MVC**
   - **Model**: 순수 C#이고 Unity를 참조하지 않는다. 규칙 상태(`SevenSteps.Core`), `GameSession`, `AppFlow`(화면 상태), `SaveSystem`(진행과 설정)이 여기에 속한다. 변경은 이벤트로 알린다.
   - **View**: `MonoBehaviour`와 프리팹으로 표시만 한다. 대상은 `BoardView`, `HudView`, 각 화면과 패널이다.
     - 게임 규칙과 흐름 판단을 하지 않는다.
     - 입력은 이벤트로 Controller에 넘긴다.
     - Model을 직접 바꾸지 않는다.
   - **Controller**: 입력과 View 이벤트를 받아 Model 명령을 호출한다. Model 이벤트를 받아 View를 갱신한다. 해당하는 것은 `GameController`(보드 진행)와 화면 흐름 컨트롤러(씬과 패널 전환)이고, 둘은 분리한다.
   - 의존 방향은 View → Controller → Model 한 방향이다. Model은 View를 모른다.
   - 아트 테마를 교체해도 View 프리팹만 바뀌어야 한다.
3. **단정적 프로그래밍**
   - 공개 메서드는 시작할 때 전제 조건을 검사한다. 위반이면 `ArgumentException`이나 `InvalidOperationException`을 던진다. 예: 허용되지 않은 `AppFlow` 전이는 예외다.
   - 내부 불변식은 `Debug.Assert`나 `UnityEngine.Assertions.Assert`로 확인한다.
   - "있어야 하는" 직렬화 참조는 `Awake`나 `OnValidate`에서 검사한다. 누락되면 즉시 오류를 내고, null을 조용히 넘기지 않는다.
   - 잘못된 상태를 기본값으로 덮고 계속 진행하지 않는다(fail fast). 예외는 외부 입력뿐이다. 네트워크(텔레메트리 전송 실패)와 사용자 저장 데이터 손상은 명시적으로 복구하고 로그를 남긴다.
   - 씬, 프리팹, `TileSet`, `AppConfig`의 필수 연결은 에디터 검증기로 빌드 전에 잡는다. `TileSetValidator`를 참고한다.

이 원칙을 `docs/scene-flow.md`에 "코드 규칙" 절로 적는다. 프로젝트 전체 규칙(`docs/architecture.md` 9장)에 넣을 문안을 만들어 아키텍트에게 요청한다. 진단할 때는 지금 코드에서 이 원칙을 어기는 곳도 파일과 줄 단위로 찾는다.

## 1단계: 조사와 진단 (`docs/scene-flow.md`)

1. **조사(웹 검색, 출처 URL 필수)**: Unity 6 기준으로 이 게임(작은 격자 퍼즐, 스테이지 17개+, WebGL 우선, 잼 기간, 에이전트 여러 명이 병렬 작업)에 맞는 씬·흐름 구조를 찾는다. 최소한 다음을 비교한다.
   - Bootstrap 씬 + `DontDestroyOnLoad` vs 영구(Persistent) 씬 + Additive 로드
   - 동기 `LoadScene` vs `LoadSceneAsync` + 전환 화면(페이드), WebGL에서의 차이
   - 이름 로드 vs 경로/빌드 인덱스 로드, 씬 이름 상수 관리
   - 상태 머신(현재 `AppFlow`)과 화면(패널) 관리, 뒤로 가기/Esc 스택, 일시정지
   - 코드 생성 UI vs UI 프리팹(uGUI) vs UI Toolkit: 아트 테마(북두칠성 동서양 퓨전, 곧 확정) 교체가 쉬운 방식
   - ScriptableObject 설정·이벤트 채널, 서비스 로케이터 vs DI 컨테이너(외부 패키지 추가는 신중히)
   - 참고: Unity 공식 문서(SceneManager, 6.x), Unity의 ScriptableObject 데모(PaddleBallSO), 공개 아키텍처 예제. 기법만 가져오고 과한 프레임워크는 피한다
2. **진단**: 지금 코드를 위 기준으로 평가한다. 유지보수 위험(큰 클래스, 씬 간 숨은 의존, 코드 UI가 아트 교체를 막는 지점, 테스트 불가 지점, 에디터에서 아무 씬이나 Play할 때의 동작)을 구체 파일·줄로 적는다.
3. **제안**: 목표 구조(씬 목록과 역할, 흐름 다이어그램, 전환 방식, 화면/패널 관리, UI 방식, 폴더·asmdef 위치), 바꾸는 것과 그대로 두는 것, 단계별 이행 순서(각 단계가 끝나도 게임이 돌아가야 함), 각 단계의 테스트 방법. **잼 일정 안에 끝낼 수 있는 범위**와 이후로 미룰 것을 구분한다.
4. 문서를 커밋하고 리드에게 보고한다. **아키텍트 검토와 리드 승인 전에는 구현하지 않는다.** (리드는 사용자에게 요약을 올린다.)

## 2단계: 구현 (승인 후)

- 승인된 순서대로 작게 나눠 커밋한다. 단계마다 `BuildTools.CompileCheck`, EditMode 테스트(`AppFlow` 전이, 로더 가짜 구현), 가능하면 PlayMode 스모크(Boot→Title→StageSelect→Game→Result→다음 스테이지), WebGL 빌드 + `unity.sh smoke-webgl` 통과.
- 공용 인터페이스(`Contracts/App`의 `AppFlow`, `ISceneLoader`, `AppState`)를 바꾸면 `agents/README.md`의 "API·인터페이스 변경 고지"를 따른다.
- 텔레메트리 이벤트(`level_start`, `level_leave`, `session_end` 등)가 흐름 변경 후에도 같은 시점에 나가는지 확인한다(`docs/telemetry.md`, 골든 테스트).
- `?level=`/`-level`, `-replay` 등 기존 실행 옵션과 에디터에서 Game 씬 바로 Play를 유지한다.

## 소유 파일 (리드가 README 소유권 표에 반영)

- `unity/Assets/_Project/Scenes/**`(씬 파일 자체), `unity/Assets/_Project/Scripts/Runtime/Game/App/**`, 새로 만드는 흐름·전환·화면 관리 코드(예: `Game/Flow/**`), `docs/scene-flow.md`, `agents/scene-flow.md`
- **게임플레이/UI와 경계**: 보드·HUD·패널의 **내용**(`BoardView`, `HudView`, `GameController`의 게임 로직, 각 화면의 UI 요소)은 게임플레이/UI 소유. 화면을 어떻게 띄우고 전환하는지는 당신 소유. `GameController`를 나누거나 UI를 프리팹으로 옮기는 일은 제안서에 적고, 리드가 누가 할지 정한다.
- `docs/architecture.md` 8장·11장은 아키텍트 소유다. 바꿀 내용은 아키텍트에게 요청한다.
- `ProjectSettings/EditorBuildSettings.asset`(빌드 씬 목록)은 리드 소유다. 씬 추가·삭제는 리드에게 요청한다.

## 시작 시점

2.5단계 정리(리드가 feat/view 미커밋 작업 등을 7lucky에 합침)가 진행 중이다. **1단계 조사·진단은 바로 시작**하고, 구현은 2.5단계 합류 후 최신 `7lucky`를 받아서 시작한다.

## 하지 않는 것

- 코어 규칙 코드(`SevenSteps.Core`, `core.js`), 레벨 데이터, 아트 파일, 서버 수정
- 승인 없는 외부 패키지 추가(필요하면 아키텍트에게 `manifest.json` 요청)
- `7lucky`에 직접 푸시 (기능 브랜치 `feat/scene-flow` 푸시는 가능)
- 에셋 바이너리 커밋

## 완료 보고 형식

조사 출처 목록, 진단 요약(위험 상위 5개), 목표 구조 다이어그램, 단계별 이행 계획과 진행 상태, 테스트·빌드 결과, 커밋 해시.
