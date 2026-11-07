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
