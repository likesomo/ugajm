# 검토: feat/scene-flow d426f71 (U5 Title·언어·크레딧, `-ui5` 뒤) + 클리어 → 여정 순서

검토자: 아키텍트, 2026-11-12.
- 코드를 읽었다.
- d426f71 detached에서 헤드리스로 직접 돌렸다. EditMode OK(scene-flow 보고 843/843), scene-validate OK(0 오류).
- 캡처 `reports/scene-flow-u5-title-2026-11-12/after-u5-title-ko.png`를 봤다.
- 플레이어 빌드는 만들지 않았다.

**결론: 승인(토글 뒤 병합).** 키트 경고(아래 K1)는 gameplay-ui가 고친 뒤 `U5KitAreErrors = true`로 바꾼다. U5 기본 켜기 전 필수다.

## 맞는 것
- **토글**: `TitleScreen.Awake`가 `UiStyleToggle.U5`를 한 번 묻는다. 켜져 있으면 `U5TitleView`·`U5LanguageView`·`U5CreditsView`와 `TitleWorld`를 만들고, 지금 뷰는 null로 남긴다.
  - `uiIcons`와 카메라 null은 `MissingReferenceException`으로 막는다.
  - 꺼져 있으면 지금 경로 그대로다. 바뀐 줄은 null 검사와 분기뿐이다.
- **같은 컨트롤러**
  - 시작 돌은 `CanContinue`면 `ContinueJourney`, 아니면 `BeginJourney`다.
  - 지도는 `OpenStageSelect`, 설정·크레딧·언어는 기존 오버레이와 `LanguageController`(`ILanguagePanel`)다.
- **키트 재사용**: 캔버스 하나에 `FocusPlate` 하나를 쓰고, 키트 부품을 가져다 쓴다. 따로 만든 키트가 없다(계약 1장).
- **배치**: `touch-targets.csv` U5 title과 같다. 지도는 왼쪽 위, 설정은 오른쪽 위, 크레딧은 왼쪽 아래, 언어는 오른쪽 아래, 시작 64는 가운데 아래다. 캡처도 시안 그대로다.
- **`UiLayoutCheck`**: U5 Title·언어·크레딧·HUD를 등록했다. 14.4 `FocusPlate` 예외(채운 9-slice 테두리 = 여백)도 넣었다.
- **토글 관계**
  - `StageSelectToggle`: U5면 v4 지도가 기본이다(계획 2c).
  - `JourneyToggle`: U5만으로는 여정이 켜지지 않는다(`-newmap`이나 지도 기본값일 때만). 아래 순서를 만들 때까지의 임시 동작으로 맞다.

## K1 (gameplay-ui, U5 기본 켜기 전) 키트 판 높이와 HUD 제목 판
- scene-validate 경고(지금은 `U5KitAreErrors = false`라 경고로 나온다)
  - `FocusPlate`: 판 안쪽 높이는 34인데 글자 사각형은 21이다(Jua 18px의 실제 줄 높이). 그래서 위아래 여백이 6.5로 8보다 작고, 따라서 "판 없음"도 함께 뜬다.
    - 원인: `SizeFor`가 높이를 `max(preferredHeight, fontSize)`로 잡는데, 검사기의 글자 사각형(14.1, `preferredHeight`)과 값이 다르다.
    - 고치는 법: 검사기와 같은 계산을 쓴다. `Text.preferredHeight`, 또는 같은 `GenerationSettings`에 실제 `rect` 폭을 넣은 값이다.
  - U5 HUD 제목 판: 글자 y 22가 안전 영역 24 밖이고, 여백도 부족하다. 제목 판을 아래로 내리거나 높이를 맞춘다(14.11 위 띠 24~72 안).
- 고친 뒤 scene-flow가 `U5KitAreErrors = true`로 바꾼다. 그 커밋에서 U5 위반 0을 보인다.

## 리드 질문: "U5 클리어 먼저, 다음 = 여정 전진, 지도 = 여정 복귀"가 흐름 계약에 맞나
**맞다. `IResultPanel`과 `AppFlow`는 바꾸지 않고, 여정 분기 위치만 승리 순간에서 결과 버튼으로 옮기면 된다.** 담당은 scene-flow(`GameFlowController`, `GameJourney`, `JourneyReturn`)다.

1. **승리**: `OnWinPresented`는 여정과 상관없이 항상 `flow.Go(Result)` → `result.Show(model)`(U5 클리어)다.
   - `JourneyRoute.After` 분기를 여기서 뺀다.
   - 이야기 저장점(16.5 "결과 패널이 열린 뒤")은 결과가 열리는 보통 경로로 지켜진다. 여정용 `ClearShown` 호출은 필요 없어진다.
2. **다음**(`Next`, `State == Result`): `JourneyRoute.After(Journey, ...)`로 나눈다.
   - `Advance`: `result.Hide()` → `Advancing(next)`(`GameJourney`가 보드를 떼고 다음 보드를 올림) → `ContinueTo(next)`.
     - `ContinueTo`의 전제 조건을 `State == Game`에서 **`State == Result`**로 바꾼다.
     - `AppFlow`의 Result → Game 전이는 이미 허용되고, 같은 씬이라 로드도 없다.
     - 텔레메트리 `via: next`는 `ContinueTo`에서 한 번만 낸다. `Next`에서 미리 내지 않는다.
   - `PlanetComplete`: `result.Hide()` → `PlanetCompleting(stage)`(뒤로 빠지기 + `JourneyReturn` 완료, 나가는 길 불) → `OpenStageSelect`. Result → StageSelect 전이는 이미 허용된다.
   - 여정이 꺼져 있으면 지금처럼 `flow.GoToGame(next)`다(다시 로드).
   - 마지막 스테이지는 지금처럼 엔딩이다.
3. **지도**(클리어의 지도, Esc): 여정이 켜져 있으면 `JourneyReturn`의 **되돌아가기** 종류로 뒤로 빠진 뒤 `OpenStageSelect`다.
   - `JourneyReturn`에 종류를 더한다: `PlanetComplete`(길 불 켬)와 `Back`(길 불 없음, 그 행성 자세). 순수 모델이고 EditMode 테스트를 붙인다.
4. **다시 하기**(`Again`, R): 바뀌지 않는다. 같은 스테이지를 다시 로드한다.
5. **입력 잠금**: 전진·복귀 연출 동안 `GameJourney.Blocks`가 다음·다시 하기·지도와 R·Esc·Enter를 막는다. 두 번 누르면 두 번 전진하는 경로가 없어야 한다.
   - U5 클리어의 별돌(1f90617 R1의 월드 별돌)은 `result.Hide()`에서 먼저 치운다.
6. **"여정은 결과를 건너뛰지 않는다"를 전역 규칙으로 할지**
   - 이 순서를 U5 클리어에만 둘지, `-newmap` 여정 전체(지금 UI 결과 패널 포함)에 둘지 정해야 한다.
   - **전역을 권한다.** 결과를 건너뛰는 경로(`OnWinPresented`의 Advance·PlanetComplete 분기)가 아예 없어져 코드가 하나로 준다.
   - 지금 UI 결과 패널도 같은 `IResultPanel`이라 추가 비용이 없다. 리드가 확인해 준다.
7. **테스트**
   - EditMode: `GameFlowController`(가짜 `IResultPanel`·가짜 journey 핸들러)로 Win → Result → Next → Advancing → ContinueTo(Result), PlanetComplete, 지도 → Back, 마지막 → 엔딩, 잠금 중 두 번 누르기.
   - PlayMode: `JourneyFlowTests` 둘을 "결과를 거친다"로 고친다. 결과 상태를 확인하고 Next를 누르게 한다.

## 구현 확인: feat/scene-flow d23c100 (클리어 뒤 여정) — **승인**

- d23c100 detached에서 헤드리스로 직접 돌렸다.
  - EditMode: OK(scene-flow 보고 845/845)
  - playtest: **12 통과**, 13 건너뜀. 건너뛴 것은 `Explicit` 캡처 테스트다.
- **설계대로 구현됐다**
  - `OnWinPresented`는 항상 `flow.Go(Result)`다. 결과를 건너뛰는 분기는 **전역으로 지웠다**(권장안). 이야기 저장점 `ClearShown`은 `ShowClear`에서 한 번 나간다.
  - `Next()`는 `JourneyRoute.After`로 나눈다.
    - Advance: `Leave` = `Leaving = true`, `result.Hide()`, `Advancing`.
    - PlanetComplete: `PlanetCompleting`.
    - 그 밖: 지금처럼 다시 로드하고, 마지막은 엔딩이다.
  - `ContinueTo`의 전제 조건은 `State == Result && Leaving`이다. 텔레메트리 `via: next`는 여기서 한 번만 나간다.
  - 지도·Esc(여정 켜짐, Result): `Returning` → `JourneyReturn.Begin(..., ReturnKind.Back)` → `FinishReturn`(전제 조건 `returning && Result`) → StageSelect.
  - `StageSelectController.OnSettled`는 `Back`이면 길 불 없이 그 행성 지도를 보인다. gameplay-ui 파일의 두 줄 분기다. 소유자 확인은 리드가 한다.
  - **잠금**: `Next`·`Again`·`OpenStageSelect`가 `Leaving` 동안 아무것도 하지 않는다. 두 번 눌러도 두 번 전진하지 않는다. `Enter`가 새 스테이지에서 `Leaving`을 푼다.
  - `ReturnKind`는 append-only enum이다. 정의되지 않은 값은 `ArgumentOutOfRangeException`이다.
  - `JourneyCheck`(smoke)와 `JourneyFlowTests`가 결과 → Next 경로를 탄다.
- `IResultPanel`과 `AppFlow`는 바뀌지 않았다. 지금 UI 결과 패널과 U5 클리어가 같은 경로를 쓴다.
- 메모: `docs/stage-select-transitions.md`도 함께 고쳤다(scene-flow 문서).

## 훑어보기: feat/scene-flow 12088de (행성 끝 클리어의 지도·Esc, 리드 결정) — 문제없음

- `OpenStageSelect`에서 Result이고 여정이 켜져 있을 때, 행성 끝(`JourneyRoute.After == PlanetComplete`)이면 `PlanetCompleting`, 그 밖이면 `Returning`(Back)이다. 그래서 행성 끝의 길 불은 Next로 가든 지도로 가든 한 번 켜진다.
- `Leaving` 잠금은 그대로다. EditMode 테스트가 더해졌다.
- 계약(`IResultPanel`·`AppFlow`)은 바뀌지 않는다.

## 검토: feat/scene-flow 477d540 (U5 지도·표면 조작, U5KitAreErrors = true) — **승인**

- 477d540 detached에서 헤드리스로 직접 돌렸다. EditMode OK(scene-flow 보고 852/852). scene-validate OK는 **U5 키트 위반이 오류인 상태에서** 나온 결과다.
- 캡처 `after-u5-surface.png`를 봤다.
- **`U5MapControls`**: 뒤로 48(왼쪽 위, Esc)과 설정 48(오른쪽 위)이다. 계약 3장 지도·표면 행과 같다.
  - `FocusPlate` 추적과 명시적 좌우 이동이 있다. 보이지 않을 때는 패널을 끈다.
- **뒤로의 동작**: 숨긴 v4 갈매기표 버튼의 `onClick`을 부른다(표면이 보이면 표면 것, 아니면 지도 것). 그래서 다이브·복귀 중 막는 조건이 지금처럼 그 뷰 한 곳에 남는다. 좋다.
  - gameplay-ui 파일에는 읽기 전용 `Back` 접근자 한 줄씩만 더했다.
- **보이는 조건**(`MapControlsShown`, `Update`): StageSelect이고, 로딩·다이브(`Pending`)·`JourneyReturn.Pending`이 아니고, 오버레이가 없거나 맨 위가 표면일 때다.
  - 매 프레임 사전 조회뿐이고 할당은 없다. `SetShown`은 값이 바뀔 때만 일한다.
- **`U5KitAreErrors = true`**: K1이 해결된 뒤라 맞다. 이제 U5 키트 위반은 빌드 전 오류다.
- **메모 N1 (gameplay-ui, 막지 않음)**: 표면 화면의 행성 이름 "이끼의 궤도"(위 가운데 금색 글자)는 판 없이 별하늘 위에 있다. 이 커밋의 변경은 아니다.
  - 14.4(3D 장면 위 글자는 판 위)에 걸리는지 `UiLayoutCheck` 등록을 확인한다. 등록돼 있는데 통과한다면 허용 목록 이유를 본다.
