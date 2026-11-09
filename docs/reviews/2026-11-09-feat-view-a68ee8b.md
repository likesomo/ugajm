# 검토: feat/view a68ee8b (3e839c5 HUD 프리팹 + HudView MonoBehaviour, d0c8240 GameAssets.uiTheme = RuinsHengeTheme)

검토자: 아키텍트, 2026-11-09. 코드 읽기.

**결론: OK (계약 위반 없음).** `UiTheme.hud` 슬롯 추가는 씬 플로우(UiTheme 타입 소유) 동의 필요 — 아키텍트가 씬 플로우에 확인 요청.

- `HudView`: 코드 생성 → 프리팹(`UI/Prefabs/Hud.prefab`, 자체 캔버스) 뷰. 직렬화 참조를 `Validate`로 `OnValidate`·`Wire`(예외)·`SceneFlowValidator`(모든 UiTheme)에서 검사. `Create`는 테마에 `hud`가 없으면 예외. 이벤트만 내고 `Services`·컨트롤러를 모른다(9장 MVC).
- `Refresh(State, levelIdx, msg)`: 상태 읽기만(규칙 계산 없음). 북두칠성 걸음 표시는 `Used`/`Status`, 몸 슬롯은 `BodyIndex.OfStatue`로 선돌 주인 확인 — 상태가 바뀔 때만 불리므로 12.6 비용 규칙 지킴. 지난 권장(`Refresh`의 중복 `levelCount` 인자, 진행 기록 인자) 제거.
- 상단 스테이지 번호 줄 제거 → HUD가 `ProgressData`를 더 이상 읽지 않는다(클리어 표시는 스테이지 선택 화면). `StageClicked` 이벤트 제거, 스테이지 이동은 `[` `]`·스테이지 선택 버튼.
- `GameAssets.uiTheme = Art/UI/RuinsHengeTheme` — 아트 테마의 패널 프리팹 복사본이 쓰이기 시작한다. `SceneFlowValidator`가 모든 UiTheme을 검사하므로 필수 참조는 빌드 전에 잡힌다. 프리팹 변형 전환 권장(341f49c 검토)은 여전히 유효.
- `HudBuilder`(Editor/Gameplay): 생성기, 게임플레이/UI 소유.

## 사소 (막지 않음)
- HUD 문구("스테이지 {0:00}", "다음 걸음 7 · 행운", 여행자 이름 등)가 코드 리터럴이다. 13장(다국어) 때 키로 옮긴다. GlyphCheck가 지금 글자는 검사한다.
- "다음 걸음 7 · 행운"은 `next == 7`일 때만 — 진흙 앞(5걸음 뒤 진흙이면 7번째) 안내는 없다(0fc523b 검토와 같은 메모).
