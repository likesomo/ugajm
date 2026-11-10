# 검토: feat/scene-flow b4ed8b6 (7afaefa WebGL 템플릿, d2321f6 UiLayoutCheck + 지도·로딩·챕터 카드·설정·타이틀·언어 선택 배치)

검토자: 아키텍트, 2026-11-10. 리드 공지: 보드 영역(14.6) 미구현, 스프라이트 border는 `SpriteBordersPending`(12 상한), HUD/결과는 경고만. 후속 커밋(cd4ba4e 받은 뒤 `FitButtonSkin`·`MenuButton` 정리)은 차이만 본다.

**결론: 승인.** S1은 계약 보완(이 커밋에서 docs 14.8에 반영), 나머지는 후속 커밋에서.

## 7afaefa WebGL 템플릿
- `ProjectSettings.asset`은 `webGLTemplate` 한 줄, `ProjectSetup`도 같은 값을 넣어 `setup` 재실행에도 유지.
- 16:9 창 맞춤(레터박스), 바깥 `#101C32`, `devicePixelRatio: 1`(FPS 후보 1 해소), 로드 막대. 14.9가 16:9만 오류로 보는 전제와 맞다.
- smoke-webgl과 호환: board-pixels는 캔버스를 마젠타 경계 상자로 찾으므로 남색 바깥은 측정 밖.
- 기본 템플릿의 전체 화면 버튼·경고 배너는 없다. itch.io는 자체 전체 화면 버튼이 있어 문제없음. 오류는 `alert` 한 번.

## d2321f6 `UiLayoutCheck` (14장 대조)
- 14.9: 화면을 Edit Mode에서 만들고(`Title`, `Language`, `Settings`, 지도, 행성별 목록 7, 로딩 팁 3, 챕터 카드 7, HUD 가장 긴 힌트·이름, Result), ko·en, 1280×720(=1920×1080 같은 단위) 오류·1280×800 경고. 가장 긴 문자열·`150/150` 진행.
- 14.1 글자 사각형: `TextGenerator`의 실제 글리프 쿼드(공백 제외)라 규칙보다 정확. 손글씨 side bearing 2 여유.
- 14.2 크기: `fontSize` × 언어 역할 배율, best fit은 최소값, 18 미만은 면제 불가. 맞음.
- 14.3 대비: 판 스택을 검정·흰색 위에 합성한 낮은 값, 스프라이트 PNG 색을 실제로 샘플. 선 굵기 2·대비 3:1.
- 14.4 판: `im.pixelsPerUnit`(스프라이트 PPU / 캔버스 기준 PPU × multiplier)로 border를 환산 — 정확한 식이다(feat/view f525128 N1의 100 고정보다 낫다). 판 알파 0.75, 여백 8.
- 14.5 장식: 회전 사각형 SAT로 글자 + 4 교차, 판 border가 글자를 지나면 decoration 오류. 겹침·넘침(Wrap/Truncate = 자기 rect, Overflow = 부모).
- 14.7 모달: 모달 안의 화면 전체·raycast·알파 0.6 이상 막을 찾고 그 앞 UI는 제외. 없으면 오류.
- 14.8: 이유 없는 면제 오류, 화면별 면제 목록 `NOTE` 출력.
- 메뉴 실행은 기존처럼 `SaveCurrentModifiedScenesIfUserWantsTo` 뒤라 열린 씬을 말없이 버리지 않음.

## 화면 수정
- 로딩: 북두칠성 상자 576 폭(화면 45%, 14.10의 40~50%), 별 44, 최신 별에 빛 맥동, `n / 7` 크게. `Animate`는 스케일 대입뿐(할당 없음), 문자열은 진행이 바뀔 때만.
- 챕터 카드: 행성 이름·소개 뒤 판, 칩 이름 22(best fit 14 제거, 두 줄 허용).
- 언어 선택·설정·지도: 버튼 높이 72, 카드 배치.

## 메모
- **S1 (계약)**: 안전 영역(14.6) 위반을 `UiLayoutRule.Overflow` 면제로 건너뛴다. 14.8 플래그에 안전 영역이 없어서 생긴 대용인데, 넘침과 안전 영역은 다른 규칙이다. **`UiLayoutRule.Safe = 64`를 추가**하고 안전 영역 검사는 그 플래그만 본다(docs 14.8 갱신, `UiLayout.cs`는 scene-flow 후속 커밋).
- **S2** 14.1 "마스크 안"(`Mask`/`RectMask2D`)을 아직 보지 않는다. 지금 화면에 스크롤 목록이 없어 오탐은 없다. 스크롤이 생기면 마스크 밖 요소를 제외.
- **S3** `decoded` 텍스처 캐시가 static이고 `DestroyImmediate`되지 않아 에디터에서 검사를 반복하면 메모리가 늘어난다. `Check` 끝에서 비울 것.
- **S4** `ChapterEntryView`의 자체 `Plate`(알파 0.85)는 feat/view f525128의 `Ui.Plate`(0.9)와 중복. cd4ba4e를 받은 뒤 `Ui.Plate`로 통일.
- **S5** `Ui.MenuButton`의 라벨 +2 위로 올림과 `FitButtonSkin`의 라벨 offset은 순서에 따라 결과가 달라진다(리드 공지대로 후속에서 정리). 정리 뒤 둘 중 하나만 라벨 위치를 정하게.
- **S6** 14.6 보드 영역 검사는 `HudView.BoardFrame` 상수가 아니라 실제 HUD 사각형과 보드 투영으로(14.6에 적음).
