# UI v3 — 실제 GameSession 선택 리뷰 (2026-11-11)

`unity/Assets/_Project/Art/Test/UiV3/ChoiceReview.unity`를 열고 Play. 레벨 4 원본 데이터와 실제 `GameSession(this)`를 쓴다. 방향키 또는 화면 아래 방향 버튼으로 걷고, K/X 또는 선택 카드 클릭·탭으로 결정한다. Tab/좌우로 미리보기 포커스, Enter 확정, Z 되돌리기. 정식 씬/HUD/Core 규칙은 수정하지 않았다.

## 먼저 볼 캡처

- `choice-state-sheet.png`: 평소, 6걸음, 일곱째/KEEP 미리보기, SWITCH 미리보기, KEEP 후, SWITCH 후, 결과, EN/frost.
- `before-after.png`: 왼쪽은 v2의 실제 Unity moss-choice 원본, 오른쪽은 v3 실제 Unity 레벨 4. 다른 레벨/카메라 구성임을 감안한 UI 구조 비교다.
- `seven-planet-foot-stars.png`: moss/hollow/carved/frost/joinery/crumble/altar의 실제 저장된 BoardShapeA 재질 위 6걸음. 원본 1280×720 파일에서 별을 판단한다.
- `ko|en-moss|frost|carved-*.png`: 각 상태 원본. `experienced-focus-*`는 세 번 확정 뒤 포커스한 결과만 설명하는 밀도다.

검증 단언 216개 PASS, 원본 상태 캡처 70개와 시트/비교 4개를 저장했다. 모든 상태 PNG는 Unity Camera.Render → RenderTexture → ReadPixels다. 상태 시트는 이 PNG만 Unity Texture2D로 축소 배열했다. 생성형 이미지나 HTML 캡처를 사용하지 않았다. 보드/행성은 저장된 ChapterPreset·BoardShapeA 프리팹과 실제 조명이며, 형제/고스트/선돌/별은 리뷰 전용 Unity UI 도형이다. production StarTrail 셰이더 적용을 주장하지 않는다.

## 구현

선택 판정은 SessionUpdate의 State.Status만 읽는다. Choose일 때 보드에 30% 암부, 형제 선택 링, 다음 형제 머플러색 카드 테두리, 그림·이름·키·결과 설명을 표시한다. 보드 투영 영역의 좌우 빈 여백 중 넓은 쪽에 패널을 놓고 형제 높이에 맞춰 안전 범위로 제한한다. 보드 여백은 리뷰 프레이밍이 미리 확보한다. 첫 세 번 확정 전에는 두 결과를 모두 설명하고 이후에는 호버/키보드 포커스 결과만 설명한다. 횟수는 리뷰 세션 메모리 기준이다.

KEEP는 현재 칸 선돌 고스트·출발점 다음 형제·점선, SWITCH는 현재 칸 다음 형제·매듭만 표시한다. KEEP 확정 시 굳은 선돌 별과 경로가 남고 SWITCH에는 두 형제색 매듭이 남는다. 현재 경로는 형제색 별빛 점과 직각 빛줄기, 이미 끝난 경로는 굳은 별자리로 표시한다. 일곱째는 큰 각성 별, 6걸음은 마지막 빈 별 크기를 17~23px로 맥동한다. 발치 일곱 별은 기본 16px, 머플러색 바깥별/명확한 빈 중심이며 HDR/bloom에 기대지 않는다. 참조는 `concepts/world-2026-11-10/star-trail/v2/states.png`다.

확정 연출은 0.38초의 IBoardPresenter.Present이며 GameSession의 입력 잠금/완료 콜백과 연결했다. 완료 후 다음 형제로 넘긴다. Undo는 애니메이션을 취소하고 스냅샷으로 복원한다. 입력은 프로젝트 설정에 맞는 Unity Input System과 InputSystemUIInputModule을 사용한다. 선택 중 이동 버튼은 비활성이고 키 이동도 차단한다. 설명은 보드 오른쪽 여백 안에 있다.

## 확인 표

| 항목 | 방법 | 결과/범위 |
|---|---|---|
| 보드 칸 비가림 | 실제 보드 RenderTexture 전체 영역과 선택 패널 Rect.Overlaps 단언 | KO/EN × moss/frost/carved 비겹침; UiLayoutCheck 14.6 보드 제외 기하 규칙 적용 |
| 키 입력 | Input System 가상 Keyboard에 실제 Key state event를 넣고 런타임 PollKeyboard 호출 | K KEEP, X SWITCH, Z Undo, Tab 포커스; Present 동안 다른 선택 억제 |
| 마우스 | Unity EventSystem의 IPointerClickHandler로 실제 Button 실행 | KEEP 결과가 실제 GameSession State와 일치 |
| 터치 | 동일 Button에 pointerId=0인 PointerEventData 전달 | SWITCH 결과 일치; 포커스 변경 중 동일 타깃 유지 및 6→7번째 이동도 PASS; 물리 터치 기기 검사는 별도 |
| 미리보기 | Rules.Dispatch(ChooseSnapshot, Keep/Switch) 결과와 ghost 좌표/선돌 칸 비교 | 출발점/현재 칸/선돌 일치; JSON 보존 |
| Undo | 선택 확정 후 Z, 다시 Undo | Choose 복원 후 6걸음 Play로 복귀 |
| 선택 없는 칸 | 실제 레벨 1 출구 및 별도 7번째 구멍 테스트 지도 | 선택 패널 없음 |
| 발치 별 | 1280×720 원본과 일곱 실제 행성 재질 시트 육안 확인 | 기본 16px, 7개 구분, 6걸음 마지막 별 맥동 |
| CompileCheck | BuildTools.CompileCheck | PASS — compile.log |
| GlyphCheck | production GlyphCheck + 리뷰 소스 한글의 Jua TTF cmap 검사 | PASS — production + 리뷰 한글 cmap, assertions.txt/glyph-check.txt |
| EditMode 전체 | 필터 없이 -runTests -testPlatform EditMode | PASS — 801/801, 실패·건너뜀 0, editmode.xml |
| WebGL | 리뷰 씬만 BuildPipeline.BuildPlayer | PASS — Succeeded, 오류 0, 19,281,698 bytes; Builds/UiV3WebGL |

리뷰 Canvas 전체를 production UiLayoutCheck의 등록 화면에 넣지는 않았다. 위 배치 PASS는 보드 제외 기하 검사이며 정식 HUD 전체 검사 통과로 확대 해석하지 않는다. 저장/접근성/일반 해상도 및 정식 HUD 이식은 gameplay-ui가 담당한다.

## 재현

Unity 6000.6.8f1, 프로젝트 `unity`에서 배치 실행:

```
Unity.exe -batchmode -projectPath unity -executeMethod SevenSteps.Art.UiV3Batch.Run -logFile capture.log
Unity.exe -batchmode -projectPath unity -executeMethod BuildTools.CompileCheck -logFile compile.log
Unity.exe -batchmode -projectPath unity -runTests -testPlatform EditMode -testResults editmode.xml -logFile editmode.log
Unity.exe -batchmode -projectPath unity -buildTarget WebGL -executeMethod SevenSteps.Art.UiV3Batch.BuildWebGL -logFile webgl.log
```

레벨 4 입력 `DURRRRR` → 선택. KEEP 후 `DDRRRRR` → Win. 캡처 배치는 Replay나 State 강제 변경 없이 Command를 하나씩 보내고 presenter 완료 후 진행한다. 비교용 v2 원본 경로는 배치 스크립트에 명시되어 있다. `Resources/ReviewLevels.json`은 리뷰 씬의 독립 실행용 원본 데이터 복사다.
