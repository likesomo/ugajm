# 3단계 플레이테스트 1차 — 준비 목록

작성: 리드, 2026-11-08. **2026-11-10 갱신: 50스테이지·7행성·3별·다국어 기준** (레벨 디자인 초안 `design/levels/playtest-1-prep-draft.md`, 백엔드 당일 절차 `docs/telemetry.md` 6장). 소유: 리드. 항목이 끝나면 체크하고 커밋 해시를 적는다.

**사용자 승인 전에는 하지 않는 것**: 서버 외부 공개(Cloudflare Tunnel, Fly.io 등), itch.io 업로드.

테스트 시나리오(5명 회전 배치), 관찰 지표, 흥미도 모델 보정 계획은 초안 1~3장, 별 기준 점검은 6장, 스테이지별 리플레이·최소 몸은 `design/levels/stage-report.md`.

## A. 빌드 (리드)

- [x] Unity 라이선스 복구 (2026-11-10)
- [ ] 7lucky에서 `compile` · `test` · `verify`(50/50) · `scene-validate` 통과
- [ ] `build-webgl` → `smoke-webgl`(1, 29, 50) 통과. 로그의 "1 URP assets included in build" 다음 줄이 Mobile_RPAsset인지 확인
- [ ] `build-win` → `smoke` 통과 (진행자 PC 예비)
- [ ] 테스트 빌드 커밋 해시를 이 문서와 `design/levels/playtest-guide.md`에 적는다
- [ ] 7행성 피날레 확인: `stage-report.md`의 리플레이로 7, 14, 21, 28, 35, 42, 50을 `?level=N&replay=…`로 돌려 결과 화면에 ★3이 나오는지. 8·15·22·29·36·43(행성 첫 스테이지)에서 챕터 카드가 나오는지
- [ ] 한국어·영어 각 1회: `?lang=en`으로 타이틀 → 행성 선택 → 1번 → 결과 화면까지 한국어가 섞이지 않는지 (아래 E-2가 끝나야 통과)
- [ ] 별 저장: ★1로 깬 뒤 ★3으로 다시 깨면 ★3, 반대 순서면 ★3 유지

## B. 테스트 대상 결정 (사용자)

- [ ] **무엇으로**: Unity WebGL(권장) / 웹 프로토타입(`/play/`). 같은 텔레메트리 스키마
- [ ] **어디서**: 진행자 PC 현장(외부 공개 불필요) / 원격 테스터(→ 서버 외부 공개, 사용자 승인)
- [ ] 테스터 인원·일정 (목표 5명 이상)
- [ ] **세션 길이**: 50스테이지 전체는 2~3시간 → 회전 배치(모두 1~14, 각자 15/22/29/36/43부터, 행성마다 2명 이상, 1인 50~60분)를 쓸지
- [ ] **언어**: 대부분 한국어, 영어는 1명이 1~2장만(문구 길이·잘림 확인). 기록은 `lang`으로 나눠 본다

## C. 수집 서버 (백엔드/분석) — 당일 절차 `docs/telemetry.md` 6장

- [x] 대시보드 50스테이지·7행성 이름·별 분포·`lang` 필터, 내보내기에 `lang`·`planet` 열 (feat/server 725dac2)
- [x] 서버가 WebGL 빌드를 `/webgl/`로 제공하고 `?telemetry=`를 붙인다 (d6296cf)
- [ ] 새 WebGL 빌드로 `/webgl/` 다시 확인
- 당일 순서:
  1. 전날: `build-webgl` → `smoke-webgl` 통과, `node --test server/test/` 통과
  2. 서버: `STATS_TOKEN` 랜덤 생성·기록 후 `DB_PATH=server/data/playtest-<날짜>.sqlite node server/src/index.mjs` (테스트 내내 터미널 유지)
  3. 시작 2분 전: `curl localhost:8787/healthz` = ok, 테스터 주소 `http://localhost:8787/webgl/`(같은 와이파이는 PC IP)로 1스테이지 클리어 → 대시보드(`/dashboard#token=<토큰>`)에 새 빌드·unity-webgl이 보이면 OK. 확인 기록은 기간 필터로 제외
  4. 진행 중: 대시보드 수동 새로고침, 필터(클라이언트·빌드·언어·기간·행성)로 퍼널·이탈률·별·히트맵·타임라인
  5. 끝: 서버 Ctrl+C, DB 파일 보관(git 제외), `attempts.csv`·`levels.csv`·`events.jsonl`을 `?since=<날짜>`로 내보내 레벨 디자인에 전달
- [ ] **사용자 결정**: itch.io 빌드는 iframe이라 `?telemetry=`를 줄 수 없다 → `AppConfig.telemetryEndpoint`에 고정 HTTPS 주소 필요. 원격 테스터는 터널 1개(`TRUST_PROXY=1`)로 `/webgl/`·`/play/` 둘 다 (`server/README.md`)

## D. 진행 안내 (레벨 디자인)

- [x] `playtest-guide.md` 50스테이지 기준, 관찰표·메모 양식 `playtest/TEMPLATE-notes.md`
- [x] `stage-report.md` (막힌 테스터 상태 확인, 피날레 리플레이) (feat/levels b028d15)
- [x] 스테이지 문구 점검: 용어 선돌/형제, 25 이름 '멈춰 세우는 선돌' (845387c, 487c981, a805d9e)
- [ ] 행성 한 줄(`chapters.json` tagline, ko/en 7개) 채우기 (형식: feat/architecture 9c3dfc1)
- [ ] 테스트 뒤: `tools/calibrate.mjs` → `interest-curve.html` 실측 겹치기 → 행성별 보정안. 표본 5명 이상만, D 가중치는 1~21에서만 ±0.1

## E. 게임 화면 (게임플레이/UI, 씬 플로우)

- [x] 결과 화면 별 3개 연출 (feat/view 0d30f0f)
- [ ] E-1 결과 화면 조건 문구(초안 5.3 `result.*`), 못 채운 별은 흐리게, 조건 문구는 늘 보이게 — 게임플레이/UI
- [ ] E-2 **영어에서 한국어가 나오는 곳**: 행성 선택 스테이지 이름(F1, Patrick Hand에 한글 없음 → WebGL 빈칸)·챕터 카드 `LevelCatalog.ChapterCard` — 씬 플로우 / `MessagePresenter`·`ChooseNudge` 한국어 상수 → `msg.*` — 게임플레이/UI
- [ ] E-3 용어: 메시지 "석상"→"선돌", "몸"→"형제", `stageselect.best_bodies` "형제 {0}명" / "{0} brothers"
- [ ] E-4 가장 긴 힌트(23번, ko 56자 / en 97자)가 두 줄 안에 들어가는지 1280×720과 WebGL 기본 창에서
- [ ] Codex UI 시안(`C:/unityjam/shared-assets/seven-steps/concepts/ui/en-ko.html`) 적용: 타이틀·행성 선택·로딩/챕터 진입·설정(씬 플로우), 결과·HUD(게임플레이/UI). 테스트를 막지는 않는다
- [ ] 첫 실행 시 텔레메트리 동의/전송 상태가 화면에 보이는지, 테스터별 플레이어 ID 분리(시크릿 창이면 충분한지)

## F. 아트 (Codex 워커, 코디네이터 경유)

- [x] 방향 확정(`design/art/art-bible.md`), RuinsHenge 세트가 `AppConfig.tileSet`, KEEP 외형 기본 중성 선돌 (feat/architecture d5a2be8)
- [ ] 7행성 `ChapterPreset`(id = `chapters.json` artPreset: moss/hollow/carved/frost/joinery/crumble/altar) — Codex 제작 → 게임플레이/UI 적용(docs 12.8) 후 리드가 `AppConfig.chapters`를 한 번에 채움. **테스트를 막지 않는다** (지금 상태로 진행 가능). 바뀌면 `tileset-validate`·`smoke-webgl` 필수
- [ ] 도리이 닮은 붉은 문 교체 (Codex)

## G. 1차 테스트에서 답을 얻을 질문

1. 7번째 걸음 규칙을 설명 없이 이해하는가 (1~3). 4에서 KEEP을 힌트 문구만 보고 쓰는가
2. **톱니 곡선**: 행성 첫 스테이지(8, 15, 22, 29, 36, 43)가 앞 행성 피날레보다 쉽게 느껴지는가
3. **막힘 예측**: 33, 41, 42, 49, 50에서 실제로 지치거나 그만두는가
4. **별이 동기가 되는가**: 클리어 뒤 ★3을 노리고 다시 하는 비율(`via=again`), ★2 갈림 스테이지에서 최소 몸 수를 맞추려 하는가
5. **★3이 너무 어려운 행성**: 행성 안에서 ★3 비율 20% 미만 스테이지가 3개 이상이면 완화안 (특히 33/49/50/41/42/34/35). 26은 선돌 없이 ★2 가능 → keeps=0 비율
6. 힌트 문구가 규칙을 너무 많이 알려 주는가 (15 숫자 문 보강 여부)
7. 행성 카드·문구가 여정으로 읽히는가
8. 아트에서 상태(선돌, 레버, 숫자 문·선돌 수 문 열림, 금 간 바닥, 진흙)를 읽을 수 있는가
