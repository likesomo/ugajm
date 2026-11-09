# 검토: feat/scene-flow 0bcade4 (S8 흐름 검사 `-flowcheck`, smoke 흐름 단계)

검토자: 아키텍트, 2026-11-10. 코드 읽기 검토. 씬 플로우 보고: smoke 7/7 + flow 9/9 OK.

**결론: 승인.** Contracts `LaunchOptions.FlowCheck` 추가, docs 11장·변경 기록은 아키텍트가 갱신함.

- `FlowCheck`는 `-flowcheck` + `-replay` + 바로 게임 진입일 때만 켜진다. 마지막 수를 제외한 리플레이는 즉시 적용, 마지막 수는 `board.Send`로 실제 연출을 거쳐 이긴다 → Result 문구가 행성 끝·여정 끝·중간 스테이지에 맞는지, Next 버튼 표시, Next 후 스테이지·챕터 카드·챕터 프리셋을 검사. 실제 플레이어 코드 경로를 통과하므로 EditMode로 못 보는 연결(cf65d84 F2 같은 누락)을 잡는다.
- 필요한 것을 생성자로 받고(null 단정), `GameController`는 필드 3개만 늘었다. 20초 타임아웃, 결과는 한 줄 로그 + 종료. 텔레메트리는 `AppConfig.telemetryEndpoint`가 비어 있어 스모크에서 나가지 않는다.
- `unity.sh smoke` 흐름 단계: 로그에서 OK 줄과 예외를 본다. 행성마다 마지막 스테이지 + 중간 대조 2개.

## 메모
- **N1(사소)** `?flowcheck=`에 1/true 외 값이면 조용히 꺼진다. 다른 옵션(`newplayer`, `smoke`, `loadDelay`)은 잘못된 값을 `Warnings`에 넣으므로 같은 방식이 맞다(`0|false`는 끔, 그 밖은 경고).
- `FlowCheck`는 플레이어 빌드(Game 어셈블리)에 들어가고 `ScreenCapture`를 쓴다. `-flowcheck`가 없으면 생성되지 않으니 문제없다. WebGL에서는 스크린샷 경로가 없어 캡처 줄이 돌지 않는다.
