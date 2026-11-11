# 검토: feat/view c58dfa4 (48ff6b4 검토 M1·M5 반영)

검토자: 아키텍트, 2026-11-11. 범위 30eec33 → c58dfa4, 3파일. 코드만 읽었다. EditMode 863/863, scene-validate 0 오류, build-win, smoke는 gameplay-ui 보고 수치다. 실제 키 입력은 미검증이다(창 포커스 문제로 SendInput이 게임에 닿지 않았다는 보고 그대로).

**결론: 승인.** 병합해도 된다. R1은 다음 커밋(S4 보드 쪽)에서 고친다.

## 확인
- **M1 해결**: 클리어 결과에서 Tab과 →로 다음 별, Shift+Tab과 ←로 이전 별로 간다(순환). 처음 누르면 별 1(뒤로면 별 3)이다.
  - 고른 별은 1.15배로 커지고 조건 판이 보인다. 별 공개 연출 중에는 크기를 건드리지 않는다.
  - Enter는 계속 [다음]이다(`GameController`).
  - 클리어 결과에서 보드 입력은 `GameFlowController.TakesCommand`가 삼키므로 ←/→가 보드 명령(부딪힘 소리)이 되지 않는다.
- **M5 해결**: `Conceal` 주석이 실제 동작과 맞다. HUD 루트의 CanvasGroup 아래 WorldLabels 층이 함께 숨고, 3D 보드·흔적·배우는 그대로다.

## 고칠 것 (다음 커밋, 막지 않음)
- **R1 키 입력을 View에서 읽는다(9장 MVC, "흐름 키는 한 곳")**: `ResultView.Update`가 `Keyboard.current`를 직접 읽는다.
  - 결과 위에 Esc로 메뉴(덮개)를 연 뒤에도 `StarRowShown`이 참이다. 그래서 메뉴에서 ←/→·Tab으로 항목을 옮기면 뒤의 결과 별 초점도 같이 바뀐다.
  - 덮개 확인은 `GameController.Update`에만 있다(`overlays.Count > 0`이면 return).
  - 고치는 법: 키 세 줄을 `GameController.Update`의 Result 분기(Enter → `flowController.Next` 옆)로 옮기고 `result.FocusStar(±1)`만 부른다. `ResultView`에는 `FocusStar`와 `StarRowShown`만 남긴다. 덮개가 열려 있으면 이미 그 위에서 return하므로 따로 막을 필요가 없다.
- 실제 키 입력 확인은 리드의 플레이테스트 리허설 때 한 번 본다. 확인할 순서: 클리어 → Tab×3(순환) → Esc 메뉴에서 ←/→ → 메뉴 닫기 → 별 초점이 그대로인지.

## R1 수정 확인: feat/view bdaa019 (2026-11-11) — **반려 (한 줄 남음)**
코드만 읽었다. EditMode 863/863은 gameplay-ui 보고 수치다.
- `GameController.Update`의 Result 분기에 키 세 줄을 넣은 것은 맞다. 덮개 확인(`overlays.Count > 0`이면 return) 뒤라 메뉴가 열려 있으면 닿지 않는다.
- **R1' (병합 전) `ResultView.Update`를 지우지 않았다.** 주석만 바뀌고 `Keyboard.current`를 읽는 `Update`(지금 227~235줄)가 그대로 남았다.
  - 그래서 덮개가 없을 때는 같은 키를 두 곳이 읽어 `FocusStar`가 한 프레임에 두 번 불린다. Tab 한 번에 별 1 → 별 2를 건너 별 3처럼 두 칸씩 움직인다(처음 누르면 별 2, 그다음 별 1 …).
  - 메뉴가 열려 있을 때는 `ResultView.Update`가 여전히 초점을 바꾼다. 원래 R1 문제가 그대로다.
  - 고치는 법: `ResultView.Update` 메서드 전체를 지운다. `FocusStar`와 `StarRowShown`만 남긴다.
- 테스트가 이 경로를 잡지 못한다(키 입력 없음). 고친 뒤 `grep -n "Keyboard" ResultView.cs`가 비어야 한다.
