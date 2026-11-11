# 검토: feat/view ecf50a2 (1024x768 Result 경고 20 → 0) + 1458ea0 (scene-flow 3a7804e 병합)

검토자: 아키텍트, 2026-11-11. 코드만 읽었다. scene-validate 0 오류, UiLayout 경고 0(남은 15건은 StoryImages 스프라이트 없음 경고 1과 exempt NOTE), EditMode 875/875는 gameplay-ui 보고 수치다. WebGL은 미검증이다.

**결론: 승인.** Contracts 변경은 없다.

## ecf50a2
- 원인: 클리어 결과의 행성·여정 줄 판(고정 640, 아래 가운데)이 4:3(1108 단위)에서 오른쪽 아래 [다음] 칸과 겹쳤다.
- 수정: 판을 아래쪽 가로로 늘리고 양쪽 여백을 같게 했다(안전 24 + 8 + [다음] 폭 252 + 간격 16 = 300).
  - 1280에서 680, 4:3에서 508이다. 가운데에 있고 [다음]과 겹치지 않는다. 14.7(보드 위 결과, 하단 가운데 판)과 맞다.
  - 글이 줄어든 폭에 들어가는지는 UiLayoutCheck가 가장 긴 행성·여정 줄로 본다(1024x768 경고 0).
- `ResultPanel.prefab`은 `ResultLayoutBuilder`가 다시 만든 YAML이다. 손으로 고친 것이 아니라 빌더 결과라 diff가 크다.

## 1458ea0 병합 (UiLayoutCheck.cs)
- 3a7804e의 `Resolutions` 세 줄(1280x720 오류, 1280x800·1024x768 경고)과 요약 주석이 그대로 있다.
- 4ea1ec0의 HUD 시작 줄(가장 긴 시작 줄 스테이지, `BuildHud`의 `ShowLine`)과 'finale line' 화면이 함께 남았다. 충돌 해결로 빠진 것이 없다.
