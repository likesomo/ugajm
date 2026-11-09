# 검토: feat/view 6aac6e5 (12.8 챕터 프리셋, 보드 쪽: ChapterStage, BoardView.UseChapter, BoardController.UseChapters)

검토자: 아키텍트, 2026-11-09. 코드 읽기. 아직 씬 루트에 연결되지 않음(`UseChapters`를 부르지 않으면 지금 동작 그대로).

**결론: 수정 1개 후 OK.** 나머지는 12.8과 맞다.

## 수정 필요 (연결 전에)
- **하늘 프리팹의 부모**: 12.8·`SkyPreset.skyPrefab` 계약은 "보드 카메라의 자식, 로컬 위치·회전 유지(카메라를 따라감)"이다. 지금 `ChapterStage.Enter`는 `Instantiate(skyPrefab, parent)`(씬 루트)라 스테이지마다 `FitCamera`로 카메라가 옮겨지면 하늘이 따라가지 않는다. `Object.Instantiate(chapter.sky.skyPrefab, cam.transform, false)`로. 행성(`planetPrefab`)은 지금처럼 월드 원점(씬 루트)이 맞다.

## 맞는 것
- 세트 결정 순서 `TileSetFor`: `AppConfig.tileSet` → 챕터 `tileSet` → `GameAssets.tileSet`, 셋 다 없으면 예외(12.8).
- 순서: `EnterChapter`(챕터가 바뀌면 하늘·행성·조명 교체 + `BoardView.UseChapter`) → `session.Load`(Rebuild가 새 세트로) → `Fit(board.Grid)`(같은 챕터여도 매번). 챕터 비교가 `ChapterPreset` 참조라 `""`와 첫 챕터 id가 같은 프리셋이면 같은 챕터로 본다(S4 후속 문제를 이쪽은 자연히 피함).
- 머티리얼 교체: 프리팹을 만들 때 한 번 `sharedMaterials` 배열 교체, 상태 머티리얼(`activeMaterial`/`inactiveMaterial`)도 `Override`를 거침. `renderer.material` 복제·매 프레임 작업 없음.
- 조명: 키만 그림자(`shadowsAllowed`), 필·림은 그림자 끔. ambient Flat. 매 프레임 작업 없음.
- 단정적: 생성자 null 검사, `UseChapters` 두 번·Init 전 호출 예외.

## 메모 (막지 않음)
- 챕터 바뀜 감지가 두 곳이다: 흐름 `GameFlowController.ChapterEntering`(id 문자열, 전환 카드용)과 보드 `ChapterStage.Enter`(프리셋 참조). 연결할 때 `chapterFor`와 `ChapterEntering`이 같은 정규화(`ChapterFileDto.Get(id)` → `AppConfig.Chapter(info.ArtPreset)`)를 쓰게 할 것.
- 챕터가 없는 스테이지(`null`)로 가면 하늘·행성은 지우지만 조명·ambient·배경색은 이전 챕터 값이 남는다. 문서화된 동작("씬 그대로")이지만 임시 세트용 `Default` 챕터를 두면 사라진다(12.8).
- 하늘 크기: 직교 카메라 크기가 스테이지마다 바뀐다. 아트 프리뷰는 매 프레임 크기를 맞췄는데 정식 경로에서는 매 프레임 금지다. 필요하면 하늘 루트도 `IStageEnvironment`를 구현하게 해 `Fit` 때 한 번 맞추는 방안(아키텍트에게 요청하면 12.8에 추가).
- 연결(씬 루트에서 `ChapterStage` 생성, 방향광 3개)은 Game 씬 소유자 씬 플로우와 함께.
