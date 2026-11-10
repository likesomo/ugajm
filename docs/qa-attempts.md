# QA 시도 기록 형식 `reports/qa/attempts.jsonl` (v1)

작성: 코어, 2026-11-10. 계획: `production/qa-play-plan.md` 1절(하네스)과 3절(Unity 교차 검증).

이 문서는 다음 둘이 함께 따르는 계약이다.
- 쓰는 쪽: JS 하네스 `unity/Tools/qa-play.mjs` (코어)
- 다시 실행하는 쪽: C# `BuildTools.QaReplay` (scene-flow)

형식을 바꾸면 `v`를 올리고 두 쪽을 같이 고친다. 기준 재생 구현은 `qa-play.mjs`의 `replayLine()`이고, 테스트는 `unity/Tools/qa-play.test.mjs`다.

## 1. 파일

- UTF-8, BOM 없음. 한 줄에 JSON 객체 하나이고 줄 끝은 `\n`이다.
- 덧붙이기만 한다. 줄을 고치거나 지우지 않는다.
- 테스터 여러 명이 같은 파일에 쓴다. 줄 순서는 테스터별로만 의미가 있다.
- 위치: 기본은 `<repo>/reports/qa/`. 환경 변수 `QA_DIR`로 바꿀 수 있다.
- 세션 상태는 `reports/qa/sessions/<tester>.json`이다. 하네스 내부 파일이라 형식을 보장하지 않는다.

## 2. 용어

| 용어 | 뜻 |
|---|---|
| 스테이지 세션 | `start --level N`부터 그 스테이지의 `clear` 또는 `gave_up`까지. 게임의 시도(LoadStage부터 클리어까지, `BoardController.attempt`)와 같다. **별 계산의 카운터는 이 범위로 센다.** |
| 런(run) | 스테이지 세션의 시작 또는 마지막 재시작부터 지금까지 입력한 토큰 전체. 게임 `State`의 undo 체인과 같은 범위다. |
| 시도(줄) | 아래 경계로 끝나는 한 구간. `attempts.jsonl` 한 줄이 시도 하나다. |

## 3. 입력 토큰 `inputs`

글자 하나가 명령 하나다.

| 토큰 | 명령 | 상태 변화 |
|---|---|---|
| `U` `D` `L` `R` | 이동 (`Rules` Move) | 있음 |
| `K` | KEEP | 있음 |
| `X` | SWITCH | 있음 |
| `Z` | 되돌리기 (Undo) | 있음 (`prev`로 돌아감) |
| `H` | 풀이 보기·힌트 사용 (게임의 H) | **없음**. 세기만 한다 |

- 기록되는 토큰은 모두 실제로 효과가 있었던 명령이다.
  - 막힌 이동(bump)은 기록하지 않는다.
  - 되돌릴 것이 없을 때의 `Z`는 하네스가 오류로 거절하므로 기록되지 않는다.
  - 선택 상태가 아닐 때의 `K`/`X`도 같다.
  - 재생하는 쪽은 `U D L R K X Z` 토큰마다 상태가 바뀌었는지 확인한다(`ReferenceEquals(r.State, before)`이면 불일치).
- **재시작은 토큰이 아니다.** 재시작은 줄을 끝내는 경계다(4절).
- `inputs`는 항상 **런 전체**다. 그래서 줄 하나만으로 `newState(level)`에서 다시 실행할 수 있다.
  - 보통은 `from = 0`이다.
  - 실패(`fell`/`out_of_bodies`) 뒤에 `Z`로 이어서 플레이하면 다음 줄의 `inputs`는 앞 줄의 `inputs`로 시작한다. `from`은 이 줄에서 새로 입력한 첫 토큰의 위치다.
- 하네스의 H는 **스테이지 힌트 문구**(levels.js `hint`)만 보여 준다. 게임의 H는 솔버 경로를 보여 주지만, 블라인드 QA라서 다르게 한다. 세는 방식과 별 영향(★1로 제한)은 게임과 같다.

## 4. 시도 경계와 `outcome`

| `outcome` | 언제 줄을 쓰나 | 최종 `status` |
|---|---|---|
| `clear` | 출구에 7번째 걸음으로 도착 (`win`). 스테이지 세션이 끝난다 | `win` |
| `fell` | 상태가 `lose`가 되었고, 그 명령의 이벤트에 `fall`이 있음 (마지막 형제가 1~6번째 걸음에 구멍에 떨어짐) | `lose` |
| `out_of_bodies` | 상태가 `lose`가 되었고 `fall`은 없음 (마지막 형제가 선돌·다리·굳은 바닥이 되거나 출발점에서 사라짐) | `lose` |
| `restart` | `restart` 명령. 단, 이 시도에서 새로 입력한 토큰이 1개 이상일 때만 줄을 쓴다 | `play` 또는 `choose` |
| `gave_up` | `giveup` 명령. 스테이지 세션이 끝난다. **항상 별도 줄이다**(15번째 시도의 outcome을 바꾸지 않는다). 이 시도에 새 토큰이 있으면 그 시도를 끝내는 줄이고, 없으면 `inputs: ""`, `from: 0`인 **포기 표시 줄**이다(최종 상태 = 시작 상태, 시도로 세지 않음. level-design `qa-report.mjs`가 그렇게 읽는다) | 어느 것이든, `win` 제외 |

- 1~6번째 걸음에 구멍에 떨어져도 형제가 남아 있으면 경계가 아니다. 다음 형제가 깨어나고 같은 시도가 이어진다. 게임도 그렇다.
- `lose` 뒤에 할 수 있는 명령은 다음뿐이다.
  - `undo`: 같은 런을 이어 가는 새 시도. 다음 줄의 `from`이 0보다 크다.
  - `restart`: 새 토큰이 없으므로 줄은 쓰지 않고 재시작 수만 센다.
  - `giveup`
- **재시작 수 세기**(게임 `CountAttempt`과 같음): 현재 상태에 `prev`가 있을 때만 `levelRestarts`에 1을 더한다. 막 시작했거나 막 재시작한 상태에서 다시 재시작하면 세지 않는다. 줄 기록 여부(위 표)와는 별개다.

## 5. 필드

| 필드 | 형 | 뜻 |
|---|---|---|
| `v` | int | 형식 버전. 지금은 `1` |
| `tester` | string | 테스터 ID. `[A-Za-z0-9_-]{1,32}` |
| `level` | int | 스테이지 번호, 1부터 |
| `stageName` | string | levels.js `name` (ko, 언어와 상관없이 같은 키) |
| `chapter` | string | 챕터 id (`moss` …) |
| `mapHash` | string | 맵 행을 `\n`으로 이은 문자열의 SHA-256 앞 16 hex. 재생 쪽은 levels.json 맵으로 같은 값을 계산해 다르면 "맵 바뀜"으로 건너뛴다 |
| `lang` | string | 테스터가 본 문구의 언어 `ko`/`en` |
| `attempt` | int | 이 테스터·스테이지의 시도 번호. 1부터, 파일 전체(이전 스테이지 세션 포함)에서 센다 |
| `outcome` | string | 4절 |
| `failReason` | string\|null | `fell`/`out_of_bodies`일 때 마지막 형제를 잃은 이벤트: `fall` `bridge` `seal` `vanish` `stone`. 그 밖에는 `null` |
| `inputs` | string | 3절. 런 전체 |
| `from` | int | `inputs`에서 이 시도가 새로 입력한 첫 토큰의 위치 |
| `steps` | int | `inputs[from:]`의 `U D L R` 수 (되돌린 이동 포함) |
| `keeps` / `switches` | int | `inputs[from:]`의 `K` / `X` 수 |
| `undos` / `hints` | int | `inputs[from:]`의 `Z` / `H` 수 |
| `bodiesUsed` | int | 이 런에서 쓴 형제 수 = `bodies − 남은 형제 + (status가 lose면 0, 아니면 1)`. clear면 `core.js bodiesUsed()`와 같다 |
| `levelUndos` `levelHints` `levelRestarts` | int | 스테이지 세션 시작부터 이 줄까지의 누계(앞 줄들 포함). 게임 `AttemptCounts`와 같다 |
| `stars` | int\|null | clear일 때만 `stars({bodiesUsed, minBodies: levels.json expected.minBodies, hints: levelHints, undos: levelUndos, restarts: levelRestarts})`, 그 밖에는 `null` |
| `finalKey` | string | 최종 상태 키 (6절) |
| `finalHash` | string | `finalKey`의 UTF-8 SHA-256 앞 16 hex (소문자) |
| `startT` / `t` | string | 시도 시작·끝 시각 (ISO 8601 UTC). 비교 대상이 아니다 |

## 6. 최종 상태 키 `finalKey`

`core.js keyOf()`와 같은 순서이고, 상태만 이름으로 쓴다(C# `Rules.Key`는 상태를 숫자로 쓰므로 그대로 쓰지 않는다).

```
start|pos|used|bodies|statues|bridges|broken|status|flipped
```

- `start`, `pos`: 칸 번호 `y*w + x`. `w`는 가장 긴 행의 길이다(`parseLevel`/`Level.Parse`).
- `used`: 현재 형제가 쓴 걸음, 0~7. `bodies`: 남은 형제 수.
- `statues` `bridges` `broken`: 칸 번호를 오름차순으로, 쉼표로 잇는다(공백 없음). 비면 빈 문자열.
- `status`: `play` `choose` `win` `lose` 중 하나(C# `Status.ToString().ToLowerInvariant()`).
- `flipped`: `1` 또는 `0`.
- 예: `22|28|7|1|23,29||||win|0`

## 7. 재생 절차 (QaReplay가 할 일)

줄마다 다음을 한다.

1. `level`의 맵으로 `mapHash`를 계산한다. 다르면 `skipped: map changed`로 남긴다.
2. `newState(level)`에서 `inputs`의 토큰을 차례로 적용한다.
   - `U D L R`: `Move`
   - `K`/`X`: `Keep`/`Switch`
   - `Z`: `Undo`
   - `H`: 아무것도 하지 않음
   - 효과 없는 토큰이 있으면 불일치다.
3. 비교 항목:
   - `finalKey`
   - `outcome`과 최종 상태가 4절 표에 맞는지
   - `bodiesUsed`
   - `fell`/`out_of_bodies`면 마지막 명령의 이벤트로 `failReason`
   - `clear`면 `stars`. `Stars.Of(bodiesUsed, expected.minBodies, levelHints, levelUndos, levelRestarts)`로 다시 계산한다.
   - 카운터(`steps` `keeps` `switches` `undos` `hints`)는 `inputs[from:]`에서 다시 센다.
4. `levelRestarts`는 재생에서 알 수 없는 값(재시작은 토큰이 아님)이므로 기록값을 그대로 쓴다.

JS 기준 구현: `node unity/Tools/qa-play.mjs verify`가 `attempts.jsonl` 전체를 3번 항목으로 다시 검사한다.

## 8. 예

```json
{"v":1,"tester":"A","level":1,"stageName":"일곱 걸음","chapter":"moss","mapHash":"6428e42a396ce53f","lang":"ko","attempt":1,"outcome":"gave_up","failReason":null,"inputs":"RRRRRRH","from":0,"steps":6,"keeps":0,"switches":0,"undos":0,"hints":1,"bodiesUsed":1,"levelUndos":0,"levelHints":1,"levelRestarts":0,"stars":null,"finalKey":"11|17|6|3||||play|0","finalHash":"…","startT":"2026-11-10T13:27:54.155Z","t":"2026-11-10T13:27:54.482Z"}
```

## 9. 하네스 사용법과 규칙 (테스터용 요약)

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
- 테스트: `node --test unity/Tools/qa-play.test.mjs`
