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
| `level` | int | 스테이지 번호, 1부터 |
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
| `stars` | int\|null | clear일 때만 `stars(bodiesUsed, expected.minBodies, levelHints, levelUndos, levelRestarts)` (C# `Stars.Of`), 그 밖에는 `null` |
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
- 솔버, `expected`, 최소 몸, 리플레이는 출력하지 않는다. 클리어하면 별은 보여 준다(게임과 같음).
- 15회 제한은 테스터 규칙이다. 하네스가 강제하지 않는다. 넘으면 테스터가 `giveup`한다.
