# WebGL FPS 후보 (코드 기준 사전 조사, 측정 전)

작성: 아키텍트, 2026-11-10. 배경: Codex 측정 WebGL 10~22 FPS. 리드가 일반 Chrome에서 1·29·50을 재측정해 30 미만이면 아키텍트 + 게임플레이/UI가 원인 조사. 아래는 코드·설정만 보고 뽑은 후보이고, **기대 효과 큰 순**. 측정은 `?artperf=1`(feat/art 6448f82, 10초 뒤 10초 평균·중앙값·p95 로그)로 하나씩 끄고 비교한다.

| # | 후보 | 근거 (코드·설정) | 확인·대책 |
|---|---|---|---|
| 1 | **고해상도 화면에서 캔버스가 devicePixelRatio 배로 그려짐** | `webGLTemplate: APPLICATION:Default`. Unity 6 기본 템플릿은 캔버스 버퍼를 CSS 크기 × `devicePixelRatio`로 잡는다. DPR 2 노트북이면 픽셀 4배(render scale 0.8로도 2.56배) | 측정 기기의 `window.devicePixelRatio`부터 기록. 대책: 커스텀 템플릿(ui-layout-fix의 16:9 맞춤 템플릿, 씬 플로우)에서 `config.devicePixelRatio = 1` 또는 `Math.min(dpr, 1.5)` |
| 2 | **실시간 그림자** | 7개 조명 key `shadows: 1`이었음, Mobile_RPAsset `m_MainLightShadowsSupported: 1`, 1024, 거리 50 → 그림자 패스가 보드·환경을 한 번 더 그림 | 6448f82에서 key 그림자 끔. 남는 것: 에셋의 지원 플래그(그림자 키워드 변형). 측정에서 개선 확인 |
| 3 | **방향광 3개(key·fill·rim) 모두 켜짐** | `Lighting_*.asset` 8개 모두 `enabled: 1` ×3. URP에서 두 번째부터는 추가 조명이고 Mobile_RPAsset `m_AdditionalLightsRenderingMode: 1`(픽셀 단위) → 모든 픽셀에서 조명 2개 추가 계산 | WebGL에서 fill·rim 끄기(`ChapterStage`가 조명 적용 시 플랫폼 분기, 게임플레이/UI) 또는 `m_AdditionalLightsRenderingMode: 2`(정점), 또는 fill을 ambient 색으로 대체 |
| 4 | **드로우콜 ~400** | 12.8 기록: 1280×720 400 안팎(유적 레벨 8: 403). `BoardView`가 칸마다 프리팹 `Instantiate`(456·535행). WebGL은 드로우콜당 CPU 비용이 데스크톱보다 큼 | `?artperf`에 `FrameTimingManager`/`ProfilerRecorder("Draw Calls Count")` 추가해 CPU·GPU 시간 분리. CPU가 크면 정적 바닥·벽을 스테이지 로드 때 `Mesh.CombineMeshes`(재질별)로 합침 — `Snap` 재그리기(5장)와 충돌 없게 정적 칸만 |
| 5 | HDR 렌더 타깃 | Mobile_RPAsset `m_SupportsHDR: 1`이었음 | 6448f82에서 끔 |
| 6 | 하늘 오버드로 | 하늘은 화면 전체 불투명 메시 1장(Background 큐, ZWrite Off) + 행성. WebGL은 이제 정점색 `RuinsSkyLow` | 낮음. 오버드로 1겹 |
| 7 | 반투명 | 반투명 재질은 `LuckyRing.mat` 하나(작은 링) | 낮음 |
| 8 | 보드 숫자 라벨 매 프레임 갱신 | `BoardView.LateUpdate`가 라벨마다 `WorldToScreenPoint` + `anchoredPosition`·`fontSize` 대입. 값이 같으면 uGUI는 다시 짓지 않지만 액터 이동·카메라 맞춤 중에는 라벨 캔버스 전체가 다시 그려짐 | 낮음~중간. 라벨을 별도 Canvas로 분리돼 있는지 확인, 움직이는 라벨만 갱신 |
| 9 | 매 프레임 할당 | 일반 경로 `Update`·`LateUpdate`(BoardController 입력, GameController, TelemetryRunner, BoardView 라벨, 카드 타이머, RuinsHengeSky)에 할당 없음. `HudView.LateUpdate`의 `GetComponentsInChildren`은 `?smoke`일 때만. 텔레메트리 큐 저장은 5초마다·바뀐 경우만, 디스크 Flush는 일시정지·종료 때만 | 낮음. GC 스파이크는 p95로 확인 |
| 10 | MSAA·렌더 스케일 | Mobile_RPAsset `m_MSAA: 1`(끔), `m_RenderScale: 0.8` | 이미 낮음. 1번이 해결 안 되면 WebGL만 0.7 검토 |

측정 순서 제안: (1) 기기 DPR 기록 → `config.devicePixelRatio = 1` 한 빌드로 비교, (2) fill·rim 끔, (3) CPU/GPU 분리해서 드로우콜 판단. 1·3은 코드 몇 줄이라 현장 테스트 전에 가능하다.
