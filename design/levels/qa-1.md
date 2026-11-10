# 플레이 QA 1차 — 레벨별 시도 횟수 (AI 블라인드 테스터)

작성: 레벨 디자인. 계획 `production/qa-play-plan.md` 5절, 시도 형식 `docs/qa-attempts.md`(코어). 상태: **틀만 준비, 테스트 데이터 대기**.

## 0. 이 문서를 읽기 전에 — AI 테스터의 한계

- 테스터 3명(A 신중형, B 탐색형, C 초보형)은 Claude 에이전트다. 텍스트 격자(`qa-play.mjs show`)로 보고, 3D 보드·애니메이션·소리·조작감은 겪지 않는다. **사람 플레이테스트 1차(현장, 회전 배치 `playtest-guide.md` 2.1)의 사전 신호로만** 쓴다
- 사람과 다르게 나올 것으로 예상하는 점:
  - 걸음 수 세기·경로 계획은 사람보다 쉽다 → "너무 쉬움" 경고는 사람에게 그대로 옮기지 않는다
  - 공간 직관(얼음 미끄러짐, 레버로 바뀌는 벽, 금 간 바닥의 순서)은 텍스트 격자라 사람보다 어려울 수 있다 → 해당 행성의 "너무 어려움"은 범례·표시 문제인지 먼저 본다
  - 지루함·짜증·포기 의사가 없다. 포기는 15회 상한에서 기계적으로 나온다
  - 3명은 표본이 아니다. 보정(`telemetry-mapping.md` 3장)에는 쓰지 않고, **어디를 사람 테스트에서 집중해 볼지** 고르는 데 쓴다
- 예측 시도 수 = 1 + 5 × D (모델 D, `interest-model.md`). 시도 수를 예측한 적이 없어 임시로 둔 식이다. 이번 결과로 식을 고치고, 고친 식은 다음 QA부터 쓴다
- 금지 파일(`stage-report*`, `stage-plan-50*`, `batch*.md`, `judgement.json`, `experiments/`, 솔버, `expected`)을 열지 않았다는 테스터 보고: A ☐ B ☐ C ☐

## 1. 만드는 법

```bash
export PATH="/c/Program Files/nodejs:$PATH"
node prototypes/seven-steps/tools/level-metrics.mjs --json reports/level-metrics-50.json --html design/levels/interest-curve.html   # 모델 예측
node prototypes/seven-steps/tools/qa-report.mjs --metrics reports/level-metrics-50.json      # → reports/qa/qa-report.json, 아래 2장 블록
node prototypes/seven-steps/tools/level-metrics.mjs --json reports/level-metrics-50.json --html design/levels/interest-curve-50.html \
  --title "50스테이지 + QA 1차 실측" --qa reports/qa/qa-report.json                           # 곡선에 실측 시도 선
```
- 입력: `reports/qa/attempts.jsonl`(한 시도 한 줄), `reports/qa/notes-<tester>.md`(스테이지 번호로 시작하는 줄을 메모로 모음), Unity 교차 검증 `reports/qa/unity-replay.json`(불일치가 있으면 그 시도는 버그로 코어에 넘기고 집계에서 뺀다)
- `attempts.jsonl`에 형식 오류(JSON 아님, 모르는 outcome, 스테이지 이름이 이 빌드와 다름)가 있으면 스크립트가 멈춘다

**지표 정의**
| 지표 | 계산 |
|---|---|
| 시도 횟수 | 테스터·스테이지별 시도 줄 수(`attempt` 최댓값과 큰 쪽). 재시작·추락·몸 소진이 각각 한 시도 |
| 첫 시도 클리어 | 그 스테이지 첫 시도의 outcome = `clear`인 테스터 비율 |
| 포기 | `gave_up`(15회 초과)이 있는 테스터 수 |
| 실패 원인 | `clear`가 아닌 시도의 `outcome[:failReason]` 개수 |
| 입력/최소 | 클리어한 시도의 입력 수(U D L R K X, 되돌리기 제외) ÷ 최소 몸 해의 입력 수(`stage-report.json` minInputs) |
| ★ 분포 | 테스터별 클리어 시도의 별 |
| 풀이 보기 | `hints > 0`인 시도가 있는 테스터 비율 |

**경고**: 포기 / 평균 시도 ≥ 예측 × 2 / 모든 테스터가 1회에 클리어 / 행성 첫 스테이지(8, 15, 22, 29, 36, 43)의 평균 시도 > 직전 피날레(7, 14, 21, 28, 35, 42)

## 2. 결과 (자동 생성)

<!-- qa:begin (tools/qa-report.mjs가 다시 씀. 이 블록 밖에만 손으로 적는다) -->
아직 데이터 없음.
<!-- qa:end -->

## 3. 해석과 보정안 (데이터가 온 뒤 손으로)

- 경고별 원인 (맵 / 힌트 문구 / 텍스트 표시 한계 / 규칙 오해)
- 사람 플레이테스트 1차에서 집중해 볼 스테이지
- 맵·문구 수정안 (리드·사용자 승인 뒤에만 반영)
- 예측 시도 식 수정안
