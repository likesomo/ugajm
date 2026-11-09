# 플레이 기록(텔레메트리) 스키마 — v1

게임(웹 프로토타입, Unity WebGL/데스크톱)이 수집 서버로 보내는 익명 플레이 이벤트의 공용 계약이다.
소유: 백엔드/분석 (`feat/server`). 바꿀 때는 `agents/README.md` "API·인터페이스 변경 고지"를 따르고 아래 변경 기록에 적는다.

- 기준 구현: `prototypes/seven-steps/src/telemetry.js` (`Tracker`가 `dispatch` 결과를 이벤트로 바꾼다). Unity 구현은 이 파일과 같은 이벤트를 내야 한다.
- 수집 서버: `server/` (`POST /v1/events`), 실행·배포는 `server/README.md`

## 1. 전송 단위 (배치)

`POST /v1/events`, 본문은 JSON. `Content-Type: application/json` 또는 `text/plain`(`navigator.sendBeacon`용) 둘 다 받는다.

```json
{
  "v": 1,
  "playerId": "6f1c…(UUID v4)",
  "sessionId": "b2e0…(UUID v4)",
  "client": "web-proto",
  "build": "proto-ba443bd",
  "events": [
    { "seq": 0, "t": 1762500000000, "type": "session_start", "firstTime": true, "cleared": 0 },
    { "seq": 1, "t": 1762500000050, "type": "level_start", "level": 1, "levelName": "일곱 걸음", "attempt": 1, "via": "boot", "bodies": 3 }
  ]
}
```

| 필드 | 형식 | 설명 |
|---|---|---|
| `v` | 정수 | 스키마 버전. 지금은 `1` |
| `playerId` | UUID 문자열 | 기기(브라우저)에 저장한 랜덤 값. 사람을 식별하는 정보와 연결하지 않는다 |
| `sessionId` | UUID 문자열 | 게임을 켤 때마다 새로 만든다. 탭을 숨겼다 돌아오면 같은 값을 쓴다 |
| `client` | `web-proto` \| `unity-webgl` \| `unity-desktop` | 보낸 클라이언트 |
| `build` | 문자열 ≤ 40자 | 버전·커밋 (예: `0.1.0+ba443bd`, `proto-ba443bd`) |
| `events[]` | 1~100개 | 이벤트 목록. 본문 전체 64KB 이하 |
| `events[].seq` | 정수 ≥ 0 | 세션 안에서 1씩 증가. 서버는 `(sessionId, seq)`가 같으면 중복으로 버린다 → 재전송해도 안전 |
| `events[].t` | 정수 (ms, Unix epoch) | 클라이언트 시각 |
| `events[].type` | 문자열 | 아래 이벤트 종류 |

공통 필드(`v` `playerId` `sessionId` `client` `build`)는 배치 머리에 한 번만 둔다. 서버가 이벤트마다 펼쳐서 저장한다.

### 응답

- `200 {"accepted": n, "duplicates": d, "rejected": [{"seq": 3, "error": "…"}]}` — 이벤트 단위로 검증한다. 일부가 틀려도 나머지는 저장된다
- `400` 배치 머리가 틀림, `413` 64KB 초과, `403` 허용되지 않은 Origin, `429` 속도 제한 (`Retry-After` 초)
- 클라이언트는 `2xx`·`400`·`413`·`403`이면 그 배치를 큐에서 지운다(다시 보내도 같은 결과). 네트워크 오류·`429`·`5xx`만 재시도한다

## 2. 좌표와 공통 값

- 칸 좌표는 `[x, y]` (0부터, 왼쪽 위가 `[0,0]`, 레벨 맵 문자열의 열·행). 맵 밖은 `null`
- `level`: 1부터 시작하는 스테이지 번호(현재 빌드의 순서). 순서가 바뀔 수 있으므로 `level_start`에 `levelName`과 `levelKey`도 보낸다
- `levelKey`: 스테이지 내용의 해시(8자리 소문자 16진수). 문자열 `<몸 수>
<행1>
<행2>…`(각 행은 레벨 폭까지 `#`으로 채우고 출발점은 `S`)의 FNV-1a 32비트. 맵이나 몸 수가 바뀌면 달라지고 순서만 바뀌면 같다. 기준 구현 `telemetry.js` `levelKey()`, 예: 스테이지 1 → 골든 파일 참고
- `chapter`: `levels.json`/`levels.js` 스테이지의 `chapter` 값(문자열 id, `docs/architecture.md` 12.8) 그대로. 없으면 보내지 않는다. 서버는 현재 빌드 레벨 데이터의 챕터를 우선 쓰고, 이 값은 지금 빌드에 없는 맵의 챕터를 알 때만 쓴다
- `body`: 이번 스테이지에서 몇 번째 몸인지 (1부터). `level.bodies - state.bodies + 1`
- `step`: 그 이동이 몇 번째 걸음인지 (1~7). `state.used + 1`
- `tile` 종류: `floor` `exit` `pit` `ice` `cracked` `bridge` `plate` `door` `gate` `wall` `start`(현재 출발점 칸)

## 3. 이벤트

스테이지에 속한 이벤트(`level_start`~`level_leave`, `idle`)에는 모두 `level`이 있다.

| type | 언제 | 필드 |
|---|---|---|
| `session_start` | 게임을 켰을 때, 숨긴 탭이 다시 보일 때 | `firstTime`(이 기기에서 처음), `cleared`(저장된 클리어 스테이지 수), `resumed`(탭 복귀면 true) |
| `level_start` | 스테이지 진입·클리어 뒤 재시작 | `level`, `levelName`, `levelKey`(선택), `chapter`(선택, 챕터 문자열 id ≤40자, 스테이지에 챕터가 있을 때만), `attempt`(이 기기에서 이 스테이지를 시작한 횟수, 1부터), `via`: `boot` \| `url` \| `next` \| `select` \| `again`, `bodies`(몸 수) |
| `move` | 방향 입력 하나 (막혀도 보낸다) | `level`, `body`, `step`, `dir`: `up` \| `down` \| `left` \| `right`, `from`, `to`(멈춘 칸, 막혔으면 막은 칸), `result`: `moved` \| `blocked` \| `slid` \| `fell` \| `bridged` \| `door` \| `gate`, 선택: `blockedBy`(막은 tile 종류 또는 `statue` `edge`), `crumbled`(지나온 금 간 바닥이 무너졌으면 true) |
| `seventh_step` | 7번째 걸음이 어딘가에 닿았을 때 (`move` 바로 뒤) | `level`, `body`, `at`, `tile`, `effect`: `win` \| `bridge` \| `seal` \| `vanish` \| `choose` \| `statue`(마지막 몸이라 자동 석상) |
| `keep` | KEEP 선택 | `level`, `body`, `at` |
| `switch` | SWITCH 선택 | `level`, `body`, `at` |
| `undo` | 되돌리기 (되돌릴 게 있을 때만) | `level`, `body`, `step`(되돌리기 전 쓴 걸음 수), `at`(되돌리기 전 위치), `status`(되돌리기 전 `play` \| `choose` \| `lose` \| `win`) |
| `restart` | 재시작 | `level`, `body`, `step`, `at`, `moves`(이번 시도의 이동 수) |
| `hint` | 풀이 보기 | `level`, `body`, `step`, `at` |
| `body_lost` | 몸 하나를 썼을 때 | `level`, `body`(잃은 몸 번호), `at`, `reason`: `fell`(7번째가 아닌 걸음에 구덩이) \| `vanish`(출발점에서 7번째) \| `statue`(KEEP·자동 석상) \| `switch` \| `bridge` \| `seal`, `wasted`(`fell`·`vanish`면 true) |
| `level_fail` | 몸을 모두 잃음 | `level`, `at`, `moves`, `bodiesUsed` |
| `level_clear` | 출구 도착 | `level`, `durationMs`(이번 시도 시작부터), `moves`, `undos`, `restarts`, `hints`, `fails`, `bodiesUsed`, `minMoves`(선택, 솔버 최단 이동 수) |
| `level_leave` | 클리어하지 않고 다른 스테이지로 감 | `level`, `toLevel`, `durationMs`, `moves`, `undos`, `restarts`, `hints`, `fails`, `body`, `step`, `at` |
| `idle` | 입력 없이 30초 이상 지나고, 다음 입력·스테이지 이동·탭 숨김으로 그 멈춤이 끝났을 때 | `level`, `idleMs`(실제 멈춘 시간, 탭이 숨겨진 시간 제외), `body`, `step`, `at`, `status`, `bodiesLeft`, `statues`(석상 수) |
| `session_end` | 탭 숨김·닫힘 | `reason`: `hidden` \| `unload`, `level`(그때의 스테이지, 없으면 생략), `cleared`(그 스테이지 클리어 상태였는지), `body`, `step`, `at`, `sessionMs` |

규칙:
- 한 번의 입력에서 나오는 이벤트 순서: `idle`(있으면) → `move` → `seventh_step` → `body_lost` → `level_fail` 또는 `level_clear`
- 스테이지 "시도"는 `level_start`에서 시작해 `level_clear` / `level_leave` / `session_end`로 끝난다. 시도 중 `restart`는 같은 시도로 센다(재시작 수로 집계)
- 클리어한 뒤 다시 하기(R)는 `level_start` `via: "again"`으로 새 시도를 연다
- 리플레이(`?replay=…`) 입력은 기록하지 않는다
- 알 수 없는 필드는 서버가 그대로 보관한다(`data` JSON). 새 필드는 선택 필드로만 추가하고, 의미가 바뀌면 `v`를 올린다

## 4. 전송 사양 (클라이언트)

| 항목 | 값 |
|---|---|
| 배치 | 5초마다 또는 큐가 20건이 되면 전송, 한 배치 최대 100건 |
| 로컬 큐 | 웹: `localStorage` `seven-steps-telemetry-queue`, 최대 1000건(넘치면 오래된 것부터 버림). 오프라인·서버 없음에도 게임은 그대로 동작 |
| 재시도 | 네트워크 오류·429·5xx: 5s → 10s → 20s … 최대 5분 간격. `Retry-After`가 있으면 따른다 |
| 페이지 닫힘 | `visibilitychange`(hidden)·`pagehide`에서 `session_end`를 넣고 `navigator.sendBeacon(url, text/plain)`으로 남은 큐를 보낸다 |
| 시간 초과 | 요청 10초 |
| 서버 주소 | 웹 프로토타입: `?telemetry=<url>` → `localStorage` → 페이지가 수집 서버(`/play/`)에서 열렸으면 같은 출처 → `localhost`면 `http://localhost:8787` → 그 외에는 전송 안 함 |
| 옵트아웃 | `?telemetry=off` 또는 `?telemetry=0`(Unity와 같음) 또는 화면 아래 "기록 전송" 토글. `localStorage` `seven-steps-telemetry`=`off`면 큐도 만들지 않는다 |

### Unity 구현 메모 (게임플레이/UI 에이전트용, 인터페이스는 아키텍트가 정함)

- 이벤트 생성은 `GameController`가 `Rules.Dispatch` 전후 상태와 결과 이벤트를 비교해 만든다 (`telemetry.js`의 `Tracker.command`와 같은 대응표). 코어(`SevenSteps.Core`)는 고치지 않는다
- `playerId`: `PlayerPrefs`에 UUID 저장. WebGL에서는 IndexedDB로 유지된다
- 전송: `UnityWebRequest` POST(`application/json`), 큐는 메모리 + 종료 시 `PlayerPrefs`에 남은 것 저장(최대 1000건)
- **WebGL 페이지 닫힘**: `Application.quitting`은 브라우저 탭 닫힘에 불리지 않는다. `sendBeacon`을 부르는 `.jslib`가 필요하다 — `visibilitychange`(hidden) 때 JS 쪽에서 C#의 `OnPageHidden`을 `SendMessage`로 부르고, C#이 `session_end`를 큐에 넣은 뒤 큐 JSON을 jslib 함수 `TelemetryBeacon(url, json)`에 넘겨 `navigator.sendBeacon(url, new Blob([json], {type:'text/plain'}))`로 보낸다. 64KB를 넘으면 앞에서 100건씩 잘라 여러 번 부른다
- 데스크톱: 종료 시(`Application.quitting`) `session_end` `reason: "unload"`를 보내고 최대 1초 기다린다
- `build`: `Application.version` + 커밋 해시, `client`: `unity-webgl` / `unity-desktop`
- 서버 주소: WebGL은 페이지 URL `?telemetry=` → 빌드 설정값, 데스크톱은 `-telemetry <url>` 인자 → 빌드 설정값. `off` 또는 `0`이면 끈다 (`docs/architecture.md` 11.4)

## 5. 개인정보

- 저장하는 것: 위 필드와 서버 수신 시각뿐. IP 주소·User-Agent는 저장하지 않는다 (속도 제한용 IP는 메모리에서만 해시로 쓰고 버린다)
- 이름·이메일·계정 등 사람을 식별하는 값은 보내지 않는다. 자유 입력 텍스트 필드가 없다
- 플레이어는 언제든 끌 수 있다(옵트아웃). 외부 분석 서비스는 쓰지 않는다

## 변경 기록

| 날짜 | 버전 | 내용 |
|---|---|---|
| 2026-11-07 | v1 | 최초 확정 |
| 2026-11-07 | v1 | `level_start.levelKey`(선택) 추가 — 레벨 디자인 요청. 선택 필드라 버전은 그대로 |
| 2026-11-09 | v1 | `level_start.chapter`(선택) 추가 — 50스테이지·챕터 대비. 선택 필드라 버전은 그대로. 웹 `Tracker.levelStart(state, level, name, via, chapter)`. Unity `TelemetryTracker`는 `LevelCatalog.ChapterOf`가 생기면 같은 자리(`levelKey` 다음)에 넣는다. 챕터 없는 지금 스테이지에서는 이벤트가 같으므로 골든 변화 없음 |
