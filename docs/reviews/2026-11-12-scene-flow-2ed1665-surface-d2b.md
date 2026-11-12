# 리뷰: feat/scene-flow 2ed1665 — U5 표면 스테이지 선택 D2b(Orbit sparks)

- 날짜: 2026-11-12 · 리뷰어: 아키텍트 · 기준: 7lucky 1da9a1d
- 범위: StageSelectModel 최고 별(0..3)·Perfect, ConstellationFigure(순수 데이터), ConstellationView(노드·링크), ConstellationGlyphs, UiArc, PlanetSurfaceView constellation 경로, StageSelectController.SurfaceFirst(U5), UiLayoutCheck 표면 3개(KO/EN), 문자열 `ui5.surface.caption*`, 테스트(ConstellationTests 5, StageSelectTests, U5TitleFlowTests, PlayCapture)
- 검증(헤드리스, 분리 체크아웃): EditMode 863/863, scene-validate OK. 캡처 `reports/scene-flow-surface-d2b-2026-11-12/` 5장 확인

## 판정: 승인 (R1은 다음 커밋에서, 병합을 막지 않음)

## 확인한 것
1. **MVC**: 상태(클리어·최고 별·Perfect·현재)는 `StageSelectModel`에만 있고 검증(0..3, 행 길이)은 생성자 assert. 그림(앵커·간선)은 `ConstellationFigure` 순수 데이터, 그리기 규칙(`Drawn`, `Current`)은 모델만 읽는 정적 함수. `ConstellationView`는 Bind로 채우는 뷰, 입력은 `PlanetSurfaceView`가 받아 `PlaceChosen` → 컨트롤러. 저장 읽기는 TitleScreen 모델 빌더(`Progress.BestStars`) 한 곳. 좋음.
2. **흐름**: `SurfaceFirst`는 U5에서만 켜지고 끄면 예전 경로 그대로. 다이브·Commit/Cancel 계약(stage-select-transitions.md)은 바뀌지 않음. 문서 한 줄 추가됨.
3. **80 px 타깃**: 노드 루트가 80×80 투명 히트 이미지 + Button, 글리프는 자식 `Glyph`만 크기 변경(누름 .91·호버 1.17)이라 히트 박스는 줄지 않음. 테스트가 7개 행성 모두 화면 안·서로 겹치지 않음·뒤로/설정/캡션과 겹치지 않음을 1280×720 기준으로 확인. 앵커가 축별로 늘어나 좁은 화면(4:3)에서도 가장 가까운 쌍(moss 6–7 등)의 가로 간격이 80 이상이라 괜찮음.
4. **U5 키트 / 14장**: 캡션은 `Ui.Plate` + `PlateFit`(좌우 16, 위아래 8, 14.4)로 한 줄, 캔버스당 FocusPlate 규칙과 겹치지 않음(설명 판이 아니라 캡션). 링크(희미한 선)와 현재 표시 화살표는 이유를 단 Decoration 예외, 하늘 배경은 Decoration·Contrast·Overlap·Board 예외(장면 그림). U5 표면에서 행성 이름 판을 숨겨, 지난 열린 메모(표면 행성 이름 판 없음, 14.4)는 이 경로에서 사라짐.
5. **a4218ad/91446f1 경계**: 표면 그림은 여전히 `IPlanetSurfaceArt.TryGetSurface` → `PlanetBody.UseProjection` 한 경로. 별자리는 그림·`PlanetSurfaceSet`를 모르고, 바뀐 것은 카메라 프레이밍(정사영 3.9, 중심)뿐. 캡처에서 서리·제단·이끼 투영 정상.
6. **생성 텍스처**: `ConstellationGlyphs`는 한 번 만들고 캐시, `SubsystemRegistration`에서 정적 초기화(도메인 리로드 꺼짐 규칙). `DontSave`. 좋음.
7. **캡처**: 진행 중(서리 4 클리어·5 현재·3 포커스, 별 0~3 불꽃), 처음(이끼 1 현재, 도전 전), 완벽(제단 오라). 상태가 노드만으로 읽힘.

## 고칠 것
- **R1 (작음, 다음 커밋)**: 드래그 취소 플래그가 남는다. `dragCancelled`는 `BeginDrag`에서 켜고 `onClick`에서만 끄는데, 실제 EventSystem은 드래그가 시작되면 `eligibleForClick = false`라 클릭을 보내지 않는다. 그래서 노드에서 드래그한 뒤 **키보드 Enter/Submit 한 번이 무시된다**(다음 PointerDown 전까지). 테스트는 드래그 뒤 `onClick.Invoke()`를 직접 불러 이 경우를 못 본다. 고칠 방법: `EndDrag`에서(또는 `Select`/Submit 경로에서) 플래그를 내린다 — 실제로는 드래그 시 클릭이 오지 않으므로 플래그 자체를 빼고 EventSystem에 맡겨도 된다. 테스트: 노드 드래그(Down→BeginDrag→EndDrag, 클릭 없음) 뒤 Submit이 다이브하는지.

## 메모 (선택)
- N1: 구분자가 KO `|`, EN `·`로 다르다. 의도(Jua 글자)라면 문자열 파일 주석이나 문서에 한 줄. 문서는 `|`로 적혀 있음.
- N2: 할로 숨쉬기·현재 맥동이 매 프레임 `Graphic.color`를 바꿔 표면 캔버스를 다시 만든다. 메뉴 화면이라 허용하지만, WebGL에서 무거우면 별자리 층을 하위 Canvas로 분리.
- N3: 후속 수정(제단 8-1 닫는 선이 몸체를 가로지름)은 캡처로 확인, 리드 승인대로 진행. 빼면 `EdgesDrawAsStagesClear…`의 altar 기대값도 함께.
- N4: 뒤로 버튼은 기존 `BackChevron`(얇은 선). U5 키트 IconButton 48로 바꾸는 건 표면·지도 공통 작업으로 따로.

## 추가: daff0d6 — D2b 첫 클리어 흔적(ClearNews) 승인

- 검증: 헤드리스 EditMode 868/868 (분리 체크아웃)
- 결정한 모양 그대로: `Game/Flow/ClearNews`(Services, 저장 안 함), `ClearNote{Stage, FirstClear, BestBefore, BestAfter}`, Record의 assert(0..3, 내려가지 않음, 첫 클리어면 before 0)·새것 없음 무시·스테이지별 합침, `TryTake(stages, into)` 할당 없음. 기록은 `GameFlowController.OnWon`의 MarkCleared 앞뒤, Take는 `StageSelectController.OpenSurface` 한 곳, 뷰는 받은 목록만 재생(UiLayoutCheck·테스트 Bind는 null). MVC 맞음.
- 재생: before 상태로 열고 0.25 s 뒤 별 금색 + 새 링크 0.35 s 그리기(앞 스테이지 쪽에서) + 새 불꽃 0.18 s, 오라는 끝에. 노드 누름·움직임 줄이기는 즉시 끝 상태.
- 메모(막지 않음):
  - M1: `StageSelectViews.OpenSurface`가 오버레이가 열려 있으면 그냥 돌아가는데 소식은 컨트롤러가 이미 꺼냈다 → 그 경우 재생이 사라짐(지도 입력이 오버레이 아래에서 막혀 있으면 실제로는 안 생김). 순서를 바꾸려면 뷰가 열 수 있는지 먼저 묻거나, 못 열면 되돌려 넣기.
  - M2: `ConstellationView.Bind`가 소식과 저장이 다르면 예외(InvalidOperationException). 지금 경로(같은 Progress에서 모델·소식)에서는 일관되고, 실행 중 진행 초기화는 부팅 때(-newplayer)뿐이라 `Clear()` 호출 지점이 없어도 안전. 나중에 실행 중 "진행 지우기"가 생기면 그곳에서 `ClearNews.Clear()` 필수.
