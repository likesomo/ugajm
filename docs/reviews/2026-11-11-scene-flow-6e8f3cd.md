# 검토: feat/scene-flow 6e8f3cd (이야기 S4 흐름 쪽)

검토자: 아키텍트, 2026-11-11. 기준 7lucky b03ce11(S2 계약 9eda0b0, docs 16). 코드만 읽었다. EditMode 861/861, scene-validate 0 오류, build-win + smoke(flowcheck 50 → 엔딩 4장 → StageSelect)는 scene-flow 보고 수치다. WebGL과 smoke-webgl은 미검증이다.
범위: `StoryLoad`, `StoryBook`, `EndingController`/`EndingView`, `GameFlowController`(EndingRequested·ClearShown), `GameController`(Esc·엔딩 키), `TitleViews`(타이틀 한 줄·별지도), `SceneLoader`(로딩 새김), `ProjectSetup`, `SceneFlowValidator.ValidateStory`, `LocalizationCheck`, `UiLayoutCheck`, `ResultView` 2줄.

**결론: 승인.** Contracts 변경은 없다. 16장 계약을 그대로 쓴다. 병합은 리드 계획대로 bcb2126 체인 뒤에 한다.

## 계약 대조 (맞음)
- **로드 실패(A8, 16.3)**: `StoryLoad.Catalog`가 `FromJson` 예외를 잡아 오류 로그를 남기고 `StoryBook.Off()`를 쓴다. 게임은 계속되고, 모든 줄이 null이며 저장도 없다.
  - 빌드 전에는 `ValidateStory`가 같은 로드를 오류로 막는다. 빌드에 실린 파일은 이미 통과한 것이다.
- **개발 실행**: smoke, replay, flowcheck, titlecheck에서는 메모리 `StoryProgress`를 쓰고 저장 콜백이 없다. 11.3.2 튜토리얼과 같다.
- **저장 시점(16.5)**: 클리어 결과가 열린 뒤(`ClearShown → story.Save`)와 엔딩 끝(`EndingFinished`)에 저장한다. `Mark*`는 메모리만 바꾼다.
- **엔딩(16.4·16.8)**
  - 카드 1~4를 `StoryRules.Ending(win, card, truths, chapterCount)`로 고르고, 문장은 `Catalog.Text`로 만든다. truths는 `StoryTruths.FinaleCleared`(스테이지 이름 키)에서 센다.
  - 이미지는 `entry.image` → `StoryImageSet.Get`이고, 없으면 글만 보인다.
  - 엔딩이 끝나면 본 카드만 `MarkShown`하고 `EndingSeen`을 켠다. 카드 3까지 봤고 AllTruths면 `FinalTruthSeen`도 켠다. 0.5 #7과 같다.
  - Esc는 BackNavigator가 덮개를 닫는 것으로 받아 건너뛰기가 된다. 한 경로다.
- **50 [끝까지 보기]**: `GameFlowController.Next`가 마지막 스테이지면 `EndingRequested`를 낸다. 이야기가 꺼져 있으면 바로 별지도로 간다.
- **타이틀 갈래**: `title` 한 줄을 고르고, 엔딩을 봤으면 `after "ending"` 줄을 쓴다. 이야기가 꺼져 있으면 기존 `title.tagline`을 쓴다.
- **별지도(0.5 #1·#2)**: 새김 표식은 `FinaleCleared(id)`로 켠다. 깬 순서와 상관없다. dipper 선은 `from`의 피날레가 클리어면 금색이다. 모양은 코드 분기가 아니라 데이터에서 온다(A13).
- **검사(16.2·3.2)**
  - `ValidateStory`: 로드 실패는 오류다. 이미지가 빠지면 `required = false`라 경고 1줄이다. `StoryImages.asset`은 19키가 비어 있다.
  - `LocalizationCheck`: 모든 줄과 형제 이름을 두 역할 글꼴의 cmap으로 검사한다.
  - `UiLayoutCheck`: 타이틀 2개(엔딩 전·후), 엔딩 카드 4장(갈래 중 가장 긴 줄, `{brother}`는 가장 긴 이름으로 치환), 별지도(모든 새김과 선 켬)를 등록했다.
- **14.7 Esc**: 클리어 결과가 덮개 없이 떠 있으면 Esc가 메뉴를 연다. R은 기존 그대로다.
- **`ResultView` 2줄(gameplay-ui 파일)**: 마지막 스테이지에서도 [다음]이 보이고, 라벨은 `result.ending`이다. 동작이 최소인 수정이라 허용한다. 아래 M1을 본다.

## 메모 (막지 않음)
- **M1 병합 순서 (리드)**: gameplay-ui가 14.7 "보드 위 결과"로 `ResultView`를 새로 짜는 중이다. 이 2줄이 그 작업과 충돌한다.
  - 나중에 들어오는 쪽이 두 가지를 지켜야 한다: 마지막 스테이지에서 [다음]을 보이고 라벨을 `result.ending`으로 둔다, 그리고 `NextRequested`를 `GameFlowController.Next`에 연결한다.
  - 그 뒤 scene-flow가 'Result clear' 14.7 검사를 등록한다(보고의 남은 일과 같다).
- **M2 dipper와 지도 선**: 금색으로 칠하는 대상은 `UiTheme.Dipper` 선 가운데 story.json `dipper`와 겹치는 것뿐이다.
  - story.json에만 있고 테마에 없는 선은 조용히 안 그려진다.
  - `altar→circle`은 북극성 노드가 아직 없으니 의도대로다. 그 밖의 선이 빠지면 데이터 실수다.
  - `ValidateStory`에서 "circle이 아닌데 테마 선이 없는 dipper 줄"을 경고하면 좋다.
- **M3 앱 종료 저장(16.5)**: 저장 시점 넷 가운데 앱 종료가 아직 없다.
  - 지금은 로딩 새김 횟수만 다음 결과나 엔딩까지 메모리에 남는다. 영향이 작다.
  - 새김 첫 등장(`CarvingSeen`)을 넣을 때 그 연출 끝 저장과 `Application.quitting` 저장을 같이 넣는다.
- **M4 `StoryBook`이 부트 때의 `save.Story`를 붙든다.**
  - 지금 `StartNewPlayer`는 부트스트랩에서 StoryBook을 만들기 전에만 불리므로 맞다.
  - 실행 중에 새 플레이어로 바꾸는 기능이 생기면 StoryBook도 다시 만들어야 한다.
- **M5** `SceneFlowValidator`에서 `ValidateStory`의 문서 주석이 `ValidateThemes` 요약 주석 사이에 끼었다. `ValidateThemes`의 설명이 `ValidateStory`에 붙어 있으니, 다음 커밋에서 순서를 바로잡는다.
- **남은 일(보고 그대로)**: 새김 첫 등장 연출과 `CarvingSeen`, 진실 다시 보기, 북극성 표식, 타이틀 하늘 별, 'Result clear' 검사, WebGL·smoke-webgl.
