# 검토: feat/view 5e0a1cb (챕터 프리셋 적용: AppConfig.chapters 7개, AppConfig.tileSet 비움)

검토자: 아키텍트, 2026-11-10. 기준 `origin/7lucky` 548f3fd ... 5e0a1cb.
**직접 확인**(임시 브랜치 7lucky + feat/view): tileset-validate 0 오류(Chapter presets 0/0 — chapters.json의 7개 artPreset이 모두 `AppConfig.chapters`에 있음, 경고는 기존 mud), scene-validate OK. 게임플레이/UI 보고: test 744/744, build-webgl(Mobile_RPAsset) + smoke-webgl 1/29/50 보드 픽셀 87509/118582/39461, 콘솔 오류 0.

**결론: 승인.**

## 확인한 것
- `AppConfig.chapters`의 GUID 7개 = `Chapter_moss/hollow/carved/frost/joinery/crumble/altar`, chapters.json 순서. 옛 `Chapter01`(moss-ruins) 제외 — 034c910 사후 검토 M1과 맞음.
- 세트 결정 순서(12.8) `AppConfig.tileSet → 챕터 tileSet → GameAssets.tileSet`을 `BoardController.TileSetFor` 한 곳에서 쓰고, 셋 다 없으면 이유를 적은 예외(9장 단정적 규칙).
- `GameController.Awake`: `LevelCatalog.Load`(80행) 뒤에 `ChapterPresetFor(cfg)(0)`(90행)이라 순서 문제 없음. 시작 스테이지가 0이 아니어도 `LoadStage`의 `EnterChapter`가 Rebuild 전에 세트를 바꾼다. HUD는 세트에서 `BodyColor`만 읽고 그 값은 세트마다 같다(12.6).
- 검증기 완화는 정확히 "tileSet 비고 chapters 있음"만 허용, 둘 다 비면 오류 유지. 챕터별 세트는 TileSetValidator가 본다.
- 034c910 검토 A1(하늘은 Destroy로 교체)·A2(Fit은 Play 중)·M3(displayName 화면에 안 씀)에 맞다는 보고 확인.

## 메모
- `SceneFlowValidator.cs`는 씬 플로우 소유 파일이다(한 줄 조건). feat/scene-flow와 같은 파일을 만지지만 다른 덩어리라 자동 병합된다(임시 병합으로 확인). 씬 플로우에 알려만 둔다.
- feat/scene-flow 최신(a06ea19·8cc1537)과 7lucky+feat/view를 합치면 `Strings/ko.json`·`en.json`, `Game/App/LevelCatalog.cs`가 충돌한다(두 쪽 모두 13.5 언어별 조회를 추가). 나중에 합치는 쪽(씬 플로우)이 `LevelCatalog.TextOf`(feat/view) 하나로 합치기를 권한다.
- docs/architecture.md 12.8 "보드 세트 결정 순서"를 적용된 상태로 고쳤다(feat/architecture).
