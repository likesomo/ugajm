# 코드 검토: `feat/view` 2단계 (게임플레이/UI)

- 검토자: 아키텍트, 2026-11-07
- 대상: 로컬 `feat/view` d982a65 + 71a4c6c(7lucky 머지). 아직 원격에 없음. 작업 공간에 커밋 안 된 텔레메트리 수정(`TelemetryRecord` → Contracts 타입 전환 중)이 있어서, 텔레메트리 부분은 커밋된 코드 기준으로 보고 전환 후 다시 본다.
- 기준: `docs/architecture.md` (4·5·7·8·9·11장), `docs/telemetry.md` v1

## 결론

구조는 문서대로 잘 옮겨졌다. 아래 **필수 2건**을 고치고 `origin/7lucky`(Contracts 텔레메트리 v1, 석상 수 문, 레버 표기)를 받은 뒤 다시 컴파일·테스트하면 합쳐도 된다.

잘 된 점:
- `GameController`가 `GameSession`만 거쳐 상태를 바꾼다. 직접 `Rules.Dispatch` 호출 없음. 입력 코드는 잠금을 직접 보지 않는다.
- `BoardView`가 `IBoardPresenter` 계약을 지킨다: `done`은 끝날 때 한 번, 취소 때는 안 부름. `Snap`/`Rebuild`가 연출 중 임시 오브젝트(링)를 정리한다. 모든 표시를 `State`에서 다시 계산한다.
- 프리팹은 `TileSet`, 셀 종류는 `TileId.At`, 위치·클릭은 `BoardGrid`만 쓴다. 글자 분기 없음. 카메라는 `CameraRig`로 맞춰 쿼터뷰 전환 준비가 됐다.
- 씬 분리(Boot/Title/Game), `Bootstrap`이 11.2 순서대로 서비스를 등록, 에디터에서 씬을 바로 실행해도 같은 부트스트랩을 탄다.
- `Update`/`LateUpdate`에 LINQ·`params`·문자열 결합 없음. v0의 `Pressed(params …)`도 고쳤다.
- 소유권: `ProjectSettings`, `BuildTools`, asmdef, Contracts, Core를 건드리지 않았다. jslib는 WebGL만 켜져 있다.

## 필수

1. **텔레메트리를 Contracts v1 타입으로 전환** (작업 중인 것 마무리). `HttpTelemetry.Track`이 옛 `TelemetryEvent`(`Name`/`Strings`/`Numbers`)를 쓰고 있어서 최신 `7lucky`와 합치면 컴파일되지 않는다. `TelemetryEvent`/`TelemetryJson`/`TelemetryNames`/`TelemetryContext`를 쓰고, `TelemetryRecord`는 지운다. `seq`·`t`는 보내는 쪽이 `Stamp`로 붙인다. 골든 비교는 `TelemetryJson.WriteEvent(…, includeTime: false)`.
2. **서버 주소 우선순위**(11.4): `TelemetryRunner.CreateTelemetry`가 `LaunchOptions.TelemetryUrl`(`?telemetry=<url>`, `-telemetry <url>`)을 무시하고 `AppConfig.telemetryEndpoint`만 본다. `launch.TelemetryUrl`이 있으면 그것을 먼저 쓴다. `off`는 `LaunchOptions`가 이미 처리한다.

## 고치면 좋음

3. **결과 패널이 걷는 도중에 뜬다.** `OnSessionUpdated`에서 Win이면 바로 `flow.Go(AppState.Result)`를 부르는데, `Updated`는 연출 **시작 전**에 발생한다. 새로 추가한 `GameSession.Presented`(연출이 끝난 뒤 한 번)에서 `Result`로 넘어가도록 바꾼다. 저장(`MarkCleared`)은 지금 위치에 둬도 된다.
4. **새 기믹 표시**: `7lucky`에 석상 수 문(`Q`)이 들어왔다. `BoardView.IsActive`에 `TileKind.CountDoor => Rules.DoorOpen(s, 'Q')`, 라벨에 `Level.StatueCount` 숫자가 없으면 Q 문이 늘 닫혀 보인다. 이벤트 `Slide`/`DoorOpen`/`DoorClose`(효과음·연출)도 아직 안 쓴다. 레버(`TileKind.Lever`·`FlipWall`, `Flip` 이벤트, 6.2장)는 코어 구현이 들어온 뒤.
5. **옵트아웃 때 `playerId`**: 11.3은 "거부하면 지움"인데 지금은 그대로 남는다. 끌 때 `Settings.playerId = ""`로 지우고, 다시 켜면 새로 만든다(지금 다시 켤 때 새로 만드는 코드는 이미 있음).

## 사소함

6. `Snap`이 연출 중 시작된 `Pop` 코루틴(다시 깨어난 플레이어, 상태 바뀐 타일)을 멈추지 않아서, `Snap` 직후 최대 0.22초 동안 크기가 덮어써질 수 있다. 연출용 코루틴을 목록에 넣고 `StopAnim`에서 멈추면 "Snap 그림 = 상태"가 정확해진다.
7. `TelemetryRunner.CreateTelemetry`의 `Destroy(old)`는 프레임 끝에 지워지므로 같은 이름 오브젝트가 한 프레임 둘 생긴다(jslib가 이름으로 부름). 부트스트랩이 두 번 도는 경우(에디터에서만)라 영향은 작다. `DestroyImmediate`나 기존 러너 재사용을 권한다.
8. 연출 중 Undo가 큐 입력만 취소하면(`GameSession`) `Updated`가 없으므로 텔레메트리 `undo`도 없다. 실제로 일어나지 않은 이동이라 이게 맞다. 그대로 둔다.

## 문서 쪽에서 맞춘 것 (아키텍트)

- jslib 이름은 구현대로 `SevenSteps_SendBeacon`, `SevenSteps_RegisterVisibility`로 11.5를 고쳤다.
- 데스크톱 종료 때 1초 대기용 `Thread.Sleep` 루프는 telemetry.md 4장 요구라 9장 WebGL 규칙의 예외로 적었다(`#if !UNITY_WEBGL || UNITY_EDITOR` 안에서만).
- `GameSession.Presented` 추가(3번용).

---

## 재검토: `origin/feat/view` 57907b9 (2026-11-07)

- 기준 `d8195e7`. 현재 `7lucky` cc82b04(레버 13b2d86 포함)와의 합치기를 임시 브랜치에서 실제로 만들어 확인했다.
- **결론: 합쳐도 된다.**

### 합치기 확인 (7lucky cc82b04 + feat/view 57907b9)
- 텍스트 충돌 없음 (`git merge-tree` 깨끗, 실제 머지도 충돌 없음).
- `feat/view` 자체 변경은 소유 범위 안에만 있다: Core·Contracts·asmdef·ProjectSettings·Packages·Golden·`docs/`를 건드리지 않음. 차이 목록에 그 파일들이 보이는 것은 기준이 `7lucky`보다 뒤라서이고, 머지하면 `7lucky` 쪽이 그대로 남는다.
- EditMode 207/207 (게임플레이/UI 골든 `TelemetryGoldenTests` 포함), Windows 빌드 + smoke 6개(1, 3, 8, 11, 13, 14) 예외 0, WebGL 빌드 성공(14.4MB, 0 errors, jslib 링크 확인).
- 화면 확인: smoke 11번 스크린샷에서 Q 문이 열린 상태(숫자 금색)로 그려지고 HUD가 "석상 1 / 1 문 열림"을 보인다.

### 필수 2건 → 해결
1. 텔레메트리가 Contracts v1 타입(`TelemetryEvent`/`TelemetryJson`/`TelemetryNames`/`TelemetryContext`)을 쓴다. `TelemetryRecord` 삭제. 골든 테스트가 서버 골든과 같은 이벤트를 확인한다.
2. `CreateTelemetry`가 `LaunchOptions.TelemetryUrl`을 먼저 쓰고 없으면 `AppConfig.telemetryEndpoint`. `off`는 `LaunchOptions`가 처리.

### 권장 항목 상태
- 4번 Q 문: 해결 (`IsActive`에 `Rules.DoorOpen(s,'Q')`, 라벨 `Level.StatueCount`, 석상·플레이어가 서 있어도 숫자 표시).
- 3번 결과 패널 타이밍: 아직(이 브랜치의 기준에는 `GameSession.Presented`가 없었음). 합친 뒤 `Presented`에서 `Result`로 넘어가게 바꿀 것.
- 5번 옵트아웃 시 `playerId` 지우기: 아직. 다시 켤 때 새 ID를 만드는 코드는 있으므로 끌 때 지우기만 추가하면 된다.
- 6·7번(사소): 그대로. 다음 작업 때.
- 레버(`TileKind.Lever`/`FlipWall`, `Flip` 이벤트): 코어 13b2d86이 이 브랜치보다 나중이라 아직 표시 없음. `TileSet.lever`/`flipWalls`는 이미 있으니 `IsActive`에 `Lever => s.Flipped`만 추가하면 기본 표시는 된다(뒤집힘 벽은 `TileId` variant로 모양이 바뀜). 레버 스테이지가 들어오기 전에 할 것.

### 문서 쪽 맞춤 (아키텍트)
- jslib 함수 이름: 이 브랜치가 telemetry.md 이름(`TelemetryBeacon`, `TelemetryRegisterVisibility`)으로 바꿨다. 11.5를 다시 그 이름으로 고쳤다(내가 직전에 `SevenSteps_*`로 바꾼 것을 되돌림).

---

## 사후 검토: 2a9e801(검토 반영), 43f3f38(레버 표시) — 리드가 c12fd42로 합친 뒤

**결론: 문제 없음.** 남은 권장은 옵트아웃 시 `playerId` 지우기 하나.

- 결과 패널: `GameSession.Presented`에서 `Status.Win && flow.State == Game`일 때만 `Result`로. 걷는 도중 패널이 뜨던 문제 해결. 연출이 Undo/Restart로 취소되면 `Presented`가 없으므로 패널도 안 뜸 — 맞다.
- `Snap` 정리: 크기 트윈을 목록(`tweens`)에 모아 `StopAnim`에서 멈추고 목표 크기로 맞춘다. "Snap 그림 = State" 지켜짐. 목록은 `Present`마다(`StopAnim`) 비워지므로 계속 늘지 않는다.
- `Slide`: 첫 칸은 걸음, 나머지는 홉 없이 미끄러짐. `DoorOpen`/`DoorClose`: 이벤트 시점에 그 칸의 활성 상태를 미리 바꾸고, 마지막 `Draw`는 이미 같은 값이라 두 번 재생하지 않는다.
- 레버: `IsActive`에 `Lever => s.Flipped`(State 필드 읽기, 규칙 계산 아님). `Flip` 이벤트에서 `TileId.At(r.State, i)`로 모양이 바뀐 뒤집힘 벽 칸만 다시 만들고, 마지막 `Draw`는 같은 `TileId`라 다시 만들지 않는다. 메시지·효과음(임시로 switch 소리) 추가. 6.2 계약과 맞다.
- 사소: 이벤트마다 `new WaitForSeconds`(명령당 몇 개라 무시 가능). 효과음은 아트가 레버 소리를 만들면 `GameAssets`에 필드 추가.
- jslib 이름은 다시 `SevenSteps_*`로 돌아갔다 → 11.5를 코드 기준으로 확정했다.

---

## 사후 검토: 27cb609 (카메라 안 A, Game 씬 설정, 기본 꺼짐) — 7lucky 156c054로 합쳐진 뒤

**결론: 문제 없음.** `BoardView.Init`이 선택적 `CameraRig?`를 받아 `TileSet.cameraRig` 대신 쓴다. 꺼져 있으면 v0와 같은 동작. yaw 0이라 입력 회전 불필요(12.2).

- 참고: 카메라의 기준 자리는 `TileSet.cameraRig`다(세트마다 벽 높이·라벨 위치가 다르므로). 아트의 실제 세트가 오면 그 세트의 `cameraRig`에 pitch 50을 넣고, 씬의 `overrideCamera`는 꺼 둔 채 미리 보기용으로만 쓴다.
