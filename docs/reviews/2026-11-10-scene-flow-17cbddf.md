# 검토: feat/scene-flow 17cbddf (UiLayoutCheck 보드 영역 14.6, `BoardView.Fit` 공개)

검토자: 아키텍트, 2026-11-10. scene-flow 보고: 대상 스테이지 14(가장 넓음 17×3)·49(가장 높음 9×9)·23(가장 긴 힌트)·36(가장 긴 이름), 위반 0.

**결론: 승인.** B1은 다음 커밋에서.

## 계약 관점: `BoardView.Fit(cam, grid, rig)` (gameplay-ui 파일)
- `FitCamera`의 본문을 그대로 static으로 옮긴 것이다(회전·투영·`HudView.BoardViewport(cam.aspect)`·중심 이동·클립 면). 인스턴스 상태를 읽지 않고 `FitCamera`가 이를 부르므로 게임 화면은 바뀌지 않는다. `1.2` 상수는 `ActorHeight`로 이름만 붙었다.
- 게임과 검사기가 **같은 식**을 쓰게 되어 14.6의 "보드 투영은 게임과 같은 맞춤 뒤의 값"을 코드로 보장한다 — 이 방향이 맞다. 공개 범위: Game 어셈블리의 public static이고 Contracts가 아니므로 공용 계약 변경은 아니다. 다만 이제 에디터 검사기가 의존하므로 **시그니처를 바꿀 때는 scene-flow와 같이** 바꾼다(docs 14.6에 기록). 소유는 그대로 gameplay-ui.
- `rig`이 `CameraRig`(struct)라 null 걱정 없음, `cam` null은 예외.

## 검사기
- `BoardArea`: 같은 `TileSetFor`(AppConfig.chapters → 챕터 TileSet), 같은 `GridFor`, Game 씬의 `overrideCamera`/`cameraRig`, 캔버스 비율의 카메라로 보드 셀 + 배우 높이 8개 꼭짓점을 투영, + 8. 14.6 정의 그대로.
- 글자 사각형·판·버튼이 그 사각형과 겹치면 `board`, `UiLayoutRule.Board`로 면제 가능.
- AppConfig를 경로로 다시 읽는 것(빈 씬을 열면 씬이 잡고 있던 참조가 풀림)과 Game 씬이 열려 있을 때 카메라 선택을 먼저 읽는 순서 수정 — 맞다.

## 메모
- **B1** HUD는 `Rules.NewState`로만 만든다. 그래서 **오른쪽 칸(`sidePanel`: 석상 수·메시지 줄·KEEP/SWITCH)은 숨겨진 채 검사된다** — 겹침·판·보드 영역 모두 미검사. 7걸음 뒤 `Status.Choose` 상태(리플레이로 7걸음)와 가장 긴 메시지(긴 스테이지의 H 풀이 줄)로 HUD 변형 하나를 더 만들 것. 오른쪽 칸 x 980은 `BoardFrame` 끝 964 밖이라 지금은 통과할 가능성이 크지만, 메시지 줄이 길어 칸이 아래로 자라는 경우를 잡아야 한다.
- **B2** HUD 결과는 `HudResultAreErrors = false`라 경고다. 보드 영역 위반은 HUD에서만 나오므로 지금은 빌드를 막지 않는다. gameplay-ui 쪽 경고가 0이 되면 리드가 켠다.
- 넓이·높이 선택이 `W`, `H` 각각 최댓값이다. 쿼터뷰 rig에서는 카메라 공간 크기가 둘 다에 걸리지만 두 극단을 다 넣었으므로 충분.
