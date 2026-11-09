# 검토: feat/view eafd97b (12.6 몸별 외형, BoardView·HudView)

검토자: 아키텍트, 2026-11-09. 코드 읽기(배치 실행은 리드 합치기 때).

**결론: OK (계약 위반 없음).** 사소한 권장 1개.

- 플레이어: `BodyIndex.Current`가 바뀌면 `SpawnPlayer`로 그 몸의 프리팹(`PlayerFor`)으로 교체하고 걸음 라벨을 옮긴다. `Respawn`·Undo·Restart가 모두 `Draw`를 거치므로 상태만으로 다시 그려진다(5장). 낙하·숨쉬기·복귀 트윈의 크기도 지금 몸의 `TileVisual.Scale`을 쓴다.
- 석상: 칸마다 `StatueFor(BodyIndex.OfStatue(s, i))`, 주인을 `statueOwners`에 기억해 Undo 후 다른 몸이 같은 칸에 KEEP하면 다시 만든다. 사라진 석상은 주인 기록도 지운다.
- 비용: `OfStatue`는 Undo 사슬을 걷지만 `Draw`(상태가 바뀔 때)에서만 부른다. `LateUpdate`는 크기만 바꾼다. 매 프레임 할당 없음.
- HUD: 몸 아이콘을 `BodyColor(k)`로, 쓴 몸은 흐리게(알파 0.25), 지금 몸은 크게(26px), 실패 뒤에는 모두 흐리게. 몸 4~5개 스테이지는 색이 순환(임시 허용, 6.4).
- 규칙 유출 없음. `spent = Level.Bodies - s.Bodies`는 `BodyIndex.Current`와 같은 값(게임은 `Level.Bodies`로 시작)이라 문제 없음.

## 권장 (사소)
- `HudView.BodyLook`이 `game.GetComponent<BoardView>()`로 세트를 찾는다. View가 컨트롤러의 컴포넌트를 뒤지는 것은 9장 MVC(참조는 Controller → View)에 어긋난다. 실패하면 예외(단정적)이고 주석에 S3까지 임시라고 적혀 있어 막지는 않는다. 더 간단한 대안: `GameController`가 `board.Init` 직후 `hud.SetBodyLook(tiles)`로 넘긴다(한 줄). 챕터가 바뀌어 세트가 바뀌어도 같은 호출로 갱신된다.
