# 일곱 걸음 플레이 기록 서버

게임이 보내는 익명 플레이 이벤트를 모으고, 플레이어가 **어느 스테이지 어느 칸에서 헤매고 그만두는지** 보여 주는 서버.
이벤트 스키마: [`docs/telemetry.md`](../docs/telemetry.md) (v1). 소유: 백엔드/분석 (`feat/server`).

- Node.js 26 기본 모듈만 사용 (`node:http`, `node:sqlite`). 외부 패키지 없음, `npm install` 불필요
- 같은 서버가 웹 프로토타입(`/play/`)과 Unity WebGL 빌드(`/webgl/`)도 제공한다 → 서버 하나, 주소 하나로 플레이테스트를 돌린다

## 바로 플레이테스트

```bash
export PATH="/c/Program Files/nodejs:$PATH"
STATS_TOKEN=secret node server/src/index.mjs
#   play       http://localhost:8787/play/          ← 웹 프로토타입
#   webgl      http://localhost:8787/webgl/         ← Unity WebGL (Builds/WebGL)
#   dashboard  http://localhost:8787/dashboard#token=secret
```

`/webgl/`은 주소에 `telemetry`가 없으면 `?telemetry=<이 서버 주소>`를 붙여 다시 보낸다 (Unity 빌드의 `AppConfig.telemetryEndpoint`가 비어 있어서 주소를 알려 줘야 보낸다). `level`·`replay` 같은 다른 파라미터는 유지된다.

다른 서버(`python -m http.server 8777` 등)로 프로토타입을 열어도 `localhost`면 자동으로 `http://localhost:8787`로 보낸다.
다른 주소는 `?telemetry=https://…`(브라우저에 기억됨), 끄기는 `?telemetry=off`(또는 `0`)나 게임 화면 아래 "기록 전송" 버튼.

가짜 데이터로 대시보드 보기:

```bash
node server/tools/fake-data.mjs --players 80 --db server/data/telemetry.sqlite   # 서버를 끈 상태에서, 또는
node server/tools/fake-data.mjs --players 80 --url http://localhost:8787          # 실행 중인 서버로 전송
```

가짜 플레이어는 실제 규칙(`core.js`)과 실제 `Tracker`(`telemetry.js`)로 플레이하므로 이벤트 모양이 진짜와 같다. 8·12스테이지는 일부러 더 어렵게 시뮬레이션한다.

## 플레이테스트 날 실행 절차

Git Bash, 저장소 루트에서. 확인 기록: 2026-11-08 Unity WebGL 빌드(`0.1.0+ef67c4d`)와 웹 프로토타입이 이 절차로 이벤트를 보내 대시보드에 나오는 것을 확인했다 (가짜 데이터 아님).

**1. 빌드 준비** (전날)
- Unity WebGL: `unity/Tools/unity.sh build-webgl` → `unity/Tools/unity.sh smoke-webgl` (보드가 보이는지). 결과는 `Builds/WebGL/`. 다른 작업 공간의 빌드를 쓰려면 서버를 띄울 때 `WEBGL_DIR=<그 폴더>`
- 빌드 번호는 `Application.version`(예: `0.1.0+ef67c4d`)으로 이벤트에 들어간다. 대시보드 "빌드" 필터로 나눠 본다. `-dirty`가 붙으면 커밋 안 된 변경이 있는 빌드다

**2. 서버 시작** (테스트 내내 켜 둔다, 이 터미널을 닫지 않는다)

```bash
export PATH="/c/Program Files/nodejs:$PATH"
export STATS_TOKEN=$(node -e "console.log(require('crypto').randomBytes(12).toString('base64url'))")
echo "token: $STATS_TOKEN"                       # 적어 둔다. 대시보드·내보내기에 필요
DB_PATH=server/data/playtest-$(date +%F).sqlite node server/src/index.mjs
```

- 토큰은 실행할 때마다 정한다. 지정하지 않으면 실행마다 랜덤 토큰이 로그에 찍힌다(재시작하면 바뀜)
- `DB_PATH`를 날짜별로 두면 그날 데이터만 따로 모인다. 한 파일에 계속 쌓아도 대시보드 "기간" 필터·API `since`로 나눌 수 있다
- 서버를 다시 켜도 DB 파일의 데이터는 남는다. 끄는 동안 게임이 보낸 이벤트는 게임 쪽 큐에 남았다가 다시 보낸다 (웹: `localStorage`, Unity: PlayerPrefs, 최대 1000건)

**3. 테스터에게 줄 주소**

| 상황 | 웹 프로토타입 | Unity WebGL |
|---|---|---|
| 이 PC에서 직접 | `http://localhost:8787/play/` | `http://localhost:8787/webgl/` |
| 같은 와이파이의 다른 기기 | `http://<이 PC IP>:8787/play/` | `http://<이 PC IP>:8787/webgl/` (아래 주의) |
| 원격 테스터 (터널, **사용자 승인 후**) | `https://<터널 주소>/play/` | `https://<터널 주소>/webgl/` |

- `/webgl/`이 `?telemetry=`를 자동으로 붙이므로 주소를 그대로 주면 된다. 특정 스테이지부터: `/webgl/?level=8`, 웹은 `/play/?level=8`
- 다른 서버에서 연 빌드는 직접 붙인다: WebGL `?telemetry=http://localhost:8787`, 데스크톱 `"Seven Steps.exe" -telemetry http://localhost:8787`, 웹 프로토타입 `?telemetry=http://localhost:8787`(브라우저에 기억됨)
- 끄기: `?telemetry=off` (Unity는 설정 화면의 동의 항목, 웹은 화면 아래 "기록 전송")
- 같은 와이파이: 서버는 모든 주소(`HOST=0.0.0.0`)에서 받는다. Windows 방화벽이 8787 포트를 물어보면 "개인 네트워크"만 허용. 다른 기기에서 Unity WebGL은 아직 확인하지 않았다(휴대폰은 조작이 맞지 않을 수 있음)
- 원격 테스터용 임시 터널(계정 없이, 실행마다 주소가 바뀜): 서버를 `TRUST_PROXY=1`로 띄우고 다른 터미널에서 `cloudflared tunnel --url http://localhost:8787` → 출력된 `https://….trycloudflare.com`. `TRUST_PROXY=1`이어야 `/webgl/`이 `https://` 주소를 붙인다. **외부 공개라 사용자 승인 후에만** 한다 (아래 "배포 선택지")

**4. 시작 전 확인** (2분)
1. `curl http://localhost:8787/healthz` → `{"ok":true}`
2. 대시보드 `http://localhost:8787/dashboard#token=<토큰>`를 연다
3. 테스터에게 줄 주소로 직접 들어가 1스테이지를 깨고, 5~10초 뒤 대시보드 "새로고침" → 이벤트 수가 늘고 "빌드" 목록에 새 빌드·클라이언트(`unity-webgl`/`web-proto`)가 보이면 된다
4. 확인용 기록을 빼고 보려면 대시보드 "기간"을 테스트 시작 시각 뒤로 두거나, 확인 뒤 서버를 끄고 날짜별 DB 파일을 지운 다음 다시 켠다

**5. 테스트 중**
- 대시보드는 자동 갱신되지 않는다. "새로고침"을 누른다
- 둘 다 5초 또는 20건마다 보낸다. 탭을 닫거나 숨기면 `sendBeacon`으로 남은 것을 보낸다

**6. 끝난 뒤**
- 서버를 `Ctrl+C`로 끈다 (DB가 닫힌다). DB 파일(`server/data/playtest-<날짜>.sqlite`)을 보관한다. git에는 넣지 않는다
- 레벨 디자이너용 내보내기:
  ```bash
  T=<토큰>; B=http://localhost:8787/api/export
  curl -H "Authorization: Bearer $T" "$B/attempts.csv?since=2026-11-08" -o attempts.csv
  curl -H "Authorization: Bearer $T" "$B/levels.csv?since=2026-11-08" -o levels.csv
  curl -H "Authorization: Bearer $T" "$B/events.jsonl?since=2026-11-08" -o events.jsonl
  ```
  대시보드 위쪽 "내보내기" 링크도 같은 파일을 준다 (현재 필터 적용)

**itch.io에 올린 빌드**는 iframe 안에서 열리므로 테스터가 주소에 `?telemetry=`를 붙일 수 없다. itch 빌드에서 받으려면 고정된 HTTPS 서버 주소를 `unity/Assets/_Project/Data/AppConfig.asset`의 `telemetryEndpoint`에 넣고 빌드해야 한다 → 실행마다 바뀌는 임시 터널로는 안 되고, 고정 주소(이름 있는 Cloudflare 터널 또는 아래 B~E)가 필요하다. 사용자 결정 사항.

## 설정 (환경 변수)

| 변수 | 기본값 | 설명 |
|---|---|---|
| `PORT` | `8787` | |
| `HOST` | `0.0.0.0` | |
| `DB_PATH` | `server/data/telemetry.sqlite` | SQLite 파일 (git 제외). WAL 모드 |
| `STATS_TOKEN` | 실행마다 랜덤(로그에 출력) | 통계 API·내보내기 토큰. 배포 때는 반드시 지정 |
| `ALLOWED_ORIGINS` | (없음) | 추가 허용 Origin, 쉼표 구분. `https://example.com` 또는 `*.example.com` |
| `TRUST_PROXY` | `0` | `1`이면 속도 제한에 `X-Forwarded-For`, `/webgl/` 주소에 `X-Forwarded-Proto`·`X-Forwarded-Host` 사용 (터널·프록시 뒤에서) |
| `PROTO_DIR` / `LEVELS_JS` | `prototypes/seven-steps` | `/play/`와 레벨 맵·최단 해 출처 |
| `WEBGL_DIR` | `Builds/WebGL` | `/webgl/`로 제공할 Unity WebGL 빌드 폴더 |

기본 허용 Origin: `localhost`·`127.0.0.1`(모든 포트), `https://*.itch.io`, `https://*.itch.zone`, `https://*.hwcdn.net`(itch HTML5 CDN). Origin이 없는 요청(데스크톱 빌드, curl)은 받는다.

## API

| 경로 | 인증 | 설명 |
|---|---|---|
| `POST /v1/events` | 없음 | 배치 수신. `application/json` 또는 `text/plain`(sendBeacon). 최대 100건·64KB. `(sessionId, seq)` 중복은 무시 |
| `GET /healthz` | 없음 | `{"ok":true}` |
| `GET /play/` | 없음 | 웹 프로토타입 |
| `GET /webgl/` | 없음 | Unity WebGL 빌드 (`WEBGL_DIR`). `telemetry` 파라미터가 없으면 이 서버 주소를 붙여 302. 미리 압축된 파일(`.unityweb`은 실제 gzip일 때, `.gz`)은 브라우저가 gzip을 받으면 `Content-Encoding: gzip`, Content-Type은 안쪽 확장자(`.wasm` → `application/wasm` 스트리밍 컴파일, `.data` → octet-stream, `.js`). 아니면 그대로 보내고 Unity JS 폴백이 푼다 |
| `GET /dashboard` | 페이지는 공개, 데이터는 토큰 | 대시보드 (`#token=…`로 한 번 열면 브라우저에 기억) |
| `GET /api/levels` | 토큰 | 현재 빌드 레벨 맵, `levelKey`, 최단 이동 수 |
| `GET /api/stats/overview` | 토큰 | 합계, 빌드·클라이언트 목록, 언어별 세션 수(`langs`) |
| `GET /api/stats/levels` | 토큰 | 스테이지별 퍼널·이탈·헤맴 지표 |
| `GET /api/stats/heatmap?level=N` | 토큰 | 현재 빌드 스테이지 N의 칸별 idle·undo·몸 사용·막힘·방문·떠난 위치. 맵이 지금과 같은 시도만(`otherMapAttempts`에 뺀 수) |
| `GET /api/stats/chapters` | 토큰 | 챕터별 시작·끝 스테이지 도달·전부 클리어·그만둔 플레이어 (현재 빌드 스테이지만) |
| `GET /api/stats/timeline?limit=50` | 토큰 | 최근 플레이어의 세션별 시도 목록 |
| `GET /api/export/attempts.csv` · `attempts.json` | 토큰 | 시도당 한 줄 (`design/levels/telemetry-mapping.md` 4.1 열) |
| `GET /api/export/events.jsonl` · `events.json` | 토큰 | 원본 이벤트 + 배치 머리 + `attemptId` |
| `GET /api/export/levels.csv` · `levels.json` | 토큰 | 스테이지별 집계 |

토큰: `Authorization: Bearer <token>` 또는 `?token=<token>`. 모든 `/api/*`는 필터 `client`, `build`, `lang`(UI 언어, 이벤트에 없으면 세션의 직전 값; 언어를 보내지 않은 옛 기록은 언어 필터에서 빠진다), `since`(=`from`), `to`를 받는다 (ms 또는 ISO 날짜, 서버 수신 시각 기준).

```bash
curl -H "Authorization: Bearer $STATS_TOKEN" "http://localhost:8787/api/export/attempts.csv?client=web-proto&since=2026-11-07" -o attempts.csv
```

### 지표 정의

- **스테이지 맞추기**: 스테이지는 현재 빌드(`levels.js`) 순서로 보인다. 예전 빌드의 시도는 `levelKey`(맵 해시) → 스테이지 이름 → (둘 다 없으면) 번호 순으로 지금 스테이지를 찾는다. 그래서 스테이지를 끼워 넣거나 순서를 바꿔도 섞이지 않는다. 이름은 같고 맵만 고친 경우는 같은 스테이지로 세고(히트맵은 지금 맵만), 지금 빌드에 없는 맵은 "이전 맵"으로 따로 센다. `attempts.csv`의 `stageNow`(지금 번호, 이전 맵은 빈칸)·`stageMatch`(`key`/`name`/`number`/`old`)·`chapter` 열
- **챕터**: 현재 빌드 레벨 데이터의 `chapter` 기준(없으면 이벤트의 `chapter`). 레벨에 챕터가 하나라도 있으면 대시보드에 챕터 칸·필터·묶음이 나온다. 챕터의 "그만둠" = 플레이어의 마지막 시도가 이 챕터의 미클리어 스테이지. 행성 이름(`planet`)은 `design/levels/chapters.json`(`CHAPTERS_JSON`로 바꿈)에서 읽어 대시보드 챕터 이름, `/api/levels`·`/api/stats/levels`·`/api/stats/chapters`, `attempts`·`levels` 내보내기에 넣는다
- **언어**: `attempts`의 `lang` 열 = 그 시도의 `level_start` 때 UI 언어(없으면 세션의 직전 값)

- **시도**: `level_start`부터 `level_clear`(clear) / `level_leave`·다른 `level_start`(leave) / 세션이 30분 넘게 조용함(quit)까지. 아직 30분이 안 됐으면 open
- **클리어율**: 그 스테이지를 시작한 플레이어 중 클리어한 플레이어 비율
- **이탈률**: 플레이어의 마지막 시도가 이 스테이지이고 클리어하지 못한 비율 = 여기서 그만둔 플레이어
- **세션 종료율**: 이 스테이지를 미클리어로 둔 채 끝난 세션 비율 (나중에 돌아왔어도 센다)
- **activeMs** = `durationMs − idle 합`. **lastStuckToClear**: 마지막 막힘(30초 이상 idle, 또는 연속 undo 5회)이 끝난 뒤 클리어까지의 이동 수·시간
- **별**: 클리어 시도마다 `level_clear.stars`, 없으면 서버가 B안으로 계산(`docs/telemetry.md` "별"). 대시보드는 플레이어마다 그 스테이지의 최고 별로 ★1·★2·★3 분포와 ★3 비율을 보인다. 맵이 바뀐 예전 기록은 최소 몸 수를 몰라 "별을 모르는 클리어"로 뺀다. `attempts.csv`의 `stars`, `starsSource`(`client`/`server`/빈칸), `levels.csv`의 `stars1~3`, `star3Rate`, `minBodies`
- **몸 낭비**: `body_lost`의 `fell`(7번째가 아닌 걸음에 구덩이) + `vanish`(출발점에서 7번째)

## 테스트

```bash
node --test server/test/
```

검증·수집(CORS, 크기, 속도 제한, 중복, sendBeacon), 집계(시도·이탈·히트맵·내보내기), 가짜 플레이 30명 HTTP 왕복, `Tracker` 이벤트, 골든 파일 최신 여부.

## Unity 일치 검사용 골든

```bash
node server/tools/telemetry-golden.mjs      # → server/fixtures/telemetry.golden.json
```

코어의 규칙 트레이스(`origin/feat/core`의 `unity/Assets/_Project/Tests/Golden/traces.golden.json`)와 수동 시나리오를 `Tracker`에 통과시킨 결과. Unity의 텔레메트리 기록기는 같은 명령열에 같은 이벤트를 내야 한다. 형식은 파일 안의 `notes` 참고. `telemetry.js`를 고치면 다시 만들어 커밋한다 (테스트가 확인).

## 개인정보

IP·User-Agent를 저장하지 않는다 (속도 제한은 프로세스마다 바뀌는 salt로 해시한 값을 메모리에만 둔다). 이름·이메일 등 식별 정보와 자유 입력 텍스트를 받지 않는다. 플레이어는 게임에서 끌 수 있다.

## 배포 선택지 (사용자 결정 필요)

itch.io에 올린 WebGL이 보내려면 서버가 **HTTPS로 인터넷에 열려** 있어야 한다. 이 서버는 SQLite 파일 하나를 쓰므로 **디스크가 유지되는 곳**이 필요하다 (재시작 때 파일이 지워지는 무료 플랜은 데이터가 사라진다). 아래는 2026-11 기준으로 알고 있는 정보이며, 가격·무료 한도는 선택 전에 각 사이트에서 다시 확인해야 한다.

| 후보 | 비용 | 데이터 유지 | 설정 난이도 | 맞는 상황 |
|---|---|---|---|---|
| **A. Cloudflare Tunnel (이 PC에서 실행)** | 무료 (계정 필요, 임시 주소는 계정 없이 `trycloudflare.com`) | PC 디스크 그대로 | 낮음: `cloudflared tunnel --url http://localhost:8787` | 잼 기간 플레이테스트. PC가 켜져 있을 때만 수집 |
| **B. Fly.io** (VM + 볼륨) | 소액 과금 가능 (신용카드 등록 필요) | 볼륨 1GB로 유지 | 중간: Dockerfile + `fly launch` + 볼륨 | 상시 운영, 잼 이후에도 수집 |
| **C. Render** (Web Service) | 무료 플랜은 디스크 없음·15분 무입력 시 잠듦 → 유료(디스크) 필요 | 유료 플랜 디스크 | 낮음: GitHub 연결 | 유료를 감수할 때 |
| **D. Railway** | 체험 크레딧 후 과금 | 볼륨 | 낮음 | B와 비슷, 더 간단한 UI |
| **E. 작은 VPS** (Oracle Cloud Free, 기존 서버 등) | 무료~월 몇 달러 | 그대로 | 높음: HTTPS(Caddy 등)·방화벽 직접 | 이미 쓰는 서버가 있을 때 |

**추천**: 잼 기간에는 **A(Cloudflare Tunnel)**. 코드 변경 없이 지금 바로 되고 비용이 없다. 플레이테스트 세션 동안 이 PC에서 서버와 터널을 켜 두고, 터널 주소를 Unity 빌드의 `AppConfig.telemetryEndpoint`(또는 WebGL URL `?telemetry=`)에 넣는다. 잼 이후 상시 수집이 필요하면 **B(Fly.io)**로 옮긴다 (SQLite 파일을 그대로 복사).
임시 `trycloudflare.com` 주소는 실행할 때마다 바뀌므로 빌드에 고정하려면 Cloudflare 계정의 고정 터널(이름 있는 터널 + 도메인)이 필요하다.

배포할 때 공통: `STATS_TOKEN` 지정, `TRUST_PROXY=1`(터널·프록시 뒤), 배포 도메인이 itch 밖이면 `ALLOWED_ORIGINS`에 추가.
