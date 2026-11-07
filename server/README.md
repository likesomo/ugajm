# 일곱 걸음 플레이 기록 서버

게임이 보내는 익명 플레이 이벤트를 모으고, 플레이어가 **어느 스테이지 어느 칸에서 헤매고 그만두는지** 보여 주는 서버.
이벤트 스키마: [`docs/telemetry.md`](../docs/telemetry.md) (v1). 소유: 백엔드/분석 (`feat/server`).

- Node.js 26 기본 모듈만 사용 (`node:http`, `node:sqlite`). 외부 패키지 없음, `npm install` 불필요
- 같은 서버가 웹 프로토타입도 `/play/`로 제공한다 → 같은 출처라 설정 없이 플레이테스트 기록이 쌓인다

## 바로 플레이테스트

```bash
export PATH="/c/Program Files/nodejs:$PATH"
STATS_TOKEN=secret node server/src/index.mjs
#   play       http://localhost:8787/play/          ← 테스터에게 이 주소
#   dashboard  http://localhost:8787/dashboard#token=secret
```

다른 서버(`python -m http.server 8777` 등)로 프로토타입을 열어도 `localhost`면 자동으로 `http://localhost:8787`로 보낸다.
다른 주소는 `?telemetry=https://…`(브라우저에 기억됨), 끄기는 `?telemetry=off`(또는 `0`)나 게임 화면 아래 "기록 전송" 버튼.

가짜 데이터로 대시보드 보기:

```bash
node server/tools/fake-data.mjs --players 80 --db server/data/telemetry.sqlite   # 서버를 끈 상태에서, 또는
node server/tools/fake-data.mjs --players 80 --url http://localhost:8787          # 실행 중인 서버로 전송
```

가짜 플레이어는 실제 규칙(`core.js`)과 실제 `Tracker`(`telemetry.js`)로 플레이하므로 이벤트 모양이 진짜와 같다. 8·12스테이지는 일부러 더 어렵게 시뮬레이션한다.

## 설정 (환경 변수)

| 변수 | 기본값 | 설명 |
|---|---|---|
| `PORT` | `8787` | |
| `HOST` | `0.0.0.0` | |
| `DB_PATH` | `server/data/telemetry.sqlite` | SQLite 파일 (git 제외). WAL 모드 |
| `STATS_TOKEN` | 실행마다 랜덤(로그에 출력) | 통계 API·내보내기 토큰. 배포 때는 반드시 지정 |
| `ALLOWED_ORIGINS` | (없음) | 추가 허용 Origin, 쉼표 구분. `https://example.com` 또는 `*.example.com` |
| `TRUST_PROXY` | `0` | `1`이면 속도 제한에 `X-Forwarded-For` 사용 (프록시 뒤에서) |
| `PROTO_DIR` / `LEVELS_JS` | `prototypes/seven-steps` | `/play/`와 레벨 맵·최단 해 출처 |

기본 허용 Origin: `localhost`·`127.0.0.1`(모든 포트), `https://*.itch.io`, `https://*.itch.zone`, `https://*.hwcdn.net`(itch HTML5 CDN). Origin이 없는 요청(데스크톱 빌드, curl)은 받는다.

## API

| 경로 | 인증 | 설명 |
|---|---|---|
| `POST /v1/events` | 없음 | 배치 수신. `application/json` 또는 `text/plain`(sendBeacon). 최대 100건·64KB. `(sessionId, seq)` 중복은 무시 |
| `GET /healthz` | 없음 | `{"ok":true}` |
| `GET /play/` | 없음 | 웹 프로토타입 |
| `GET /dashboard` | 페이지는 공개, 데이터는 토큰 | 대시보드 (`#token=…`로 한 번 열면 브라우저에 기억) |
| `GET /api/levels` | 토큰 | 현재 빌드 레벨 맵, `levelKey`, 최단 이동 수 |
| `GET /api/stats/overview` | 토큰 | 합계, 빌드·클라이언트 목록 |
| `GET /api/stats/levels` | 토큰 | 스테이지별 퍼널·이탈·헤맴 지표 |
| `GET /api/stats/heatmap?level=N` | 토큰 | 칸별 idle·undo·몸 사용·막힘·방문·떠난 위치 |
| `GET /api/stats/timeline?limit=50` | 토큰 | 최근 플레이어의 세션별 시도 목록 |
| `GET /api/export/attempts.csv` · `attempts.json` | 토큰 | 시도당 한 줄 (`design/levels/telemetry-mapping.md` 4.1 열) |
| `GET /api/export/events.jsonl` · `events.json` | 토큰 | 원본 이벤트 + 배치 머리 + `attemptId` |
| `GET /api/export/levels.csv` · `levels.json` | 토큰 | 스테이지별 집계 |

토큰: `Authorization: Bearer <token>` 또는 `?token=<token>`. 모든 `/api/*`는 필터 `client`, `build`, `since`(=`from`), `to`를 받는다 (ms 또는 ISO 날짜, 서버 수신 시각 기준).

```bash
curl -H "Authorization: Bearer $STATS_TOKEN" "http://localhost:8787/api/export/attempts.csv?client=web-proto&since=2026-11-07" -o attempts.csv
```

### 지표 정의

- **시도**: `level_start`부터 `level_clear`(clear) / `level_leave`·다른 `level_start`(leave) / 세션이 30분 넘게 조용함(quit)까지. 아직 30분이 안 됐으면 open
- **클리어율**: 그 스테이지를 시작한 플레이어 중 클리어한 플레이어 비율
- **이탈률**: 플레이어의 마지막 시도가 이 스테이지이고 클리어하지 못한 비율 = 여기서 그만둔 플레이어
- **세션 종료율**: 이 스테이지를 미클리어로 둔 채 끝난 세션 비율 (나중에 돌아왔어도 센다)
- **activeMs** = `durationMs − idle 합`. **lastStuckToClear**: 마지막 막힘(30초 이상 idle, 또는 연속 undo 5회)이 끝난 뒤 클리어까지의 이동 수·시간
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
