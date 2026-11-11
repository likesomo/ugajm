# 검토: feat/view 4ea1ec0 (이야기 S4 보드 쪽: StoryDirector + 잠깐 줄)

검토자: 아키텍트, 2026-11-11. 범위 378bf38 → 4ea1ec0, 12파일. 코드만 읽었다. EditMode 873/873(StoryDirectorTests 9), scene-validate 0 오류, build-win, smoke는 gameplay-ui 보고 수치다. 실제 화면의 시작 줄(카드 닫은 뒤)과 피날레 줄은 미검증이다(리허설 때 확인).

**결론: 승인.** Contracts 변경은 없다. 16.7·16.8과 story-integration 0.1·0.2와 맞는다.

## 계약 대조 (맞음)
- **16.7 startLine**: `LevelCatalog.StartLineOf` → `LevelDto.StartLine`이고, `StoryDirector.StartText`가 story·hint·"" 중 하나만 고른다. 그 밖의 값은 `ArgumentException`이다(계약과 같음).
- **0.2 잠깐 줄 하나**: `ITransientLine`(HudView)에 시작 줄과 피날레 줄이 같은 자리로 나온다. 시간은 max(2.5초, 글자 × 0.12초)다. HudView가 스스로 이야기를 띄우던 코드(3초, `storyStage`)는 지웠다. 그래서 줄을 정하는 곳이 `StoryDirector` 하나다.
- **사라지는 때**: 첫 Present, Snap(Undo·Restart, GameSession 108·119줄), Rebuild(다음 스테이지), 결과(Presented Win). Restart는 Snap이라 다시 나오지 않는다.
- **덮개**: 행성 카드·메뉴가 덮고 있으면 기다리고, 걷히면 그때부터 시간을 센다. 보이는 중에 덮이면 숨겼다가 걷히면 시간을 처음부터 다시 센다.
- **16.8 피날레 줄**: `Updated(Present, Win)`에서 `StoryTruths.IsFinaleStage`(챕터 키 경계, A4)로 판정해 보류한다. 별자리 피날레가 시작될 때 보이고, 결과가 열리면 사라진다. 피날레가 돌지 않으면 보류를 버린다(별지도가 진실을 지닌다).
- **16.5 세기**: 줄이 화면에 나올 때 `MarkShown`(메모리)이고, 저장은 기존 `ClearShown`이다. 개발 실행과 ArtPerf는 조용하다(줄도 세기도 없음).
- **0.1**: 순간 문구, 별자리 문구, `ResultModel.FinaleLine`은 만들지 않았다. 결과 화면은 별·별자리·다음만이다(14.7).
- 순수 C#(`Game/Story`)이고, 글은 함수로 받아 보일 때의 언어로 만든다. 9장 MVC와 맞다.

## 메모 (막지 않음)
- **M1 피날레 길이**: 0.1은 피날레를 "약 1.2초"로 적었지만, 줄을 읽을 시간만큼(2.5초 이상, 긴 줄은 3~4초) 피날레를 붙든다. Enter·클릭으로 넘길 수 있으니 받아들인다. 리허설에서 길게 느껴지면 줄 시간과 별도로 붙드는 시간에 상한을 둔다.
- **M2 구독 순서 의존**: `held`는 StoryDirector의 `Updated` 처리에서 채워지고, `BoardView.Animate`가 Win 부분에서 `FinaleStarting`을 부른다. BoardController가 먼저 구독했으므로, Animate가 첫 `yield` 없이 Win 부분까지 가면 `held`가 아직 null이라 줄이 조용히 빠진다.
  - 지금은 Win이 늘 이동(경로 1칸 이상, 이동 루프에서 yield)으로 오므로 일어나지 않는다.
  - 더 단단하게 하려면 `FinaleStarting`에서 보류를 기다리지 말고, 그때 판 상태가 Win이고 피날레 스테이지면 `finaleLine()`을 바로 부른다. 다음에 그 파일을 고칠 때 한다.
- **M3 Enter 한 번이 두 일을 하는가 (미검증)**: 피날레·줄 붙듦을 Enter로 넘기면 같은 프레임에 `done` → 결과가 열린다. 그 프레임에 `GameController.Update`가 Result 상태를 보면 같은 Enter가 [다음]이 되어 결과를 건너뛸 수 있다. 기존 별자리 피날레 건너뛰기와 같은 길이고 이번에 새로 생긴 것은 아니다. 리허설 때 "피날레에서 Enter → 결과가 남는가"를 본다.
- **M4 다시 하기(R, 결과에서)**: `GameFlowController.Again`은 `GoToGame` → Rebuild라서 시작 줄이 다시 나온다. 이전 HudView는 같은 스테이지면 다시 안 냈다. 다시 들어온 것으로 보면 맞고, 거슬리면 마지막으로 보인 스테이지를 기억해 막는다. 리허설 판단에 맡긴다.
- **M5 언어 변경**: 줄이 보이는 중에 언어를 바꾸면 남은 시간 동안 이전 언어 글이 남는다(`Apply`는 글꼴만 바꾼다). 드물고 짧다.

## 남은 일: 메뉴의 시작 줄 다시 보기 (0.2) — 결정
- **나눔**: gameplay-ui가 `StoryDirector.ShowStartAgain()`(그 스테이지의 시작 줄을 `waitingStart`에 넣고 `CoverChanged()`를 부름)과 GameController 연결을 넣는다. 메뉴 항목과 이벤트(`MenuPanel`/`MenuController`)는 scene-flow가 한 줄로 넣는다.
- 메뉴가 덮고 있는 동안에는 기다렸다가 메뉴가 닫히면 보이므로, 기존 덮개 처리를 그대로 쓴다.
- 시작 줄이 ""인 스테이지에서는 항목을 숨기거나 비활성으로 둔다.
- 순서: gameplay-ui의 메서드가 먼저 들어가고, scene-flow가 레이아웃 커밋 뒤에 연결한다. 리드가 배분을 바꾸면 그에 따른다.

## 이어서: feat/view ebdb653 (메뉴 다시 보기 진입점) — **승인**
코드만 읽었다. EditMode 874/874는 gameplay-ui 보고 수치다.
- 위 결정과 같다. 이름만 `ReplayStartLine()`(bool)이다. 시작 줄을 처음과 같은 시간으로 `waitingStart`에 넣고 `CoverChanged()`를 부른다. 그래서 메뉴가 닫힐 때 보이고, 다음 이동에 사라진다.
- 줄이 없거나 스테이지가 없으면 false이고 상태를 바꾸지 않는다. scene-flow는 이 값으로 항목을 비활성으로 둘 수 있다(미리 알려면 같은 조건의 `HasStartLine` 속성이 필요하다. 필요하면 요청한다).
- 조용한 실행에서도 보이는 것은 맞다. 플레이어가 누른 것이다.
- **메모 N1**: 클리어 결과 위에서 Esc 메뉴를 열고 누르면 줄이 숨겨진 HUD(14.7 `Conceal`)에 걸려 보이지 않는다. 다음 스테이지의 Rebuild에서 지워지니 해는 없다. 결과 상태에서는 항목을 숨기거나, `ReplayStartLine`이 Result에서 false를 돌려주는 쪽이 깔끔하다(scene-flow가 연결할 때 정한다).
- 연결(scene-flow): `MenuController`에 `() => storyLines.ReplayStartLine()`를 넘긴다. `storyLines`가 `menu`보다 뒤에 만들어지므로 람다로 늦게 읽는다.
