# 숫자 연금술사 — Lucky Number Alchemist

적은 숫자입니다. 내 행운의 수로 **+ − × ÷** 를 계산해 적을 그 문화권의 **💀 죽음의 수**로 만드세요. 층마다 그 숫자를 계속 믿을지(**KEEP**), 바꿀지(**SWITCH**) 결정합니다. 서양·동양·수학의 행운 인식이 층의 규칙이 됩니다.

## 실행

ES Module을 쓰기 때문에 `file://`로는 열리지 않습니다. 로컬 서버로 실행하세요.

```sh
python -m http.server 8765    # http://127.0.0.1:8765
```

조작: 연산자 클릭 또는 `Q W E R`(+ − × ÷) · `Z X`(±1) · `T Y`(해금 연산) → 적 클릭 또는 숫자키. 제단에서 `K` KEEP, 엔딩에서 `R` 재시작.

## 검증 (하네스)

| 명령 | 내용 |
|---|---|
| `node tools/sim.mjs [판수]` | 봇 전략 4종 × 숫자 6종 밸런스 시뮬레이션과 결정론 검사. 결과는 `reports/sim-latest.json` |
| `node tools/smoke.mjs` | 브라우저 없이 모든 화면을 모든 연산자 선택 상태로 렌더하고 `undefined`/`NaN`/예외를 검사 |
| `?seed=N&op=sub&replay=[...]` | URL에 시드, 선택 연산자, 명령 목록을 넣으면 같은 화면을 재현 |

## 구조

- `src/core.js`: 순수 게임 로직. 모든 상태 변경은 `dispatch(state, cmd)` 하나로만 합니다. 난수는 시드로 받습니다. `solve()`(BFS 해법), `preview()`, `fit()`(숫자-문화권 적합도)은 UI와 봇이 함께 씁니다.
- `src/data.js`: 숫자, 문화권, 연산, 아이템, 밸런스 수치 (`TUNING`)
- `src/render.js`: DOM 렌더러 (상태를 읽기만 함)
- `src/main.js`: 입력, 시드, UI 전용 상태(선택한 연산자)
- `prototypes/v1-13floors/`: 폐기된 v1 (평가 기록: `production/playtests/`)
- `design/gdd/`: 컨셉 문서, 로그라이크 성장 검토

## itch.io 업로드

```sh
python tools/pack.py   # dist/lucky-alchemist.zip
```

itch.io에서 Kind of project를 **HTML**로 정하고 zip을 올린 뒤 "This file will be played in the browser"를 체크합니다. 권장 뷰포트는 1200×800입니다.
