# 석재 질감 후속 — 7개 석종과 물성 베이크

기준 커밋은 `4760464`다. 회색 판에 같은 크랙을 덮거나 행성별 tint만 바꾸던 처리를 제거하고, 사용자 지시 석종별 색·입자·미세 요철·러프니스·AO를 두 장의 공유 아틀라스에 베이크했다. 큰 7행성 실루엣, 문, 이끼 배치와 그림자는 유지했다. 새 행성별 r2 시안은 작업 시점에 아직 전달되지 않아 아래 사용자 석종 매핑을 기준으로 구현했다.

## 석종

| 행성 | 석종/아틀라스 슬롯 | 표면 표현 |
|---|---|---|
| moss | 회백 화강암, 0/1/14/15 | 장석·석영·드문 운모의 불규칙 입자, 스와치 기준 약 6–8mm 도메인(UV/모듈 스케일에 따라 달라짐), 세 판석 변형 |
| hollow | 현무암·부석, 8 | 어두운 치밀한 바탕과 국소적인 밝은 다공질 영역, 크기 다른 기공 |
| carved | 사암, 9 | 모래 입자, 낮은 대비의 물결치는 층리, 짧은 정 자국 |
| frost | 대리석·석영, 10 | 밝은 방해석 바탕, 약하고 가지친 광물맥, 석영 포인트, 낮은 러프니스 |
| joinery | 점판암 + 기존 목재, 11 | 어두운 청회색 바탕, 방향 있는 엽리, 드문 얇은 박리 흔적 |
| crumble | 석회암·트래버틴, 12 | 크림색 바탕, 작은 공극과 길쭉한 용식 흔적, 약한 층 |
| altar | 사르센 사암, 13 | 단단한 회갈색 사암, 더 촘촘한 입자와 풍화 얼룩 |

석종은 `_BoardStoneSlot`으로 실제 서로 다른 아틀라스 영역을 선택한다. `_BoardRockTint`는 흰색이다. 기존 행성 하늘·원경·동굴·계단·목조 보강·파편·선돌 링은 보존해 색과 형태 구분을 함께 유지한다. frost의 이끼는 계속 0이다.

## 형태와 반복 억제

- 넓은 .07m 모따기를 유지하되 판석 bevel ring 높이를 불균일하게 하고, 석재 box 일부 꼭짓점을 4–17mm 안쪽으로 깎아 작은 spalling을 넣었다. 중심은 깨끗하게 둔다.
- 화강암 판석 3종의 윗면 균열은 각각 0·1·2개다. 옆면에는 윗면 크랙을 복사하지 않는다. 다른 석종도 다각형 크랙 네트워크를 사용하지 않는다. 대리석의 옅은 광물맥은 구조적 균열과 구분한다.
- 물때는 윗부분에서 아래로 내려오는 가느다란 얼룩이다. 지의류는 낮은 대비의 드문 점·군집으로 만든다. 정 자국과 층리도 광물 입자와 별도 주파수/방향을 쓴다.
- FBX 면마다 UV crop/offset을 달리한다. 단일 타일은 object-origin seed, 환경 결합 메시의 각 블록은 UV2.y의 결정적 seed를 추가 사용한다. 팔레트 셀 경계를 넘지 않도록 clamp한다.
- `CombineMeshes` 뒤 정점 수와 UV seed cursor가 정확히 일치하는지 단정 검사한다. 결합 렌더러를 늘리지 않는다.

## 베이크와 런타임

Blender MCP로 `build_board_shape_a.py`를 실행한다. `stone_recipes.py`의 각 석종 height field를 128×128 실제 high-poly relief로 만들고 Cycles selected-to-active로 low receiver에 노멀·AO를 베이크한다. 색·러프니스의 물성장은 같은 recipe에서 함께 저장한다. 정점 접촉 AO와 기존 정적 그림자 베이크도 유지한다.

런타임은 계속 **두 장**, 각각 **1024×1024**, 4×4개 256px 스와치다.

| 파일 | 채널 | Unity importer |
|---|---|---|
| `BSA_ColorAO.png` | RGB: 색 + 미세 AO, A: 기존 8개 정적 그림자 프로필 | Default, sRGB, alphaIsTransparency=false |
| `BSA_NormalRough.png` | RG: 탄젠트 노멀 XY, B: 러프니스, A: AO | **Default, linear**, RGBA uncompressed |

노멀 텍스처를 NormalMap 타입으로 가져오면 Unity의 swizzle이 B/A 물성 채널을 훼손한다. 빌더가 Default/linear로 강제하고, 실제 PNG의 러프니스 범위도 검사한다. 셰이더는 RG에서 노멀 Z를 재구성하며 같은 texture fetch로 러프니스와 AO도 읽는다.

윗면은 map roughness에서 .12를 내려 닳은 표면의 반사를, 옆면은 .20을 더해 거친 면을 표현한다. 고정 specular exponent 대신 roughness에 따라 반사 폭과 세기를 바꾼다. 그림자용 아틀라스 알파와 얼음 opacity .92는 계속 독립이다. 비-BSA 렌더 경로와 게임 규칙은 바꾸지 않는다.

이전 두 장은 1024×512였으므로 RGBA8+mip 기준 GPU texture 용량은 약 **5.33 → 10.67 MiB**다. 아틀라스/샘플/재질/환경 draw 수는 증가하지 않지만 texture 메모리는 증가한다. 7개 장별 텍스처 쌍을 별도로 만들지 않았다.

## 실제 증거

공유 루트: `C:/unityjam/shared-assets/seven-steps/concepts/themes/board-shape-2026-11-10-r2/unity/stone-pass/`.

- `before/`: 4760464의 실제 화면을 보존했다.
- `after/blender-stone-library.png`: 실제 베이크를 쓴 Blender 7석종 샘플.
- `after/blender-bake-color.png`, `blender-bake-normal.png`, `blender-bake-roughness.png`, `blender-bake-ao.png`: 패킹 전처럼 읽을 수 있게 분리한 실제 채널 증거. Assets 밖의 검토용 이미지로 런타임 아틀라스는 늘지 않는다. `packed-channel-proof.json`에 슬롯별 실제 roughness/AO 범위를 기록했다.
- `after/stone-granite-mcp.png`, `blender-03-baked.png`: 같은 pitch 50의 Blender MCP 렌더.
- `after/<id>-stone-closeup.png`: 실제 Unity 카메라를 가까이 옮긴 1280×720 화면. 이전 화면용 숫자/HUD는 숨긴다.
- `after/seven-stone-closeups-unity.png`: 7석종 실제 Unity 근접 시트.
- `after/seven-planets-unity.png`, `seven-planets-before-after.png`: 전체 7행성 실제 화면과 전후 비교.
- `after/moss-stone-before-after.png`: 회백 화강암 전체 화면 전후.
- `after/8x6-blender-unity-concept.png`, `12x5-blender-unity-concept.png`, `10x3-blender-unity-concept.png`: Blender | Unity 실제 | 기존 R2 A hero 3열.
- `after/unity-review-geometry.csv`: 실제 tris·재질·숫자 투영 크기.

Blender 열은 Cycles 키트 렌더이고 Unity 열만 실제 게임 렌더다. 확대 캡처를 전체 화면 해상도 개선처럼 표시하지 않는다. 새 행성별 r2 시안과의 최종 미술 승인은 별도 코디네이터 검토 사항이다.

## 검증·성능

기준 WebGL 빌드는 4760464의 완료 빌드를 복사해 보존했다. 일반 Chrome/RTX3060/ANGLE D3D11, 1280×720 canvas, DPR1, Mobile render scale .8, HDR=false, 준비10초+측정10초로 1·29·50번을 각각 3회 전후 측정했다. 측정 동안 Blender 렌더/Unity 빌드를 중지했다.

CompileCheck, GlyphCheck(missing 0), TileSet(errors 0), GUID 131개 안정성, 7행성×4크기×반복 Fit, 장식 한 칸 여유, 환경 1 draw, packed texture 형식/러프니스 범위/장별 recipe 일치 검사를 통과했다. EditMode **776/776** 통과, WebGL 빌드 **오류 0 / 20,452,616 bytes**다. 기존 UI 경고 38개, 문 높이 경고 4개, placeholder 경고 1개는 이전과 같다.

최대 clean board는 carved 12×5 **26,692 tris**, 환경 22,372 tris로 이전과 같다. 리뷰 root(하늘/HUD 제외)의 moss 8×6은 18,496 tris·렌더러 62개·재질 4종이다. 숫자 글리프는 제안 8×6에서 38px / 셀 높이59.31px = 64.1%로 유지된다. 이전 완료 WebGL 17,799,491 bytes보다 압축 빌드가 2,653,125 bytes 늘었다. baseline의 4개 Build 파일 SHA256은 이전 완료 기록과 모두 일치했다.

| 스테이지 | 이전 FPS 3회 | 이후 FPS 3회 | 실제 WebGL draws 전→후 | 전체 3D mesh tris 전→후 |
|---|---|---|---|---|
| 1 moss | 60.00 / 60.00 / 60.00 | 60.00 / 60.00 / 60.00 | 45 → 45 | 18,453 → 18,453 |
| 29 joinery | 60.00 / 60.00 / 60.00 | 60.00 / 60.00 / 60.00 | 62 → 62 | 22,293 → 22,293 |
| 50 altar | 60.00 / 60.00 / 60.00 | 60.00 / 60.00 / 60.00 | 98 → 98 | 25,708 → 25,708 |

전후 각 9개, 총 18개 원본 샘플을 모두 보존했다. 모든 샘플 p95는 17ms다. 59.998~60.004 FPS의 미세한 카운터 차이는 반올림했으며 성능 향상으로 해석하지 않는다. 최종 콘솔 예외/오류는 0이다. mesh tris는 하늘 포함/HUD 글자 제외다. Chrome 버전은 157.0.8103.50이다. 원본 JSON/console/화면/SHA256은 공유 `performance/`에 있다.

최종 smoke-webgl도 통과했다. 1/29/50번 격리 보드 픽셀은 **44,462 / 61,326 / 54,679**(기준 500 이상), 보드 숨김 대조군은 **0**(500 미만), 콘솔 오류 0이다. `verification/smoke-final/`에 원본을 보존했다.

## 리드 적용

- 이 후속 커밋을 4760464 위에 병합한다. 기존 FBX·프리팹·머티리얼 GUID를 유지했다.
- 공유 BoardShapeA FBX/PNG뿐 아니라 **갱신된 texture .meta**도 동기화한다. 기존 NormalMap importer를 유지하면 물성 채널이 깨진다. 바이너리는 Git에 넣지 않는다.
- 원본은 Assets 밖 `C:/unityjam/shared-assets/seven-steps/Source/Blender/SS_BoardShapeA.blend`다. main build와 `stone_recipes.py`, `stone_atlas_bake.py`를 함께 두어 MCP로 재생성한다.
- `render_stone_library.py`, `render_board_shape_a_review.py`, `proof_stone_channels.py`, `BoardShapeABuilder.ReviewWindows`, `compare-stone-pass.ps1`로 증거를 재생성한다.
- AppConfig·정식 씬·기본 카메라는 이 패스에서 바꾸지 않았다. 기존 리드 연결 책임은 유지된다.
