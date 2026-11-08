# Seven Steps — Blender 제작·Unity 전달 규격

> 현행 확정 방향은 [아트 바이블](art-bible.md)과 이 문서 마지막 ‘현행 유적×선돌 키트 전달’이다. 아래 초기 Dipper 추천은 보존된 탐색 기록이다.

2026-11-08. 북두칠성 퓨전 세 안을 실제 메시 키트로 만들기 위한 설계다. **추천은 먹돌 관측대(기존 slug `dipper-ink-engraving`)**이며, 청자 기하 관측대와 별지도 책의 무대도 같은 모듈 규격을 쓴다. 이번에는 Unity 프로젝트를 변경하지 않는다.

## 형태와 작업 분담

| 항목 | Blender에서 제작 | Unity URP에서 제작 |
|---|---|---|
| 실루엣 | 두꺼운 블록, 6–8면 망토, 8면 머리, 낮은 문·손잡이, 1–2단 베벨 | 위치·회전·짧은 스케일 트윈, KEEP/SWITCH 상태 |
| 먹돌/청자/금속 | 넓은 평면과 모서리 베벨, 공통 UV0 팔레트 배치 | Simple Lit의 색/roughness 대응 smoothness·metallic. 투명·SSS 없음 |
| 판화 선 | 물리적 홈이나 가는 튜브를 모델링하지 않음 | **텍스처**: 외곽 띠에만 저대비 인쇄선. 생산 아틀라스의 작은 trim 영역 사용. 전체 화면 셰이더 아님 |
| 한지 섬유 | 종이 시트·섬유 메시 없음. 먹선안도 플레이 바닥은 단단한 은회 석재 | **셰이더**: 선택적 저주파 미세 노이즈를 알베도에 ±3% 이내. 잼 기본값 OFF. 별도 종이 텍스처 없이 성립해야 함 |
| 그림책 페이지 | 책표지1개+페이지 블록2개, 직선 홈. 낱장·종이 구름·말린 종이 없음 | 색 그라디언트 배경, 오른쪽 빈 패널의 UI. 그림 배경은 필수 아님 |
| 얼음 | 바닥 메시 재사용, 넓은 홈 3개는 블록아웃의 인레이 또는 얕은 형상 | **셰이더/텍스처**: 불투명 청백+3개의 평행선 마스크. 굴절·Opaque Texture·투명 정렬 없음 |
| 숫자·주별·발자국 | 메시/UV에 굽지 않음. 출력용 글자는 `90_PREVIEW` 컬렉션에만 | **화면 라벨(uGUI)**: 문 숫자·석상 아이콘·1–7 별 HUD. **얕은 Quad 데칼**: 발자국·시작원·7 도장. Decal Renderer Feature는 필수 아님 |

모든 베벨은 폭0.015–0.025m를 기본으로 하고 판 외곽만0.04–0.06m까지 허용한다. Subdivision Surface·초미세 해칭 홈·천 시뮬레이션·다층 유리는 쓰지 않는다. 폴리 수는 **모디파이어 적용 후 삼각형**으로 센다. Unity에서 실제 측정하기 전에는 아래 값을 성능 보증으로 해석하지 않는다.

## 셀·피벗·크기

[아키텍처 12장](../../docs/architecture.md) 기준: 1칸=1유닛, 바닥 윗면 Unity y=0, 칸 중앙 피벗, 벽·문·올라온 벽 높이≤0.6, 몸≤1이다. Blender는 Z-up이므로 바닥 윗면z=0·발바닥z=0으로 작업한다. 바닥 본체는 아래로0.12m, 셀 내부 메시 폭은0.96m로 하여 셀간 틈0.04m를 남긴다. 원점은 바닥 윗면의 중앙이며, 메시 중심에 원점을 자동 재설정하지 않는다. 문·레버의 자식 회전축만 별도로 둔다.

## 공통 모듈 키트 — 17종

삼각형 범위는 본제작 목표이며 행의 최대치가 예산이다. 재질 슬롯은 **단일 모듈 안의 슬롯**이며 전역 공유 재질 종류와 다르다. 같은 머티리얼을 쓴 부품은 전달 전 가능하면 합친다. 프리팹의 상태 자식은 기능 때문에 분리할 수 있다.

| 모듈 / 이름 | 최대 외형 W×D×H(m) | 예상 tris | 슬롯 | 애니메이션·상태 |
|---|---|---:|---:|---|
| 바닥 `SS_Tile_Floor` | .96×.96×.12 (윗면0) | 44–96 | 1 | 없음. 회전 가능한 무방향 무늬만 randomYaw90 |
| 얼음 `SS_Tile_Ice` | .96×.96×.12 | 44–240 | 1 | UV선은 정적, 미끄러짐은 액터 트윈 |
| 벽 `SS_Tile_Wall` | .90×.90×.55 | 44–120 | 1 | 없음 |
| 숫자 문 `SS_Tile_NumberDoor` | .86×.30×.55 | 132–240 | 2 | `GatePanel` 열림/닫힘 자식. 숫자는 화면 라벨 |
| 석상 수 문 `SS_Tile_CountDoor` | .86×.30×.55 | 132–240 | 2 | 같은 구조, 청자 테두리·별돌 배지로 구별 |
| 레버 `SS_Tile_Lever` | .55×.40×.40 | 120–240 | 2 | `HandlePivot`1축 회전. 베이스는 고정 |
| 뒤집힘 벽 상 `SS_Tile_FlipWall_Up` | .88×.24×.55 | 44–120 | 1 | variant0; 코드 트윈/교체 |
| 뒤집힘 벽 하 `SS_Tile_FlipWall_Down` | .88×.24×.05 | 44–96 | 1 | variant1; 별도 슬롯 메시, 동일 위치 |
| 발판 `SS_Tile_Plate` | .70×.70×.04 | 44–96 | 1 | 눌림 트윈≤.02m; a/b/c 라벨·색은 Unity |
| 구덩이 `SS_Tile_PitRim` | .96×.96×.12 | 176–240 | 1 | 가운데는 실제 빈 공간, 보드 밑면을 깊게 표시 |
| 다리 `SS_Tile_Bridge` | .90×.96×.10 | 132–240 | 1 | 생성 팝만. 넓은 판3개, 밧줄/목재결 형상 없음 |
| 금 간 바닥 `SS_Tile_Cracked` | .96×.96×.12 | 88–180 | 1 | 2조각 또는 폭 넓은 균열1개. 붕괴는 자식 하강/비활성 |
| 출구 `SS_Tile_Exit` | .74×.74×.05 | 44–120 | 1 | 낮은 패드+화면 출구 배지. 높은 포털 없음 |
| 테두리 모서리 `SS_Border_Corner` | .30×.30×.18 | 88–160 | 1 | L자 블록, 보드 밖 전용 |
| 테두리 직선 `SS_Border_Straight` | 1.00×.15×.18 | 44–96 | 1 | 보드 밖 전용. 반복 부품 |
| 플레이어 `SS_Actor_Player` | .44×.35×.90 (프록시 깊이≤.44) | 100–480 | 1 | 기본 리그 없음, 루트 이동·살짝 기울기 |
| 석상 `SS_Actor_Statue` | 플레이어와 동일 | **같은 메시** | 1 | Animator 없음. 재질/팔레트만 돌색으로 |

발판과 연결된 일반 문 A/B/C는 별도 조형을 추가하지 않고 `SS_Tile_CountDoor`의 배지를 a/b/c로 바꾼다. 숫자 문1–7도 하나의 메시와 라벨7종이다. 시작점·이전 시작점·발자국·행운 표시는 모두 Quad/라벨로 처리하며 17종 메시 수에 넣지 않는다.

6×6–7×7 보드의 계획 예산: 바닥 포함 가시 기하5만 tris 이하, 액터 최대5개, 공유 머티리얼6종 이하, 주요 메시당1–2서브메시. 작은 고정 카메라에서 LOD 전환은 필요하지 않다. 폴리 감축보다 투명 overdraw·다수 재질·전체 화면 패스를 먼저 피한다. SRP Batcher가 draw call을 무조건 하나로 합치는 것은 아니므로 실제 프레임 디버거 확인은 후속 단계다.

## 팔레트 아틀라스·UV·재질

생산 기본은 **공유 팔레트 그라디언트 아틀라스1장, 최대512×512 RGBA**다. 블록아웃 스크립트는256×256 RGB8색 띠와 하단32px의 작은 구름문 trim을 한 장에 생성한다. 색 lookup UV는v≥.15, trim은v<.125라 서로 겹치지 않는다. 원본 .blend 안에도 동일 이미지를 연결하고 FBX에는 임베드하지 않는다. 최종 원본 텍스처와 .meta는 공유 폴더에만 둔다.

- UV0는 색 띠를 참조한다. 단색면은 최소4×4px 영역, 띠 경계에서4px 패딩, 미세한 명도 변화는 세로 그라디언트로 해결한다. UV seam은 망토 뒤·바닥 아래·블록 뒤쪽으로 보낸다. 색 lookup 방식에는 ‘미터당 texel’ 개념을 강제하지 않는다.
- 무늬가 있는 외곽 trim만 **128px/m**를 기준으로 하며1m부품은128px 길이, 허용 오차±10%. 패딩4px·최소 선폭2px. 팔레트와 trim을 한 장에 배치한 생산 아틀라스에서만 적용한다. 바닥 중앙에는 선·섬유 무늬를 넣지 않는다. 보조 UV1은 이 trim/절차노이즈의 일관된 크기 좌표용이며 두 번째 텍스처를 뜻하지 않는다.
- URP 표면 재질4종: `SS_M_OpaqueAtlas`(돌/옷/테두리, metallic0 smoothness≈.2), `SS_M_MetalAtlas`(metallic≈.55 smoothness≈.6), `SS_M_GlazeAtlas`(불투명 smoothness≈.72), `SS_M_FrostOpaqueAtlas`(불투명 smoothness≈.55). 같은 Opaque 셰이더의 돌색 변형 `SS_M_StoneAtlas`1종과 라벨/Quad용 `SS_M_MarkUnlit`1종을 더해 총6종이다. 배경은 카메라 clear color를 사용하고 스튜디오 미리보기 재질은 게임에 넣지 않는다.
- Blender roughness와 URP smoothness는 단순히 숫자만 대응시켜 출발한 뒤 실제 광원 아래에서 비교한다. 셰이더 노드가 FBX로 이식된다고 가정하지 않는다. Blender Object Info의 tint는 프록시 표현이고 Unity는 같은 셰이더를 쓰는 석상 머티리얼의 색 파라미터로 재구성한다. 런타임 Material 인스턴스를 매번 생성하지 않는다.
- 알파=1, Normal Map·Emission·Clear Coat 기본 OFF. 얼음의 활주 홈과 청자의 넓은 가장자리 반사는 크고 단순하게 한다. 숫자·국자·발자국을 UV0에 굽거나 메시로 돌출하지 않는다.

팔레트는 변형마다 색8개 이내로 재배치하되 재질 클래스·UV 구조는 공유한다. 먹선은 은회/밤먹, 데코는 빙옥/법랑, 그림책은 청백/보라/제본 남색을 쓴다. 재질 보드는 외형 기준이며 색 값·노드의 권위는 이 문서와 실제 머티리얼이다.

## 캐릭터와 석상

추천 기본은 **리그 없는 트윈**이다. 8면 머리+6–8면 잘린 원뿔 망토+짧은 발2개, 외투 앞의 굵은 접힘1개만 모델링한다. 신체가 망토 안에 들어가므로 걷기 뼈 없이도 한 칸 이동·기울기±5°·높이.02m 이내 바운스로 충분하다. 얼음은 기울기만 줄이고 위치 트윈, 낙하는 루트z 하강, 석화는 같은 메시의 무광 재질 교체·정지다. 생산 시 플레이어와 석상은 동일 Mesh GUID를 참조한다.

| 안 | 추가 제작 | 장점 | 비용·제약 |
|---|---|---|---|
| **리그 없음 — 권장** | 루트 트윈, 피벗발바닥0 | 동일 석상 재사용, FBX 축/리그 문제 최소, 적은 런타임 비용 | 발걸음은 추상적. 본체 변형 없음 |
| 단순 Generic4뼈 | Root/Hip/LegL/LegR, 필요하면 Cloak1 추가 | 짧은 보폭·석화 포즈 가능 | 스킨·루프·축 검증 약0.5–1인일 추가. 망토는 rigid weight, 천시뮬 없음 |

프록시 턴어라운드의 앞/옆/뒤/3/4는 전부 같은 조형이어야 한다. 그림이 각 view에서 다르게 생성되어도 정점 구조를 그대로 복제하지 않는다. 앞 방향은 Blender -Y, 위는+Z, 오른쪽+X로 고정하며 파일에 작은 축 기준 Empty를 두되 게임에 내보내지 않는다.

## 컬렉션·이름·내보내기

```text
01_EXPORT_LIBRARY
  SS_Tile_Floor          (Empty, origin 0,0,0)
    FloorMesh
  SS_Tile_NumberDoor
    FrameMesh
    GatePanel
  SS_Tile_Lever
    BaseMesh
    HandlePivot          (회전 중심)
      HandleMesh
  SS_Actor_Player
    CloakSharedMesh
  SS_Actor_Statue        (동일 Mesh, 돌 재질)
02_BOARD_INSTANCES       (배치 미리보기, 내보내지 않음)
90_PREVIEW_LABELS_NO_EXPORT
99_STUDIO_NO_EXPORT     (카메라·조명·배경)
```

Object와 Mesh는 `SS_범주_이름[_상태]`, 재질은 `SS_M_*`, 텍스처는 `SS_Palette_*`. `.001` 자동 이름과 음수 스케일은 정리한다. Modifier 적용 후 삼각형을 세고 custom split normals 또는 flat face+베벨 경계를 유지한다. 넓은 평면에 과한 smooth shading을 넣지 않는다.

**FBX를 우선**한다. 이 프로젝트에 glTF importer 추가가 필요하지 않기 때문이다. Blender Scene Units Metric, Unit Scale1, 1m셀. Mesh의 Rotation/Scale을 Apply하여0/1로 정리하고 원점은 유지한다. Selected Objects만 내보내며 object types Mesh+Empty, Scale1, Apply Unit ON, Apply Scalings FBX Units Scale, Forward **-Z**, Up **Y**, Apply Modifiers ON, static Apply Transform ON(스크립트 `bake_space_transform=True`), Add Leaf Bones OFF, Bake Animation OFF, Camera/Light 제외, Path Mode Auto, Embed Textures OFF다. 기본 축 변환과 별도로 파일을90° 수동 회전하지 않는다.

스켈레톤을 채택하면 **Apply Transform의 Armature/애니메이션 영향은 별도 검증**한다. 정적 메시 설정을 그대로 확정하지 않고4뼈 테스트파일로 축·클립을 확인한다. 가만히 서 있는 캐릭터·무기 노드는 불필요하다. glTF가 꼭 필요할 때만 아키텍트와 importer를 확인한 뒤 `.gltf + .bin + 외부 atlas`로 내보낸다. 어느 방식도 Blender의 셰이더 노드를 URP 머티리얼로 그대로 옮기지 않는다.

Unity 후속 임포트(이번 미수행): Scale Factor1, Convert Units ON, Normals Import, Read/Write OFF, Import Cameras/Lights OFF, Generate Colliders OFF, 불필요한 Animation OFF. 자동으로100배/0.01배가 나오면 프리팹 scale로 덮지 말고 export units와 Convert Units를 맞춘다. **1m 기준 큐브와 바닥 Bounds**를 먼저 대조한다. root rotation0·scale1, 바닥윗면y0·캐릭터발y0 확인 후 진행한다. 축 변환의 정답은 화면의 ‘그럴듯함’이 아니라 이 검사다.

원문: [Blender FBX Manual](https://docs.blender.org/manual/en/5.3/files/import_export/fbx_legacy.html), [Unity Model Import Settings](https://docs.unity.com/en-us/engine/6000.0/manual/assets-and-media/asset-types/models/importing/fbximporter-model). 내보내기 세부값은 이 프로젝트의 정적 키트에 맞춘 권장 프리셋이며 현재 Unity에 임포트 검증한 결과가 아니다.

## TileSet/TileVisual 연결

- `floor / floorAlt / wall / pit / ice / cracked / bridge / exit`에 해당 SS_Tile 프리팹을 넣는다. pit는 빈 칸의 테두리만, bridge는 구덩이 위 채운 면이다. board plinth는 프리뷰/배경이며 floor로 중복 배치하지 않는다.
- `gate`는 순번 숫자 문, `countDoor`는 석상 수 문, `plates[3] / doors[3]`는 문자 발판과 일반 문. 모양은 공유해도 배지·상태는 구별한다.
- `lever`는 HandlePivot 자식을 갖는다. `flipWalls[0]`는 Up, `[1]`은 Down. `inactiveChild=GatePanel` 등 실제 존재하는 이름만 지정한다. `materialTarget`은 단일 지정 Renderer로 두고 청자/활성재질을 적용한다. 여러 Renderer 전체에 자동 전파된다고 가정하지 않는다.
- `player / statue`는 같은 메시. `startMarker / oldStartMarker / footprint / luckyRing`은 별도 Quad·화면 라벨. TileVisual.offset/rotation/scale은 피벗 미세 보정에만 쓰고100배 단위 보정은 금지한다.
- `labelOffset`은 문 위약.65/몸 위약1.0에서 시작해 실제 화면을 보고 조정, `activeLabelOffset`은 내려간 문에서도 숫자가 보이게 둔다. 문 위에서 재출발하는 몸을 창살로 가리지 않는다.
- 카메라는 **직교 pitch50 yaw0을 명시**한다. 코드의 `CameraRig.Quarter` 기본값이35/45인 점을 그대로 복사하지 않는다. 기존 아키텍처12장의 A안을 따른다.
- 상태 트리거는 Animator가 있을 때만 지정하고 없으면 기존 코드 트윈을 사용한다. 연결 후 TileSet Validator →17스테이지 smoke → 브라우저 WebGL 실제 성능·가독성 순으로 확인한다. 이 단계는 사용자 본안 선택 후다.

## 재현 가능한 블록아웃 스크립트

[build_dipper_blockout.py](blender/build_dipper_blockout.py)는 외부 패키지 없이 bpy로17모듈·팔레트1장·6재질·6×6보드·EEVEE 카메라/광원을 만든다. 플레이어/석상은 실제 동일 Mesh datablock이다. 숫자 글자는 PREVIEW 컬렉션의 렌더 보조물이며 FBX 대상에서 제외한다. 캐릭터·문·벽 높이는 스크립트 assert로 확인한다. 결과 JSON에는 모디파이어 적용 후 실제 tris·Bounds·재질 슬롯을 쓴다.

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 -P design/art/blender/build_dipper_blockout.py -- --out C:/unityjam/shared-assets/seven-steps/concepts/themes/dipper-3d-variants/blender --export-fbx
```

출력은 지정 공유 `dipper-3d/` 아래로 제한된다. `.blend`, `.fbx`, 팔레트png·렌더png·측정JSON은 공유 폴더에만 두며 git에 넣지 않는다. 스크립트는 설치가 끝난 Blender에서 재실행할 수 있다. GPT 보드·키트 이미지는 조형 목표, EEVEE 렌더가 있다면 **실제 생성된 프록시의 증거**이며 둘을 같은 수준의 완성품으로 주장하지 않는다.

직전3D 재설계 당시에는 설치가 끝나지 않아 실제 Blender 실행을 생략했다. **후속 다수 시안 작업에서 설치 완료 안내를 받아 Blender5.2.3 LTS로 실제 실행·EEVEE 렌더·FBX17개 내보내기까지 검증했다.** 5.2의 렌더 엔진 enum은 `BLENDER_EEVEE`라 기존 `BLENDER_EEVEE_NEXT`와의 fallback을 추가했다. `--python-exit-code 1`로 Python 오류를 정상 종료와 혼동하지 않게 했다.

실제 결과는 `C:/unityjam/shared-assets/seven-steps/concepts/themes/dipper-3d-variants/blender/`의 `SS_Dipper_Blockout.blend`, `blockout-eevee.png`, `SS_Palette_256.png`, `blockout-report.json`, `fbx/*.fbx`다. 가시 보드4,584tris, 모듈17종, 공유재질6종, 같은 플레이어/석상 Mesh datablock과 문·벽≤.6/몸≤1/바닥윗면0 assert가 통과했다. 바닥44tris·얼음176·문132·레버116·몸100, 몸의 실제 높이.87m다. 고정 별 좌표·연결은 PREVIEW 컬렉션에만 있고 FBX에 넣지 않는다. 공유 재질6종에는 PREVIEW/Studio2개가 포함되어 게임의 최종6종 구성과 역할은 다르다. Unity 임포트·WebGL 플레이 성능은 아직 검증하지 않았다.

출력 경로 가드는 `dipper-3d/` 또는 `dipper-3d-variants/` 공유 하위만 허용한다. 실제 블록아웃에는 두 개의 먹돌 받침과 얇은 구리 띠·같은 아틀라스 하단 trim을 더했지만 GPT 배경·최종 광택·완성게임UI를 재현한 것은 아니다. 여러 기믹을 모은 프리뷰 보드이며 솔버로 검증한 실제 레벨이 아니다.
# 현행 유적×선돌 키트 전달 — Blender5.2.3

현행 미술 기준: [아트 바이블](art-bible.md). 위의 기존 Dipper 제작 기록과 구분한다. 새 제작은 Unity 프로젝트를 변경하지 않았다.

공유 자산 `C:/unityjam/shared-assets/seven-steps/Assets/Art/RuinsHenge/` → Unity 링크 `Assets/Shared/Art/RuinsHenge/`. Models에21 FBX와 SS_RuinsHenge_Chapter01.blend, Textures/RH_Atlas512.png, Reports/production-report.json 및 fbx-roundtrip.json. 렌더는 `concepts/themes/ruins-henge/blender/`.

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 -P design/art/blender/build_ruins_henge.py -- --out C:/unityjam/shared-assets/seven-steps/Assets/Art/RuinsHenge --renders C:/unityjam/shared-assets/seven-steps/concepts/themes/ruins-henge/blender
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 -P design/art/blender/verify_ruins_henge_fbx.py
```

Metric/scale1, Blender Z-up→FBX -Z Forward/Y Up, Apply Transform/Scale, Selected Objects=MESH+EMPTY, Animation off, Add Leaf Bones off, 텍스처 상대 경로·임베드 끔.

컬렉션: 01_EXPORT_LIBRARY(모듈원점0),02_KIT_PREVIEW,03_BOARD_PREVIEW,90/91_*_LABELS_NO_EXPORT,99_STUDIO_NO_EXPORT. 모델별 루트 Empty와 하위 메시/훅만 선택 내보낸다. 숫자·프리뷰 라벨·조명·카메라는 제외한다. LOD/리그 없음,1단 베벨 적용 후 내보내기. 각 모델 피벗=칸 중앙 지면,바닥윗면 Blender z0→Unity y0.

공유 UV0 아틀라스와 버텍스 컬러 `MossMask.R` 유지. Blender 노드는 URP로 자동 전송되지 않으며 외부 텍스처 경로가 있어도 Unity 머티리얼을 따로 만든다. URP BaseAtlas는 Atlas×컬러에 VertexColor.R×노이즈(.7–1)를 사용해 이끼색을 혼합한다. 모든 모델 UV/컬러는 Blender FBX 재수입 확인, Unity 실제 임포트 검사는 다음 단계다.

문 훅 `SS_Tile_NumberDoor__GatePanel`, `SS_Tile_CountDoor__GatePanel`을 inactiveChild로 연결하고, 레버 `SS_Tile_Lever__HandlePivot`에 트윈 회전을 연결한다. 뒤집힘 벽은 Up/Down 두 프리팹, 바닥 변형에만 randomYaw90. 숫자/석상 수/별은 별도 화면 라벨, 몸 발밑과 labelOffset은 카메라50°에서 확인한다. 출구·얼음·구덩이도 TileId→TileSet 필드에 맵핑한다.

TileSet은 기존 규격대로 Prefabs/Tiles와 Data/TileSets에서 구성해야 하나 **이번에는 생성하지 않는다**. 다음 디스패치에6 URP재질→프리팹/TileVisual→TileSet→TileSetValidator→17스테이지 smoke→WebGL 브라우저 검수 순으로 진행한다. 챕터별 TileSet 대 공유세트 재질오버라이드 및 조명/하늘 ScriptableObject는 아키텍트·게임플레이가 경계를 정한다.
