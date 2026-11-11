# 행성 지도 전환 (stage select v4) — scene-flow ↔ gameplay-ui 인터페이스

기준: `C:/unityjam/shared-assets/seven-steps/concepts/ui/stage-select-2026-11-11/IMPLEMENT.md` (사용자 승인 v4, 2026-11-11), v3/v4 README의 전환 스트립.
합의: 2026-11-11 gameplay-ui(feat/view) ↔ scene-flow(feat/scene-flow). 서로 뷰를 직접 부르지 않고 이벤트와 모델로만 연결한다.

## 담당

| 조각 | 담당 | 위치 |
|---|---|---|
| 행성 몸체 `PlanetBodySet`(SO, 행성마다 mesh·material·scale·seatHeight) + `PlanetBodies.Create` → `PlanetBody`(Key, Radius, SeatLocal) | gameplay-ui | `Game/UI/Planets/`, `Data/PlanetBodies.asset` (v4b는 에셋 교체만) |
| 지도·표면 길 뷰, `StageSelectController`(순수 C#), 별 길 켜기, 형제 표시, 입력 | gameplay-ui | `TitleViews`/`PlanetMapView3D` |
| 다이브(지도 → 보드), 이어 가기(클리어 → 다음 보드), 행성 완료 → 지도 복귀의 카메라·보드 연출, 건너뛰기 | scene-flow | `Game/Flow/Journey/` |
| `JourneyReturn`(순수 C#, Services) | scene-flow | `Game/Flow/JourneyReturn.cs` |
| 보드 쪽 훅 `CameraHeld`, `DetachSnapshot()` | scene-flow가 작성, gameplay-ui 리뷰 | `BoardView`/`BoardController` (작은 별도 커밋) |

## 1. 다이브 (Title → Game, 1.5초)

1. 지도에서 행성을 고르면(클릭·탭·Enter) `StageSelectController.StageChosen(StageChoice)`가 난다.
   `StageChoice { Planet, PlanetKey, Stage, Source(Map|Surface), Body(PlanetBody, 월드), Camera(지도 카메라), Commit(), Cancel() }`.
   지도 클릭 = 그 행성의 첫 미완료 스테이지. 다 깬 행성은 표면 길을 연다(이벤트 없음). 표면 돌 클릭 = 그 스테이지.
2. 선택이 걸려 있는 동안 지도 뷰는 입력을 무시하고, 이름·한 줄 소개·`‹`·별 줄·형제 표시를 숨기고, 카메라와 몸체를 움직이지 않는다.
3. scene-flow가 카메라를 지금 자세에서 몸체 꼭대기(`Body.transform` + `SeatLocal`, 줌은 `Radius`)로 옮긴 뒤 `Commit()`(= select 알림 + `flow.GoToGame(stage)`)을 부른다. `Commit()`은 한 번만(두 번째는 예외). 흐름이 받지 못하면 `Cancel()`.
4. 씬 교체는 `SceneLoader`의 0.15초 페이드가 덮는다. Game 씬은 행성 위 넓은 자세에서 보드 맞춤 자세로 카메라를 옮기고, 보드가 표면에서 올라온다.
5. `StageChosen` 구독자가 없으면(여정 흐름 꺼짐) 컨트롤러가 직접 `PlayStage`를 부른다(예전 흐름).

## 2. 이어 가기 (클리어 → 다음 보드, 1.5초)

같은 행성의 다음 스테이지로 갈 때 결과 패널 없이: 형제가 출구 방향(오른쪽 +X)으로 걸어 나가고, 다음 보드가 그쪽에서 솟아오르고, 카메라가 따라간다. Game 씬 안에서 scene-flow가 한다. 보드 쪽 필요한 것은 아래 두 훅뿐이다.
- `bool CameraHeld { set; }`: 참이면 BoardView가 카메라를 다시 맞추지 않는다. 거짓이 되면 한 번 맞춘다.
- `BoardSnapshot DetachSnapshot()`: 지금 타일·배우의 정지 사본(루트 하나, 배우 Transform 목록, 로직 없음). 부른 쪽이 지운다.

## 3. 행성 완료 → 지도 (1.5초)

1. 행성의 마지막 스테이지(전체 마지막 제외)를 깨면 Game 씬이 카메라를 보드에서 뒤로 뺀 뒤 `JourneyReturn.Begin(planet, key)`, `flow.Go(StageSelect)`.
2. Title 씬은 카메라를 그 행성 가까이에서 시작해 지도 자세로 뺀다(`MapCamera`, `Body(planet)`는 지도 뷰가 읽기 전용으로 연다). 끝나면(또는 건너뛰면) `JourneyReturn.Settle()`.
3. `Pending` 동안 지도 뷰는 새 별 길을 켜지 않고 초점을 잡아 두고 카메라·몸체를 움직이지 않는다. `Settled`에서 새 별 길을 켜고 다음 행성에 초점을 둔다. Pending이 아니면 보일 때 바로 켠다(“본 별 길” 저장).

## 4. 건너뛰기와 토글

- 전환 중 아무 입력(클릭·키·터치)이나 도착 자세로 바로 가고, 그 입력은 소비한다(퍼즐 이동이 되지 않음). 움직임 줄이기 설정도 같은 길.
- 새 지도 / 예전 지도: `TitleScreen.planetBodies`(기본 켬), `-oldmap` / `?oldmap=1` (gameplay-ui).
- 여정 전환(다이브·이어 가기·복귀): 새 지도일 때 켜짐, `-journey off` / `?journey=0`으로 끔 (scene-flow). 꺼지면 예전 흐름(결과 패널, 동기 이동).
