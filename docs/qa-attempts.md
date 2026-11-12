# QA 시도 기록 형식 `reports/qa/attempts.jsonl` (v1)

작성: 코어, 2026-11-10. 계획: `production/qa-play-plan.md` 1절(하네스)과 3절(Unity 교차 검증).

이 문서는 다음 셋이 함께 따르는 계약이다.
- 쓰는 쪽: JS 하네스 `unity/Tools/qa-play.mjs` (코어)
- 다시 실행하는 쪽: C# `BuildTools.QaReplay` (scene-flow, `unity.sh qa-replay`)
- 집계: `prototypes/seven-steps/tools/qa-report.mjs` (level-design)

형식을 바꾸면 `v`를 올리고 모두 같이 고친다. 기준 구현은 `qa-play.mjs`의 `replayLine()`이고, 테스트는 `node --test unity/Tools/qa-play.test.mjs`다.

## 1. 파일

- UTF-8, BOM 없음. 한 줄에 JSON 객체 하나이고 줄 끝은 `\n`이다.
- 덧붙이기만 한다. 줄을 고치거나 지우지 않는다.
- 테스터 여러 명이 같은 파일에 쓴다. 줄 순서는 테스터별로만 의미가 있다.
- 위치: 기본은 `<repo>/reports/qa/`. 환경 변수 `QA_DIR`로 바꿀 수 있다.
- 세션 상태 `reports/qa/sessions/<tester>.json`은 하네스 내부 파일이라 형식을 보장하지 않는다.

## 2. 용어

| 용어 | 뜻 |
|---|---|
| 스테이지 세션 | `start --level N`부터 그 스테이지의 `clear` 또는 `gave_up`까지. 게임의 시도(LoadStage부터 클리어까지, `BoardController.attempt` / `AttemptCounts`)와 같다. **별 계산의 카운터는 이 범위로 센다.** |
| 시도(줄) | 4절의 경계로 끝나는 한 구간. `attempts.jsonl` 한 줄이 시도 하나다. |

## 3. 입력 토큰 `inputs`

글자 하나가 명령 하나다.

| 토큰 | 명령 | 상태 변화 |
|---|---|---|
| `U` `D` `L` `R` | 이동 (`Rules` Move) | 있음 |
| `K` | KEEP | 있음 |
| `X` | SWITCH | 있음 |
| `Z` | 되돌리기 (Undo) | 있음 (`prev`로 돌아감) |
| `!` | 재시작 (Restart) | 있음 (`newState`) |
| `H` | 풀이 보기·힌트 사용 (게임의 H) | **없음**. 세기만 한다 |

- **`inputs`는 스테이지 세션 시작부터 이 줄이 끝날 때까지의 토큰 전체다.** 그래서 줄 하나만으로 `newState(level)`에서 다시 실행할 수 있다. 같은 스테이지 세션의 다음 줄은 앞 줄의 `inputs`로 시작한다.
- `from`은 `inputs`에서 이 시도의 첫 토큰 위치다. `inputs[from:]`이 이 시도의 몫이다.
- 기록되는 토큰은 모두 실제로 효과가 있었던 명령이다.
  - 막힌 이동(bump)은 기록하지 않는다.
  - 되돌릴 것이 없을 때의 `Z`, 선택 상태가 아닐 때의 `K`/`X`는 하네스가 오류로 거절하므로 기록되지 않는다.
  - `!`는 **게임이 재시작으로 세는 경우에만** 기록한다. 현재 상태에 `prev`가 있을 때다(게임 `AttemptCounts`와 같음). 막 시작했거나 막 재시작한 상태에서 하는 재시작은 세지도 기록하지도 않는다.
  - 재생하는 쪽은 `U D L R K X Z` 토큰마다 상태가 바뀌었는지 확인한다(`ReferenceEquals(r.State, before)`이면 불일치). `!`는 `prev`가 없는 상태에서 나오면 불일치다.
- 하네스의 H는 **스테이지 힌트 문구**(levels.js `hint`)만 보여 준다. 게임의 H는 솔버 경로를 보여 주지만, 블라인드 QA라서 다르게 한다. 세는 방식과 별 영향(★1로 제한)은 게임과 같다.

## 4. 시도 경계와 `outcome`

| `outcome` | 언제 줄을 쓰나 | 최종 `status` (Unity `open` = 둘 다 아님) |
|---|---|---|
| `clear` | 출구에 7번째 걸음으로 도착 (`win`). 스테이지 세션이 끝난다 | `win` |
| `fell` | 상태가 `lose`가 되었고, 그 명령의 이벤트에 `fall`이 있다(마지막 형제가 1~6번째 걸음에 구멍에 떨어짐) | `lose` |
| `out_of_bodies` | 상태가 `lose`가 되었고 `fall`은 없다(마지막 형제가 선돌·다리·굳은 바닥이 되거나 출발점에서 사라짐) | `lose` |
| `restart` | `restart` 명령. 이 시도에 `!` 말고 다른 토큰이 1개 이상 있을 때만 줄을 쓴다. **줄의 `inputs`는 재시작 직전에서 끝난다**(재시작 전 상태가 최종 상태). 기록되는 `!`는 다음 시도의 첫 토큰이 된다 | `play`/`choose` → Unity `open` |
| `gave_up` | `giveup` 명령. 스테이지 세션이 끝난다. **항상 별도 줄이다**(15번째 시도의 outcome을 바꾸지 않는다) | `win` 말고 무엇이든 → Unity는 `lose`면 그 판정, 아니면 `open` |

- **포기 표시 줄**: `gave_up` 줄 중 `steps + keeps + switches + undos + hints == 0`인 줄(앞 줄 뒤로 재시작 말고는 아무것도 하지 않음)은 시도가 아니라 포기 표시다. 시도 수에 넣지 않는다.
- 1~6번째 걸음에 구멍에 떨어져도 형제가 남아 있으면 경계가 아니다. 다음 형제가 깨어나고 같은 시도가 이어진다. 게임도 그렇다.
- `lose` 뒤에 할 수 있는 명령: `undo`(같은 판을 이어 가는 새 시도), `restart`, `giveup`.
- Unity 재생은 `restart`/`gave_up` 줄을 최종 상태로 `open`(또는 `lose`)으로 본다. 표의 마지막 열과 맞으면 같다.

## 5. 필드

qa-play-plan 1절의 이름(`tester`, `level`, `stageName`, `attempt`, `outcome`, `inputs`, `steps`, `bodiesUsed`, `undos`, `hints`, `stars`, `failReason`, `t`)을 그대로 쓴다.

| 필드 | 형 | 뜻 |
|---|---|---|
| `v` | int | 형식 버전. 지금은 `1` |
| `tester` | string | 테스터 ID. `[A-Za-z0-9_-]{1,32}` |
| `level` | int 또는 string | 스테이지 번호, 1부터. 튜토리얼 보드는 문자열 `"T1"` `"T2"` `"T3"` (10절, 2026-11-13). C# `QaReplay`는 `"T2"`를 -2로 읽는다 |
| `stageName` | string | levels.js `name` (ko, 언어와 상관없이 같은 키). 재생 쪽은 levels.json 이름과 다르면 불일치 |
| `chapter` | string | 챕터 id (`moss` …) |
| `mapHash` | string | 맵 행을 `\n`으로 이은 문자열의 SHA-256 앞 16 hex. 참고용이며 재생 쪽 비교는 선택 |
| `lang` | string | 테스터가 본 문구의 언어 `ko`/`en` |
| `attempt` | int | 이 테스터·스테이지의 시도 번호. 1부터, 파일 전체(이전 스테이지 세션 포함)에서 센다 |
| `outcome` | string | 4절 |
| `failReason` | string\|null | `fell`/`out_of_bodies`일 때 마지막 형제를 잃은 이벤트: `fall` `bridge` `seal` `vanish` `stone`. 그 밖에는 `null` |
| `inputs` | string | 3절. 스테이지 세션 시작부터 |
| `from` | int | 이 시도의 첫 토큰 위치 |
| `steps` | int | `inputs[from:]`의 `U D L R` 수 = 적용된 이동 수. 막힘·K·X 제외, 나중에 되돌린 이동 포함 |
| `keeps` / `switches` | int | `inputs[from:]`의 `K` / `X` 수 |
| `undos` / `hints` | int | `inputs[from:]`의 `Z` / `H` 수 |
| `levelUndos` `levelHints` `levelRestarts` | int | `inputs` 전체의 `Z` / `H` / `!` 수. 스테이지 세션 시작부터의 누계이며 게임 `AttemptCounts`와 같다 |
| `bodiesUsed` | int | 지금 판(마지막 `!` 뒤)에서 쓴 형제 수 = `bodies − 남은 형제 + 1`. 패배(`lose`)면 스테이지 몸 수 전체(`bodies`). C#: `Math.Min(Stars.BodiesUsed(s), level.Bodies)` |
| `stars` | int\|null | clear일 때만 `stars({ bodiesUsed, bodies, hints: levelHints })` (C# `Stars.Of(bodiesUsed, level.Bodies, levelHints)`, design/levels/stars.md 0: ★2 힌트 없음, ★3 형제 모두), 그 밖에는 `null`. 2026-11-11 이전 줄의 `stars`는 B안(최소 몸·Z/R) 값이다 |
| `stateKey` | string | 최종 상태 키 (6절) |
| `stateHash` | string | `stateKey`의 해시 (6절) |
| `startT` / `t` | string | 시도 시작·끝 시각 (ISO 8601 UTC). 비교 대상이 아니다 |

## 6. 최종 상태 키와 해시

- `stateKey`는 C# `Rules.Key(state)`와 같은 문자열이다. `core.js keyOf()`와 필드 순서가 같고, 상태만 enum 번호로 쓴다(Play 0, Choose 1, Win 2, Lose 3).

```
start|pos|used|bodies|statues|bridges|broken|status|flipped
```

  - `start`, `pos`: 칸 번호 `y*w + x`. `w`는 가장 긴 행의 길이다.
  - `used`: 현재 형제가 쓴 걸음, 0~7. `bodies`: 남은 형제 수.
  - `statues` `bridges` `broken`: 칸 번호를 오름차순으로, 쉼표로 잇는다(공백 없음). 비면 빈 문자열.
  - `flipped`: `1` 또는 `0`.
  - 예: `11|17|6|3||||0|0`
- `stateHash` = `stateKey`의 UTF-8 바이트에 대한 **FNV-1a 32비트**(offset 2166136261, prime 16777619), 소문자 16진 8자리.
  - C# `QaReplay.StateHash`와 같다.
  - 참고값: `keyHash("a") = e40c292c`.
- **골든값 (JS와 C#가 바이트 단위로 같은지 확인)**: 스테이지 29에 `inputs = "R!HDURRRRRKZKDDR"`를 3·7절대로 적용하면 다음이 나와야 한다.
  - `stateKey = 10|29|3|2|15|||0|1` (선돌 1개, 레버가 당겨진 상태)
  - `stateHash = e57f027f`
  - JS는 `qa-play.test.mjs`의 golden 테스트가 이 값을 고정하고, C#은 scene-flow EditMode 테스트가 같은 값을 확인한다.
  - 스테이지 29 맵이나 규칙이 바뀌면 두 테스트를 함께 고친다.

## 7. 재생 절차 (QaReplay가 할 일)

줄마다 다음을 한다.

1. `level`의 levels.json 스테이지를 연다. `stageName`이 다르면 불일치다.
2. `newState(level)`에서 `inputs` 토큰을 차례로 적용한다.
   - `U D L R`: `Move`
   - `K`/`X`: `Keep`/`Switch`
   - `Z`: `Undo`
   - `!`: `Restart`
   - `H`: 상태를 바꾸지 않고 힌트 수만 센다
   - 효과 없는 토큰(3절)이 있으면 불일치다.
3. 비교한다.
   - `stateKey`, `stateHash`
   - 최종 상태가 4절 표의 `outcome`과 맞는지(`fell`과 `out_of_bodies`는 마지막 명령의 `Fall` 이벤트로 가른다)
   - `failReason`(fell/out_of_bodies일 때, 마지막 명령에서 형제를 잃은 이벤트)
   - `bodiesUsed`
   - `levelUndos`/`levelHints`/`levelRestarts`(= 전체 `inputs`의 `Z`/`H`/`!` 수)
   - `stars`(clear일 때 `Stars.Of`)
   - `steps`/`keeps`/`switches`/`undos`/`hints`(`inputs[from:]`에서 센다)

JS 기준 구현: `node unity/Tools/qa-play.mjs verify`가 `attempts.jsonl` 전체를 이 절차로 다시 검사한다.

## 8. 예

```json
{"v":1,"tester":"A","level":1,"stageName":"일곱 걸음","chapter":"moss","mapHash":"6428e42a396ce53f","lang":"ko","attempt":1,"outcome":"gave_up","failReason":null,"inputs":"RRRRRRH","from":0,"steps":6,"keeps":0,"switches":0,"undos":0,"hints":1,"levelUndos":0,"levelHints":1,"levelRestarts":0,"bodiesUsed":1,"stars":null,"stateKey":"11|17|6|3||||0|0","stateHash":"d975950e","startT":"2026-11-10T13:27:54.155Z","t":"2026-11-10T13:27:54.482Z"}
```

## 9. 하네스 사용법 (테스터용 요약)

```bash
node unity/Tools/qa-play.mjs start  --tester A --level 1 [--lang ko|en]
node unity/Tools/qa-play.mjs show   --tester A [--json]
node unity/Tools/qa-play.mjs move   --tester A RRDD      # 첫 막힘에서 멈춘다
node unity/Tools/qa-play.mjs keep|switch|undo|restart|hint|giveup|status --tester A
node unity/Tools/qa-play.mjs verify                      # attempts.jsonl 전체 재검사
```

- 잘못된 명령, 시작하지 않은 세션, 범위 밖 스테이지, 진행 중인데 다른 `start`를 하면 `error: …`를 내고 종료 코드 2로 끝난다.
- 솔버, `expected`, 최소 몸, 리플레이는 출력하지 않는다. 클리어하면 별과 별 설명은 보여 준다(게임과 같음). 2026-11-13부터 하네스 문구는 naive(10.2).
- 15회 제한은 테스터 규칙이다. 하네스가 강제하지 않는다. 넘으면 테스터가 `giveup`한다.

## 10. QA-3: 튜토리얼 보드, 프롤로그, naive 문구 (2026-11-13)

요청: `shared-assets/seven-steps/reports/user-request-2026-11-13-qa3-tutorial.md`. 7걸음·3형제 규칙을 모르는 테스터로 "튜토리얼 뒤에 시도 수가 줄어드는가"를 본다. 대조군(튜토리얼 없음)은 스테이지 1~14, 튜토리얼군은 `story prologue` → T1 → T2 → T3 → 스테이지 1~14.

### 10.1 명령

```bash
node unity/Tools/qa-play.mjs story prologue [--lang ko|en]        # 프롤로그 7장 (튜토리얼군만)
node unity/Tools/qa-play.mjs story epilogue [--lang ko|en]        # 에필로그 (선택)
node unity/Tools/qa-play.mjs start --tester A1 --level T1 [--lang ko|en]   # T1, T2, T3 (t1도 됨)
node unity/Tools/qa-play.mjs start --tester A0 --level 1  [--lang ko|en]   # 스테이지 1~50
node unity/Tools/qa-play.mjs show|status --tester A1 [--json]
node unity/Tools/qa-play.mjs move --tester A1 RRDD
node unity/Tools/qa-play.mjs keep|switch|undo|restart|hint|giveup --tester A1
node unity/Tools/qa-play.mjs verify
```

- `story`는 `--tester`가 필요 없고 기록을 남기지 않는다. 출력은 `Data/cutscenes.json`의 페이지 순서 그대로 `[k/n]` 다음에 그 페이지의 줄들(게임 `U5CutsceneView`가 한 페이지에 보여 주는 줄).
- 튜토리얼 보드 줄: `level`이 `"T1"`..`"T3"`, `stageName`은 보드 이름(`첫 일곱 걸음` 등), `chapter` `moss`. 나머지 필드·경계·재생은 1~9절과 같다. `attempt`는 테스터·보드별로 센다.
- 튜토리얼 보드에서 `lose`가 나면 게임처럼 **자동 재시작**한다: 패배 줄(`fell`/`out_of_bodies`)을 쓰고, `!`를 기록(levelRestarts +1)하고, 다음 시도가 바로 시작된다(lose cue를 보여 준 뒤 restart cue). 테스터가 `restart`를 칠 필요가 없다.
- 튜토리얼 보드에는 `hint`가 없다(게임도 H 없음): `error: no hint on a tutorial board`. 클리어 화면·별도 없다(줄의 `stars`는 형식대로 기록하지만 출력하지 않는다).
- 집계 `qa-report.mjs`는 `"T…"` 줄을 건너뛴다(스테이지 보고서에 넣지 않음, 건너뛴 수만 출력). 튜토리얼 줄은 코디네이터가 따로 센다.
- 판정: cue 고르기는 `qa-play.mjs`의 `matchCue`(C# Core `TutorialCues`와 같은 규칙, docs/tutorial.md 2.3, 같은 시퀀스를 node·EditMode 테스트가 고정). cue 데이터는 `Data/levels.json` `tutorials`(게임이 읽는 것).

### 10.2 naive (기본이자 유일한 모드)

하네스가 스스로 하는 말(`help`, 범례, 상태, 오류, 사건 목록)은 **규칙을 설명하지 않는다**. 규칙에 닿는 문장은 게임이 그 순간 화면에 띄우는 글뿐이고, 하네스는 그 글을 `levels.js`·`Data/Strings/*.json`·`Data/cutscenes.json`에서 그대로 가져온다. QA-1 때의 설명형 문구(범례의 "그 번째 걸음으로만", "발판에 선돌이 서면 열림", "Q 문은 선돌이 정확히 N개일 때 열림", 사건의 "출구에 7번째 걸음으로 도착", "형제가 다리가 됨", 힌트의 "★1까지만", 선택 오류의 "7번째 걸음 뒤에만")는 모두 지웠다. node 테스트 `naive: …`가 하네스 몫의 글(게임 글을 빼고 남은 것)에 규칙 낱말이 없는지 검사한다.

### 10.3 조건별로 보이는 글

| 언제 | 대조군 (스테이지만) | 튜토리얼군 | 출처 |
|---|---|---|---|
| 시작 전 | 없음 | `story prologue` 7장 (아래 10.4) | `Data/cutscenes.json` prologue |
| `help` | 명령 목록만 | 같음 | 하네스 |
| 스테이지 `start`/`show` 머리 | `[n/50] 이름 (챕터)` + `시작 문구:` 스테이지 시작 줄 (`startLine`: 기본 story, 4·8은 hint) | 같음 | levels.js `story`/`hint`, 게임 `StoryDirector.StartText` |
| 스테이지 `화면 문구:` | HUD 메시지 줄: 마지막 사건의 `msg.*` (아래), 6번째 걸음 뒤 `msg.lucky_next`, 다른 걸음은 빈 줄, 처음 얼음에서 미끄러질 때 한 번 `tutorial.slide` | 같음 | `Data/Strings` (게임 `MessagePresenter`) |
| 보드 `안내:` | 해당 없음 | 보드의 cue 줄 + `[빛나는 칸 (x,y)]` (아래 10.5). 보드에서는 HUD 메시지 줄이 꺼진다(게임과 같음) | levels.json `tutorials[].cues` |
| 범례 | 칸 글자 이름만: `@ 지금 형제 * 선돌 S 출발점 E 출구 # 벽 . 바닥 o 구멍 ~ 얼음 x 금 간 바닥 m 진흙 / a-c 발판 A-C 문 1-7 숫자가 적힌 문 Q 숫자가 적힌 문(Q 숫자 참고) L 레버 R 올라온 벽 r 내려간 벽` | 같음 | 하네스 (게임은 그림) |
| 상태 줄 | `진행 중` / `KEEP 또는 SWITCH` / `클리어` / `남은 형제 없음 (undo / restart / giveup)`, `걸음 n/7`, `형제 k번째 / N (남은 형제 m)`, 위치, 출발점, 서 있는 선돌 수, `Q 문 위의 숫자: N`, 문 열림/닫힘, 이번 형제 발자국 | 같음 | HUD에 보이는 수 |
| 고르기 상태 | `keep: 선돌을 남기고 처음부터` / `switch: 여기서 다음 형제가 이어 감` | 같음 | `ui5.choice.keep/switch` (U5 버튼 설명) |
| 사건 목록 | 보이는 사실만: `걸음 n (x,y)`, `얼음에서 미끄러짐`, `구멍 (x,y)에 떨어짐`, `구멍 (x,y)에 다리가 생김`, `선돌 (x,y)`, `(x,y)가 새 출발점`, `다음 형제가 (x,y)에서 깨어남`, `문 (x,y) 열림`, `출구 (x,y): 클리어`, `남은 형제 없음` 등 | 같음 | 하네스 |
| 고르기 중 `move` | `error: 먼저 고르자: K = KEEP (선돌), X = SWITCH (새 출발점).` | 같음 | `msg.choose_nudge` |
| 걷는 중 `keep`/`switch` | `error: keep: 지금은 고를 수 없다 (진행 중)` | 같음 | 하네스 |
| `hint` (스테이지) | 스테이지 힌트 줄 + `(힌트 사용 n회 기록. 풀이를 보면 이번 판은 별 최대 1개.)` | 같음. 보드에서는 `error: no hint on a tutorial board` | levels.js `hint`(게임 H 첫 단계 Principle), `hint.confirm` 첫 문장 |
| 스테이지 클리어 | `클리어! ★★☆ (시도 n)` + `★ 클리어` / `★ 풀이를 보지 않고` / `☆ 형제 3명을 모두 써서` | 같음 | `result.star1..3` (U5 클리어 화면 별 설명) |
| 보드 클리어 | 해당 없음 | `튜토리얼 Tn 클리어 (시도 n)` + `안내:` win cue. 별 없음 | cue |
| 스테이지 패배 | `시도 n 기록: fell 또는 out_of_bodies. undo 로 이어 하거나 restart / giveup` + 화면 문구 `msg.lose` | 같음 | `msg.lose` |
| 보드 패배 | 해당 없음 | `시도 n 기록: …` + `안내:` lose cue + `(자동 재시작)` 뒤 restart cue | cue |

HUD 메시지 줄 `msg.*` (ko, 게임 그대로. 규칙에 닿는 글은 이것들이다):
`msg.choose` 행운의 7번째 걸음. 이 형제는 무엇을 할까? · `msg.stone` KEEP. 형제가 선돌이 되었다. 다음 형제는 출발점에서 깨어난다. · `msg.switch` SWITCH. 이 자리가 새 출발점이 되었다. · `msg.bridge` 7번째 걸음으로 구멍에 뛰어들었다. 형제가 다리가 되었다. · `msg.seal` 금 간 바닥 위에서 선돌이 되었다. 바닥이 영원히 굳었다. · `msg.fall` 구멍에 떨어졌다. 7번째 걸음이 아니면 아무것도 남지 않는다. · `msg.vanish` 출발점에서는 선돌이 될 수 없다. 형제 하나를 잃었다. · `msg.crumble` 금 간 바닥이 무너졌다. · `msg.win` 7번째 걸음으로 출구에 닿았다! · `msg.lose` 형제를 모두 잃었다. Z로 되돌리거나 R로 다시 시작. · `msg.undo` 한 걸음 되돌렸다. · `msg.restart` 처음부터. (힌트 본 판이면 `msg.restart_hinted` 힌트를 본 판은 다시 해도 별 최대 1개.) · `msg.flip` 7번째 걸음으로 레버를 당겼다. 벽이 뒤집혔다. · `msg.lucky_next` 다음 걸음이 행운의 7번째 걸음이다. · `tutorial.slide`(처음 한 번) 얼음에선 멈출 곳까지 미끄러져요: …
(en은 `Data/Strings/en.json`의 같은 키.)

스테이지 1~14 시작 문구(ko): 1 하나부터 센다. 일곱째에 열린다. / 2 문이 코앞이어도, 별은 아직이다. / 3 길은 많아도, 일곱 걸음 길은 하나. / 4 (hint) 7걸음을 다 쓰면 고른다. KEEP: 그 자리에 선돌을 남긴다. 선돌은 발판을 계속 눌러 준다. / 5 발판은 서 있는 이를 기다린다. / 6 붙든 돌이, 지름길도 막는다. / 7 마지막 문 너머에서 길이 시작된다. / 8 (hint) SWITCH: 선돌을 남기지 않는 대신, 지금 선 자리가 다음 형제의 출발점이 된다. / 9 혼자는 못 가도, 셋은 잇는다. / 10 일곱째 걸음에만 구멍이 다리가 된다. / 11 다리가 될까, 돌이 될까. / 12 건넌 자리에서, 다음이 깨어난다. / 13 발밑의 별이 더 깊어진다. / 14 구멍 너머, 다음 별이 비친다.

게임과 다른 점(알고 쓰는 차이): 시작 문구는 게임에서 한 번 뜨지만 하네스는 `show`마다 보인다. 로딩 화면 팁(`loading.tip.*`), 첫 이동·첫 고르기 1회 안내(`move@1`, `choose@1`)는 하네스에 없다. 게임 H는 단계별 풀이를 열지만 하네스 `hint`는 첫 단계(힌트 줄)만 보이고 매번 센다(QA-1과 같음).

### 10.4 프롤로그 (`story prologue`, ko)

`Data/cutscenes.json` 그대로(`design/story/cutscenes.md` 4절의 표와 같다). 3~6장이 규칙을 말한다: 3 "한 걸음마다 하늘에 별이 하나씩 켜진다. / 일곱째 별이 켜질 때에만 땅이 귀를 기울인다." 4 "그래서 일곱 번째 걸음이 닿는 곳만 바뀐다. …" 5 KEEP/선돌, 6 "닿으면 별 하나, 스스로 세면 둘, 셋이 모두 걸으면 셋."

### 10.5 보드 cue (ko / en, `on`, 빛나는 칸)

T1 첫 일곱 걸음 (형제 1): start 방향키로 한 칸씩 걷는다. 한 걸음마다 별이 하나 켜진다. · step 1 하나. 걸음을 세어 보자. · step 6 다음이 일곱째 걸음. 출구에 닿아야 한다. [(2,3) 출구] · win 일곱째 걸음에 닿아서 문이 열렸다. · lose 일곱 걸음이 출구에서 끝나야 한다. · restart 다시. 출구까지 딱 일곱 걸음.

T2 돌이 붙드는 길 (형제 2): start 문은 발판이 눌려야 열린다. 발판에서 일곱째 걸음을 마치자. [(5,2) 발판] · step 5 (첫째) 일곱째가 아닌 걸음은 발판을 밟아도 아무 일도 없다. · choose KEEP을 고르면 이 자리에 남아 선돌이 된다. [(5,2)] · switch SWITCH는 아무것도 남기지 않는다. 다시 해 보자. · doorOpen 선돌이 문을 열어 두었다. 다음 형제는 출구까지 딱 일곱 걸음. [(4,5) 출구] · win 일곱째 걸음만 일을 한다. 기억하자. · lose 형제를 다 썼다. 처음부터. · restart 발판에서 일곱째 걸음을 마치고 KEEP. [(5,2)]

T3 셋이 함께 (형제 3): start 문이 둘. 형제 하나가 발판 하나를 붙든다. [(5,2)] · respawn (둘째) 둘째 형제. 남은 발판으로. · respawn (셋째) 막내 차례. 열린 문을 지나 출구로. [(3,6) 출구] · win 셋이 모두 걸었다. 별 셋. · lose 형제를 다 썼다. 처음부터. · restart 발판 둘에 형제 둘, 출구에 막내.

en 줄은 `Data/levels.json` `tutorials[].cues[].text`. 보드 데이터를 바꾸면 이 절을 같이 고친다.
