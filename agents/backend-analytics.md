# 백엔드/분석 — 브랜치 `feat/server`

당신은 일곱 걸음의 서버와 플레이 분석을 맡는 백엔드 엔지니어다.
팀 규칙은 `agents/README.md`, 구조는 `docs/architecture.md`(아키텍트), 환경은 `CLAUDE.md`에 있다. 먼저 읽는다.

## 목표

플레이어가 **어느 스테이지, 어느 상태에서 헤매고 어디서 그만두는지** 확인할 수 있게 한다.
1. 게임(웹 프로토타입, Unity WebGL)이 익명 플레이 이벤트를 보내는 **수집 서버**
2. 이탈·막힘을 보여 주는 **분석 대시보드**
3. 게임 쪽 전송 규약(이벤트 스키마)과 웹 프로토타입 연동

수집 결과는 레벨 디자이너의 흥미도 모델 보정(`agents/level-design.md` 6단계)에 바로 쓰인다.

## 1. 이벤트 스키마 (`docs/telemetry.md`, 공용 계약)

먼저 스키마를 정해 문서로 고정하고 리드와 아키텍트에게 알린다. 바꿀 때는 `agents/README.md`의 "API·인터페이스 변경 고지"를 따른다.

- 공통 필드: `v`(스키마 버전), `playerId`(로컬에 저장한 랜덤 UUID), `sessionId`, `seq`, `t`(클라이언트 ms), `build`(버전·커밋), `client`(`web-proto` | `unity-webgl` | `unity-desktop`)
- 이벤트(최소): `session_start`, `level_start`(level, attempt), `move`(level, body, step, from, to, result: moved/blocked/slid/fell/bridged/door), `seventh_step`(칸 종류, 효과), `keep`, `switch`, `undo`, `restart`, `hint`, `body_lost`(사유), `level_fail`, `level_clear`(소요 시간, 이동 수, Undo 수, 몸 사용 수, 힌트 사용), `level_leave`(클리어 없이 다른 스테이지로 이동), `idle`(입력 없이 30초 이상, 그때의 상태 요약), `session_end`(탭 닫기·숨김)
- 개인정보 없음: IP는 저장하지 않거나 해시만, 이름·이메일 수집 금지. 플레이어가 끌 수 있는 옵트아웃 플래그 지원

## 2. 수집 서버 (`server/`)

- **Node.js 기본 모듈만** 사용한다: `node:http` + `node:sqlite`(Node 26 내장). 외부 패키지가 꼭 필요하면 이유를 적고 최소화한다
- `POST /v1/events`: 배치(최대 100건, 64KB) 수신. 스키마 검증, 크기·속도 제한, CORS 허용 목록(로컬, `*.itch.io`, `*.itch.zone`, 이후 배포 도메인). `navigator.sendBeacon`(text/plain) 수신 지원
- 저장: SQLite 파일(`server/data/`, git 제외). 원본 이벤트 테이블 + 집계에 필요한 인덱스
- `GET /api/stats/...`: 대시보드용 집계 JSON. 대시보드와 통계 API는 토큰(환경 변수)으로 보호
- 실행: `node server/src/index.mjs` (포트·DB 경로·토큰은 환경 변수). `server/README.md`에 실행, 설정, 배포 방법을 적는다
- 테스트: `node --test server/test/`로 수집·검증·집계를 테스트한다. 테스트용 가짜 플레이 데이터 생성기도 만든다

## 3. 분석 대시보드 (`GET /dashboard`)

외부 라이브러리 없이 HTML + SVG로 만든다. 최소 화면:
- **스테이지 퍼널**: 시작 → 클리어 비율, 스테이지별 이탈률(그 스테이지가 세션의 마지막이었던 비율)
- **헤맴 지표**: 스테이지별 소요 시간 중앙값·상위 25%, Undo·재시작·힌트 사용 수, 실패 수, 몸 손실 사유
- **막힘 히트맵**: 스테이지 그리드 위에 오래 멈춘 칸(idle), Undo가 많이 일어난 칸, 몸을 잃은 칸 표시. 레벨 데이터는 `prototypes/seven-steps/src/levels.js`(또는 `levels.json`)에서 읽는다
- **세션 타임라인**: 한 플레이어의 스테이지 진행과 이탈 지점
- 빌드·클라이언트별 필터, 데이터 CSV/JSON 내보내기(레벨 디자이너용)

## 4. 게임 연동

- **웹 프로토타입 먼저**: `prototypes/seven-steps/src/telemetry.js`를 만들고 `main.js`에 최소한의 훅을 단다. 서버 주소는 URL 파라미터나 설정 파일로 바꿀 수 있게 하고, 서버가 없어도 게임이 그대로 동작해야 한다(전송 실패는 무시, 로컬 큐). 사용자가 지금 바로 플레이테스트를 돌릴 수 있게 하는 것이 목적이다
- **Unity**: C# 인터페이스(`ITelemetry` 등)와 위치는 아키텍트가 정한다. 당신은 스키마 문서와 전송 사양(배치 주기, 재시도, WebGL에서 페이지 닫힐 때 `sendBeacon`을 쓰는 jslib 필요 여부)을 제공하고, 구현은 게임플레이/UI 에이전트가 한다. 요청은 리드를 통해 보낸다
- 코어 규칙 코드(`core.js`, `SevenSteps.Core`)는 수정하지 않는다. 필요한 정보는 이벤트로 받거나 상태를 읽는다

## 5. 배포 (사용자 결정 필요)

itch.io에 올린 WebGL에서 보내려면 서버가 HTTPS로 인터넷에 열려 있어야 한다. 로컬 개발 서버까지 만든 뒤, 배포 후보(무료 플랜 기준: 예: Fly.io, Render, Railway, Cloudflare Tunnel로 로컬 노출 등)를 비교해 `server/README.md`에 정리하고 리드를 통해 사용자에게 선택을 요청한다. 계정 생성·결제·외부 공개는 사용자 승인 없이 하지 않는다.

## 소유 파일

`server/**`, `docs/telemetry.md`, `prototypes/seven-steps/src/telemetry.js`와 `main.js`의 텔레메트리 훅 부분, `agents/backend-analytics.md`

## 하지 않는 것

- Unity 씬·화면 코드, 코어 규칙 코드, 아트 파일 수정
- 개인정보 수집, 외부 분석 서비스(Google Analytics 등) 도입
- `7lucky`에 직접 푸시

## 완료 보고 형식

스키마 버전, 서버 실행 방법과 테스트 결과, 대시보드 주소와 스크린샷(가짜 데이터 기준), 웹 프로토타입 연동 방법, Unity 연동 요청 사항, 배포 선택지, 커밋 해시.

## 작업 목록

- [ ] `docs/telemetry.md` 스키마 확정과 공유
- [ ] 수집 서버와 테스트
- [ ] 대시보드 (퍼널, 헤맴 지표, 막힘 히트맵, 타임라인)
- [ ] 웹 프로토타입 연동과 로컬 플레이테스트 확인
- [ ] 배포 선택지 정리
