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

## 2. 클리어 → 결과 → 이어 가기 (1.5초)

**여정은 결과를 건너뛰지 않는다** (리드 2026-11-12, 아키텍트 검토 `docs/reviews/2026-11-12-scene-flow-d426f71-u5-title.md`). 클리어하면 여정이든 아니든 결과가 먼저 열린다(U5 클리어 또는 지금 결과 패널, `IResultPanel`). 여정은 결과의 버튼에서 시작한다. 흐름의 분기는 `GameFlowController`에 있다. 뷰의 이벤트(`NextRequested`·`StageSelectRequested`·`RetryRequested`)는 그대로 `Next`·`OpenStageSelect`·`Again`을 부른다.

| 결과에서 | 여정 켬 | 여정 끔 |
|---|---|---|
| 다음, 같은 행성 | `Advancing` → 아래 이어 가기 → `ContinueTo`(Result → Game, via next 한 번) | 다시 로드 |
| 다음, 행성의 마지막 | `PlanetCompleting` → 3장 복귀(길 불 켬) → `FinishReturn` | 다시 로드(다음 행성) |
| 다음, 전체 마지막 | 엔딩(`[끝까지 보기]`) | 같음 |
| 지도·Esc | `Returning` → 3장 복귀(`ReturnKind.Back`, 길 불 없음) → `FinishReturn` | Title 씬 |
| 다시 하기·R | 같은 스테이지 다시 로드 | 같음 |

- 버튼 하나가 전환을 시작하면 `Leaving`이 참이 되고, 다음 스테이지에 들어갈 때까지 다음·다시 하기·지도는 아무것도 하지 않는다(두 번 누르기). 키 입력은 `GameJourney.Blocks`가 막는다.
- 결과는 전환 시작 때 `result.Hide()`로 닫힌다.

이어 가기: 형제가 출구 방향(오른쪽 +X)으로 걸어 나가고, 다음 보드가 그쪽에서 솟아오르고, 카메라가 따라간다. Game 씬 안에서 scene-flow가 한다. 보드 쪽 필요한 것은 아래 두 훅뿐이다.
- `bool CameraHeld { set; }`: 참이면 BoardView가 카메라를 다시 맞추지 않는다. 거짓이 되면 한 번 맞춘다.
- `BoardSnapshot DetachSnapshot()`: 지금 타일·배우의 정지 사본(루트 하나, 배우 Transform 목록, 로직 없음). 부른 쪽이 지운다.

## 3. 행성 완료 → 지도 (1.5초)

1. 결과에서 행성의 마지막 스테이지(전체 마지막 제외)의 다음, 또는 아무 스테이지의 지도·Esc를 누르면 Game 씬이 카메라를 보드에서 뒤로 뺀 뒤 `JourneyReturn.Begin(planet, key, kind)`, `FinishReturn`(= `flow.Go(StageSelect)`). kind는 `PlanetComplete`(다음) 또는 `Back`(지도·Esc).
2. Title 씬은 카메라를 그 행성 가까이에서 시작해 지도 자세로 뺀다(`MapCamera`, `Body(planet)`는 지도 뷰가 읽기 전용으로 연다). 끝나면(또는 건너뛰면) `JourneyReturn.Settle()`.
3. `Pending` 동안 지도 뷰는 새 별 길을 켜지 않고 초점을 잡아 두고 카메라·몸체를 움직이지 않는다. `Settled`에서 새 별 길을 켜고 다음 행성에 초점을 둔다. `LastKind == Back`이면 아무것도 켜지 않고 그 행성에 초점을 둔 지도를 다시 보인다. Pending이 아니면 보일 때 바로 켠다(“본 별 길” 저장).

## 4. 건너뛰기와 토글

- 전환 중 아무 입력(클릭·키·터치)이나 도착 자세로 바로 가고, 그 입력은 소비한다(퍼즐 이동이 되지 않음). 움직임 줄이기 설정도 같은 길.
- 새 지도 / 예전 지도 (리드 조건 2026-11-11): **기본은 지금 지도(새 지도 꺼짐)**. 새 지도는 켜는 플래그 `-newmap` / `?newmap=1`로 연다(`-oldmap` / `?oldmap=1`도 유지). 리드가 금요일 직전에 기본값만 뒤집을 수 있게 둔다 (gameplay-ui).
- 여정 전환(다이브·이어 가기·복귀): 새 지도일 때만 켜짐, `-journey off` / `?journey=0`으로 따로 끔 (scene-flow). 꺼지면 결과의 버튼이 예전처럼 바로 이동한다. U5가 혼자 켠 v4 지도는 여정을 켜지 않는다(`-newmap`이나 지도 기본값일 때만, d426f71).
- Commit/Cancel은 한 번만(assert + 예외). 다이브 중 Esc는 Cancel(카메라가 지도 자세로 돌아가고 선택이 풀림). 다른 입력(두 번째 클릭 포함)은 건너뛰기: 도착 자세로 가서 Commit을 **한 번** 부른다. 두 번째 클릭이 또 Commit하는 일은 없다.
- 병합 조건: 토글이 꺼진 상태에서 기존 smoke·smoke-webgl·flowcheck(타이틀 경로 3/3)가 그대로 통과.

## 5. 확인

- **에디터 Play Mode (기본, 2026-11-11 검증 규칙)**: `unity.sh playtest`의 `JourneyFlowTests`. 클리어 뒤 결과가 먼저 열리고, U5 클리어의 다음으로 이어 가기(스테이지 1 → 2, 다시 로드 없음, 사본 보드 없음, 카메라·보드 루트 원위치), 지금 결과 패널의 다음으로 행성 완료(첫 행성 마지막 스테이지 → Title 지도, `JourneyReturn` 풀림, 카메라가 `MapPose`, 그 행성의 나가는 별 길 켜짐), U5 클리어의 지도로 되돌아가기(`ReturnKind.Back`, 켜진 길 없음).
- 아래 플레이어 빌드 smoke 단계는 리드의 빌드 확인용이다.

### 5.1 플레이어 빌드 smoke 여정 단계 (리드 전용)

- `unity.sh smoke` 마지막 단계: `-newmap -level N -replay <풀이> -journeycheck advance|complete` (`flow-stages.mjs --journey`).
  - advance(스테이지 1): 결과가 열리면 다음을 눌러 2로 들어가고, 전환이 끝나면 사본 보드가 없어지고 보드 루트가 제자리, 카메라가 보드 것으로 돌아오고 형제가 보인다.
  - complete(첫 행성의 마지막 스테이지): Game 씬이 뒤로 빠지고, Title 씬 지도에서 `JourneyReturn`이 풀리고 `TrailLit`이 나고, 카메라가 `MapPose`에서 끝난다.
  - 스크린샷 `reports/smoke/journey-*-mid.png`, `-end.png`, `journey-complete-pullback.png`. 로그 "[SevenSteps] Journey check KIND: OK".
- 전환 시계는 한 프레임에 최대 0.05초만 센다(`JourneyRunner.MaxFrameSeconds`). 씬 로드 뒤 첫 프레임(300ms 넘음)이 0.9초 복귀를 한두 프레임에 끝내던 문제.
- 2026-11-11 결과(feat/scene-flow 6cbd5ea): compile OK, EditMode 921/921, scene-validate OK, smoke 전부 OK(리플레이 7, flowcheck 9, titlecheck 3, journey 2), build-webgl(Mobile_RPAsset) + smoke-webgl OK(보드 56536/84031/89367, 대조 0).
- 아직 자동 확인이 없는 것: 지도 → 보드 다이브(Title 씬 입력 경로). 눈으로 확인하거나 다음에 `-journeycheck dive`를 더한다.
