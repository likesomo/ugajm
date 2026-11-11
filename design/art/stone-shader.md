# 자연석 셰이더 설계와 검증

2026-11-10. 승인 R2의 눈 없는 후드·저대비 판석 수정을 반영한 **모델링 전 1–3단계 검토**다. 게임 프로젝트의 셰이더·프리팹·GUID는 변경하지 않았다. 실행 가능한 비교용 코드는 공유 `concepts/themes/stage-resources-2026-11-10/modeling-prep/prototype-unity/`에 격리했다.

## 결정과 근거

**기존 `SevenSteps/RuinsSurface` 계약을 보존하면서 석재 전용 경로를 확장하는 방향을 선택한다.** 새로운 SimpleLit 계열 재질로 전면 교체하지 않는다. 현재 파일 `unity/Assets/_Project/Art/RuinsHenge/Shaders/RuinsSurface.shader`를 직접 확인했다. 이미 BaseMap/WeatherMap/BoardShapeMap/BoardShapeNormal과 스타일 분기·환경 디테일·BoardShapeA 베이크 경로를 사용하며, 새 기믹의 의미와 GUID를 바꾸지 않고 통합할 수 있다.

| 선택지 | 장점 | 이번 판단 |
|---|---|---|
| RuinsSurface 석재 경로 확장 | 기존 스타일·색 아틀라스·판 A·ChapterPreset 대응 유지, 경량 키 라이트 제어 | 채택 방향. 새 샘플을 기존 4개 위에 덧붙이지 말고 석재 경로에서는 패킹된 맵으로 교체 |
| URP SimpleLit 기반 커스텀 | 표준 URP 조명/스페큘러 경로와 유지보수 이점 | 대체 후보. 기존 커스텀 색/스타일 이식·변형 정리와 최종 성능 재측정 필요 |
| URP Lit 전체 | 표준 PBR 유연성 | 작은 고정 직교 WebGL 보드에 필요한 범위보다 기능/변형이 많아 이번 목표에서 제외 |

프로토타입은 새 독립 `SevenSteps/Review/StonePrep` ForwardLit 패스다. 기존 RuinsSurface를 실제로 교체한 결과라고 주장하지 않는다. Unity 6000.6.8f1 / 설치된 URP 17.6.0 소스를 기준으로 작성·컴파일했다. `ChapterPreset.materialOverrides`는 실제 계약에 존재하며 문서로 확인했다. 오브젝트별 색은 버텍스 색을 우선하고, MaterialPropertyBlock은 필요할 때 배칭 영향을 측정하여 채택한다.

## 레이어와 맵 패킹

| 순서 | 입력과 계산 | 전달 규칙 |
|---|---|---|
| 1 색 + AO | 공유 ColorAO atlas RGB=albedo, A=AO | albedo는 sRGB, 조명/일몰광을 굽지 않음. 비교 맵 AO는 중성1이며 최종 하이폴리 베이크 전 |
| 2 디테일 노멀 | NormalRC atlas RG=tangent normal XY, B=roughness, A=curvature | 데이터 맵 linear. Z는 sqrt(1-dot(XY,XY))로 복원. Unity 일반 NormalMap 인코딩 대신 명시 디코딩하는 프로토타입 |
| 3 블록 변주 | 버텍스 색 또는 안정된 object ID의 .94–1.06 밝기 | 매 프레임/카메라 위치 기반 노이즈 금지, 사암 층리·목재 결·물때 방향 보존 |
| 4 엣지 마모 | curvature의 국소 약한 .045 밝기 변화 | 깊은 크랙 무늬를 모든 블록에 찍는 입력으로 쓰지 않음. 최종 curvature는 베이크/버텍스 채널 |
| 5 이끼·서리 | world-up dot × vertex R × edge/local mask × 강도 | frost는 이끼 강도0, 식생 없이 rime가 대체. 큰 쿠션/결정 실루엣은 소량 실제 지오메트리 |
| 6 물때 | 위→아래 세로 그라디언트 × 거친 옆면 | .035 정도의 약한 변화, 윗면 중앙/숫자 판독 영역 제외 |
| 7 조명 | 키 방향광 + 차가운 앰비언트 필 + 약한 스페큘러 | 알베도에 일몰광 중복 베이크 금지. HDR·SSAO·굴절·투명 레이어 없이 표현 |
| 8 판석 가독성 | top normal dot와 tile UV 중앙 마스크로 색/노멀 세기 제한 | 중앙 albedo 명도 ±8% 이내, 결은 가장자리로, 줄눈은 별도 어두운 공간으로 유지 |

최종 3샘플 설계: **ColorAO 1회 + NormalRC 1회 + 공유 overlay color 1회 = Forward 패스 3회**. 최대4회 요구를 만족하며 별도 AO·roughness·curvature·moss 텍스처를 각각 추가로 샘플하지 않는다. 단순하게 필요 없는 overlay 경로를 꺼도 비교용 코드는 고정3회라 예산이 예측 가능하다. 셰이더 소스의 SAMPLE_TEXTURE2D를 세 번 확인했다. 그림자/깊이 패스의 비용과 드로우콜은 이 수치와 별개다.

공유 비교 아틀라스는 1024×512, 4×2셀이고 첫7셀은 행성 석질, 마지막은 공유 이끼 패치다. 셀 내 UV는 .01–.99를 사용해 약한 여백을 둔다. 최종 아틀라스는 패딩·mip bleed 검사 뒤 확정해야 하며, 256px 셀은 1024 원본의 압축 견본이지 최종 밀도 보장값이 아니다. 목재/청동/얼음 원본은 별도 공유 견본으로 제공하며 최종 공유 시트 패킹은 승인 후 진행한다.

## 행성별 파라미터

| 행성 | 색 방향 / roughness 목표 | 노멀 세기 | 이끼 / 서리 | 형태·방향성 |
|---|---|---|---|---|
| moss | 회백 화강암, .75 | .38 (바닥 중앙 .05 수준) | .8 / 0 | 비방향 광물 입자·국소 이끼 쿠션 |
| hollow | 보라회 현무암·부석, .90 | .38 | 0 / 0 | 기공·천공; 큰 구멍은 메시 |
| carved | 붉은 황토 사암, .82 | .38 | 0 / 0 | 수평 층리 방향 고정, 판석 중앙은 억제 |
| frost | 흰 청회 대리석·석영, .30 | .15 | 0 / 1 | 드문 광물 맥·연마면·rime, 식생0 |
| joinery | 청회 slate, .70 | .38 | 0 / 0 | 벽개와 목재 결과 쐐기, 방향 고정 |
| crumble | 누런 다공질 석회암, .92 | .38 | 0 / 0 | 용식 공극·큰 파단·먼지; 일반판은 저대비 |
| altar | 치밀한 회갈 사르센, .78 | .38 | 0 / 0 | 거대한 온전한 면, 금빛 역광은 조명 |

거칠기는 실측 재료값이 아닌 승인용 시작값이며 smoothness를 요구하는 머티리얼에는 `1-roughness`로 전달한다. 금속성은 청동 역할에만 주고 화강암 운모를 돌 전체 metallic으로 바꾸지 않는다. 프로토타입 블록 변주는1로 고정하여 두 렌더러의 재질 비교를 돕는다.

## 판석 대비의 정량 조건

`textures/<planet>-tile-lowcontrast.png`는 1024²의 판석용 출력이다. 중앙68% 폭 영역의 albedo 선형 휘도를 지역 기준 평균에 대해 제한하고, 가장자리로 갈수록 원래 석질로 부드럽게 복원했다. 8bit PNG 양자화 여유 때문에 내부 제한은 ±6%로 설정했다. 저장 후 재디코딩하여 검사한 최대 편차는 moss6.71%, hollow7.17%, carved6.83%, frost6.02%, joinery6.95%, crumble6.50%, altar6.97%로 전부 ±8% 이내다. `textures/validation.json`이 수치의 원본이다.

이 검사는 **알베도 질감 대비**다. 광원·접지 그림자·의도된 기믹 표식까지 ±8%로 평탄화하지 않는다. hero는 눈/격자/중앙 대비를 시각 수정했으나 생성 일러스트의 모든 픽셀을 이 수치로 보장하지 않는다. 실제 메시에서는 .96m 판석 사이 .04m 줄눈을 분리하고 8×6 그리드를 코드로 배치하여 정확한 칸 수를 확보한다. 이번 도면·스와치 검사와 1초 사용자 인지 실험을 혼동하지 않는다.

## 프로토타입과 검증

공유 `modeling-prep/shader/`:

- `blender-<planet>.png`: Blender 5.2.3 Cycles 배경 CLI로 새 단순 구·블록·저대비 판석 견본만 렌더했다. `prototype-<planet>.blend`에 노드가 저장되어 있다. 열린 Blender/MCP 세션은 사용하지 않았다.
- `unity-<planet>.png`: 별도 미니 Unity URP 프로젝트에서 같은 구/블록/판석 구성과 pitch50 직교로 직접 렌더했다. `.blend` 노드는 Unity로 자동 변환된 것이 아니며 같은 레이어 역할을 HLSL로 다시 만들었다.
- `compare.png`: 두 렌더러를 행성별로 나란히 배치했다. Blender Principled/Cycles와 경량 URP의 BRDF·톤/환경 조명이 다르므로 픽셀 일치나 물리적 재료 스캔으로 주장하지 않는다.
- `unity-validation.txt`, `blender-validation.json`, `unity-render.log`, `blender-render.log`에 실제 실행 결과가 있다.
- `webgl-validation.txt`, `unity-webgl.log`: 별도 견본 프로젝트 WebGL 빌드 검사. StonePrep ForwardLit의 GLES3 full variant space=1, 필터/스트립 후1, unique internal programs=1을 확인했다. 커스텀 keyword 추가는0이다.

노멀은 이번 비교에서 **albedo 미분으로 만든 약한 프리뷰**이며 AO는 중성1, curvature도 프리뷰 마스크다. 최종 Normal·AO·curvature는 승인 뒤 실제 하이폴리에서 베이크해야 한다. 파이프라인 상태를 이름 `*-preview.png`에 표시했으며 프리뷰를 완성 베이크로 전달하지 않는다. 알베도는 생성 원본에서 타일 경계 조건을 맞추고 1024²로 내보냈으며 실제 암석 사진/CC0 스캔은 아니다.

초기 검증에서 PNG 양자화로 중앙 대비가 8%를 넘은 문제는 내부 제한을6%로 줄여 해결했고, Unity read-only shadow 프로퍼티 오류는 설치된 URP 소스의 SerializedObject 필드로 수정했다. 재실행 후 통과 결과만 최종 페이지에 표시한다. 실제 게임 전체 보드의 드로우콜·FPS가 기존 이하라는 검증은 아직 아니며, 승인 후 게임 통합 시 동일 장면 전후 측정이 필요하다. 목표는 기존6재질 이하·보드50k tris 이하·Forward 샘플4이하 유지다.
