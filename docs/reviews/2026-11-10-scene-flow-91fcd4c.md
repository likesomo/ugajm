# 검토: feat/scene-flow 91fcd4c · a869e5b (b4ed8b6 검토 S1~S5·S7 반영)

검토자: 아키텍트, 2026-11-10. 범위: 차이만(`UiLayoutCheck.cs`, `UiLayout.cs`, `UiSkin.cs`, `ChapterEntryView.cs`, `TitleViews.cs`, `SettingsPanel.cs`). S6(보드 영역) 미구현은 리드 공지대로. feat/view 8426a00은 리드가 7lucky 병합 때 합침.

**결론: 승인.**

- **S7**: `InnerRect`가 `sprite.border / (im.pixelsPerUnit × im.pixelsPerUnitMultiplier)` — uGUI `multipliedPixelsPerUnit`과 같다(8426a00 `FrameBorder`와도 같은 식). `SpriteBordersPending`·12 상한 제거, 실제 border로 0 오류(scene-flow 보고).
- **S1**: `UiLayoutRule.Safe = 64`(docs 14.8 그대로, 기존 값 불변), 안전 영역 두 검사가 `Safe`만 본다.
- **S2**: 조상 `RectMask2D`/`Mask` 밖으로 완전히 나간 요소는 제외. 메모: 반쯤 걸친 요소는 전체 상자로 검사된다(지금 스크롤 없음, 생기면 마스크와 교집합으로 자를 것).
- **S3**: 검사 끝에 디코딩한 PNG 사본 `DestroyImmediate`·캐시 비움.
- **S4**: 챕터 카드 판이 `Ui.Plate`(알파 0.9).
- **S5**: `MenuButton`의 +2 이동 삭제, 설정 버튼도 `FitButtonSkin` — 버튼 라벨 위치는 `FitButtonSkin` 한 곳.
- `Ui.Card`: `panel_wood`(border 32, 나뭇결 23~25px)를 배율 `border.w / 12`로 그려 border 12, 나뭇결이 border 안에 들어간다. 메모: 위쪽 border(`w`)만으로 배율을 정하므로 네 변 border가 다른 스프라이트가 오면 가장 큰 값을 쓸 것.
- 타이틀 메뉴·행성 목록 글자를 `panel_stone` border 32 + 8 안쪽으로 이동.
