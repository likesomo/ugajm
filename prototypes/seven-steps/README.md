# 일곱 걸음 — Seven Steps (HTML 프로토타입)

Unity 본작 전에 규칙의 재미를 확인하는 웹 프로토타입. 컨셉: `design/gdd/seven-steps-concept.md`

```sh
python -m http.server 8777    # 이 폴더에서 실행 → http://127.0.0.1:8777
node tools/solve.mjs          # 모든 스테이지 풀이 가능 여부, 최소 몸 수 → reports/seven-steps-solve.json
```

- 조작: 방향키/WASD/옆 칸 클릭, 7걸음 후 `K` KEEP / `X` SWITCH, `Z` 되돌리기, `R` 재시작, `H` 풀이 보기, `[` `]` 스테이지 이동
- 재현: `?level=8&replay=LRRRRDDKLLDDRDD` (U D L R 이동, K KEEP, X SWITCH)
- `src/core.js`: 규칙. 상태 변경은 `dispatch()` 하나, 시간·난수 없음. `solve()`는 BFS 솔버
- `src/levels.js`: 스테이지 12개 (텍스트 맵)
- 솔버 출력의 `SWITCH필수`: SWITCH 없이는 풀 수 없는 스테이지
- `src/main.js`: 화면과 입력
