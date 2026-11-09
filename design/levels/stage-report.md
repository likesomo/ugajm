# 스테이지 솔버 리포트 (50스테이지)

생성: `node unity/Tools/stage-report.mjs` (2026-11-09, 코어). 입력: `prototypes/seven-steps/src/levels.js` + `core.js` 솔버, `levels.json` expected 대조.
스테이지를 바꾸면 다시 실행한다. 리플레이 형식: U D L R 이동, K KEEP, X SWITCH (`-replay` / `?replay=`).

## 별 기준 (design/levels/stars.md 0, B안 — `core.js` `stars()` / C# `Stars.Of`)

| 별 | 조건 (한 시도 안에서) |
|---|---|
| ★1 | 클리어 |
| ★2 | ★1 + 풀이 보기(H) 0회 + 쓴 몸 수 ≤ 최소 몸 |
| ★3 | ★2 + 되돌리기(Z)·재시작(R) 0회 |

- ★2가 갈리는 스테이지(최소 몸보다 많은 몸으로도 이길 수 있음): **25/50**. 나머지는 깨면 ★2가 자동(힌트를 안 썼다면)
- 최단 입력 = 몸 수와 상관없이 입력(이동+K/X)이 가장 적은 해. 최소 몸 해와 다르면 "최단"에 몸 수를 적는다
- 상태 수 = 몸 예산 전체로 BFS 한 상태 수(한도 600000). 난이도 직접 지표는 아니고 탐색 공간 크기
- 문제: 없음 (모든 스테이지 풀이 가능, 몸 ≤ 3, levels.json 최신)

## 스테이지별

| # | 챕터 | 이름 | 몸 | 최소 몸 | 이기는 몸 수 | ★2 | SWITCH 필수 | 최소 몸 해 입력(방향) | 최단 입력 | 상태 수 | 최소 몸 해 (리플레이) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | moss | 일곱 걸음 | 3 | 1 | 1/3 | 갈림 |  | 7 (7) | 7 | 429 | `RRRRRRR` |
| 2 | moss | 늦게 도착하기 | 3 | 1 | 1/2/3 | 갈림 |  | 7 (7) | 7 | 4620 | `UDUDRRR` |
| 3 | moss | 딱 맞는 길 | 3 | 1 | 1/2/3 | 갈림 |  | 7 (7) | 7 | 3438 | `RRDDRRR` |
| 4 | moss | 돌이 된 몸 | 3 | 2 | 2/3 | 갈림 |  | 15 (14) | 15 | 1418 | `DURRRRRKDDRRRRR` |
| 5 | moss | 가까운 발판 | 3 | 2 | 2/3 | 갈림 |  | 15 (14) | 15 | 501 | `LLRRRRRKLRRDDDL` |
| 6 | moss | 길을 막는 선돌 | 3 | 2 | 2 | 자동 |  | 15 (14) | 15 | 550 | `DUDUDURKDDRRUUR` |
| 7 | moss | 두 개의 문 | 3 | 3 | 3 | 자동 |  | 23 (21) | 23 | 499 | `DUDUDULKDUDURRRKDUDDDDR` |
| 8 | hollow | 새 출발점 | 3 | 2 | 2/3 | 갈림 | 예 | 15 (14) | 15 | 985 | `RRRRRRDXDLLLLLL` |
| 9 | hollow | 바통 넘기기 | 3 | 3 | 3 | 자동 | 예 | 23 (21) | 23 | 1050 | `RLRRRRRXRRRRDDLXLLLLLLL` |
| 10 | hollow | 몸으로 놓는 다리 | 3 | 2 | 2/3 | 갈림 |  | 14 (14) | 14 | 158 | `RLRLRRRRLRRRRR` |
| 11 | hollow | 다리냐 선돌이냐 | 3 | 3 | 3 | 자동 |  | 22 (21) | 22 | 190 | `RLRLRRRRLRRRRRKRRRRDDD` |
| 12 | hollow | 건너편에서 깨어나기 | 3 | 3 | 3 | 자동 | 예 | 22 (21) | 22 | 241 | `RLRLRRRRLRLRLRXRRRDDLL` |
| 13 | hollow | 구멍 둘, 형제 셋 | 3 | 3 | 3 | 자동 |  | 21 (21) | 21 | 129 | `UDUDUDRUDUDRRRUDRRRDD` |
| 14 | hollow | 떠나온 자리 | 3 | 3 | 3 | 자동 | 예 | 23 (21) | 23 | 1701 | `LLLRRRRXRRRRRRRKLLLLLLL` |
| 15 | carved | 숫자 문 | 3 | 1 | 1/2/3 | 갈림 |  | 7 (7) | 7 | 81 | `UDRRRRR` |
| 16 | carved | 문 너머의 발판 | 3 | 2 | 2 | 자동 |  | 15 (14) | 15 | 127 | `RRRRRLRKRDUDUDD` |
| 17 | carved | 갈림길의 숫자 | 3 | 2 | 2/3 | 갈림 | 예 | 15 (14) | 15 | 3779 | `LLRLRRRXRUURRDD` |
| 18 | carved | 처음부터 다시 세기 | 3 | 2 | 2/3 | 갈림 | 예 | 15 (14) | 15 | 228 | `RLRLRRRXRRRRLRR` |
| 19 | carved | 숫자와 구멍 | 3 | 2 | 2/3 | 갈림 |  | 14 (14) | 14 | 123 | `UDUDUDRRRRRRLR` |
| 20 | carved | 두 숫자 | 3 | 2 | 2/3 | 갈림 | 예 | 15 (14) | 15 | 4721 | `LUUDUURXRRDDDUD` |
| 21 | carved | 새김돌의 계산 | 3 | 3 | 3 | 자동 | 예 | 23 (21) | 23 | 1229 | `RLRRRRUKUUDUDUDXUDLLLUU` |
| 22 | frost | 미끄러운 첫걸음 | 3 | 1 | 1/3 | 갈림 |  | 7 (7) | 7 | 468 | `RDDLLLL` |
| 23 | frost | 홀수의 저주 | 3 | 1 | 1/2/3 | 갈림 |  | 7 (7) | 7 | 23677 | `DUDURDD` |
| 24 | frost | 얼음 위의 숫자 | 3 | 2 | 2/3 | 갈림 | 예 | 15 (14) | 15 | 35637 | `DUDUDUDXLULULLU` |
| 25 | frost | 멈춰 세우는 석상 | 3 | 2 | 2/3 | 갈림 |  | 15 (14) | 15 | 3881 | `LRRRRDDKLLDDRDD` |
| 26 | frost | 미끄럼을 막는 형제 | 3 | 2 | 2/3 | 갈림 |  | 15 (14) | 15 | 10998 | `RUUUDLLKRUULLUU` |
| 27 | frost | 얼음 끝의 구멍 | 3 | 2 | 2/3 | 갈림 |  | 14 (14) | 14 | 24161 | `UDDLLULDLLULUU` |
| 28 | frost | 서리의 길목 | 3 | 2 | 2/3 | 갈림 |  | 15 (14) | 15 | 25322 | `DUDUDRUKRDLDDRR` |
| 29 | joinery | 내리는 벽 | 3 | 2 | 2/3 | 갈림 |  | 15 (14) | 15 | 1079 | `DURRRRRKDDRRRRR` |
| 30 | joinery | 열면 닫힌다 | 3 | 3 | 3 | 자동 | 예 | 23 (21) | 23 | 1723 | `DUDUDUDXURRRRRRKDRRRRRR` |
| 31 | joinery | 올려서 멈추기 | 3 | 2 | 2/3 | 갈림 |  | 15 (14) | 15 | 2511 | `DURRRRRKDUDDDRD` |
| 32 | joinery | 레버와 발판 | 3 | 2 | 2/3 | 갈림 |  | 15 (14) | 15 | 5597 | `DDDDLLUKDDLLLLD` |
| 33 | joinery | 레버와 숫자 | 3 | 3 | 3 | 자동 | 예 | 23 (21) | 23 | 8404 | `UUDUDUDXDDDRRUUKURRRDUD` |
| 34 | joinery | 레버와 바통 | 3 | 3 | 3 | 자동 | 예 | 23 (21) | 23 | 15219 | `UDUDUDUXDUDUDDRXLULLULL` |
| 35 | joinery | 결구의 장치 | 3 | 3 | 3 | 자동 | 예 | 23 (21) | 23 | 11963 | `UDUDULRXUDLDDRRKRRUDDDR` |
| 36 | crumble | 한 번만 버티는 바닥 | 3 | 3 | 3 | 자동 |  | 22 (21) | 22 | 399 | `RLRLRRRRLRRRRRKRRRRDDR` |
| 37 | crumble | 무너진 길 위의 구멍 | 3 | 3 | 3 | 자동 | 예 | 23 (21) | 23 | 4685 | `LRRLRLRXRDDLLLDXLLLUULU` |
| 38 | crumble | 하나만 남겨라 | 3 | 2 | 2 | 자동 |  | 15 (14) | 15 | 1268 | `DUDUDURKDDRRRRR` |
| 39 | crumble | 열쇠가 되는 형제 | 3 | 3 | 3 | 자동 |  | 23 (21) | 23 | 497 | `UDUDUDUKDURRRRRKDDRRRRR` |
| 40 | crumble | 남기기 전에 지나가라 | 3 | 3 | 3 | 자동 | 예 | 23 (21) | 23 | 612 | `RLRRRRRXLLLLLRRKRDUDUDD` |
| 41 | crumble | 선돌 아닌 흔적 | 3 | 3 | 3 | 자동 | 예 | 22 (21) | 22 | 2981 | `LUURRRDLUURRRDXDRRDDRR` |
| 42 | crumble | 무너지는 계산 | 3 | 3 | 3 | 자동 | 예 | 22 (21) | 22 | 14821 | `RUULLLUXULLLDDDDLLLDDR` |
| 43 | altar | 한 칸에 두 걸음 | 3 | 1 | 1/2/3 | 갈림 |  | 6 (6) | 6 | 718 | `RRRRRR` |
| 44 | altar | 진흙과 숫자 | 3 | 3 | 3 | 자동 | 예 | 20 (18) | 20 | 2715 | `UUDDDDXDUDULXLLUULLD` |
| 45 | altar | 얼음과 진흙 | 3 | 2 | 2/3 | 갈림 | 예 | 13 (12) | 13 | 62913 | `UDULUXULLDUUL` |
| 46 | altar | 칠칠의 방 | 3 | 3 | 3 | 자동 | 예 | 22 (21) | 22 | 2764 | `UDURRDDUDUDUDUXRRDDDDL` |
| 47 | altar | 레버와 선돌의 수 | 3 | 2 | 2/3 | 갈림 |  | 15 (14) | 15 | 5323 | `DUDDDDRKRRRDDRR` |
| 48 | altar | 길이 된 형제들 | 3 | 3 | 3 | 자동 |  | 21 (21) | 21 | 7127 | `LLDURRRLRRDDRRRDDRRDD` |
| 49 | altar | 일곱의 일곱 | 3 | 3 | 3 | 자동 | 예 | 21 (19) | 21 | 28672 | `LDUDDDDXRLRRUXURRRRDD` |
| 50 | altar | 원형 제단 | 3 | 3 | 3 | 자동 |  | 22 (20) | 22 | 21650 | `UUUDURUKDRRRUUKDRRDLUU` |
