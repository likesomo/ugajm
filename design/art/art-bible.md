# Seven Steps 아트 바이블 — 유적×선돌×오리엔탈

> 2026-11-10 R2 A 후속: [7행성 구분 복원·지각·숫자 문 보강](board-shape-a-distinct-followup-2026-11-10.md). 행성별 실루엣/하늘/원경을 유지하며, 이전의 획일적인 외피·공통 남청 하늘을 다시 적용하지 않는다.

> 2026-11-10 판 형태 확정: **R2 A 직사각 석재 연석 + 깎인 암반 지각**. Blender MCP 제작·공유 노멀/AO·Unity 연결과 검토 승인된 직교 프레이밍 제안은 [BoardShapeA 전달 기록](board-shape-a-delivery-2026-11-10.md)을 따른다. 기존 원형 PlanetSummit 외피는 이 모듈 키트로 교체한다.

> 2026-11-10 tone/performance follow-up: [weathered surfaces and WebGL low-spec rendering](tone-perf-2026-11-10.md). Maintain chapter hues with muted stone/timber, frost moss=0, no realtime chapter shadows, WebGL HDR off.

> 2026-11-10 revision: [distinct seven-planet silhouettes, palettes and one-cell decoration clearance](seven-planets-distinct-2026-11-10.md) supersedes the initial seven similar blockouts; frost has no moss.

> 2026-11-10 current scope: seven confirmed planets `moss`, `hollow`, `carved`, `frost`, `joinery`, `crumble`, `altar`; chapters.json is authoritative. Earlier five-planet limits below are historical. UI architecture now uses independent menhirs with one straight weathered timber beam, exposed tenons and wedges; paired curved lintels and red lacquer gateways are forbidden. See [seven-planet delivery](seven-planets-2026-11-10.md).

> EN/KO 손글씨 UI 방향: [fonts.md](fonts.md). EN 제목 Kaushan Script·본문 Patrick Hand, KO Gaegu, 수량/걸음 숫자 Jua를 추천한다. 기존 패널/트림은 유지한다.7행성의 열린 선택 화면과 실제 TTF 렌더는 공유 `concepts/ui/en/`·`ko/`를 따른다. 언어별 UiTheme 필드는 요청안이며 타입을 변경하지 않았다.

> UI 테마 후보와 게임용 환경 전달(2026-11-09): [ui-theme.md](ui-theme.md), [정식 씬 연결](unity-integration.md). UI는 남색 평탄 면/석재·목재 프레임/놋쇠, 칠성은 숫자와 병기한다.3별 평가는 검토안이며 점수 규칙 확정으로 간주하지 않는다. 세쌍둥이0.88m·같은 몸 메시를 유지하면서 머리 비율·명도를 보완하고, 벽은 이끼 줄눈과 석재/목재 장식 변형을 추가했다.

> Unity 2차 다듬기(2026-11-09): 밝은 바닥과 청회색 분할 석재 벽, 따뜻한 키·차가운 필·약한 림, 스테이지 크기에 맞춘 암석 행성·잔디 테두리·성운, 굵은 형제 색 링을 적용했다. 현재 Unity 프리뷰 기준은 [2차 검증 문서](unity-phase2.md)다. 아래 Blender 고정 반경·1차 벽 높이는 당시 제작 기록이며 Unity 프리뷰는 실제 맵 크기로 외피를 생성하고 벽 높이를 약0.41m로 사용한다.

> Unity 프리뷰 연결 완료(2026-11-09):23프리팹·6재질·RuinsHenge_Chapter01 세트, Windows1스테이지와 WebGL1/8/13 화면 검증 완료. 공용 AppConfig 전환·몸 인덱스별 A/B/C 선택은 아직 적용하지 않았다. [연결/검증 문서](unity-integration.md)를 따른다. 기존 ‘Unity 미검증’ 문구는 당시 모델 전달 단계 기록이다.

> 2026-11-09 사용자 확정: **세쌍둥이는 A안 색 구분**(청록 #568C88·황토 #B99A57·적갈 #B96C59). 같은 메시·실루엣·0.88m를 유지한다. B안 동일 외형은 기록만 남긴다. 챕터는 현재5행성 유지하며 레벨 디자이너의50스테이지/6–8행성 계획이 확정되기 전 확장하지 않는다.

> 품질 보강 후 현행값:23종(석재 블록 V2/V3 추가), 배경 포함20,202tris,6재질. Blender 원본·백업과 보고서는 Assets 밖 `C:/unityjam/shared-assets/seven-steps/Source/Blender/`에 둔다. Assets에는 FBX·PNG만(관련 .meta 포함). Chapter01 제작 팔레트의 밝은 바닥 #DEDAC8/보조돌 #A7AD98/목재 #846147을 사용하며 아래 장별 팔레트는 분위기 목표다. 세부 내역·전후 비교는 [파이프라인](blender-pipeline.md) 첫 장을 따른다.

2026-11-08 사용자 확정 방향. **모든 에이전트의 현행 아트 기준**이다. 과거 테마 시안은 탐색 기록이며 이 문서와 충돌하면 이 문서를 따른다. 기술 규격은 [architecture 12장](../../docs/architecture.md#12-3단계-준비-쿼터뷰-전환-계획), 실제 수치는 공유 폴더의 `C:/unityjam/shared-assets/seven-steps/Source/Blender/RuinsHengeReports/production-report.json`을 따른다.

## 세계와 감정

별도의 작은 우주에서 세쌍둥이 방랑자가 마지막 선돌 원형 제단 행성을 찾아간다. 셋은 같은 키와 실루엣의 형제이며, 행성마다 잔디와 이끼가 자란 관측 유적의 서로 다른 흔적을 만난다. KEEP로 남긴 형제는 같은 실루엣의 선돌이 되어 다음 걸음을 돕는다. 1–6걸음은 별 하나씩, 7번째는 국자가 완성되는 짧은 정렬이다. 돌을 세워 하늘의 때와 맞추는 원리는 스톤헨지에서 추상화하고, 다듬은 석재·목조 결구·낮은 기와 마감·절제된 청록/적갈 트림은 한국·동아시아 재료 언어에서 가져온다. 특정 유적·종교 건물을 재현하지 않는 독자적 우주다.

**추천: 북두칠성 HUD 유지 + 건축·배경의 태양 정렬.** 일곱 별은 규칙을 세기 쉽고, 해의 틈은 장소와 여정을 묶는다. 북두칠성 정렬은 게임의 창작 설정이며 실제 스톤헨지에 관한 주장으로 쓰지 않는다. 달 정렬은 보조 분위기만 허용한다. 근거와 비교는 [리서치](theme-research.md#유적선돌오리엔탈)를 참고한다.

## 배경 — 행성의 곡률과 우주

챕터 하나는 작은 행성 하나다. **보드는 행성의 평탄화한 정상부에 직접 얹는다.** 행성 앞에 떨어진 판처럼 떠 있는 배치를 사용하지 않는다. 플레이 그리드는 평면·1m·윗면0을 유지하며 곡률은 보드 바깥 지평선·외피·아래쪽에서 보인다. 칸을 구면에 휘게 배치하거나 카메라/입력을 돌리지 않는다. 현 Chapter01은 타일 아랫면/정상부를 모두 Blender z=-.12(Unity y=-.12), 정상부 반경3.65m로 만들고5×5 보드 모서리까지 지지한다.

주변은 남청 우주, 작은 다른 행성2–3개, 낮은 대비 성운과 별이다. 실제 들판·산·숲의 원경·지구 풍경 사진/HDRI를 쓰지 않는다. 풀과 이끼는 유적의 국소 표면 재료로 남긴다. 구덩이는 우주가 보이는 구멍으로 표현할 수 있으나 코어 낙하 규칙을 바꾸지 않는다. 별과 성운은 저해상 큐브맵/불투명 스카이 재료를 우선하고 고비용 투명 파티클·볼류메트릭에 의존하지 않는다. 배경은 장식이며 선택 가능한 새 칸으로 읽히지 않게 한다.

태양 축은 먼 별의 따뜻한 방향광/정렬 틈으로 이어 쓰며 정밀한 실제 천문 시뮬레이션을 주장하지 않는다. 판 위는 계속 밝고 세기 쉽게, 우주 분위기는 바깥 여백·행성 윤곽·테두리에서 낸다. 기존 들판/협곡 렌더는 탐색 기록이며 최신 배경 기준으로 사용하지 않는다.

## 캐릭터 — 세쌍둥이 방랑자

모두0.88m, 같은 후드·짧은 사다리꼴 망토·작은 발·그늘진 얼굴의 실루엣이다. 작은 머플러는 상체에 붙이고 장신구는 작은 삼각 잠금쇠로 제한한다. 배낭/등짐은 몸 폭 안의 작은 두루마리만 선택적으로 허용한다. 긴 지팡이·늘어진 등불·큰 등짐은 칸 판독과 형제의 동일 실루엣을 해치므로 기본안에서 제외한다.

**A안 확정(2026-11-09 사용자 결정).** 형제 A/B/C의 머플러·망토 밑단은 각각 청록 #568C88 / 황토 #B99A57 / 적갈 #B96C59다. 색은 형제 식별만을 뜻하며 능력·속성·고유 규칙을 추가하지 않는다. 동일 몸 메시와 같은 액세서리 형태를 쓰고, 공유512² 아틀라스의 정확한 색 패치 UV로 구분해 재질6개 이하를 유지한다. 별도 형제 인덱스·선택 링도 UI로 표시하며 필수 규칙을 색에만 의존하지 않는다.

기록: B안(셋 모두 같은 황토 외형)은 미채택이다. 옛 캐릭터 시트에는 비교 기록으로 남기되 현행 선택지로 제시하지 않는다. `SS_Actor_Triplet_Identical.fbx`와 관련 .meta는 Assets 밖 `Source/Blender/Archive/Triplet-B-option-2026-11-09/`로 보관했다.

KEEP 선돌은 같은 몸/후드/망토 윤곽의 **색 없는 중성 화강암 #B9BFC0**을 기본으로 하며 잠금쇠도 돌이 된다. 다른 키의 일반 바위로 교체하지 않는다. 형제 식별 후보는 머플러 윗면 한 면에만 같은 색의 얇은 리본/이끼 흔적을 남기는 방식이다. A/B/C 흔적 변형을 별도 제작했고, 근접 화면에서는 구분되지만 보드 축소 시 작은 색 흔적은 보조 정보이므로 UI 형제 인덱스를 대체하지 않는다. 몸 전체 착색·밑단 채색·발광은 사용하지 않는다. 흔적 채택은 가독성 검토 후보이며 무표식 중성 KEEP도 유지한다. 리그 없이 트윈, 피벗0·폭1칸 이내·높이0.88m를 유지한다.

현행 FBX: `Assets/Art/RuinsHenge/Models/SS_Actor_Triplet_A/B/C/Menhir/Menhir_A/Menhir_B/Menhir_C.fbx`(각 이름에 전체 접두어 적용).7변형은 같은 몸 메시를 공유하며 각160tris, 객체별 최대2재질이다. 신규 `RH_Atlas512_Triplets.png`는 기존 팔레트를 보존하고 최상단 패딩 구역에 승인 색3개와 중성 돌 패치를 더한512² 아틀라스이며 이 세트의6재질이 공유한다. 기존 Player/Menhir FBX는 참조 안정성을 위해 보존하고 Unity 연결 시 새 변형으로 교체한다. 원본은 `Source/Blender/SS_RuinsHenge_Planets.blend`, 검사 보고서는 `Source/Blender/RuinsHengeReports/planets-*.json`이다. [생성](blender/build_ruins_planets.py)·[FBX 검사](blender/verify_ruins_planets.py).

‘세 형제’는 이번 서사/아트 기준이다. 기존17스테이지에는 몸4개 등 자원 수가 다른 데이터가 있으므로 **코어·레벨·HUD의 몸 수를 이번 아트 작업에서3으로 강제 변경하지 않는다**. 레벨 디자이너가 챕터 재구성과 함께 조율해야 한다. 이번 행성 시안은 두 살아 있는 형제+한 KEEP 선돌로 셋을 표현한다.

## 7 confirmed chapters (2026-11-10)

The IDs and order in `unity/Assets/_Project/Data/chapters.json` are authoritative. All seven use the same playable tile kit and keep floor y=0; background geometry and lighting differ.

| ID | Environment direction | Preset |
|---|---|---|
| moss | Moss rim, broken stone, warm morning starlight | Chapter_moss |
| hollow | Open underside sectors, cool teal moon and stars | Chapter_hollow |
| carved | Recessed stone strata, counting pillar concept, warm stone | Chapter_carved |
| frost | Frost palette, bare rock rim, cold blue key | Chapter_frost |
| joinery | Faded timber palette, exposed joinery concept, amber key | Chapter_joinery |
| crumble | Irregular underside fragments, muted warm stone | Chapter_crumble |
| altar | Perimeter standing stones, moss and narrow warm starlight | Chapter_altar |

Delivery paths, references and validation: [seven-planets-2026-11-10.md](seven-planets-2026-11-10.md). Old five-chapter stage ranges are superseded by the 50-stage data.

## 칸과 규칙을 먼저 읽게 한다

- 정면 3/4 **직교 pitch 50°, yaw 0°**, 1칸=1m, 칸 중앙 피벗, Unity 바닥 윗면 y=0. Blender Z-up에서 윗면 z=0으로 만들고 FBX로 변환한다.
- 바닥 실폭 .96m로 간격을 확보한다. 벽·문·올라온 뒤집힘 벽 ≤.6m, 몸 높이 ≤1m, 발밑 원점0. 현 제작 몸은 .88m, 일반 벽은 .55m 이하이다.
- 기믹 실루엣과 의미색은 **모든 챕터에서 고정**한다. 숫자 문=두 기둥+판, 석상 수 문=같은 문에 석상 아이콘+정확한 수(0 포함), 레버=놋쇠 손잡이+석재 받침, 얼음=청회 면+평행 홈, 출구=낮은 이중 테두리.
- 플레이어·석상은 같은 망토/선돌 실루엣과 같은 메시를 사용한다. 플레이어는 명도와 발밑 표시, 석상은 돌 재질로 구별한다. 원형 종교 조각으로 만들지 않는다.
- 숫자·별·발자국·A/B는 메시나 아틀라스에 굽지 않는다. Unity 화면 라벨/별도 Quad로 표시한다. CountDoor는 현재 수와 요구 수의 정확한 일치를 표시하고 `≥`로 바꾸지 않는다.
- HUD 1–7은 숫자와 별을 함께 표시한다. 국자 1–2–3–4–1, 손잡이 **4–5–6–7**. 고정 참조: `concepts/themes/dipper-3d-variants/dipper-fixed.png`. 태양 표시는 별 카운트를 대체하지 않는다.
- 7번째에 7번 별과 연결선 완성→짧은 발밑 링/가는 정렬광→실제 이벤트 대상만 변화. KEEP가 아닌 곳에서 석상을 임의 생성하거나 모든 문·레버를 동시에 작동시키지 않는다. 출발점 소멸 등 코어 이벤트를 따른다.
- 바닥 중앙은 밝고 평탄하게, 이끼는 모서리·이음새, 잔디는 보드 외곽에 한정한다. 배경 선돌과 회랑은 플레이 영역 밖에 둔다. 생성 시안의 기믹 배치/표기는 규칙 원본이 아니다.

## 공유 키트와 제작 예산

챕터마다 새 메시 세트를 만들지 않는다. 같은 23종 메시 + 팔레트/트림 + 작은 환경 소품 + 조명/하늘 프리셋으로 차별화한다. 모서리 랜덤90도 회전은 바닥에만, 레버·방향 홈·라벨에 적용하지 않는다.

| 묶음 | 완성된 Chapter01 FBX | 목표·상태 |
|---|---|---|
| 바닥 | Floor_Clean / Floor_Moss / Floor_GrassEdge | 3변형, 윗면 z=0, 모서리 버텍스 마스크 |
| 환경 | Block_Stone / Block_Stone_V2 / Block_Stone_V3 / Block_Wood / Tile_Wall / Border_Straight / Border_Corner | 1단 베벨, 교차 줄눈, 목재 끝단 트림 |
| 몸·목표 | Actor_Player / Actor_Menhir / Tile_Exit | 몸 공유 메시, 출구 낮은 윤곽 |
| 기믹 | Tile_NumberDoor / Tile_CountDoor / Tile_Lever / Tile_FlipWall_Up / Tile_FlipWall_Down / Tile_Plate / Tile_Ice / Tile_PitRim / Tile_Bridge / Tile_Cracked | 기믹 형태 10종, 별도 UI 숫자, 트윈·상태 교체 훅 |

표의 모든 이름 앞에 `SS_`를 붙인다. 피트는 림만 있어 바닥이 없고, 다리는 독립 평평한 슬래브다. Cracked는 분할 돌 조각이며 코어 파괴 시 숨김/아래 이동으로 처리한다.

WebGL 목표: 동시 보드 **50,000 tris 이하**, 환경·효과 포함 권장80,000 이하(기기 실측 전 예산). 현 6×6 조립 예시는20,202 tris. 개별 바닥44–656, 단순 블록88–264, 문/레버100–400, 몸100–300 tris를 우선한다. 실제 종별 수는 생산 보고서가 권위값이다. LOD는 작은 고정 직교 보드에서 불필요하며 배경 오브젝트는 별도 저해상 메시/거리 컷을 쓴다.

공유 `RH_Atlas512.png` 1장, 8색 그라디언트·약한 석재 입자/목재 결+하단64px 기하 트림. UV0는 색 띠 안으로 제한해 넓은 배경에서도 이웃 팔레트가 번지지 않게 한다. 현재 색 띠는1m 폭 약55px로 디테일을 절제하며 트림·추후 마스크 목표는 **256px/m**, 최종 아틀라스 최대1024²다. 개체별 고유1K/4K 맵을 만들지 않는다.

머티리얼은 챕터당 **최대6개**: BaseAtlas(석재·목재·이끼), PlayerAtlas, MenhirAtlas, MetalAtlas, FrostOpaque, MarkUnlit(UI/프리뷰 표시). 객체당 최대2종. 얼음은 불투명, 잔디는 외곽의 작은 불투명 삼각형이다. 블룸·굴절·투명 겹침에 기대지 않는다. 같은 머티리얼은 인스턴싱/배칭을 유지하며 공유 아틀라스 하나가 draw call 하나를 보장한다고 가정하지 않는다.

이끼는 `MossMask` 버텍스 컬러 R(0=돌,1=이끼)×저주파 노이즈 마스크. Blender 노드는 렌더용이며 **FBX가 노드 셰이더를 URP로 변환하지 않는다**. Unity에서 Vertex Color.R로 재구성해야 한다. 텍스처 디테일을 더할 경우 CC0 이끼 맵을 작게 줄여 마스크로 쓰며 추가 머티리얼을 만들지 않는다.

리그는 현재 없다. 이동/회전/작은 스쿼시는 트윈으로 시작하고 걷기 리그는 판독성 테스트 후 결정한다. 문은 GatePanel 자식, 레버는 HandlePivot 훅, 뒤집힘 벽은 Up/Down 프리팹 교체로 구현한다.

## 전달 상태와 다음 연결 작업

**Unity 연결 준비 완료(모델/아틀라스 전달 단계).** Blender 5.2.3 실제 생성,23 FBX 재수입으로 UV·버텍스 컬러·원점·트라이앵글 수 확인. Unity 임포트/URP/프리팹/TileSet 검사는 아직 하지 않았고 Unity 파일은 변경하지 않았다.

- 자산: `C:/unityjam/shared-assets/seven-steps/Assets/Art/RuinsHenge/` → 기존 링크 시 `Assets/Shared/Art/RuinsHenge/`.
- 원본·FBX: `C:/unityjam/shared-assets/seven-steps/Source/Blender/SS_RuinsHenge_Chapter01.blend`, `Models/SS_*.fbx`(23개). 텍스처: `Textures/RH_Atlas512.png`.
- 스크립트: [생성](blender/build_ruins_henge.py), [FBX 재수입 검사](blender/verify_ruins_henge_fbx.py). 재현 명령과 훅은 [파이프라인](blender-pipeline.md).
- 아키텍트/게임플레이 요청: 챕터별 TileSet 또는 공유 TileSet+머티리얼 오버라이드, 조명·하늘 프리셋 ScriptableObject 경계를 정한다. 이 요청은 새 코드 구현이 아니다.
- 다음 아트 연결 작업: URP6재질, 프리팹/TileVisual 자식 훅·라벨 위치, TileSetValidator 오류0 → 17스테이지 smoke → 브라우저 WebGL 성능/판독성 확인. 챕터2–5 환경 소품·프리셋은 미제작이다.

작업량 추정(1인8시간): 공유 키트 정교화/아틀라스1.5–2일, URP·프리팹·상태연출0.75–1일, 추가4챕터 트림·소품·조명1–1.5일,17스테이지·WebGL 검수0.5–1일, **전체3.75–5.5인일**. 이번 산출은 키트의 실제 모델/FBX 기반이며 시안 수준의 배경 완성도까지 달성한 것으로 계산하지 않는다.

## 금지와 외부 재료

실제 스톤헨지 배치·연속 상인방 고리 복제, 특정 사찰/석탑/석등 복제, 드루이드·종교 고정관념, 용·붉은 등·과장된 복장, 가짜 한자/한글, 숫자 구운 메시, 칸을 덮는 풀, 어두운 바닥, 유료·계정 필요 에셋을 사용하지 않는다. 무료 재료도 라이선스 확인 후 CREDITS에 기록한다. 바이너리·이미지·.blend·FBX는 git에 커밋하지 않고 공유 폴더에 둔다.

현재 모델과 아틀라스는 자체 절차 생성이며 외부 에셋을 다운로드하지 않았다. 후보와 CC0 라이선스는 [CREDITS](CREDITS.md)의 유적 테마 항목을 따른다. GPT 시안은 제작 참고 이미지이며 CC0 다운로드 에셋으로 표시하지 않는다.
