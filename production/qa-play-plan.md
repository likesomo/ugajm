# 플레이 QA: 레벨별 시도 횟수 (데이터 기반) — 2026-11-10

작성: 코디네이터. 사용자 지시: "레벨 디자인 제작이 완료되면 플레이 QA로 각 레벨의 시도 횟수를 확인한다. 캡처 중심이 아니라 computer use로 직접 확인하거나, Unity에서 결과를 JSON으로 뽑는 데이터 기반 방식을 쓴다."

## 현재 상태 (확인됨)
- 50스테이지, 7행성. 모든 스테이지가 솔버로 풀리고 몸 ≤ 3이다. levels.json은 최신이다(`design/levels/stage-report.md`, 7lucky 7d6dce1).
- 레벨 디자이너에게 남은 작업은 없다.

## 방식 개요
1. **QA 플레이 하네스**(텍스트·JSON, 이미지 없음)로 실제 규칙 엔진 위에서 플레이한다.
2. **블라인드 테스터 에이전트**가 해답을 모른 채 1→50 순서로 플레이한다. 모든 시도는 JSONL로 기록한다.
3. **Unity 교차 검증**: 테스터의 모든 시도 입력을 Unity(C# 코어 + GameSession)에서 batchmode로 다시 실행해 결과를 JSON으로 뽑고, 하네스 결과와 1:1로 비교한다.
4. **실제 빌드 확인(computer use)**: WebGL 빌드에서 표본 스테이지를 computer use로 직접 조작해 플레이한다. 텔레메트리 서버의 `attempts.csv`로 시도 기록이 같은지 확인한다(UI 경로 검증).
5. **집계 리포트**: 레벨별 시도 횟수와 실패 원인을 흥미도 모델 예측과 비교해 레벨 디자인에 넘긴다.

## 1. QA 플레이 하네스 — `unity/Tools/qa-play.mjs` (코어 담당, 리드 배분)
- 기반은 `prototypes/seven-steps/src/core.js`와 `levels.js`이고, 게임과 같은 규칙이다.
- 세션형 CLI다. 상태는 `reports/qa/sessions/<tester>.json`에 저장한다. 명령은 다음과 같다.
  - `start --tester T --level N`
  - `show`: 보드를 텍스트 격자 + 범례 + 남은 걸음, 몸 수, 현재 몸, 석상, 문 상태로 출력한다. `--json`이면 같은 내용을 JSON으로 낸다.
  - `move U|D|L|R` (여러 개 가능: `move RRDD`)
  - `keep`, `switch`, `undo`, `restart`, `hint`(게임의 H와 같은 의미, 사용 기록)
  - `status`
- **해답 노출 금지**:
  - 솔버, 최소 몸, 리플레이, `expected` 값을 출력하지 않는다.
  - `hint`는 게임의 힌트 문구만 보여 준다. 풀이 보기는 게임과 같게 하되 사용하면 기록하고 ★ 판정에 반영한다.
- **시도 기록** `reports/qa/attempts.jsonl`. 한 시도당 한 줄이고 텔레메트리 `attempts.csv`와 같은 의미다(`design/levels/telemetry-mapping.md` 4.1).
  - 필드: `tester`, `level`, `stageName`, `attempt` 번호, `outcome`
    - `outcome` 값: `clear` / `restart` / `fell` / `out_of_bodies` / `gave_up`
  - 필드: `inputs`(U D L R K X, Z는 undo 표시), `steps`, `bodiesUsed`, `undos`, `hints`, `stars`(클리어 시), `failReason`, `t`
- 단정적 검사: 잘못된 명령과 시작 안 한 세션은 즉시 오류를 낸다. 레벨 범위도 검사한다.

## 2. 블라인드 테스터 (코디네이터가 띄움)
- Claude 에이전트 3명, 성향을 다르게 둔다.
  - A: 신중형. 먼저 세고 계획한다.
  - B: 탐색형. 바로 시도하고 실패에서 배운다.
  - C: 초보형. 힌트 문구를 적극 사용한다.
- 작업 공간: 읽기 전용 QA 워크트리 `qa`.
- **금지 파일**: `design/levels/stage-report*`, `stage-plan-50*`, `batch*.md`, `judgement.json`, `all-brothers-check.json`(모든 형제 클리어 가능 여부·최소 걸음, 2026-11-11 추가), `experiments/`, `design/story/`(최소 몸 해 성질 포함, 2026-11-10 추가), `unity/Assets/_Project/Data/story.json`(design/story 복사본, 엔딩 갈래 포함; 생기면), 솔버 코드, levels.json의 `expected`. 브리프에 명시하고, 보고에 "금지 파일을 열지 않음"을 적게 한다.
- 규칙 학습은 게임 안 정보만 쓴다. 대상은 튜토리얼 문구, 스테이지 이야기와 힌트(`show`가 출력), GDD의 플레이어용 규칙 요약 1장이다.
- 1→50 순서로 진행한다. 레벨당 **최대 15시도**이고, 넘으면 `gave_up`으로 기록하고 다음으로 간다(난이도 경고 신호).
- 레벨마다 한 줄 메모를 남긴다(막힌 이유, 깨달은 순간). 위치: `reports/qa/notes-<tester>.md`.

## 3. Unity 교차 검증 — batchmode JSON (씬 플로우 또는 아키텍트 담당)
- `BuildTools.QaReplay`(EditMode 또는 batchmode `-executeMethod`)를 만든다. 입력은 `reports/qa/attempts.jsonl`이다.
- 각 시도의 입력을 C# 코어 + GameSession으로 재생한다. 결과(`outcome`, `steps`, `bodiesUsed`, `stars`, 최종 상태 해시)를 `reports/qa/unity-replay.json`로 뽑는다.
- JS 하네스와 다르면 실패로 처리하고 목록을 낸다. 규칙 불일치는 버그로 코어에 넘긴다.
- Unity MCP는 쓰지 않아도 된다. batchmode가 같은 데이터를 낸다. 필요하면 사용자 승인을 받아 설치한다.

## 4. 실제 빌드 확인 — computer use (코디네이터)
- 최종 WebGL 빌드 + 로컬 텔레메트리 서버를 쓴다.
- 표본은 행성별 첫 스테이지와 피날레 14개(1, 7, 8, 14, 15, 21, 22, 28, 29, 35, 36, 42, 43, 50)다.
- computer use로 키보드 입력을 직접 해서 플레이한다. 확인 항목:
  - 서버 `attempts.csv`의 시도 수와 outcome이 실제 조작과 같은지
  - 별, 재시작, 되돌리기, 힌트 카운트가 맞는지
- 캡처는 증거 보조로만 남긴다. 판정은 데이터(attempts.csv와 JSON)로 한다.

## 5. 집계 리포트 — `reports/qa/qa-report.json` + `design/levels/qa-1.md` (레벨 디자인 담당)
- 레벨별 지표:
  - 테스터별 시도 횟수, 평균, 최대
  - 첫 시도 클리어율
  - `gave_up` 수
  - 실패 원인 분포
  - 평균 입력 수와 최소 몸 해 대비 비율
  - ★ 분포, 힌트 사용률
- 흥미도 모델 예측 난이도와 비교한다(`interest-model.md`). 다음 경우를 경고 목록으로 낸다.
  - 시도가 예측보다 2배 이상
  - `gave_up`
  - 너무 쉬움: 모든 테스터가 1회
  - 행성 첫 스테이지가 직전 피날레보다 어려움
- 곡선 HTML(`interest-curve-50.html`)에 실측 시도 수 선을 겹친다.
- 한계: AI 테스터는 사람과 다르다. 사람 플레이테스트 1차(현장, 회전 배치)의 사전 신호로만 쓰고, 이 사실을 리포트에 적는다.

## 순서
1. 리드가 하네스(코어)와 QaReplay(씬 플로우/아키텍트)를 배분한다. 리뷰는 아키텍트가 한다.
2. 하네스 병합 뒤 코디네이터가 `qa` 워크트리와 테스터 3명을 띄운다.
3. 테스트가 끝나면 QaReplay로 교차 검증한다. 코디네이터는 computer use로 표본을 확인한다.
4. 레벨 디자인이 리포트를 쓰고 보정안을 낸다. 리드와 사용자가 승인한다.
