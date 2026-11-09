# 플레이 기록 → 흥미도 모델 매핑 (보정 계획 v1)

`interest-model.md`의 지표(D, N, A, C, F, I)를 플레이 기록(`docs/telemetry.md` v1 스키마, `feat/server` 6f134f1)으로 어떻게 재는지 정리한다.
목적은 자동 지표의 가중치를 실측과 맞추는 것이다(`agents/level-design.md` 6단계). 보정 스크립트는 내보내기 형식이 정해지면 `prototypes/seven-steps/tools/`에 만든다.

## 1. 분석 단위와 거르기

- **단위 = 시도(attempt)**: `level_start`부터 `level_clear` / `level_leave` / `session_end` / 다음 `level_start`까지. 시도 중 `restart`는 같은 시도
- **스테이지 식별**: `level` 번호는 순서가 바뀌면 달라지므로 **`levelName` + `build`**로 묶는다. 같은 이름이라도 맵을 바꾼 빌드는 따로 본다
- **첫 시도만**: 기본 집계는 플레이어별 그 스테이지의 첫 시도(`attempt = 1`, `via ≠ again`). 다시 하기나 이미 해 본 스테이지로 돌아온 시도(`attempt > 1`)는 별도 열로 본다. `via = select`만으로는 거르지 않는다(아래)
- **`via` 뜻** (2026-11-10 리드 결정): `select` = 지도·목록에서 고른 진입. **앱 첫 진입이어도 `select`**. `boot` = 실행 인자 없이 자동 이어가기로 들어간 경우만, `url` = `?level=`·`-level`, `next` = 클리어 뒤 다음, `again` = 클리어 뒤 다시 하기. 그래서 1차에서는 테스터의 첫 스테이지(1번)와 세션 2 이어 하기도 대부분 `select`이고, `via = select` ≠ 건너뜀
- **건너뛴 시도** (`skipped`, 보정 스크립트가 계산): 플레이어가 **앞 행성의 피날레(마지막 스테이지)를 깨기 전에** 시작한 그 행성의 시도 전부. 1장(이끼 행성)은 해당 없음. `attempts.csv`의 `playerId`·`level`·`outcome`·`startT`(+ `chapters.json`의 행성 범위)로만 정하고 `via`는 보지 않는다. 회전 배치(`playtest-1-prep-draft.md` 1.2)로 뒤 행성에 들어온 기록이 여기에 들어가며, 보정에서는 따로 본다(첫 스테이지 난이도가 부풀려짐)
- **결과 분류**:
  | 결과 | 조건 |
  |---|---|
  | `clear` | `level_clear` |
  | `clear_hinted` | `level_clear`이고 `hints > 0` (풀이를 보고 깬 것 → 실력 클리어와 따로 셈) |
  | `leave` | `level_leave` (다른 스테이지로 감) |
  | `quit` | 시도 중 `session_end` 뒤 같은 세션이 돌아오지 않음 = 이 스테이지에서 게임을 끔 |
- **활동 시간** `activeMs = durationMs − Σ idleMs`. 30초 넘게 손을 뗀 시간은 고민일 수도 자리 비움일 수도 있으므로 소요 시간은 활동 시간으로 보고, idle은 따로 센다
- 표본이 플레이어 5명 미만인 스테이지는 보정에 쓰지 않는다(곡선에 회색으로만 표시)

## 2. 지표별 매핑

| 모델 지표 | 실측 신호 (이벤트·필드) | 계산 | 해석 |
|---|---|---|---|
| **난이도 D_obs** | `level_clear.durationMs`·`moves`·`minMoves`·`undos`·`restarts`·`fails`·`hints`, `idle.idleMs`, `body_lost.wasted` | 스테이지별 첫 시도의 중앙값으로 다섯 항목: ① log(activeMs) ② (undos + 3×restarts) / minMoves ③ 실패율(`fails ≥ 1`) ④ 힌트율(`hints ≥ 1`) ⑤ 낭비한 몸 수(`wasted`). 각 항목을 전체 스테이지 대비 순위 백분위로 바꿔 평균 | 자동 D와 순위 상관(스피어만)을 본다. 어긋나는 스테이지가 있으면 자동 D의 항목 가중치를 고친다 |
| **적정 도전 C_obs** | 결과, `activeMs`, `moves / minMoves` | **너무 쉬움** = 실력 클리어 & activeMs < 20초 & moves ≤ 1.3×minMoves. **너무 어려움** = `clear_hinted` + `leave` + `quit`. C_obs = 1 − (너무 쉬움 비율 + 너무 어려움 비율) | 모델의 C와 비교해 `stretch`·`sigma`·`introDiscount`를 고친다 |
| **깨달음 A_obs** | `idle`, `undo`, `level_clear`의 시각 | **돌파 시도** = 막힘(30초 이상 idle 또는 Undo 5회 이상 연속 구간) 뒤, 힌트 없이 **마지막 막힘 끝에서 1.5×minMoves 이동 안에 클리어**. A_obs = 첫 시도 중 돌파 시도 비율 | "고민하다 한 번에 풀림" = 아하의 흔적. `judgement.json`의 A를 이 값 쪽으로 옮긴다(사람 판단 50 : 실측 50) |
| **피로 F_obs** | 결과, `idle`, 스테이지 간 이동 | 실력 클리어인데도 다음 스테이지로 안 가고 끝낸 비율 + 결과가 `leave`인 비율. 기믹 조합이 같은 연속 구간에서 오르는지 본다 | 겹침 가중치(0.5/0.3/0.2)와 `streakLimit`를 고친다 |
| **새로움 N** | (직접 재지 않음) | 자동 계산 유지. 새 요소 첫 스테이지의 `body_lost.reason`·`move.result`(`fell`, `blocked` 등)이 많으면 소개가 부족하다는 신호로 별도 기록 | 소개 스테이지 맵 수정 근거 |
| **흥미도 I_obs** | 퍼널, 결과 | I_obs = 다음 스테이지 진행률(같은 세션에서 다음 `level_start`) − `quit` 비율 − 0.5×`clear_hinted` 비율 | 모델 I와 순위 상관. 가장 중요한 검증: **실제 이탈이 많은 스테이지의 모델 I가 기준선 근처인가** |
| **막힘 위치** (분석용) | `idle.at`, `undo.at`, `body_lost.at`·`reason`, `move.blockedBy` | 칸별 횟수 → 대시보드 히트맵 | 의도한 함정 칸과 실제 막히는 칸 비교. 의도하지 않은 곳에서 막히면 맵 수정 |

보조 신호:
- `level_start.via = again` 비율: 깬 스테이지를 다시 하는 비율. 높으면 재미 신호(작게 가산)
- `seventh_step.effect = vanish`, `body_lost.reason = fell`: 규칙을 잘못 이해한 흔적. 소개 스테이지에서 많으면 힌트 문구·맵 수정
- `keep`/`switch` 사용 비율 vs 솔버 해: SWITCH 필수 스테이지(4, 11)에서 SWITCH를 쓰기까지 걸린 시간 = SWITCH 이해도

## 3. 보정 절차

1. 내보내기(아래 형식)를 `reports/telemetry/`에 받는다(커밋 안 함)
2. 보정 스크립트가 스테이지별 D_obs, C_obs, A_obs, F_obs, I_obs를 계산해 `reports/level-calibration.json`으로 낸다
3. **D**: 자동 D의 다섯 항목(bodies, moves, search, rarity, traps)을 D_obs에 맞춰 음수 없는 최소제곱으로 가중치를 다시 구한다. 스테이지가 12개뿐이라 가중치 변화는 한 번에 ±0.1까지만 반영하고, 크게 벗어나는 스테이지(예: 발상형 7)는 `judgement.json`에 `dAdj`를 둔다
4. **A**: `judgement.json`의 A를 (판단 + A_obs) / 2로 바꾸고 근거에 "실측 반영 n명"을 적는다
5. **C·F·I**: I_obs와 모델 I의 순위 상관이 0.5 미만이면 C·F 상수를 조정해 다시 돌린다
6. `interest-curve.html`에 실측 I_obs를 점선으로 겹쳐 그린다(보정 전후 비교). 커밋 메시지에 상관계수 변화를 적는다

## 4. 내보내기 형식 요청 (백엔드/분석)

### 4.1 `attempts.csv` — 시도 한 줄 (가장 필요)

| 열 | 출처 / 계산 |
|---|---|
| `attemptId` | 시도 고유 키 (`sessionId` + `level_start`의 `seq`) |
| `playerId`, `sessionId`, `build`, `client` | 배치 머리 |
| `level`, `levelName`, `attempt`, `via`, `bodies` | `level_start` |
| `sessionFirstTime` | 그 세션 `session_start.firstTime` |
| `startT`, `endT` | 시도 시작·끝 이벤트 시각 |
| `outcome` | `clear` / `leave` / `quit` / `open`(아직 끝나지 않음) |
| `durationMs`, `moves`, `undos`, `restarts`, `hints`, `fails`, `bodiesUsed`, `minMoves` | `level_clear`·`level_leave` (quit이면 이벤트로 집계) |
| **`idleCount`, `idleMsTotal`, `idleMsMax`** | 시도 안의 `idle` 합계 |
| **`activeMs`** | `durationMs − idleMsTotal` |
| **`firstHintMs`** | 시도 시작부터 첫 `hint`까지 (없으면 빈칸) |
| **`bodiesWasted`**, `lostFell`, `lostVanish`, `lostStatue`, `lostSwitch`, `lostBridge`, `lostSeal` | `body_lost` 사유별 수 |
| `keeps`, `switches` | `keep`, `switch` 수 |
| **`lastStuckToClearMoves`, `lastStuckToClearMs`** | 마지막 막힘(30초 이상 idle 또는 연속 Undo 5회 이상)이 끝난 뒤 클리어까지의 이동 수·시간. 깨달음 A_obs용. 어렵다면 4.2 원본만 있어도 내가 계산함 |
| **`nextLevelStarted`** | 이 시도 뒤 같은 세션에서 다른 스테이지 `level_start`가 있었는지 (`toLevel` 포함) |
| `lang` (끝에서 둘째 열, server 725dac2) | 그 시도의 UI 언어(`level_start.lang`, 없으면 같은 세션의 직전 값). 보정은 언어별로 나눠 먼저 보고, 같은 스테이지에서 `ko`·`en` 활동 시간 중앙값이 2배 넘게 차이 나지 않으면 합친다(차이 나면 문구 문제로 따로 기록). 1차는 대부분 `ko` |
| `planet` (마지막 열, server 725dac2) | 그 스테이지 챕터의 행성 이름(ko, `chapters.json`). 행성별 집계(톱니 곡선, 행성별 ★3 비율 `playtest-1-prep-draft.md` 6장)의 묶음 키. 보정 스크립트는 열 이름으로 읽는다(열 위치에 기대지 않음) |

`levels` 내보내기에도 `planet` 열이 있다(기존 열 순서 그대로). 로컬 플레이테스트 서버·대시보드·내보내기 절차는 `docs/telemetry.md` 6장.

### 4.2 `events.jsonl` — 원본 이벤트 한 줄씩

- 저장된 이벤트 그대로 + 배치 머리 필드 + **`attemptId`** (스테이지 밖 이벤트는 빈값)
- 필터(빌드·클라이언트·기간)는 대시보드와 같게

### 4.3 공통

- `?build=…&client=…&since=…` 필터를 attempts·events 둘 다에 적용. `since`에 날짜(`YYYY-MM-DD`)만 주면 **서버 PC 로컬 시간 그날 0시**부터(server가 로컬 시간대로 고치는 중, 2026-11-10). 플레이테스트 당일이면 `since=$(date +%F)`
- CSV는 UTF-8(BOM 없음), 쉼표 구분, 헤더 한 줄. 스테이지 이름이 한글이므로 인코딩 고정
- 토큰 인증 그대로 사용 가능 (`curl -H "Authorization: Bearer …"`로 받을 수 있으면 됨)

### 4.4 스키마 제안 (선택, v1 호환)

- `level_start`에 선택 필드 `levelKey`(맵 문자열 해시 8자): 같은 빌드 안에서 맵을 고친 경우를 구분. 없으면 `build`로만 구분한다
