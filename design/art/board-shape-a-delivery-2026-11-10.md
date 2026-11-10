# BoardShapeA — R2 A Blender MCP → Unity

사용자가 선택한 R2 A(직사각 석재 연석 + 깎인 암반 지각 단면)의 실제 메시·Unity 전달 기록이다. 그림 시안과 실제 렌더를 구별한다. 작업 시작 HEAD는 `196ecea57a7d8ffadcbac74d577684150f5b4bd5`이며 `origin/7lucky`를 fetch/merge했다.

## 제작·공유 파일

- 모델링과 반복 렌더는 연결된 Blender 5.2의 `blender.execute_blender_code` MCP로 실행했다. 재현 스크립트: `design/art/blender/build_board_shape_a.py`. `BSA_BAKE=False`는 간이 블록아웃, 기본값은 실제 Cycles selected-to-active NORMAL/AO 베이크와 FBX 출력이다.
- 원본: `C:/unityjam/shared-assets/seven-steps/Source/Blender/SS_BoardShapeA.blend`. .blend와 백업은 Assets 밖에 있다. 기본 시작 씬을 지우지 않고 소유한 `BoardShapeA` 씬만 다시 만든다.
- FBX/텍스처: 공유 `Assets/Art/RuinsHenge/BoardShapeA/`, Unity에서는 `Assets/Shared/Art/RuinsHenge/BoardShapeA/`. 바이너리와 공유 .meta는 Git에 넣지 않는다. 배포 시 이 폴더 전체를 함께 전달해야 한다.
- 새 메시 키트 25종: 판석 3종, 암반 3종, 선돌 2종, 연석/코너/마름모, 상자/후면 문/담/목재 결구, 잔돌/깨진 판석/잔디, 벽 블록, 구멍/깊은 암부, 얼음, 진흙, 숫자 문 프레임/청록 판.
- 새 공유 시트는 `BSA_ColorAO.png`와 `BSA_NormalRough.png`, 각각 1024×512다. 고해상도 relief 메시에서 노멀과 AO를 실제로 베이크한다. 색/AO 시트에는 저주파 화강암 얼룩·광물 입자·마모 밝기, 노멀 시트에는 미세 결·균열·음각을 넣었다. AO는 색에도 합성되어 실시간 그림자 없이 남는다. 전역 기존 캐릭터/기믹 아틀라스는 유지한다.
- UV0는 8개 재료 swatch, UV1.x=4는 새 키트 표면 식별자다. `RuinsSurface`는 이 경로에서 두 시트·탄젠트 노멀·방향광 하이라이트를 사용한다. 줄눈은 흙색 측면과 좁은 이끼이고 중앙 숫자 영역은 깨끗하다. 실제 모따기와 버텍스 마모/수직 명암을 병용한다.
- 새로운 머티리얼 클래스는 추가하지 않았다. 판석/외피는 기존 BaseAtlas→Environment_<id> 교체를 사용한다. 얼음은 기존 FrostOpaque GUID의 Simple Lit 재질을 약한 투명도(.92)·노멀·홈으로 갱신했다(이름은 이전 호환을 위해 유지). 굴절·Opaque Texture·SSAO·새 실시간 그림자는 쓰지 않는다.
- 외부 에셋·다운로드·유료 라이선스는 사용하지 않았다. 모든 신규 메시/표면은 스크립트로 만든 원본이다.

## Unity 연결과 계약

`BoardShapeABuilder.Apply`는 기존 Floor/FloorAlt/Wall/Pit/Ice/Gate/Door/CountDoor 프리팹의 GUID와 루트를 보존하고 메시를 교체한다. Mud와 FloorThird를 추가하고 TileSet의 문 자식 훅을 `GatePanel`에 맞춘다. 문은 높이 .6m 이하의 입체 석재 아치·테라코타 테·청록 판이고 숫자는 별도 라벨이다. 숫자 위치는 판 앞면 `(0,.31,-.15)`다.

기존 `PlanetSummit_<id>`에 `BoardShapeA_Kit`을 연결했다. `IStageEnvironment.Fit(BoardGrid)`에서 보드 크기·원점·cellSize를 받아 한 번 결합하며, 새 환경은 불투명 renderer 1개/재질 1개다. 모든 장식 정점을 보드 사각형+1칸 밖으로 단정 검사한다. 배경 구체·행성 조각을 추가로 겹치지 않는다. 피벗은 타일 윗면 y=0이고 게임 규칙·레벨 데이터는 변경하지 않았다.

7행성 색/이끼/실루엣은 기존 ChapterPreset.materialOverrides와 PlanetSummit 프리팹으로 적용한다. moss=줄눈 이끼, hollow=깊고 얕은 기둥 사이 열린 하부, carved=겹친 석재 층리, frost=차가운 석재/이끼·잔디 0, joinery=하부 목재 보강, crumble=불규칙 단면/분리 파편, altar=층리와 추가 선돌이다. 하늘은 절제된 남청 별 배경이다. 키 광원은 고도 40°의 왼쪽 위 방향이고 기존 그림자 OFF/HDR OFF 설정을 유지한다.

환경 Awake의 URL 직접 검사는 `Services.TryGet<LaunchOptions>(...).ArtPerf`로 바꿨다. 프레임 샘플러는 opt-in에서만 작동하고 삼각형 집계는 측정 구간이 끝난 뒤 한 번 수행한다. SevenPlanetBuilder를 다시 실행해도 새 키트를 유지한다.

## 실제 Unity 캡처와 프레이밍 제안

공유 산출물 루트: `C:/unityjam/shared-assets/seven-steps/concepts/themes/board-shape-2026-11-10-r2/unity/`.

| 파일 | 내용 |
|---|---|
| hero-unity-default-proposed.png | R2 A 그림 시안 / 실제 Unity 기본 직교 / 실제 Unity 제안 직교, 3열 |
| seven-planets-unity.png | 실제 Unity 7행성 시트 |
| moss-8x6-ortho.png | 기본 BoardView.Fit, pitch50/yaw0 |
| moss-8x6-proposed.png | 검토 승인된 제안 프레이밍 |
| moss-12x5-proposed.png | 긴 보드 |
| moss-10x3-proposed.png | 얇고 긴 보드 |
| moss-7x7-proposed.png | 정방형 보드 |
| unity-review-geometry.csv | 실제 조립 tris·renderer·재질 수·셀 픽셀 크기 |
| blender-kit-report.json | Blender 모듈별 tris와 베이크 기록 |

캡처는 저장된 production TileSet/Chapter/Sky/Lighting/PlanetSummit 자산을 사용하는 격리 Unity 아트 리뷰 플레이어다. 실제 게임플레이/HUD 캡처로 가장하지 않는다. 제안 프레이밍만 리뷰 플레이어 안에서 바꾸며 production CameraRig/AppConfig/정식 씬을 수정하지 않았다. 원근 FOV28 비교는 초기 검토 파일로 남기고, 직교 유지를 기본으로 권장한다.

**리드에게 넘길 8×6 제안**: 직교, pitch50/yaw0, 거리30, orthoSize4.65, 목표 중심 `(0,-.75,0)`, 섬 폭 약73%, 1280×720에서 셀 77.42×59.31px. 기본은 55.19×42.28px다. ortho4.5의 80px 셀은 후면 문/약3m 지각 끝을 잘라 4.65를 선택했다. 제안 12×5=60.53×46.37px, 10×3=69.26×53.06px, 7×7=74.06×56.73px다. HUD 안전영역 및 ui-layout-fix와 조율해 이 값을 실제 카메라 맞춤에 적용해야 한다.

세쌍둥이 말·바닥 링은 이번 범위에서 변경하지 않았다. 제안 프레이밍에서도 시안보다 말의 후드/스카프 표현이 단순하고 링이 커 보인다. 캐릭터와 링의 크기·표현은 별도 검토 사항이다. 그림 시안의 약한 원근·오프라인 간접광까지 동일한 결과라고 주장하지 않는다.

## 검증·기하 예산

CompileCheck와 GlyphCheck는 PASS(검사 폰트 누락0), 아트 TileSet/7챕터 검사는 오류0/경고0, EditMode 전체는 **776/776 PASS**다. 기존 production UI 레이아웃 경고38건과 placeholder TileSet의 기존 경고1건은 유지된다. UI 안전영역 작업은 리드/ui-layout-fix 범위다. `unity/Logs/board-shape-a-final-checks.log`, `unity/Logs/batch-test.log`, `reports/editmode-results.xml`에 원본 결과를 남긴다.

실제 8×6 기믹/캐릭터 조립 예시는 17,134tris/renderer62개/재질4개다(환경renderer는 그중1개). 같은 예시12×5=20,110tris다. 7챕터×8×6/12×5/10×3/7×7의 깨끗한 보드(판석+연석+지각+장식)는 모두25k 아래이고 최대는 altar12×5의23,490tris다. `reports/board-shape-a/geometry.csv`가 전체 표다. 이는 draw call 수가 아니라 메시renderer 수이며, 실제 WebGL 호출은 성능 표로 따로 센다.

기존129개 아트 경로의 GUID 유지, 모든 장식 정점의1칸 간격, 반복Fit, 환경renderer1개, frost 이끼0을 검사했다. 초기 숨겨진 Windows 플레이어의 ScreenCapture 실패와25k 초과 중간 시도는 최종PASS로 계산하지 않았다. 최종 캡처 플레이어는 실제PNG 저장 여부도 검사한다. 선택적DOF/Panini shader stripping과Unity 종료 시 임시 할당 경고는 기존 로그에도 나타나며 보존한다.

최종 WebGL 빌드: **PASS, 오류0, 17,937,125bytes** (`unity/Logs/batch-build-webgl.log`). `smoke-webgl`: **PASS**, 보드 픽셀1번45,347 /29번63,127 /50번55,747, 숨김 대조군0, 콘솔 예외0. 검증 원본은 공유 `unity/verification/`에도 복사했다. 최종 재검사도 EditMode776/776이다.

## 일반 Chrome 성능

이전 빌드 보존 위치: `Builds/WebGL-BoardShapeA-before`. 빌드의 index.html 버전은 `0.1.0+bbab2fa-dirty`다. 작업 시작 HEAD와 이전 플레이어에 찍힌 커밋은 서로 다르며, dirty 플레이어를 clean 커밋 재현물로 설명하지 않는다. 이전 작업의 `tone-after-compatible-webgl.log`와 일치하는 기존 빌드를 그대로 복사했다.

측정: 일반 visible Chrome, 같은 독립 프로필/창 1280×720, 실제 canvas buffer 1280×720(Mobile render scale0.8), DPR1, Chrome 157.0.8103.50, RTX3060 / ANGLE D3D11. URL은 level1/29/50, replay=R, artperf=1이다. 각각 10초 준비+10초 Unity 프레임 샘플을 3회 실행한다. Unity 빌드/리뷰 플레이어는 정지한다. draw count는 동일한 WebGL draw entry-point 계측을 두 빌드에 적용해 마지막 10초의 실제 호출을 집계한다(URP Profiler의 통계와 같은 정의라고 가정하지 않는다).

재현 스크립트: `design/art/measure-board-shape-a.mjs`. raw JSON/console/진단 화면은 `reports/board-shape-a/performance/`와 공유 산출물 하위 performance에 보존한다. 리프레시 제한 약60 FPS인 환경이므로 같은 FPS를 GPU 여유나 저사양 성능 향상으로 해석하지 않는다.

| 스테이지 | 이전 FPS 1/2/3회 | 이후 FPS 1/2/3회 | 이전→이후 draw calls/frame | 이후 전체 장면 tris / 환경 tris |
|---|---|---|---|---|
| 1 moss | 60.004 / 60.000 / 60.000 | 60.000 / 60.004 / 59.998 | 149 → 45 | 16,508 / 12,114 |
| 29 joinery | 60.004 / 60.000 / 60.000 | 60.000 / 60.004 / 59.998 | 207 → 62 | 19,144 / 13,510 |
| 50 altar | 60.004 / 60.000 / 59.998 | 60.004 / 59.998 / 60.004 | 336 → 98 | 28,412 / 19,034 |

각 스테이지의 드로우 수는 3회 모두 같았다. 모든 샘플의 p95는 약 17.00ms다. 50번 평균 FPS는 60.0007→60.0020으로 리프레시 제한/타이머 오차 범위에서 동일하다. **50번 FPS 유지와 드로우콜 비증가 기준을 충족**했고 드로우콜은 70.8% 감소했다. 50번은 하늘까지 포함 28,412tris로 50k 상한 아래이며, 기믹을 포함한 전체 장면은 권장 25k를 조금 넘는다. 이를 25k 이하라고 보고하지 않는다.

이전/이후 빌드 모두 HDR=false, DPR1, 같은 Mobile render scale0.8과 같은 버퍼 크기다. `chrome-gpu.txt`에는 chrome://gpu에서 읽은 ANGLE D3D11/RTX3060과 드라이버 32.0.16.1088 정보도 보존했다. 이후 빌드 표시는 `0.1.0+196ecea-dirty`이며 실제 변경 커밋은 완료 보고에 별도로 적는다. `before-build-hashes.json`/`after-build-hashes.json`이 측정 바이너리를 구분한다. 이전 BuildTools 보고 15,653,361bytes 대비 새 17,937,125bytes(+2,283,764bytes)로, 새 베이크 시트가 포함되어 빌드 크기가 늘었다.

## 리드 연결 사항

1. 기존 ChapterPreset 7개와 materialOverrides, SkyPreset의 PlanetSummit 참조는 유지한다. 새 `BoardShapeA_Kit.asset`와 공유 BoardShapeA 폴더/.meta를 함께 배포한다.
2. AppConfig/정식 씬은 리드 소유이며 이번에 수정하지 않았다. 기존 production 참조로 새 형태가 들어간다. 프레이밍 제안값은 HUD 안전영역 작업과 합쳐 별도 적용한다.
3. 키트 재생성: Blender MCP에서 생성 스크립트 실행 → Unity `SevenSteps.Art.BoardShapeABuilder.BatchPrepare`. 리뷰 빌드: `BoardShapeABuilder.ReviewWindows`, 플레이어 `-boardshape-capture <공유 unity 폴더>`.
4. `design/art/compare-board-shape-a.ps1`로 3열 비교/7행성 시트를 재생성한다. 기본 CameraRig를 바꾸지 않고 직교 제안을 먼저 검토한다.
