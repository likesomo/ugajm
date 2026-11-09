# 50스테이지 반영 순서

50개 후보는 `experiments/batch1~5/stages.mjs`에 모두 있다(묶음별 결과는 `batch1.md`~`batch5.md`, 전체 곡선은 `interest-curve-50.html`). 이 문서는 후보를 `levels.js`/`levels.json`에 넣는 순서와 각 단계에서 바뀌는 것, 누가 맡는지를 정리한다.
**2026-11-09 사용자 지시로 순서가 바뀌었다: 50개 전체를 한 번에 반영한다(`feat/levels-r1`, 진행 기록 v2가 7lucky에 들어온 직후 합침).** 아래 3장의 R1~R4 묶음 계획과 "플레이테스트 1차 뒤" 조건은 기록으로 남긴다. 15 숫자 문, 26, 33은 지금 판으로 넣고 반영 뒤 손본다(2.2).

## 1. 지금 상태 (7lucky 8dc2eea 기준)

| 항목 | 상태 |
|---|---|
| 진흙 규칙 (JS·C#, `m`) | 완료 (코어 6ae58e2) |
| `LevelDto.chapter`·`story` (C#) | 완료 (`Core/Level.cs`) |
| `LevelCatalog` 챕터 조회, 챕터 프리셋 보드 적용 | 완료 (게임플레이/UI 824ce4d) |
| `export-levels.mjs`가 `chapter`·`story`를 옮김 | 완료 (있을 때만 씀) |
| `design/levels/chapters.json` → `Data/chapters.json` 복사 | **아직 없음** (레벨 디자인, 1차 반영 때) |
| export 단정 검증 (챕터 존재·순서, 예산 ≤ 3, 전부 풀림, 이름 중복 없음) | **아직 없음** (레벨 디자인, 1차 반영 때 켬) |
| 웹 프로토타입 진흙 표시 | 완료 (feat/levels 95e626b) |
| 챕터 프리셋 에셋 (7행성) | 아트. 없으면 `artPreset`이 같은 프리셋을 같이 쓴다(6.4) |

## 2. 반영 전 손질 (레벨 디자인, 플레이테스트 결과를 본 뒤)

1. **15 숫자 문**: 플레이테스트 7.2 기준으로 보강 여부 결정(지금은 숫자 문이 장식, 예측 I 0.34)
2. **34 레버와 발판**(I 0.33, 기준선 아래), **26 미끄럼을 막는 형제**(I 0.35, D 0.56), **35 결구의 장치**(기믹 필수 근거 약함): 판 축소 또는 다른 후보로 교체
3. 플레이테스트에서 실제로 막힌 스테이지(기존 17 중 유지하는 11개)의 힌트 문구·맵을 7장 기준으로 수정
4. 모든 스테이지에 `chapter`(문자열 id)와 `story`(세쌍둥이 시점 한 줄) 작성, 힌트 문구 검토(탐색 후보는 임시 문구)
5. `design/levels/chapters.json` 작성: `moss`, `hollow`, `carved`, `frost`, `joinery`, `crumble`, `altar` (planet, concept, intro, artPreset)
6. 복도형 판(2장 11~13 등)을 행성 표면 모양에 맞게 굽힐지 아트와 상의(걸음 수가 같으면 해가 같다)

### 2.1 손질 결과 (2026-11-09, feat/levels)

| 항목 | 결과 |
|---|---|
| 26 미끄럼을 막는 형제 | 새 판(`stage-search --strict`, 몸 2, SWITCH 불필요). D 0.56 → 0.40, 예측 I 0.35 → 0.43. 얼음을 바닥으로 바꾸면 몸 1 |
| 34 레버와 발판 | 더 엄격한 후보로 바꾸고 **32로 옮김**(5장: 31 올려서 멈추기 → 32 레버와 발판 → 33 레버와 숫자 → 34 레버와 바통 → 35 결구의 장치). 예측 I 0.33 → 0.40. 레버나 발판을 빼면 몸 1 |
| 35 결구의 장치 | 레버를 빼면 몸 1, 얼음을 빼면 못 풂(이전 판은 레버를 빼도 같은 크기의 다른 해). 예측 I 0.44 → 0.39 |
| 50개 전체 | 예측 I 최저 0.34(15 숫자 문, 플레이테스트 대기). 그 밖은 모두 0.38 이상 |
| `chapters.json` 초안 | `design/levels/chapters.json` (7행성, 6.4 형식: id, planet, concept, intro, artPreset). artPreset은 아트 프리셋이 생기기 전까지 id와 같게 둠 |
| chapter / story 문구 | `design/levels/story-50.json` (50개, 세쌍둥이 시점 한 줄, 최장 39자). 반영 때 `levels.js`에 합침 |
| 남은 약한 곳 | 33 레버와 숫자: 숫자 문을 빼도 같은 크기의 다른 해가 있다(레버는 필요). 반영 전 한 번 더 본다 |

`stage-search.mjs --strict`: 기믹을 빼면 못 풀거나 몸 수·길이가 바뀌어야 한다("해만 다름"은 탈락).

### 2.2 지금 판으로 넣고 반영 뒤 손볼 것 (메모)

| # | 이름 | 문제 | 다음 |
|---|---|---|---|
| 15 | 숫자 문 | 숫자 문을 바닥으로 바꿔도 같은 해(장식). 예측 I 0.34 | 플레이테스트 7.2 기준으로 결정 |
| 26 | 미끄럼을 막는 형제 | 최소 몸 2에서 선돌을 남기지 않는 해도 있다. 이름의 "선돌로 미끄럼을 막는다"를 안 써도 깨진다(`stars.md` 2장) | 선돌 멈춤이 꼭 필요한 판으로 교체 검토 |
| 33 | 레버와 숫자 | 숫자 문을 바닥으로 바꿔도 같은 크기의 다른 해가 있다(레버는 필요) | `--strict` 레버+숫자 문 후보 탐색 |

## 3. 반영 단위

`levels.js`는 늘 **새 순서의 1번부터 N번까지**만 담는다(옛 스테이지와 새 스테이지를 섞지 않는다). 1차 반영은 지금 17개보다 줄지 않게 **묶음 1+2(20개)**로 한다.

| 반영 | levels.js 내용 | 장 | 조건 |
|---|---|---|---|
| R1 | 1~20 | 1, 2, 3(15~20) | 2장 손질 1·3~5 완료. 형식 도입(검증 켬, chapters.json) 같은 커밋 |
| R2 | 1~30 | +3장 피날레, 4장, 5장 시작 | 26 손질 |
| R3 | 1~40 | +5장, 6장 | 34·35 손질 |
| R4 | 1~50 | +6장 피날레, 7장 | — |

R1~R4는 각각 `feat/levels` 커밋 하나이고, 아래 4장의 갱신을 맡은 사람들과 같은 날 맞춘다. 3장을 반으로 자른 R1(15~20만 3장)은 장 피날레(21)가 없는 상태라, 사이 기간에는 20번이 마지막 스테이지다.

## 4. 반영할 때마다 바뀌는 것

| 무엇 | 어디 | 누가 | 할 일 |
|---|---|---|---|
| 스테이지 데이터 | `prototypes/seven-steps/src/levels.js` | 레벨 디자인 | 후보 복사, `chapter`·`story` |
| 챕터 데이터 | `design/levels/chapters.json` → `Data/chapters.json` | 레벨 디자인 | R1에서 만들고 복사 단계를 `export-levels.mjs`에 넣음 |
| `levels.json` | `unity.sh levels` | 레벨 디자인 | 단정 검증 통과해야 써짐 |
| 규칙 골든 (`Tests/Golden/levels.golden.json`, `traces.golden.json`) | `unity.sh golden` (`gen-golden.mjs`) | 코어 | 다시 만들고 `test` 통과 확인 |
| 텔레메트리 골든 (`server/fixtures/telemetry.golden.json`) | `server/tools/telemetry-golden.mjs` | 백엔드/분석 | 수동 시나리오가 스테이지 번호(`MANUAL[].level`)를 쓰므로 새 번호로 확인 후 다시 만듦. Unity `TelemetryGoldenTests` 통과 |
| C#↔JS 일치 | `unity.sh verify` | 리드 | "N/N" 확인 (17 → 20/30/40/50) |
| 데스크톱 smoke 리플레이 | `unity.sh smoke` 의 `spec` 목록 (지금 `1:R 3:… 8:… 9:… 13:… 15:… 16:…`) | 리드 | 아래 5장 표로 교체 |
| WebGL smoke 리플레이 | `unity.sh smoke-webgl` (지금 `1, 8, 13`) | 리드 | 아래 5장 표로 교체 |
| 재현 예시 | `CLAUDE.md` (`-level 8 -replay LRRRRDDK`) | 리드 | 새 8번(새 출발점) 기준으로 |
| 저장 데이터 | `ProgressData.cleared`·`bestStage`가 **스테이지 번호**로 저장됨 | 씬 플로우/게임플레이 | 번호가 바뀌므로 옛 기록이 다른 스테이지에 붙는다. 레벨 세트 버전(예: `levels.json`의 해시)이 바뀌면 진행 기록을 비우거나 이름으로 옮기는 처리 필요 |
| 가짜 플레이 데이터 | `server/tools/fake-data.mjs` (8·12스테이지를 어렵게 시뮬레이션) | 백엔드/분석 | 어려운 스테이지 번호 갱신(선택) |
| 플레이테스트 안내 | `design/levels/playtest-guide.md` 3장 표 | 레벨 디자인 | 2차 테스트 전에 새 스테이지로 |
| 웹 프로토타입 | `levels.js`를 그대로 씀 | 레벨 디자인 | 손 플레이로 묶음 전체 확인(진흙 표시 포함) |
| 아트 챕터 프리셋 | `ChapterPreset`, `AppConfig.chapters` | 아트·게임플레이/UI | `chapters.json`의 `artPreset` id가 모두 있어야 `TileSetValidator` 통과(켜질 때) |

순서: 레벨 디자인 커밋(levels.js, chapters.json, export 검증, levels.json) → 코어 골든 → 백엔드 골든 → 리드가 smoke 목록·CLAUDE.md 갱신 후 `compile`·`test`·`verify`·`smoke`·`smoke-webgl` → 7lucky 합침.

## 5. smoke 리플레이 제안 (R4 기준 번호, 솔버 최단 해)

데스크톱 7개(지금 개수 유지):

| 스테이지 | 보는 것 | 리플레이 |
|---|---|---|
| 1 일곱 걸음 | 시작, 7번째 | `R` |
| 4 돌이 된 몸 | KEEP·발판·문 | `DURRRRRK` |
| 8 새 출발점 | SWITCH | `RRRRRRDX` |
| 10 몸으로 놓는 다리 | 구멍 다리 | `RLRLRRR` |
| 22 미끄러운 첫걸음 | 얼음 미끄럼 | `RDDLLLL` |
| 29 내리는 벽 | 레버 | `DURRRRRKDDR` |
| 43 한 칸에 두 걸음 | 진흙 | `RRRRRR` |

WebGL 3개: `1:R`, `29:DURRRRRKDDR`(레버, 2챕터 이후 프리셋 전환 확인), `50:UUUDURUK`(원형 제단, 마지막 챕터). R1~R3에서는 그 반영에 있는 번호만 쓴다(예: R1은 데스크톱 `1, 4, 8, 10`, WebGL `1:R`, `8:RRRRRRDX`, `10:RLRLRRR`).

### 5.1 CLAUDE.md 재현 예시

지금 `"Seven Steps.exe" -level 8 -replay LRRRRDDK` / `?level=8&replay=LRRRRDDK` → 새 번호 기준 **`-level 29 -replay DURRRRRKDDR`** / `?level=29&replay=DURRRRRKDDR` (29 내리는 벽: 돌아가기, KEEP, 레버. 옛 8번과 같은 맵).

### 5.2 텔레메트리 골든 수동 시나리오 번호 (`server/tools/telemetry-golden.mjs` `MANUAL`)

| id | 지금 level | 그 명령이 맞는 스테이지 | 새 level |
|---|---|---|---|
| manual-hint-then-clear, manual-select-leave, manual-clear-again | 1 | 일곱 걸음 | **1** (그대로) |
| manual-blocked-wall-edge | 2 | 늦게 도착하기 | **2** (그대로) |
| manual-keep-switch | 4 | 새 출발점 | **8** |
| manual-fail-undo-restart | 5 | 몸으로 놓는 다리 | **10** |
| manual-gate-solution | 6 | 숫자 문 (`UDRRRRR`) | **15** |
| manual-plate-door-crack | 9 | 한 번만 버티는 바닥 (`RLRLRRRRLRRRRRKRRRRDDR`, 새 36의 최단 해와 같음) | **36** (지금 17스테이지의 9번은 멈춰 세우는 석상이라 이 명령과 이미 어긋나 있다. 12스테이지 v0 시절 번호로 보임) |

맵이 옛 스테이지와 같으므로 명령은 그대로 쓴다. `level` 번호만 바꾼 뒤 `unity.sh golden`.

## 6. 확인 목록 (반영 커밋마다)

- [ ] `node prototypes/seven-steps/experiments/run.mjs batchN` 전부 OK, 기믹 필요
- [ ] `node prototypes/seven-steps/tools/level-metrics.mjs` → `interest-curve.html` 갱신, 예측 I 기준선 아래 없음(있으면 이유)
- [ ] `node prototypes/seven-steps/tools/solve.mjs` 전부 풀림, 최소 몸 ≤ 3
- [ ] `unity.sh levels` 단정 검증 통과
- [ ] 웹 프로토타입 손 플레이 (새 스테이지 전부)
- [ ] 리드에게 보고: 바뀐 번호표(옛 이름 → 새 번호), 골든·smoke 갱신 요청
