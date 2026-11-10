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
- `lang`(선택, `session_start`·`level_start`): 그때의 UI 언어 `ko` \| `en`(형식 `[a-z]{2,3}`, 지역 붙이면 `pt-BR`). `docs/architecture.md` 13장. 플레이어가 설정에서 언어를 바꾸면 다음 `level_start`부터 새 값. 스테이지·챕터 이름(`levelName` 등)은 언어와 상관없이 ko 원문을 보낸다(13.5). 서버는 이벤트에 `lang`이 없으면 같은 세션의 직전 값을 이어 받아 저장하고, 대시보드 언어 필터는 그 값으로 거른다
- `chapter`: `levels.json`/`levels.js` 스테이지의 `chapter` 값(문자열 id, `docs/architecture.md` 12.8) 그대로. 없으면 보내지 않는다. 서버는 현재 빌드 레벨 데이터의 챕터를 우선 쓰고, 이 값은 지금 빌드에 없는 맵의 챕터를 알 때만 쓴다
- `body`: 이번 스테이지에서 몇 번째 몸인지 (1부터). `level.bodies - state.bodies + 1`
- `step`: 그 이동이 몇 번째 걸음인지 (1~7). `state.used + 1`. 단 진흙(`m`)에 들어가는 `move`는 2걸음이 들어 `state.used + 2`(막힌 이동은 항상 `used + 1`). 그래서 진흙을 지나면 `step`이 하나 건너뛰고, 진흙으로 7걸음이 되면 `seventh_step`이 나온다
- `tile` 종류: `floor` `exit` `pit` `ice` `cracked` `bridge` `plate` `door`(A~C와 석상 수 문 `Q`) `gate` `wall` `start`(현재 출발점 칸) `mud`(`m`) `lever`(`L`) `flip_up`(지금 올라와 막는 뒤집힘 벽) `flip_down`(지금 내려가 지나갈 수 있는 뒤집힘 벽). 뒤집힘 벽은 그 순간의 상태로 적는다. 새 기믹은 소문자 이름을 더한다(서버는 목록이 아니라 `[a-z_]` 20자 이내인지만 본다)

## 3. 이벤트

스테이지에 속한 이벤트(`level_start`~`level_leave`, `idle`)에는 모두 `level`이 있다.

| type | 언제 | 필드 |
|---|---|---|
| `session_start` | 게임을 켰을 때, 숨긴 탭이 다시 보일 때 | `firstTime`(이 기기에서 처음), `cleared`(저장된 클리어 스테이지 수), `resumed`(탭 복귀면 true), `lang`(선택) |
| `level_start` | 스테이지 진입·클리어 뒤 재시작 | `level`, `levelName`, `levelKey`(선택), `chapter`(선택, 챕터 문자열 id ≤40자, 스테이지에 챕터가 있을 때만), `attempt`(이 기기에서 이 스테이지를 시작한 횟수, 1부터), `via`: `boot` \| `url` \| `next` \| `select` \| `again`, `bodies`(몸 수) |
| `move` | 방향 입력 하나 (막혀도 보낸다) | `level`, `body`, `step`, `dir`: `up` \| `down` \| `left` \| `right`, `from`, `to`(멈춘 칸, 막혔으면 막은 칸), `result`: `moved` \| `blocked` \| `slid` \| `fell` \| `bridged` \| `door` \| `gate`, 선택: `blockedBy`(막은 tile 종류 또는 `statue` `edge`), `crumbled`(지나온 금 간 바닥이 무너졌으면 true) |
| `seventh_step` | 7번째 걸음이 어딘가에 닿았을 때 (`move` 바로 뒤) | `level`, `body`, `at`, `tile`, `effect`: `win` \| `bridge` \| `seal` \| `vanish` \| `choose` \| `statue`(마지막 몸이라 자동 석상), 선택: `flipped`(레버를 밟아 뒤집힘 벽이 바뀌었으면 true) |
| `keep` | KEEP 선택 | `level`, `body`, `at` |
| `switch` | SWITCH 선택 | `level`, `body`, `at` |
| `undo` | 되돌리기 (되돌릴 게 있을 때만) | `level`, `body`, `step`(되돌리기 전 쓴 걸음 수), `at`(되돌리기 전 위치), `status`(되돌리기 전 `play` \| `choose` \| `lose` \| `win`) |
| `restart` | 재시작 (시작 상태가 아닐 때만: 아무것도 안 했거나 되돌려 시작 상태로 온 뒤의 R은 보내지 않는다, architecture 6.5) | `level`, `body`, `step`, `at`, `moves`(이번 시도의 이동 수) |
| `hint` | 풀이 보기 | `level`, `body`, `step`, `at` |
| `body_lost` | 몸 하나를 썼을 때 | `level`, `body`(잃은 몸 번호), `at`, `reason`: `fell`(7번째가 아닌 걸음에 구덩이) \| `vanish`(출발점에서 7번째) \| `statue`(KEEP·자동 석상) \| `switch` \| `bridge` \| `seal`, `wasted`(`fell`·`vanish`면 true) |
| `level_fail` | 몸을 모두 잃음 | `level`, `at`, `moves`, `bodiesUsed` |
| `level_clear` | 출구 도착 | `level`, `durationMs`(이번 시도 시작부터), `moves`, `undos`, `restarts`, `hints`, `fails`, `bodiesUsed`, `minMoves`(선택, 솔버 최단 이동 수), `stars`(선택, 1~3, 아래 "별") |
| `level_leave` | 클리어하지 않고 다른 스테이지로 감 | `level`, `toLevel`, `durationMs`, `moves`, `undos`, `restarts`, `hints`, `fails`, `body`, `step`, `at` |
| `idle` | 입력 없이 30초 이상 지나고, 다음 입력·스테이지 이동·탭 숨김으로 그 멈춤이 끝났을 때 | `level`, `idleMs`(실제 멈춘 시간, 탭이 숨겨진 시간 제외), `body`, `step`, `at`, `status`, `bodiesLeft`, `statues`(석상 수) |
| `session_end` | 탭 숨김·닫힘 | `reason`: `hidden` \| `unload`, `level`(그때의 스테이지, 없으면 생략), `cleared`(그 스테이지 클리어 상태였는지), `body`, `step`, `at`, `sessionMs` |

규칙:
- 한 번의 입력에서 나오는 이벤트 순서: `idle`(있으면) → `move` → `seventh_step` → `body_lost` → `level_fail` 또는 `level_clear`
- 스테이지 "시도"는 `level_start`에서 시작해 `level_clear` / `level_leave` / `session_end`로 끝난다. 시도 중 `restart`는 같은 시도로 센다(재시작 수로 집계)
- 클리어한 뒤 다시 하기(R)는 `level_start` `via: "again"`으로 새 시도를 연다
- `level_start.via` 정의 (2026-11-10 결정, 앱 실행의 첫 진입인지와 상관없이 **플레이어가 어떻게 들어왔는지**로 정한다):
  - `select`: 지도·스테이지 목록에서 골라 들어감. **앱을 켜고 처음 들어가는 스테이지도 지도·목록에서 골랐으면 `select`**
  - `boot`: 플레이어가 고르지 않고 앱이 바로 스테이지로 들어감 — 실행 인자로 바로 게임 진입, 저장된 진행으로 자동 이어가기, 에디터 `startLevel`, 웹 프로토타입 첫 화면
  - `url`: 주소·실행 인자가 스테이지 번호를 지정함(WebGL·웹 `?level=N`, 데스크톱 `-level N`). 기존 값 그대로
  - `next`: 결과 화면·HUD의 "다음", `again`: 클리어 뒤 다시 하기
  - Unity `EntryTracker`(씬 플로우/게임플레이 UI)는 첫 진입이어도 지도·목록 선택이면 `select`. 골든 `manual-first-entry-select` 트레이스(트레이스 최상위 `via` = 첫 `level_start`의 via)
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

## 별 (`level_clear.stars`)

`design/levels/stars.md` B안. 한 시도(`level_start`부터 클리어까지, 재시작 포함) 기준:
- ★1: 클리어
- ★2: 풀이 보기(`hints`) 0이고 `bodiesUsed == minBodies`(그 맵을 깰 수 있는 가장 적은 몸 수, 솔버 `minBodies` = `levels.json` `expected.minBodies`)
- ★3: ★2이고 `undos` 0, `restarts` 0 (`undos`는 실제로 되돌린 Undo, `restarts`는 시작 상태가 아닐 때의 R만 센다 — 위 `undo`·`restart` 이벤트 수와 같다)

판정 함수는 코어 `core.js` `stars()`(C# `Stars.Of`, 골든 `Tests/Golden/stars.golden.json`). `telemetry.js`는 이것을 `starsFor`로 다시 내보낸다. 최소 몸 수를 모르면 보내지 않는다. 서버는 `stars`가 없는 옛 기록도 `level_clear`의 `bodiesUsed`·`hints`·`undos`·`restarts`와 현재 맵(같은 `levelKey`)의 최소 몸 수로 다시 계산한다.

## 6. 로컬 플레이테스트 한 페이지 (서버 + 대시보드)

이 PC에서 서버를 띄우고, 테스터가 플레이하는 동안 대시보드로 보고, 끝나면 레벨 디자인용 파일을 내보낸다. Git Bash, 저장소 루트 기준. 자세한 설명과 옵션은 [`server/README.md`](../server/README.md) "플레이테스트 날 실행 절차".

**준비** (전날): Node 26(`export PATH="/c/Program Files/nodejs:$PATH"`, `npm install` 없음). Unity WebGL은 `unity/Tools/unity.sh build-webgl` → `unity/Tools/unity.sh smoke-webgl` 통과한 `Builds/WebGL/`. 서버 확인은 `node --test server/test/`.

**1. 서버 켜기** (테스트 내내 이 터미널을 열어 둔다)
```bash
export PATH="/c/Program Files/nodejs:$PATH"
export STATS_TOKEN=$(node -e "console.log(require('crypto').randomBytes(12).toString('base64url'))"); echo "token: $STATS_TOKEN"
DB_PATH=server/data/playtest-$(date +%F).sqlite node server/src/index.mjs
```

**2. 주소**
| 용도 | 주소 |
|---|---|
| Unity WebGL (테스터) | `http://localhost:8787/webgl/` (전송 주소 `?telemetry=`를 서버가 자동으로 붙인다. 스테이지 지정 `?level=29`, 영어 `?lang=en`) |
| 웹 프로토타입 (테스터) | `http://localhost:8787/play/` |
| 대시보드 (진행자) | `http://localhost:8787/dashboard#token=<토큰>` |
| 같은 와이파이의 휴대폰·PC (테스터) | `http://<이 PC의 IP>:8787/webgl/`. 서버가 켜질 때 `[server] LAN http://192.168.….:8787/webgl/` 줄로 알려 준다(모든 주소에서 받음). `?telemetry=`는 그 IP 주소로 붙고, 같은 주소로 오는 전송은 허용 목록 없이 받는다 |

**방화벽** (같은 와이파이 기기를 받을 때만): 서버를 처음 켤 때 Windows가 "Node.js 액세스 허용"을 물으면 **허용**. 현장 와이파이는 보통 "공용 네트워크"로 잡히므로(`Get-NetConnectionProfile`의 `NetworkCategory`) 그 종류에 체크한다. 창을 놓쳤거나 휴대폰에서 안 열리면 관리자 PowerShell에서 `New-NetFirewallRule -DisplayName "Seven Steps 8787" -Direction Inbound -Protocol TCP -LocalPort 8787 -Action Allow`, 끝나면 `Remove-NetFirewallRule -DisplayName "Seven Steps 8787"`. 공유기 밖(인터넷)으로는 열리지 않는다. 손님용 와이파이는 기기끼리 막혀 있을 수 있다(그때는 진행자 PC로만).

**3. 시작 전 확인** (2분): `curl http://localhost:8787/healthz` → `{"ok":true}`. 휴대폰을 쓰면 휴대폰에서 LAN 주소로 한 번 열어 본다. 테스터 주소로 1스테이지를 깨고 5~10초 뒤 대시보드 "새로고침" → 빌드 목록에 새 빌드(`0.1.0+<커밋>`)와 `unity-webgl`이 보이면 된다. 확인 기록은 "기간" 필터로 빼거나, 서버를 끄고 그날 DB 파일을 지운 뒤 다시 켠다.

**4. 대시보드에서 보는 것** (자동 갱신 없음, "새로고침")
- 필터: 클라이언트, 빌드, 언어(`ko`/`en`, `lang`), 기간, 챕터(행성 7개)
- 챕터: 행성별 시작·끝 스테이지 도달·전부 클리어·그만둠. 행성 이름은 `design/levels/chapters.json`
- 스테이지 퍼널·이탈률(50스테이지, 행성별 묶음), 표: 클리어 시간 중앙·상위 25%, Undo·재시작·힌트, 몸 손실, ★1~★3 분포와 ★3 비율
- 막힘 히트맵(스테이지 선택: 오래 멈춘 칸·Undo·몸 사용), 세션 타임라인

**5. 끝난 뒤**: 서버 `Ctrl+C`. DB 파일은 보관(git 제외). 레벨 디자인용 내보내기(대시보드 위쪽 링크도 같음, 현재 필터 적용):
```bash
T=<토큰>; B=http://localhost:8787/api/export; D=$(date +%F)   # 오늘(한국 날짜)
for f in attempts.csv levels.csv events.jsonl; do curl -sH "Authorization: Bearer $T" "$B/$f?since=$D&tz=Asia/Seoul" -o "$f"; done
```
- `attempts.csv`/`.json`: 시도 한 줄. 열은 `design/levels/telemetry-mapping.md` 4.1 + 끝에 `chapter, stageNow, stageMatch, stars, starsSource, lang, planet`
- `levels.csv`/`.json`: 스테이지 집계(`chapter`, `planet`, 퍼널, 헤맴, `stars1~3`, `star3Rate`, `minBodies`)
- `events.jsonl`/`.json`: 원본 이벤트 + `attemptId`
- 모든 내보내기는 `client`, `build`, `lang`, `since`, `to` 필터를 받는다. 시각(서버 수신 기준): `2026-11-10`·`2026-11-10T09:00`(오프셋 없음)은 `tz`(IANA `Asia/Seoul` 또는 `+09:00`, 없으면 **서버 PC의 시간대**) 기준 그 시각, `2026-11-10T09:00+09:00`·`…Z`는 그대로, 숫자는 epoch ms. `to`는 그 시각 전까지(`since=2026-11-10&to=2026-11-11` = 11월 10일 하루). 잘못된 값은 400. CSV는 UTF-8(BOM 없음). 엑셀에서 열 때는 "데이터 > 텍스트/CSV"로 UTF-8 지정

**가짜 데이터로 미리 보기**: 서버를 끈 상태에서 `node server/tools/fake-data.mjs --players 120 --db server/data/fake.sqlite` 후 `DB_PATH=server/data/fake.sqlite`로 켠다(7개 행성 모두에 플레이어가 있다. `lang`은 4명 중 1명 `en`).

**원격 테스터 (사용자 승인 전, 준비만)**: 외부 공개라 사용자 승인 없이 하지 않는다. 승인되면 서버를 `TRUST_PROXY=1`로 켜고 다른 터미널에서 `cloudflared tunnel --url http://localhost:8787` → 출력된 `https://….trycloudflare.com/webgl/`을 테스터에게 준다(실행마다 주소가 바뀜). `cloudflared`는 아직 설치하지 않았다. itch.io 빌드에서 받으려면 고정 HTTPS 주소가 필요하다 → `server/README.md` "배포 선택지".

## 변경 기록

| 날짜 | 버전 | 내용 |
|---|---|---|
| 2026-11-07 | v1 | 최초 확정 |
| 2026-11-10 | v1 | 이벤트 변화 없음. 6장: 같은 와이파이 기기 주소·방화벽. 서버: 같은 주소(LAN IP로 연 `/webgl/`)에서 오는 전송을 허용 목록과 관계없이 받음(전에는 403), 시작할 때 LAN 주소 출력 |
| 2026-11-10 | v1 | `level_start.via` 정의 확정(3장 아래): 지도·목록에서 고른 진입은 앱 첫 진입이어도 `select`, `boot`은 고르지 않은 바로 진입(실행 인자로 바로 게임, 자동 이어가기)만, `url`은 그대로. 이벤트 형식은 같음. 골든에 `manual-first-entry-select` 추가(213 트레이스) — Unity `TelemetryGoldenTests`는 첫 `LevelStart`에 트레이스의 `via`를 써야 통과. 서버: `since`/`to` 날짜를 서버 시간대(또는 `tz`)로 해석, epoch ms 필터 버그 수정(대시보드 "기간"이 걸러지지 않던 문제), 몸 사용 사유 표시 석상 → 선돌 |
| 2026-11-10 | v1 | 이벤트 변화 없음. 6장 로컬 플레이테스트 절차 추가. 서버 내보내기: `attempts`에 `lang`·`planet` 열(끝에 추가), `levels`·`chapters`에 `planet`(design/levels/chapters.json). 대시보드 챕터 이름을 행성 이름으로 |
| 2026-11-07 | v1 | `level_start.levelKey`(선택) 추가 — 레벨 디자인 요청. 선택 필드라 버전은 그대로 |
| 2026-11-10 | v1 | 재시작 셈을 게임 규칙(architecture 6.5)에 맞춤: 시작 상태에서 누른 R은 `restart` 이벤트도 `restarts` 셈도 없음(`level_clear.restarts`, ★3에 영향). Undo는 원래 실제로 되돌린 것만 셈. 골든 재생성(재시작 이벤트 32건 빠짐). Unity `TelemetryTracker`도 같은 조건(직전 상태의 `Prev`가 없으면 무시) |
| 2026-11-09 | v1 | `session_start.lang`·`level_start.lang`(선택) 추가 — 다국어(architecture 13장). 웹 `Tracker({ lang })`·`setLang`, `initTelemetry({ lang })`(기본 `ko`). Unity는 `Localizer` 현재 언어를 `level_start` 끝(`bodies` 다음)과 `session_start` 끝에. 골든에는 넣지 않음(변화 없음) |
| 2026-11-09 | v1 | `level_clear.stars`(선택, 1~3) 추가 — 3별 B안("별" 절). 골든 재생성(클리어 이벤트마다 `stars`). Unity `TelemetryTracker`도 같은 판정으로 넣어야 골든 테스트가 통과한다 |
| 2026-11-09 | v1 | 진흙·레버 반영(코어 6ae58e2 뒤): 진흙에 들어가는 `move.step`은 `used + 2`, 진흙으로 7걸음이 되면 `seventh_step`. `tile`에 `mud` `lever` `flip_up` `flip_down`, `Q` 문은 `door`(그래서 Q 문을 지나는 `move.result`도 `door`). `seventh_step.flipped`(선택). 서버는 칸 이름을 형식으로 검사. 골든 재생성(125개 중 44개 트레이스 변경). Unity `TelemetryTracker`도 같은 규칙으로 맞춰야 골든 테스트가 통과한다 |
| 2026-11-09 | v1 | `level_start.chapter`(선택) 추가 — 50스테이지·챕터 대비. 선택 필드라 버전은 그대로. 웹 `Tracker.levelStart(state, level, name, via, chapter)`. Unity `TelemetryTracker`는 `LevelCatalog.ChapterOf`가 생기면 같은 자리(`levelKey` 다음)에 넣는다. 챕터 없는 지금 스테이지에서는 이벤트가 같으므로 골든 변화 없음 |
