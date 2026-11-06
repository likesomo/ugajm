# 일곱 걸음 — 무료 에셋 조사

> 기준 목록: `design/art/seven-steps-asset-needs.md` · 컨셉: `design/gdd/seven-steps-concept.md`
> 조사일: 2026-11-06. 전제: Unity 6 + URP, WebGL → itch.io, 상업적 이용 가능.
> 표기: **확인** = 이번에 해당 페이지(또는 공식 GitHub 저장소)를 직접 열어 확인함. **미확인** = 페이지에 정보가 없거나 접속이 막혀 확인하지 못함. 미확인 항목은 다운로드 후 직접 확인해야 한다.
> 에셋은 다운로드하지 않았다. 파일 목록은 제작자 공식 GitHub 저장소의 트리로 확인했다(아래 각주).

---

## 1. 요약 — 추천 조합

### 추천: KayKit 한 작가 조합 (전부 CC0, 크레딧 의무 없음)
| 역할 | 에셋 |
|---|---|
| 환경/타일 | **KayKit Dungeon Remastered** (무료판 200+ 모델) |
| 발판·문·기믹 보조 | **KayKit Prototype Bits** (무료판 64+ 모델, 같은 작가·같은 그라디언트 아틀라스 방식) |
| 캐릭터 | **KayKit Adventurers** (무료판 캐릭터, 리깅·애니메이션 포함) |
| 애니메이션 | **KayKit Character Animations** (150+ 휴머노이드 애니메이션, 무료) |
| VFX | Unity **Particle Pack** (Unity 6 URP 호환 표기) + **Kenney Particle Pack** 스프라이트 |
| UI | **Kenney UI Pack** + **Kenney Game Icons** + 한글 폰트 **Pretendard** 또는 **Jua** (OFL) |
| 사운드 | **Kenney RPG Audio / Interface Sounds / Music Jingles** + Freesound CC0 차임 + OpenGameArt CC0 BGM |
| 트윈 | **PrimeTween** 또는 **DOTween**(무료판) |

이유: 환경·캐릭터·애니메이션이 모두 Kay Lousberg 한 사람의 작품이고 같은 1024×1024 그라디언트 아틀라스 텍스처 방식이라 화풍이 자동으로 맞는다. 캐릭터가 작고 머리가 큰 비율이라 쿼터뷰 그리드에서 잘 읽힌다. 전부 CC0라 크레딧 의무가 없다.

### 대안: Kenney Mini 시리즈 (전부 CC0)
- **Kenney Mini Dungeon**(20 모델, 애니메이션 포함 표기) + **Kenney Mini Characters**(25 파일, 애니메이션 포함 표기) + Kenney UI/오디오.
- 한 작가·한 시리즈라 화풍은 맞지만, 두 팩 모두 페이지에 세부 목록이 없어 벽·문·바닥 종류와 애니메이션 종류는 **미확인**. 모델 수가 적어(20개) 숫자 문·발판 등은 KayKit보다 직접 제작 비중이 크다.

### 2D 탑다운으로 갈 경우 (핵심만)
| 에셋 | URL | 라이선스 | 내용 | 평가 |
|---|---|---|---|---|
| 0x72 DungeonTileset II | https://0x72.itch.io/dungeontileset-ii | 에셋 CC0, 코드 MIT (확인) | 16×16 타일, 바닥·벽(오토타일), 가시 함정, 스위치·버튼, 애니메이션 캐릭터 (확인). 문은 페이지에 언급 없음(미확인) | 발판·캐릭터까지 한 화풍으로 해결. 2D 1순위 |
| Kenney Tiny Dungeon | https://kenney.nl/assets/tiny-dungeon | CC0 (확인) | 16×16, 130 파일. 개별 내용 미확인 | Kenney UI와 결이 맞음. 내용은 다운로드 후 확인 |

---

## 2. P0 커버리지 표 (추천 조합 기준)

| P0 항목 | 채우는 에셋 / 방법 | 상태 |
|---|---|---|
| 바닥 타일 (변형 2~3종) | Dungeon Remastered `floor_tile_small`, `floor_tile_small_decorated`, `floor_tile_small_weeds_A/B`, `floor_tile_large` | 채워짐 |
| 벽 블록 | Dungeon Remastered `wall`, `wall_half`, `wall_corner`, `wall_pillar` 등. 한 칸짜리 큐브 벽이 필요하면 Prototype Bits `Primitive_Cube` | 채워짐 (칸 크기 맞춤 필요, §5) |
| 구덩이 | 바닥 미배치 + 가장자리 `floor_foundation_*`(바닥 측면 마감 조각). Prototype Bits `Primitive_Floor_Hole`도 후보 | 부분 — 어두운 바닥면(검은 Quad) 직접 추가 |
| 얼음 바닥 | **직접 제작**: `floor_tile_small` 복제 + URP/Lit Transparent 하늘색 머티리얼(Smoothness 높게) | 직접 제작 필요 |
| 발판 a~c (색 3종) | **직접 제작**: `floor_tile_big_grate` 또는 Prototype Bits `Floor`를 얇게 눌러 쓰고 머티리얼 색 3종(빨/파/노). Dungeon Remastered에 발판 모델은 없음(파일 목록 확인) | 직접 제작 필요 (간단) |
| 문 A~C (열림/닫힘) | Dungeon Remastered `wall_gated`, `wall_corner_gated`(창살), `wall_doorway`(문틀). 열림은 창살 부분을 아래로 트윈 — 창살이 별도 메시인지 **미확인**. 안 되면 문틀 + Prototype Bits `Door_A/Door_B`를 내리는 방식 | 채워짐 (분리 여부 미확인) |
| 숫자 문 1~7 | `wall_doorway` 또는 `wall_arched` + **TextMeshPro 3D 텍스트**로 숫자 표시 | 부분 — 숫자는 TMP로 직접 |
| 출구 | `stairs`(아래로 내려가는 계단) 또는 `wall_arched` + 빛 파티클 | 채워짐 |
| 플레이어 (순례자) | Adventurers 무료 캐릭터(Knight/Barbarian/Rogue/Rogue_Hooded/Mage/Ranger, itch 페이지 기준) 중 1명. 순례자 느낌은 `Rogue_Hooded` 또는 `Mage` | 채워짐 |
| 석상 | 같은 모델 + 회색 돌 머티리얼(URP/Lit, 아틀라스 대신 단색 회색 또는 아틀라스를 흑백으로) + Animator 정지 | 직접 제작 필요 (머티리얼 교체만) |
| 애니메이션 대기·걷기 | Character Animations (idle, walking 범주 확인) | 채워짐 |
| 애니메이션 미끄러짐 | idle 또는 걷기 정지 포즈 + 위치 트윈 | 직접 제작 필요 (트윈) |
| 애니메이션 떨어짐 | Character Animations에 "fall" 명시 없음. jumping 범주에 공중 루프가 있는지 **미확인**. 대안: 아래로 위치 트윈 + 축소 | 미확인 / 트윈 대체 |
| 석화 포즈 | 아무 포즈(예: interacting, cheering)에서 Animator `speed = 0` | 직접 제작 필요 (정지) |
| 7번째 걸음 도장 VFX | **직접 제작**: Kenney Particle Pack의 원·링 계열 스프라이트(포함 여부 미확인)로 Particle System 1개 — 금색, 크기 0→1 확대 + 알파 감소, URP Bloom | 직접 제작 필요 |
| 발자국 + 걸음 번호 | **직접 제작**: 바닥 위 TextMeshPro(월드) 숫자 1~7 + 반투명 원형 Quad | 직접 제작 필요 |
| 한글 폰트 | Pretendard / Noto Sans KR / Jua (모두 OFL) → TMP Font Asset 변환 | 채워짐 (변환 작업 필요, §6) |
| 걸음 칸 7개 / 몸 아이콘 | Kenney UI Pack의 원형 요소(포함 여부는 **미확인**) 또는 Unity UI Image 원형 스프라이트 | 직접 제작 가능 |
| 패널·버튼 | Kenney UI Pack (button, panel 태그 확인) | 채워짐 |
| 발소리 (1~6) | Kenney RPG Audio (footstep 태그 확인, 개별 파일 미확인) | 채워짐 (내용 미확인) |
| 7번째 걸음 종소리 | Freesound CC0: `Chimes Sparkle` (lukabea, 36.5초 → 앞부분 잘라 쓰기) 등 | 채워짐 (편집 필요) |
| 트윈 라이브러리 | PrimeTween 또는 DOTween | 채워짐 |

**무료로 그대로 안 채워지는 P0**: 얼음 바닥, 발판 색 3종, 숫자 문 숫자, 석상 머티리얼, 7번째 걸음 도장, 발자국·걸음 번호, 미끄러짐·석화 연출. 전부 머티리얼 교체·TextMeshPro·파티클 1개·트윈으로 해결되는 수준이라 잼 첫날 범위 안이다. 떨어짐 애니메이션은 확인 전까지 트윈으로 대체하는 것을 기본안으로 둔다.

---

## 3. 카테고리별 후보

### 3-1. 환경 / 타일 (3D)
| 이름 | URL | 라이선스 | 채우는 항목 | URP/Unity 6 주의 | 평가 |
|---|---|---|---|---|---|
| **KayKit Dungeon Remastered** | https://kaylousberg.itch.io/kaykit-dungeon-remastered | CC0, 크레딧 선택 (확인) | 바닥 변형 다수, 벽 일체, 창살 벽(`wall_gated`), 문틀(`wall_doorway`), 아치(`wall_arched`), 금 간 바닥(`floor_tile_small_broken_A/B`, P1), 금 간 벽(`wall_cracked`), 기둥·횃불·돌무더기(`pillar`, `torch_lit`, `rubble_*`, P2), 계단, 가시 함정(`floor_tile_big_spikes`) — 파일 목록 확인¹ | FBX·glTF·OBJ 제공, v1.1에 Unity용 FBX(스케일·방향 보정) 추가 (확인). URP 언급 없음 → 임포트 후 머티리얼이 분홍이면 URP/Lit로 교체(아틀라스 1장이라 머티리얼 1개). glTF를 쓰려면 별도 임포터 패키지 필요하므로 FBX 권장 | 무료판만으로 P0 환경 대부분 해결. 유료 Extra($)는 대체 텍스처 6종 — 필요 없음 |
| **KayKit Prototype Bits** | https://kaylousberg.itch.io/prototype-bits | CC0 (확인) | `Door_A/B`, `Primitive_Cube`(1칸 벽), `Primitive_Floor_Hole`(구덩이), `Floor`, `Pillar_A/B` — 파일 목록 확인² | 위와 동일 (FBX·glTF·OBJ, 그라디언트 아틀라스) | 발판·문 대용 블록이 필요할 때 보조. 프로토타입 느낌이라 메인 화면에는 일부만 |
| Quaternius Modular Dungeon | https://quaternius.com/packs/modulardungeon.html | CC0 (확인) | 48 모델, 세부 구성 미확인 | FBX·OBJ·Blend. 페이지상 "텍스처 없음" → 단색 머티리얼 | Quaternius 캐릭터와 묶는 대안. 구성 미확인 |
| Kenney Mini Dungeon | https://kenney.nl/assets/mini-dungeon (itch: https://kenney-assets.itch.io/mini-dungeon) | CC0 (확인) | 20 모델, 애니메이션 포함 표기. 바닥·벽·문 구성 미확인 | OBJ·FBX·glTF (itch 페이지 확인) | 대안 조합의 환경 |

### 3-2. 캐릭터 · 애니메이션
| 이름 | URL | 라이선스 | 채우는 항목 | URP/Unity 6 주의 | 평가 |
|---|---|---|---|---|---|
| **KayKit Adventurers** | https://kaylousberg.itch.io/kaykit-adventurers | CC0 (확인) | 무료판 5 캐릭터(Knight, Barbarian, Rogue+Hooded, Mage, Ranger) + 장신구, 리깅·기본 이동 애니메이션 포함 (확인) | FBX·glTF. 리그 타입(Unity Humanoid 설정 가능 여부) **미확인** — Generic으로 두고 같은 리그의 KayKit 애니메이션만 쓰면 리타깃 불필요 | 추천 캐릭터. 작은 비율이라 그리드에서 잘 읽힘 |
| **KayKit Character Animations** | https://kaylousberg.itch.io/kaykit-character-animations | CC0 (확인) | 150+ 애니메이션: idle, 피격, death, spawning, interacting, walking, running, jumping, sneaking, cheering 등 (확인). "fall" 명시 없음 | FBX·glTF. Rig_Medium(100+)·Rig_Large(25+) 대상. KayKit 캐릭터와 호환되도록 업데이트됨 (확인) | idle·walk·spawning(새 몸 깨어남)·cheering(클리어) 해결 |
| Quaternius Ultimate Modular Characters | https://quaternius.com/packs/ultimatemodularcharacters.html | CC0 (확인) | 11 캐릭터(4파트 조합), 24 애니메이션 (확인). 애니메이션 이름 미확인 | FBX·OBJ·Blend·glTF. 리그 정보 미확인 | Quaternius 계열 대안 |
| Quaternius Universal Animation Library | https://quaternius.itch.io/universal-animation-library | CC0 (확인) | 무료 Standard 45 애니메이션, 범용 휴머노이드 리그, 리타깃 전제 (확인). 이름 목록 미확인 | Standard판 포맷 미확인 | 애니메이션 보강용 |
| Kenney Mini Characters | https://kenney.nl/assets/mini-characters | CC0 (확인) | 25 파일, 애니메이션 포함 표기. 종류 미확인 | 포맷 미확인 | 대안 조합의 캐릭터 |
| Mixamo | https://www.mixamo.com/ | Adobe 약관. 검색 결과 기준 공식 FAQ에 "개인·상업·비영리 프로젝트(게임 포함)에 로열티 프리", 크레딧 불필요, 원본 파일 단독 재배포 금지. Adobe ID 필요. FAQ 페이지 직접 열람은 403으로 **미확인** | 떨어짐·미끄러짐 등 부족한 동작 보충 | KayKit 리그와 다르므로 Unity Humanoid 리타깃 필요(KayKit의 Humanoid 설정 가능 여부 미확인) | 최후 보충용. 화풍(과장된 동작)이 다를 수 있음 |

### 3-3. VFX
| 이름 | URL | 라이선스 | 채우는 항목 | URP/Unity 6 주의 | 평가 |
|---|---|---|---|---|---|
| **Unity Particle Pack** (Unity Technologies) | https://assetstore.unity.com/packages/vfx/particles/particle-pack-127325 | 무료, Standard Unity Asset Store EULA (확인) | 불·폭발·연기 계열(키워드 기준, 상세 목록 미확인) → 석화 먼지(P1), 낙하 먼지(P1) | **URP: Unity 6000.0 / 6000.3 호환 표기** (확인). v3.3, 2026-10-08 갱신. 용량 160 MB — 필요한 프리팹만 남길 것 | Unity 6 URP 확인된 유일한 후보. 사실적 스타일이라 로우폴리와 안 맞는 효과는 빼기 |
| Cartoon FX Remaster Free (Jean Moreno) | https://assetstore.unity.com/packages/vfx/particles/cartoon-fx-remaster-free-109565 | 무료, Standard EULA (확인) | 카툰풍 마법·연기·폭발(키워드 기준, 개수 미확인) → SWITCH 빛기둥, 석화 | Built-in·URP 호환 표기, 단 호환 표에 2020.3·2021.3만 있음 → **Unity 6 동작 미확인**. R 1.5.1, 2025-12-18 | 화풍은 로우폴리와 가장 잘 맞음. 임포트 후 확인 필요 |
| Kenney Particle Pack | https://kenney.nl/assets/particle-pack | CC0 (확인) | 512×512 파티클 스프라이트 80장. 원·별·빛 종류 포함 여부 미확인 | 텍스처만 제공 → URP Particles/Unlit 머티리얼에 넣어 사용 | 7번째 걸음 도장·발자국 직접 제작의 재료 |

### 3-4. UI · 한글 폰트
| 이름 | URL | 라이선스 | 채우는 항목 | 주의 | 평가 |
|---|---|---|---|---|---|
| **Kenney UI Pack** (v2.0 리메이크) | https://kenney.nl/assets/ui-pack | CC0 (확인) | 430 파일, button·panel·slider 태그 (확인) | 스프라이트 → Unity UI 9-slice 설정 | KEEP/SWITCH 버튼, 클리어 패널 |
| **Kenney Game Icons** | https://kenney.nl/assets/game-icons | CC0 (확인) | 105 아이콘. 되돌리기·재시작·힌트 아이콘 포함 여부 **미확인** | — | P1 아이콘 |
| **Pretendard** | https://github.com/orioncactus/pretendard | SIL OFL (확인). 글꼴 단독 판매 외 상업 이용·수정·재배포 가능 | 본문·UI 한글 | 9 굵기 + 가변 폰트. TMP 변환 시 정적 TTF(굵기별) 사용 | 깔끔한 고딕. 가독성 1순위 |
| **Noto Sans KR** | https://github.com/google/fonts/tree/main/ofl/notosanskr | SIL OFL (`OFL.txt` 확인) | 본문·UI 한글 | 저장소에는 가변 폰트 `NotoSansKR[wght].ttf`만 확인됨. TMP는 가변 축을 쓰지 못하므로 기본 굵기로만 나옴 → 굵기가 필요하면 Google Fonts 다운로드의 정적 파일 사용(정적 파일 포함 여부 미확인) | 무난한 대안 |
| **Jua** (주아체) | https://github.com/google/fonts/tree/main/ofl/jua | SIL OFL (`OFL.txt` 확인) | 제목·스테이지 이름 | 단일 굵기 `Jua-Regular.ttf` (확인) | 둥글고 귀여워 KayKit 화풍과 어울림. 제목용 |

### 3-5. 사운드 · BGM
| 이름 | URL | 라이선스 | 채우는 항목 | 주의 | 평가 |
|---|---|---|---|---|---|
| **Kenney RPG Audio** | https://kenney.nl/assets/rpg-audio | CC0 (확인) | 50 파일, footstep·foley 태그 (확인). 문 소리 언급 없음 | 포맷 미확인 | 발소리 P0 1순위 |
| **Kenney Impact Sounds** | https://kenney.nl/assets/impact-sounds | CC0 (확인) | 130 파일, impact·foley 태그. 발소리 포함 여부 미확인 | 포맷 미확인 | 석화(돌), 발판 눌림, 무너짐 후보 |
| **Kenney Interface Sounds** | https://kenney.nl/assets/interface-sounds | CC0 (확인) | 100 파일, click·button 태그 | 포맷 미확인 | UI 클릭 (P1) |
| **Kenney Music Jingles** | https://kenney.nl/assets/music-jingles | CC0 (확인) | 85 파일, jingle 태그 | 포맷 미확인 | 클리어 징글 (P1), 7번째 걸음 차임 대체 후보 |
| **Freesound (CC0 필터)** | https://freesound.org/ | 소리마다 다름: CC0 / CC BY(크레딧 필수) / **CC BY-NC(상업 불가 — 사용 금지)** (FAQ 확인). 다운로드에 로그인 필요 | 7번째 걸음 차임: `Chimes Sparkle` https://freesound.org/people/lukabea/sounds/660493/ (CC0, 36.5초 WAV, 확인). 실패음: `Sad Chime Effect` https://freesound.org/people/zembacraftworks/sounds/455289/ (CC0, 7초, 확인) | 검색 시 라이선스 필터를 CC0로 고정. 긴 파일은 잘라서 OGG로 | P0 차임 확보 |
| **OpenGameArt CC0 앰비언트 BGM** | 예: https://opengameart.org/content/contemplation-0 , https://opengameart.org/content/jrpg-pack-4-calm | 검색 결과 기준 CC0 (Contemplation 0 — 루프·앰비언트·calm 태그 / JRPG Pack 4 Calm — 심리스 루프). **사이트가 403을 반환해 페이지 직접 확인 못 함 → 미확인** | BGM 1~2곡 (P1) | 받을 때 페이지의 License 칸 재확인. CC-BY 곡(예: Indie Meditations)은 크레딧 필요 | 후보만 확보 |

### 3-6. 기술 에셋
| 이름 | URL | 라이선스 | 용도 | Unity 6 / WebGL | 평가 |
|---|---|---|---|---|---|
| **PrimeTween** | https://github.com/KyryloKuzyk/PrimeTween (Asset Store 또는 npm 스코프 레지스트리 `com.kyrylokuzyk`) | 자체 라이선스 (LICENSE 확인): 무료·상업 게임 사용·수정 가능, **PrimeTween 자체의 재배포·재판매 금지** | 칸 이동, 문 열림, UI | WebGL에서 await 동작 명시. 런타임 할당 없음 표방. Unity 6 명시 여부 미확인 | UPM(npm) 설치라 저장소에 소스가 안 들어가 공개 저장소에 유리 |
| **DOTween** (무료판) | https://dotween.demigiant.com/ | 자체 라이선스 (확인): 상업 사용 자유, 원본 그대로 재배포 시 저작권 고지 + readme 포함, 수정본 재배포 금지 | 동일 | **Unity 6 ~ 2022.3 호환, WebGL 지원** 명시 (확인). Unity 6000.6 관련 수정은 Asset Store판 필요 | 자료·예제가 가장 많음 |
| Quick Outline | https://assetstore.unity.com/packages/tools/particles-effects/quick-outline-115488 | 무료, Standard EULA (확인) | 외곽선 (P1) | 원본 2018.4, 2022-03 v1.1. **URP 지원 미확인** | 안 되면 URP Renderer Feature로 직접 |
| URP 기본 기능 (패키지 아님) | — | Unity 포함 | 돌·얼음 머티리얼(URP/Lit), 블룸(Volume → Bloom), 배경(카메라 Solid Color) | — | 셰이더·포스트·배경 P1~P2는 별도 에셋 없이 해결 |

---

## 4. 라이선스 · 크레딧 표기 정리

| 에셋 | 라이선스 | 크레딧 의무 | 주의 |
|---|---|---|---|
| KayKit 전 팩 | CC0 | 없음 (권장: "Kay Lousberg, www.kaylousberg.com") | 수정 없는 원본 재판매·자기 작품 주장 금지 요청 |
| Kenney 전 팩 | CC0 | 없음 (권장) | — |
| Quaternius | CC0 | 없음 | — |
| 0x72 DungeonTileset II | 에셋 CC0 / 코드 MIT | 없음 (코드를 쓰면 MIT 고지) | — |
| Unity Asset Store 무료 (Particle Pack, Cartoon FX Free, Quick Outline) | Standard EULA, Extension Asset | 없음 | **원본 파일을 공개 저장소(GitHub public)에 올리면 안 됨** — 빌드에 포함은 가능. 저장소가 공개라면 `Assets/ThirdParty/AssetStore/`를 `.gitignore`에 |
| Mixamo | Adobe 약관 | 없음 | 원본 FBX 단독 재배포 금지 → 공개 저장소 주의 |
| Pretendard / Noto Sans KR / Jua | SIL OFL 1.1 | 없음 | 글꼴 단독 판매 금지. 빌드에 폰트가 들어가므로 크레딧 화면이나 배포 폴더에 폰트 이름 + OFL 고지를 넣는 것이 안전 |
| Freesound | 소리마다 CC0 / CC BY / CC BY-NC | CC BY면 **필수** (제목·작성자·URL·라이선스) | **CC BY-NC는 상업 이용 불가 → 사용 금지**. CC0만 쓰면 의무 없음 |
| OpenGameArt | 곡마다 다름 | CC-BY면 필수 | 다운로드 전에 페이지에서 재확인 |
| DOTween 무료판 | 자체 | 없음 | 수정본 재배포 금지. 공개 저장소에 원본을 넣으면 readme·고지 유지 |
| PrimeTween | 자체 | 없음 | 소스·tgz를 파생물에 넣어 배포 금지 → npm 설치 권장 |

**권장 크레딧 문구(선택 사항, itch.io 페이지·크레딧 화면)**: `3D models: KayKit by Kay Lousberg (CC0) · UI & SFX: Kenney (CC0) · Fonts: Pretendard / Jua (SIL OFL 1.1) · SFX: freesound.org (CC0)` — CC-BY 소리를 쓰면 해당 줄을 추가.

---

## 5. 직접 제작 방법 메모

| 항목 | 방법 |
|---|---|
| 그리드 스케일 | KayKit 바닥 타일 한 장의 실제 크기는 **미확인**. 임포트 후 `floor_tile_small`의 Bounds를 재서 그리드 1칸 = 1유닛이 되도록 프리팹 루트 스케일을 한 번 정해 모든 타일에 공통 적용 |
| 얼음 바닥 | `floor_tile_small` 프리팹 변형 + URP/Lit, Surface Type Transparent, 하늘색 알파 0.6, Smoothness 0.9 |
| 발판 a~c | 납작한 바닥 조각 + 머티리얼 3색. 눌림은 Y −0.05 트윈 |
| 문 A~C | 창살 메시가 분리돼 있으면 그 부분만 아래로 트윈. 아니면 문 전체를 바닥 아래로 내림 |
| 숫자 문 | 아치 프리팹 위에 TextMeshPro(3D) 숫자. 현재 걸음과 같으면 숫자 색을 금색으로 |
| 석상 | 같은 캐릭터 프리팹 복제 → 머티리얼을 회색 돌(URP/Lit) 1개로 교체, Animator 정지 |
| 7번째 걸음 도장 | Particle System 1개(Burst 1, 원형 스프라이트, 크기 0→2, 금색→투명) + URP Bloom. 화면 흔들림·차임과 같은 프레임에 |
| 발자국·걸음 번호 | 바닥 위 반투명 원 Quad + TMP 숫자, 지나간 칸에 생성 |
| 한글 TMP 폰트 | Font Asset Creator → Character Set `Characters from File`에 게임에 쓰는 모든 문자열(스테이지 이름·UI 문구)을 모은 텍스트 파일 지정 → 정적 아틀라스. 공식 문서: 문자가 많으면 메인 + 폴백 폰트 에셋으로 나눔. 원본 TTF는 Resources에 있거나 다른 곳에서 쓰이지 않으면 빌드에 포함되지 않음(문서 확인) → WebGL 용량 절약을 위해 정적 아틀라스 권장 |

---

## 6. 확인 못 한 점

1. **KayKit 바닥 타일의 실제 크기**와 그리드 1유닛 맞춤 방법 (임포트 후 측정).
2. **KayKit 캐릭터를 Unity Humanoid로 설정할 수 있는지** (Mixamo 리타깃 가능 여부가 여기에 달림).
3. **KayKit Character Animations에 떨어짐(fall) 동작이 있는지** — 목록에 "jumping"만 있음. Adventurers 무료판에 포함된 "기본 이동 애니메이션"의 정확한 이름.
4. `wall_gated`의 **창살이 별도 메시인지** (문 열림 연출 방식 결정).
5. 확인한 파일 목록은 GitHub 저장소(1.0판)¹²의 것. itch.io 최신판(v1.1 등)과 파일명이 다를 수 있음.
6. **Cartoon FX Remaster Free의 Unity 6 동작**, **Quick Outline의 URP 지원**.
7. Kenney 팩들(Mini Dungeon, Mini Characters, Particle Pack, UI Pack, Game Icons, 오디오 팩)의 **개별 파일 목록과 오디오 포맷** — Kenney 페이지에 목록이 없음.
8. **Mixamo 공식 FAQ 원문** (403으로 직접 열람 실패, 검색 요약으로만 확인).
9. **OpenGameArt BGM 후보 페이지** (403으로 직접 열람 실패, 검색 결과로만 CC0 확인).
10. Quaternius 팩의 리그·애니메이션 이름, Universal Animation Library Standard판 포맷.
11. PrimeTween의 Unity 6 명시 지원 여부.
12. KayKit itch.io 페이지의 유료 티어 가격이 본문과 다운로드 영역에서 서로 다르게 표기됨 (무료판만 쓰므로 영향 없음).

---

¹ https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0 (LICENSE.txt: CC0)
² https://github.com/KayKit-Game-Assets/KayKit-Prototype-Bits-1.0 · 캐릭터: https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0 (README: 4 캐릭터 + 75 애니메이션 표기, itch 페이지는 무료판 5 캐릭터로 표기 — 판 차이)
