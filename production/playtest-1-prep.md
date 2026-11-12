# 3단계 플레이테스트 1차 — 준비 목록

작성: 리드, 2026-11-08. **2026-11-10 갱신: 50스테이지·7행성·3별·다국어 기준** (레벨 디자인 초안 `design/levels/playtest-1-prep-draft.md`, 백엔드 당일 절차 `docs/telemetry.md` 6장). 소유: 리드. 항목이 끝나면 체크하고 커밋 해시를 적는다.

**사용자 승인 전에는 하지 않는 것**: 서버 외부 공개(Cloudflare Tunnel, Fly.io 등), itch.io 업로드.

테스트 시나리오(5명 회전 배치), 관찰 지표, 흥미도 모델 보정 계획은 초안 1~3장, 별 기준 점검은 6장, 스테이지별 리플레이·최소 몸은 `design/levels/stage-report.md`.

## A. 빌드 (리드)

- [x] Unity 라이선스 복구 (2026-11-10)
- [x] 7lucky에서 `compile` · `test`(769/769) · `verify`(50/50) · `scene-validate`(0/0) · `tileset-validate` 통과 — 후보 빌드 `e6dda07` (2026-11-10)
- [x] `build-webgl` → `smoke-webgl`(1, 29, 50) 통과: board 28738/25992/22581, empty 대조 0, 콘솔 예외 0, Mobile_RPAsset 확인 (`e6dda07`)
- [x] `build-win` → `smoke` 통과: 리플레이 7/7, 행성 경계 흐름 9/9(별 3), 타이틀 경로 3/3(시작→1 select, 지도→8 select, 이어하기→8 boot) (`e6dda07`)
- [x] **플레이테스트 1 후보 빌드 `e2b6932` (2026-11-11)**: 최소 UI v3(6575a12) + 행성 2~7 모델링(e05c3c1, 병합 6fd36c7) + hollow 이음선·altar 그림자 수정(bcb2126, 사용자 결정대로 금요일 전 도착분 포함) + 이야기 S1~S3 데이터(story.json, 스테이지 줄 50·행성 카드 7, startLine 4·8 hint). 검사: EditMode 852/852, scene-validate·tileset-validate 0 오류, build-win+smoke, WebGL 21.12MB + smoke-webgl 보드 56644/85671/89594·대조 0, story-check --strict-levels OK. 일반 Chrome FPS(10스테이지 1·8·14·15·22·29·31·36·43·49) 57.7~60, p95 17~18ms. 7행성 캡처 `reports/planets/`(Windows 12장, WebGL 9·14 — hollow 이음선 사라짐)
  - **갱신 후보 `56fce3e` (2026-11-11)**: 위 + 별 흔적 v2·보드 위 클리어 결과(14.7, feat/view c58dfa4 — 키보드 별 조건) + 이야기 S4 흐름(엔딩 4장, 50 [끝까지 보기], 별지도 새김·진실, 북극성, 타이틀 하늘 별, 앱 종료 저장; scene-flow 6e8f3cd·2fe7f4d). 검사: EditMode 864/864, story-check --strict-levels OK, scene-validate·tileset-validate 0 오류, build-win+smoke(flowcheck 50→엔딩→StageSelect), WebGL 21.16MB + smoke-webgl 56536/84031/89367·대조 0, Chrome FPS 60(p95 17ms; 5621331에서 10스테이지 1·8·14·15·22·29·31·36·43·49 모두 60). 이야기 S4 보드 쪽(StoryDirector·순간 줄·피날레 줄)은 아직 없음
  - 리허설 수동 확인: (1) 7번 깬 저장으로 별지도 새김·카드 초점 진실·행성 단계 패널 금색 진실 (2) 클리어 결과에서 Tab×3 순환 → Esc 메뉴에서 ←/→ → 닫기 (3) 50 클리어 → [끝까지 보기] → 엔딩 4장 → 별지도 (4) 결과에서 별 초점이 Esc 메뉴를 닫은 뒤에도 그대로인지(378bf38) (5) 이야기 S4 보드 쪽(4ea1ec0): 행성 카드를 닫은 뒤 시작 한 줄이 뜨는지, 4·8은 힌트가 뜨는지, 7번 클리어 때 피날레 줄, 피날레에서 Enter 한 번에 결과로 가는지(M3), 결과에서 R로 다시 할 때 시작 줄이 또 뜨면 거슬리지 않는지(M4) (6) 단계형 힌트(4024c25·c5a3657·b6ad087): H 연타로 원리 글 → 금색 고리(몸마다) → 전체 경로, 4·8 첫 H가 '힌트 1/3', 경로를 벗어난 뒤 '(처음부터)', Undo 뒤 연 단계 유지, 메뉴 '풀이 보기'도 다음 단계 (7) Esc 메뉴 '시작 줄 다시 보기'(93b5e04): 클릭하면 메뉴가 닫힌 뒤 시작 줄이 다시 뜨는지, 결과 화면 위 메뉴와 시작 줄 없는 스테이지에서는 항목이 안 보이는지
  - 테스트 직전 최종 7lucky로 다시 돌리고 해시를 바꾼다(이야기 S4·별 흔적 v2·결과 화면이 그때까지 승인·검증되면 포함, 아니면 이 후보). 배경(RuinsHengeSky)은 사용자 작업 중이라 손대지 않음
  - **새 스테이지 선택(행성 지도 v4, 사용자 승인 2026-11-11, `concepts/ui/stage-select-2026-11-11/IMPLEMENT.md`)**: gameplay-ui(feat/view)·scene-flow(feat/scene-flow) 구현 중. 금요일 11/13 빌드는 병합·안정화되지 않으면 지금 스테이지 선택을 유지한다(필요하면 토글 뒤). 참고 리뷰 씬 feat/art-stages 8c6e9b3 → feat/art 04c0086 병합(Editor 전용)
- [x] **플레이테스트 1 빌드 `7aa74e4` (2026-11-12)**: `bd5781a` + U5 챕터 카드(feat/view 9928b73, 행성 이름과 한 줄만) + playcapture 종료 코드 수정. 검사: compile, scene-validate, EditMode 882/882, PlayMode, playcapture U5ChapterCard. WebGL 39MB(data 29.9MB, wasm 9.9MB) + smoke-webgl 보드 48536/72686/80983·대조 0, Mobile_RPAsset. 챕터 카드는 URL로 바로 열 수 없어(행성이 바뀌는 다음 스테이지에서만, 지도 다이브는 생략) PlayMode 캡처로 확인. 이전 빌드는 `Builds/WebGL-bd5781a`(git 밖)에 보관
  - 이전 빌드 `bd5781a`: U5 UI 기본 켬(사용자 결정 2026-11-12-b, `-ui4`·`?ui5=0`이면 이전 UI) — 타이틀·언어·크레딧·부트·지도 v4 + V7 행성·표면 S2 + D2b 별자리 스테이지 선택(첫 클리어 연출)·HUD·7걸음 선택·클리어(월드 별돌)·일시정지·설정·실패·엔딩, 걸음 별 C(보드 위 하늘 별자리), ★3 = 형제 모두(6·16·38 수정), 힌트 본 판 재시작 안내 + 별 상한 표시, 첫 미끄럼 안내 + 22 힌트 문구, hollow 조명. 검사: compile, scene-validate 0 오류, verify 50/50, EditMode 877/877, PlayMode 0 실패, playcapture 21/21(58장), WebGL 39MB(data 29.9MB, V7 표면 그림으로 커짐) + smoke-webgl(1, 29, 50) 통과·Mobile_RPAsset. 옛 모양 챕터 카드가 남아 있었음(7aa74e4에서 U5판으로)
- [x] 테스트 빌드 커밋 해시를 이 문서와 `design/levels/playtest-guide.md`에 적는다 → `7aa74e4`
- [x] 7행성 피날레 확인(smoke 흐름 단계가 자동으로 함: 7·14·…·50 ★3, 결과 문구, 챕터 카드, 프리셋 교체): `stage-report.md`의 리플레이로 7, 14, 21, 28, 35, 42, 50을 `?level=N&replay=…`로 돌려 결과 화면에 ★3이 나오는지. 8·15·22·29·36·43(행성 첫 스테이지)에서 챕터 카드가 나오는지
- [x] 한국어·영어 각 1회 (scene-flow 리허설, 영어 이름·챕터 카드·설정 스크린샷): `?lang=en`으로 타이틀 → 행성 선택 → 1번 → 결과 화면까지 한국어가 섞이지 않는지 (아래 E-2가 끝나야 통과)
- [x] 별 저장 (WebGL 새로고침 후 유지, `?newplayer=1` 초기화 확인): ★1로 깬 뒤 ★3으로 다시 깨면 ★3, 반대 순서면 ★3 유지

- [x] 리허설(scene-flow, 2026-11-10): 테스터 2명 흉내로 서버→/webgl/?newplayer=1→대시보드→내보내기까지. 발견한 타이틀 '시작' 먹통(19554a3), via 규칙, since 시간대, 기간 필터 버그(e953eb0), 첫 방향키(365d5bb) 모두 수정·병합
- [ ] 테스트 빌드 해시를 `design/levels/playtest-guide.md`에 적기 (테스트 직전 최종 7lucky로)

## B. 테스트 대상 결정 (사용자, 2026-11-10 확정)

- [x] **무엇으로**: Unity WebGL, 서버의 `/webgl/`
- [x] **어디서**: 진행자 PC 현장. 외부 공개·터널 없음. 같은 와이파이의 다른 기기가 진행자 PC IP로 `/webgl/`에 접속하는 것까지 허용
- [x] **세션 길이**: 회전 배치. 모두 1~14, 이후 각자 15/22/29/36/43부터 시작, 행성마다 2명 이상, 1인 50~60분. 테스터 번호별 시작 행성은 `design/levels/playtest-guide.md` 배정표 (레벨 디자인)
- [ ] 테스터 인원·일정: 미정, 목표 5명 이상으로 준비. 정해지면 코디네이터가 전달
- [x] **언어**(리드): 대부분 한국어, 영어는 1명이 1~2장만(문구 길이·잘림 확인). 기록은 `lang`으로 나눠 본다
- 최종 빌드: `production/ui-layout-fix.md`가 병합된 뒤의 7lucky로 만들고 해시를 A절과 `playtest-guide.md`에 적는다

## C. 수집 서버 (백엔드/분석) — 당일 절차 `docs/telemetry.md` 6장

- [x] 대시보드 50스테이지·7행성 이름·별 분포·`lang` 필터, 내보내기에 `lang`·`planet` 열 (feat/server 725dac2)
- [x] 서버가 WebGL 빌드를 `/webgl/`로 제공하고 `?telemetry=`를 붙인다 (d6296cf)
- [ ] 새 WebGL 빌드로 `/webgl/` 다시 확인 (진행자 PC의 `localhost`와 같은 와이파이 기기의 PC IP 둘 다. 서버가 LAN 주소에서 받는지, Windows 방화벽 허용이 필요한지 백엔드가 확인)
- 당일 순서:
  1. 전날: `build-webgl` → `smoke-webgl` 통과, `node --test server/test/` 통과
  2. 서버: `STATS_TOKEN` 랜덤 생성·기록 후 `DB_PATH=server/data/playtest-<날짜>.sqlite node server/src/index.mjs` (테스트 내내 터미널 유지)
  3. 시작 2분 전: `curl localhost:8787/healthz` = ok, 테스터 주소 `http://localhost:8787/webgl/`(같은 와이파이는 PC IP)로 1스테이지 클리어 → 대시보드(`/dashboard#token=<토큰>`)에 새 빌드·unity-webgl이 보이면 OK. 확인 기록은 기간 필터로 제외
  4. 진행 중: 대시보드 수동 새로고침, 필터(클라이언트·빌드·언어·기간·행성)로 퍼널·이탈률·별·히트맵·타임라인
  5. 끝: 서버 Ctrl+C, DB 파일 보관(git 제외), `attempts.csv`·`levels.csv`·`events.jsonl`을 `?since=<날짜>`로 내보내 레벨 디자인에 전달
- [-] **보류 (이번 테스트에 불필요, 2026-11-10 사용자)**: itch.io 빌드는 iframe이라 `?telemetry=`를 줄 수 없다 → `AppConfig.telemetryEndpoint`에 고정 HTTPS 주소 필요. 원격 테스터는 터널 1개(`TRUST_PROXY=1`)로 `/webgl/`·`/play/` 둘 다 (`server/README.md`)

## D. 진행 안내 (레벨 디자인)

- [x] `playtest-guide.md` 50스테이지 기준, 관찰표·메모 양식 `playtest/TEMPLATE-notes.md`
- [x] `stage-report.md` (막힌 테스터 상태 확인, 피날레 리플레이) (feat/levels b028d15)
- [x] 스테이지 문구 점검: 용어 선돌/형제, 25 이름 '멈춰 세우는 선돌' (845387c, 487c981, a805d9e)
- [x] 행성 한 줄(`chapters.json` tagline, ko/en 7개) 채우기 (395f0f3)
- [ ] 테스트 뒤: `tools/calibrate.mjs` → `interest-curve.html` 실측 겹치기 → 행성별 보정안. 표본 5명 이상만, D 가중치는 1~21에서만 ±0.1

## E. 게임 화면 (게임플레이/UI, 씬 플로우)

- [x] 결과 화면 별 3개 연출 (feat/view 0d30f0f)
- [x] E-1 결과 화면 조건 문구(초안 5.3 `result.*`), 못 채운 별은 흐리게, 조건 문구는 늘 보이게 — 게임플레이/UI (cf65d84, ResultView 못 채운 별·조건 Dim)
- [x] E-2 **영어에서 한국어가 나오는 곳**: 행성 선택 스테이지 이름(F1, Patrick Hand에 한글 없음 → WebGL 빈칸)·챕터 카드 `LevelCatalog.ChapterCard` — 씬 플로우 / `MessagePresenter`·`ChooseNudge` 한국어 상수 → `msg.*` — 게임플레이/UI (a06ea19 스테이지 이름·챕터 카드, 46d4ec3 hud/msg/hint/result 문자열 표, 3f6671f 한국어 리터럴 오류 검사)
- [x] E-3 용어: 메시지 "석상"→"선돌", "몸"→"형제", `stageselect.best_bodies` "형제 {0}명" / "{0} brothers" (845387c, cf65d84)
- [x] E-4 가장 긴 힌트(23번, ko 56자 / en 97자)가 두 줄 안에 들어가는지 1280×720과 WebGL 기본 창에서 (db530fa, 넘치면 줄여 맞춤)
- [ ] Codex UI 시안(`C:/unityjam/shared-assets/seven-steps/concepts/ui/en-ko.html`) 적용: 타이틀·행성 선택·로딩/챕터 진입·설정(씬 플로우 181753a 등), 결과·HUD(게임플레이/UI 7ae849d, cf65d84). 메뉴 배경 `MenhirJoineryBackground`는 `GameAssets.uiTheme`(RuinsHengeTheme)로 타이틀·행성 선택·챕터 진입에 연결됨, 행성 지도 연결선 1-2-3-4-1·4-5-6-7 시안과 같음. **2026-11-10 시안과 화면 나란히 비교 중**(씬 플로우: 타이틀·행성 선택·로딩·챕터 진입·언어 / 게임플레이/UI: HUD·결과·설정). 테스트를 막지는 않는다
- [x] 첫 실행 시 텔레메트리 동의/전송 상태가 화면에 보이는지 (db530fa 설정 상태 줄, 01795d3 `?newplayer=1`), 테스터별 플레이어 ID 분리(시크릿 창이면 충분한지)

## F. 아트 (Codex 워커, 코디네이터 경유)

- [x] 방향 확정(`design/art/art-bible.md`), RuinsHenge 세트(행성 프리셋의 tileSet으로 적용, `AppConfig.tileSet`은 비워 둠), KEEP 외형 기본 중성 선돌 (feat/architecture d5a2be8)
- [x] 7행성 `ChapterPreset`(moss/hollow/carved/frost/joinery/crumble/altar) — Codex 034c910·93477aa, 게임플레이/UI 적용 5e0a1cb·cfde7f2, `AppConfig.chapters` 7개 등록. tileset-validate·smoke-webgl 통과(e6dda07)
- [x] 도리이 닮은 붉은 문 교체 → 선돌 + 곧은 결구 들보 `MenhirJoineryBackground` (2af01c4)
- [x] `agents/README.md`·`agents/art.md`를 50스테이지·7행성·3별·ko/en·확정 아트 기준으로 갱신 (2026-11-10)
- [ ] 성능 세 가지(`_UNLIT` 키워드, 장식 그림자 끄기, HDR 끄기) — 코디네이터가 Codex에 전달. **테스트를 막지 않는다**. 바뀌면 `tileset-validate`·`smoke-webgl` 필수

## G. 1차 테스트에서 답을 얻을 질문

1. 7번째 걸음 규칙을 설명 없이 이해하는가 (1~3). 4에서 KEEP을 힌트 문구만 보고 쓰는가
2. **톱니 곡선**: 행성 첫 스테이지(8, 15, 22, 29, 36, 43)가 앞 행성 피날레보다 쉽게 느껴지는가
3. **막힘 예측**: 33, 41, 42, 49, 50에서 실제로 지치거나 그만두는가
4. **별이 동기가 되는가**: 클리어 뒤 ★3을 노리고 다시 하는 비율(`via=again`), ★2 갈림 스테이지에서 최소 몸 수를 맞추려 하는가
5. **★3이 너무 어려운 행성**: 행성 안에서 ★3 비율 20% 미만 스테이지가 3개 이상이면 완화안 (특히 33/49/50/41/42/34/35). 26은 선돌 없이 ★2 가능 → keeps=0 비율
6. 힌트 문구가 규칙을 너무 많이 알려 주는가 (15 숫자 문 보강 여부)
7. 행성 카드·문구가 여정으로 읽히는가
8. 아트에서 상태(선돌, 레버, 숫자 문·선돌 수 문 열림, 금 간 바닥, 진흙)를 읽을 수 있는가
