# BoardShapeA 후속: 7행성·형태·이끼·베이크 그림자

기준은 `0e32fd362945cd74326f6ba46c5f00964b417e64`다. `93477aa`의 행성별 색·하늘·원경·큰 실루엣을 새 키트에 복원했고, 추가 검토의 형태·이끼 지오메트리·그림자 요청도 반영했다. 규칙/Core/스테이지 데이터는 바꾸지 않았다.

## 실제 캡처

공유 증거 루트는 `C:/unityjam/shared-assets/seven-steps/concepts/themes/board-shape-2026-11-10-r2/unity/distinct-pass/`다. `before/`에 이전 완료 시점 PNG/CSV를 보존했다.

| 파일 (`after/` 아래) | 내용 |
|---|---|
| `8x6-blender-unity-concept.png` | Blender 렌더 · Unity 실제 · R2 A hero, 요청한 3열 순서 |
| `12x5-blender-unity-concept.png` | 긴 보드, 같은 3열 |
| `10x3-blender-unity-concept.png` | 얇은 보드, 같은 3열 |
| `seven-planets-before-after.png` | 7행성 실제 시트 전후 |
| `seven-planets-unity.png` | 새 7행성 실제 Unity 화면 |
| `hero-unity-default-proposed.png` | 이전 보고의 시안 · Unity 기본 · Unity 제안 3열도 유지 |
| `moss-7x7-proposed.png` | 정사각 보드 추가 검증 |
| `blender-01-forms.png` | 넓은 모따기·분리 연석·피라미드 캡·각진 바위 단계 |
| `blender-02-moss.png` | 실제 부피가 있는 이끼 단계 |
| `blender-03-baked.png` | 베이크 후 MCP 카메라 렌더 |
| `blender-shadow-bake-proof.png` | 아틀라스에 저장한 8개 오프라인 그림자 마스크 |
| `unity-review-geometry.csv` | 실제 tris·재질·셀 크기·숫자 글리프 높이 |

Blender/Unity는 pitch 50, yaw 0, 1280×720 직교 화면을 쓴다. 제안 프레이밍은 8×6에서 orthographicSize 4.65, 타깃 `(0,-.75,0)`, 거리 30이며 다른 크기는 같은 수식으로 맞춘다. Blender 열은 키트 Cycles 렌더이고 캐릭터/HUD는 Unity 열에만 있다. R2 A hero는 원본 일러스트다. 기본 게임 카메라·AppConfig·정식 씬 연결은 변경하지 않았다.

## 7행성 구분

| 행성 | 복원한 차이 |
|---|---|
| moss | 녹회색 석재, 올리브 이끼 쿠션/띠, 청록 하늘/원경, 두 선돌 |
| hollow | 보라 암석/성운, 고리 원경 행성, 앞쪽 실제 열린 동굴 아치 두 개 |
| carved | 황토색 석재/하늘, 3단 절삭 지층과 금갈색 층 띠 |
| frost | 청백색 석재/원경, 얼음 첨탑, 환경 이끼/풀 지오메트리 0, 타일 이끼 스와치는 서리로 대체 |
| joinery | 갈색 석재/하늘, 외부 목조 보강틀과 장부 다리, 최소 이끼 |
| crumble | 적회색 석재/연무, 얕고 깨진 지각과 분리된 부유 파편 |
| altar | 황금 올리브 석재/하늘, 고리 원경, 높은 선돌 6개로 된 링 |

하늘·원경은 같은 하늘 결합 메시, 지각·보강·장식·이끼는 같은 환경 결합 메시다. 환경 렌더러/재질은 계속 하나다. 색은 `ChapterPreset.materialOverrides`, 큰 실루엣은 환경 프리팹과 `IStageEnvironment.Fit`이 결정한다.

## 형태·이끼·문

타일의 실제 모따기는 .07m이며 중앙은 비워 둔다. 연석은 1m 피치에 폭 .968m, 모따기 .07m로 블록 사이 V형 줄눈을 만든다. 모서리 캡은 피라미드 메시다. 지각은 넓고 각진 단면과 틈으로 만들고 하단의 동일한 둥근 돌기 반복을 없앴다. 회전·깊이·높이는 결정적 해시로 섞는다. 선돌은 높이별 단면이 달라지는 5면 다면체다. ledge에는 겹친 판석, 높이가 다른 돌무더기와 이끼 패치를 놓았다.

이끼는 중심이 솟은 21-tri 쿠션이다. moss의 내부 줄눈 교차점은 42% 선택 확률로 배치하며, 연석 아래와 지각 상단 틈에도 띠/패치를 섞는다. 어두운 뿌리 `#4E5B2E`, 밝은 끝 `#8C9A45`를 선형 색 변환한 아틀라스와 정점색을 쓴다. frost는 환경 쿠션/풀을 생성하지 않는다. 타일 중앙 숫자 영역에는 놓지 않는다.

숫자 문은 깊이 .34m의 석재 아치·양면 테라코타 테두리·아치형 청록 판이다. 기존 프리팹/TileSet GUID를 유지했다. 선택적 `TileVisual.labelScale`은 0이면 기존 기본값, 양수이면 셀 폭에 대한 글자 크기다. `BoardView`의 View 처리와 아키텍처 7.2에만 이를 추가했다.

| 크기 | 셀 투영 높이 | 실제 Jua 숫자 높이 | 비율 |
|---|---:|---:|---:|
| 8×6 제안 | 59.31px | 38px | 64.1% |
| 12×5 | 46.37px | 30px | 64.7% |
| 10×3 | 53.06px | 35px | 66.0% |
| 7×7 | 56.73px | 37px | 65.2% |

문 높이는 약 .80m여서 기존 보수적 .6m 경고 4건은 숨기지 않았다. 대신 문 전체 AABB와 뒤 셀 바닥 중심의 시선을 독립 비교해 pitch 50에서 가리지 않는 것을 단정 검사한다. 다른 pitch 도입 시 재검증해야 한다.

## 베이크와 빛

공유 아틀라스는 1024×512 두 장이다. high → low Cycles 선택-활성 노멀·AO 베이크를 유지한다. 미세 AO는 RGB에 곱해 저장하고 Blender BVH의 실제 모델 교차 검사로 구한 접촉 AO를 정점색에 추가한다.

ColorAO의 남는 알파에 연석 가로/세로·선돌·상자·담·뒤 문·돌무더기·문 아치의 방향 그림자를 베이크했다. Blender에서 고도 40도 왼쪽 위 광원으로 반그림자 4광선, 짧은 접촉 8광선을 쓴다. `BoardShapeAShadowField`가 Fit 로컬 좌표/프로필을 전달하고 기존 바닥/암석 셰이더가 알파를 샘플한다. 별도 그림자 렌더러, 세 번째 텍스처, 실시간 그림자/SSAO는 없다. 회전/비균일 스케일 장식은 공유 모듈 프로필을 근사 적용하므로 Blender 전체 장면 Cycles 그림자와 픽셀 단위로 같지는 않다.

캐릭터·석상·문·벽·FlipUp/FlipDown·레버는 View 전용 footprint 등록으로 불투명도 .52 블롭을 만든다. 중심이 오른쪽/아래로 치우치며 같은 방향으로 늘어난다. 기존 바닥 프래그먼트에서 계산해 추가 draw/게임 규칙 의존성이 없다. 최대 64개 영역이며 정적 영역은 48개 미만으로 단정하고 동적 영역 최소 16개 여유를 둔다.

Unity 키는 Euler `(40,125,0)`으로 화면 왼쪽 위에 두었다. 연석 외벽/지각 측면의 명도 계수 .48, 따뜻한 키와 낮은 중성 채움으로 윗면과 옆면을 구분한다. 그림자 없는 Mobile 프리셋·HDR=false를 유지한다.

얼음도 같은 키트 셰이더를 쓰되 `_SurfaceAlpha=.92`로 반투명도를 별도 지정한다. 그림자 마스크용 아틀라스 알파가 얼음 표면을 지우지 않도록 분리했다. 기존 FrostOpaque GUID/한 draw는 유지한다.

## 검증·성능

CompileCheck, GlyphCheck(missing 0), TileSet(errors 0), 기존 GUID 131개 안정성, 7행성×4크기×반복 Fit, 한 칸 장식 여유, 한 환경 draw 검사가 통과했다. EditMode는 **776/776** 통과했다. 기존 UI 경고 38개와 placeholder 경고 1개는 별도 기존 항목이다.

최대 clean board는 carved 12×5의 **26,692 tris**(환경 22,372 + 타일 4,320)로 50k 이하이다. moss 8×6 실제 리뷰 root(보드·기믹·캐릭터, 하늘/HUD 제외)는 18,496 tris, 렌더러 62개, 재질 4종이며 환경은 하나의 결합 draw다. WebGL 하늘은 별도의 기존 SkyLow 재질/한 draw다. 나머지 root 렌더러는 타일·기믹·캐릭터다.

성능은 일반 Chrome 157.0.8103.50, RTX3060/ANGLE D3D11, canvas1280×720, DPR1, Mobile render scale .8, HDR=false, 준비10초+측정10초, 1·29·50 각각 3회다. 이전 빌드 복사본은 이전 완료 빌드 SHA256과 바이트 단위 동일함을 확인했다. 전후 raw JSON/console/화면은 공유 `performance/`에 둔다. 중간 측정은 `iteration1-after.json`으로 보존한다. 최종 결과는 아래에 기록한다.

| 스테이지 | 이전 FPS 3회 | 최종 FPS 3회 | 실제 WebGL draws 전→후 | 전체 3D mesh tris 전→후 |
|---|---|---|---|---|
| 1 moss | 59.50 / 59.61 / 60.00 | 60.00 / 60.00 / 60.00 | 45 → 45 | 16,508 → 18,453 |
| 29 joinery | 60.00 / 60.00 / 59.01 | 60.00 / 60.00 / 60.00 | 62 → 62 | 19,144 → 22,293 |
| 50 altar | 58.51 / 60.00 / 58.51 | 60.00 / 60.00 / 60.00 | 98 → 98 | 28,412 → 25,708 |

모든 최종 샘플의 p95 프레임 간격은 17ms다. tris 열은 하늘을 포함한 실제 mesh 합계이며 HUD 글자 삼각형은 제외한다. 50번은 세 번 모두 약 60 FPS를 유지했고 draws는 증가하지 않았다. 기준 빌드에도 공유 호스트 타이밍 변동이 있어 작은 FPS 차이를 GPU 속도 향상으로 해석하지 않는다. 최종 WebGL 빌드는 오류 0, 17,799,491 bytes이고 검증용 스탬프는 `0.1.0+0e32fd3-dirty`다. SHA256과 로그는 `verification/`에 보존한다.

최종 smoke-webgl도 통과했다. 1/29/50번의 격리 보드 픽셀은 **44,474 / 61,484 / 54,821**(기준 500 이상), 숨김 대조군은 **0**(500 미만), 콘솔 예외는 0이다. 결과는 `verification/smoke-final/`에 복사했다. 얼음 수정 전 9회 측정은 `iteration2-after.json`으로 보존하고 위 표에는 수정 후 최종 9회만 썼다.

공유 루트의 `completion-report.md`에는 3열 비교 3장과 7행성 전후 시트를 바로 볼 수 있게 포함했다.

## 리드 연결

- 이 후속 커밋을 `0e32fd3` 위에 병합하고 공유 BoardShapeA FBX/PNG/.meta도 동기화한다. 바이너리는 Git에 넣지 않는다.
- `Chapter_<id>` materialOverrides, `PlanetSummit_<id>`, `Sky_<id>`가 새 키트를 참조한다. AppConfig·정식 씬에 ChapterPreset들을 연결한다.
- 기본 카메라는 유지했다. 제안 프레이밍을 채택하면 위 수식/타깃을 리드의 CameraRig/HUD 경로에서 적용한다.
- 비교/성능 기준은 `0e32fd3`다. 이후 리드의 `891ba0b` 쪽 BoardView.Fit 보정은 본 패스에 섞지 않았다. 병합 시 새 labelScale 처리를 유지하고 최종 HUD 프레이밍을 재확인한다.
- 원본: `C:/unityjam/shared-assets/seven-steps/Source/Blender/SS_BoardShapeA.blend`. MCP로 `build_board_shape_a.py`를 실행해 모델·베이크·FBX를 재생성하고 `render_board_shape_a_review.py`로 3개 크기 렌더를 만든다.
- Unity는 `BoardShapeABuilder.BatchPrepare`로 연결/단정 검사를 재생성한다. `ReviewWindows`와 `compare-board-shape-a.ps1 -Output ... -Before ...`로 비교를 다시 만든다.
