# 일곱 걸음 — 무료 에셋 후보

조사일: 2026-11-07. 대상: Unity 6000.6.8f1, URP 3D, WebGL. 사용자 최신 결정에 따라 **무료만** 조사 목록에 남긴다. 모든 링크는 상품 또는 제작자 원문이며, 가격·호환 정보는 조사 시점 표기다. 실제 프로젝트 임포트 검증은 하지 않았다.

## 권장 조합과 공개 저장소 정책

**KayKit Dungeon Remastered + KayKit Adventurers + Kenney UI/오디오 + Jua/Pretendard**를 기본안으로 권한다. 작은 순례자, 단순한 석재, 둥근 패널을 유지하고 청록은 이동·선택, 회색은 석상, 금색은 일곱 번째 걸음에 사용한다. 아래 Asset Store 후보는 기본안의 대체·보완용이며 여러 환경 팩을 혼합하지 않는다.

원본 바이너리는 라이선스와 관계없이 git에 커밋하지 않는다. 사용자가 Unity 계정으로 무료 상품을 내 에셋에 추가하고 다운로드한다. 추후 공유 위치는 `C:\unityjam\shared-assets\seven-steps\Assets\`, 워크스페이스 연결 위치는 `unity/Assets/Shared`이며 리드가 설정한다. 이번 단계에서는 `unity/`를 변경하지 않는다. Asset Store 파일은 공개 재배포하지 않는다([Unity EULA](https://unity.com/legal/as-terms), 특히 Appendix 1 §2.2). 무료도 CC0와 동일하지 않다.

표의 **EULA**는 Standard Unity Asset Store EULA를 뜻한다. 상품 페이지가 Extension Asset로 표시한 경우 팀원별 라이선스 요건도 다운로드 전에 확인한다. **미검증**은 Unity 6000.6.8f1에서 열기·WebGL 빌드를 아직 하지 않았다는 의미다. UI 이미지·오디오·폰트는 렌더 파이프라인 의존성이 낮다는 기술적 판단과 판매자 호환 보증을 구분했다.

## 환경 — Asset Store 무료 2종

| 후보·출처 | 가격 / 라이선스 | Unity 6 / URP | 요구 충족과 선택 이유 |
|---|---|---|---|
| [Ultimate Low Poly Dungeon — Broken Vector](https://assetstore.unity.com/packages/3d/environments/dungeons/ultimate-low-poly-dungeon-143535) | $0 / EULA, Extension Asset | 2021.2.10 표에서 URP 비호환. Unity 6 미검증; 메시만 사용하고 URP/Lit 재구성 필요 | P0 바닥·벽·문 후보, P2 소품. 185개 석재·가구 모듈로 형태가 단순함. 기존 KayKit 대체 시 한 팩으로 통일. 얼음·발판·숫자는 별도 제작 |
| [Low Poly Free Dungeon Pack — GlassOfCoins](https://assetstore.unity.com/packages/3d/environments/dungeons/low-poly-free-dungeon-pack-191979) | $0 / EULA, Extension Asset | 2020.2.2 표에서 URP 비호환. Unity 6 미검증; 머티리얼 교체 필요 | P0 환경 대체 후보. 저밀도 모듈을 시험하기 좋은 작은 팩(약 962KB). 구체적인 문·바닥 구성은 다운로드 후 확인해야 함 |

## 캐릭터·동작 — Asset Store 무료 2종

| 후보·출처 | 가격 / 라이선스 | Unity 6 / URP | 요구 충족과 선택 이유 |
|---|---|---|---|
| [RPG Tiny Hero Duo PBR Polyart — Dungeon Mason](https://assetstore.unity.com/packages/3d/characters/humanoids/rpg-tiny-hero-duo-pbr-polyart-225148) | $0 / EULA, Extension Asset | 2019.4.39 표에서 URP 호환. Unity 6 미검증 | P0 플레이어·같은 모델의 석상. 작은 비율이 그리드에서 읽히는 대안. KayKit보다 PBR 질감이 강하므로 채택 시 무광·저채도 재질로 통일; 필요한 걷기·낙하 클립 목록은 미확인 |
| [RPG Character Mecanim Animation Pack FREE — Explosive](https://assetstore.unity.com/packages/3d/animations/rpg-character-mecanim-animation-pack-free-65284) | $0 / EULA, Extension Asset | 6000.0.47 표에서 URP 호환. 6000.6 미검증 | P0 대기·걷기 등 동작 보강 후보. 모델 팩 대신 동작을 보충하는 용도. 필요한 클립 존재와 KayKit Humanoid 리타깃은 미확인; 샘플 컨트롤러·설정 스크립트는 도입 대상 아님 |

## UI — Asset Store 무료 2종

| 후보·출처 | 가격 / 라이선스 | Unity 6 / URP | 요구 충족과 선택 이유 |
|---|---|---|---|
| [Free Ui Pack — Valley Land Games](https://assetstore.unity.com/packages/2d/gui/icons/free-ui-pack-170878) | $0 / EULA, Extension Asset | Unity 6/URP 명시 보증 미확인. 일반 스프라이트는 uGUI 적용 예상, 프리팹 미검증 | P0 버튼·패널, P1 보조 아이콘 후보. 약 735KB로 작음. 7칸·몸·발자국은 콘셉트 기준 별도 제작; 개별 아이콘 목록 미확인 |
| [SIMPLE FANTASY GUI — Nayrissa](https://assetstore.unity.com/packages/2d/gui/simple-fantasy-gui-99451) | $0 / EULA, Extension Asset | 원본 Unity 5.6.2. Unity 6/URP 명시 보증 미확인; 이미지 사용 예상, 레거시 프리팹 미검증 | P0 KEEP/SWITCH·클리어 패널 대체. 판타지 던전에 어울리는 프레임 후보이나 장식량은 줄이고 청록·크림으로 맞출 것 |

## VFX — Asset Store 무료 2종

| 후보·출처 | 가격 / 라이선스 | Unity 6 / URP | 요구 충족과 선택 이유 |
|---|---|---|---|
| [Cartoon FX Remaster Free — Jean Moreno](https://assetstore.unity.com/packages/vfx/particles/cartoon-fx-remaster-free-109565) | $0 / EULA, Extension Asset | Built-in·URP 지원 명시. 기존 조사 표는 2020.3/2021.3; 무료판의 Unity 6 명시 보증 미확인, 6000.6 미검증 | P1 KEEP 먼지·SWITCH 마법 보강. 카툰풍이 권장 스타일에 가까움. 필요한 효과의 정확한 포함 여부 확인 후 색·입자 수 조정 |
| [Particle Pack / Starter Assets — Unity](https://assetstore.unity.com/packages/vfx/particles/particle-pack-starter-assets-127325) | $0 / EULA, Extension Asset | 6000.0/6000.3 URP 호환 표기. 6000.6 미검증 | P1 석화·낙하 먼지 후보. Unity 6 근거가 가장 명확함. 사실적인 불·폭발은 제외하고 먼지만 시험; 160MB 전체를 배포에 포함하지 않을 것 |

P0 행운 도장은 전용 콘셉트를 바탕으로 원형 스프라이트와 확대·알파 감소로 제작한다. 팩의 폭발 이펙트를 그대로 쓰지 않는다.

## 사운드 — Asset Store 무료 2종

| 후보·출처 | 가격 / 라이선스 | Unity 6 / URP | 요구 충족과 선택 이유 |
|---|---|---|---|
| [FREE Casual Game SFX Pack — Dustyroom](https://assetstore.unity.com/packages/audio/sound-fx/free-casual-game-sfx-pack-54116) | $0 / EULA, Extension Asset | 오디오는 URP 비의존. Unity 6 및 WebGL 디코딩·메모리 미검증 | P1 UI 클릭·성공 징글 후보. 가벼운 캐주얼 음색을 기본안과 맞추기 쉬움. 발소리·차임 포함 여부는 청취 전 확정하지 않음 |
| [Free Sound Effects Pack Starter - All categories — Bamsfx](https://assetstore.unity.com/packages/audio/sound-fx/free-sound-effects-pack-starter-all-categories-155776) | $0 / EULA, Extension Asset | 원본 Unity 2017.1. 오디오는 URP 비의존, Unity 6/WebGL 미검증 | P0~P1 발소리·문·돌 소리 탐색용. 폭넓은 보완 후보이나 파일별 포함 여부·음색 미확인. 284MB로 커서 필요한 클립만 선택 |

## 폰트 — Asset Store 무료 2종 (숫자·영문 보조 후보)

| 후보·출처 | 가격 / 라이선스 | Unity 6 / URP | 요구 충족과 선택 이유 |
|---|---|---|---|
| [Bubble Font (Free Version) — Jazz Create Games](https://assetstore.unity.com/packages/2d/fonts/bubble-font-free-version-24987) | $0 / EULA, Extension Asset; OFL 확인 안 됨 | 원본 2022.3.62, TMP 태그. Unity 6 미검증; 일반 폰트 렌더는 URP 비의존 | KEEP/SWITCH 영문·큰 숫자의 둥근 인상 비교용. 한글 지원 미확인, P0 한글 폰트 대체 불가. OFL 필수 조건 때문에 최종 채택 보류 |
| [BoldPixels Font — YukiPixels](https://assetstore.unity.com/packages/2d/fonts/boldpixels-font-332078) | $0 / EULA, Extension Asset; OFL 확인 안 됨 | 원본 6000.2.2. URP 명시 보증 미확인; 폰트 렌더는 URP 비의존, 6000.6 미검증 | 문 숫자·HUD 숫자 비교용. 명시 문자 범위에 한글 없음. 픽셀형이 둥근 기본안과 다르므로 추천도 낮음; OFL 필수 조건으로 최종 채택 보류 |

Store에서 무료라는 이유만으로 OFL 한글 요건을 충족하지 않는다. 최종 한글은 아래 OFL 후보에서 선택한다.

## 유지하는 CC0·OFL 기본 후보 (Store 외 제작자 배포)

| 범주 / 후보·출처 | 가격 / 라이선스 | Unity 6 / URP | 요구 충족·추천 이유 |
|---|---|---|---|
| 환경 [KayKit Dungeon Remastered](https://kaylousberg.itch.io/kaykit-dungeon-remastered) | 무료판 $0 / CC0 | FBX 권장, URP/Lit 재질 적용 예상; 6000.6 미검증 | P0 바닥·벽·창살·아치, P1 금 간 바닥. 최우선; 파일명·스케일은 기존 조사 참조 및 후속 검증 |
| 캐릭터 [KayKit Adventurers](https://kaylousberg.itch.io/kaykit-adventurers) + [Character Animations](https://kaylousberg.itch.io/kaykit-character-animations) | 무료판 $0 / CC0 | FBX, 동일 리그 Generic 사용 우선. URP 재질·6000.6 미검증 | P0 순례자·석상·걷기. 환경과 비율·화풍 일치. fall 클립·Humanoid 여부 미확인 |
| 환경·캐릭터 [Kenney Mini Dungeon](https://kenney.nl/assets/mini-dungeon) / [Mini Characters](https://kenney.nl/assets/mini-characters) | $0 / CC0 | 메시 재질 URP 변환 및 6000.6 검증 필요 | 기본안 전체를 바꾸는 경우의 간결한 대안. KayKit과 섞기보다 한 계열로 통일 |
| UI [Kenney UI Pack](https://kenney.nl/assets/ui-pack) / [Game Icons](https://kenney.nl/assets/game-icons) | $0 / CC0 | 이미지 기반, uGUI 9-slice 예상; 6000.6 미검증 | P0 패널·버튼, P1 보조 아이콘. 청록·크림 스킨을 입히기 쉬움; 몸·발자국 포함 여부 미확인 |
| VFX [Kenney Particle Pack](https://kenney.nl/assets/particle-pack) | $0 / CC0 | 텍스처 + URP Particles/Unlit 직접 구성; 미검증 | P0 도장 링·P1 먼지의 제작 재료. 정확한 링 이미지 존재는 미확인 |
| 음향 [Kenney RPG Audio](https://kenney.nl/assets/rpg-audio) / [Impact Sounds](https://kenney.nl/assets/impact-sounds) / [Interface Sounds](https://kenney.nl/assets/interface-sounds) / [Music Jingles](https://kenney.nl/assets/music-jingles) | $0 / CC0 | URP 비의존, 오디오 포맷·WebGL 임포트 미검증 | P0 발소리, P1 석화·클릭·징글 후보. 차임·문 소리는 청취로 확정 필요 |
| 폰트 [Jua](https://github.com/google/fonts/tree/main/ofl/jua) / [Pretendard](https://github.com/orioncactus/pretendard) / [Noto Sans KR](https://github.com/google/fonts/tree/main/ofl/notosanskr) | $0 / SIL OFL 1.1 | 한글 TMP 아틀라스 제작 필요, URP 비의존; 6000.6 미검증 | P0 한글. Jua 제목 + Pretendard 본문 추천; Noto는 대안. 라이선스 원문을 다운로드 파일과 함께 공유 폴더에 보존 |

기존 CC0 조사 근거·누락 항목은 [상세 조사](seven-steps-asset-research.md)를 유지한다. 이 단계에서는 후보를 다운로드하지 않았다. 소스별 채택 상태는 [크레딧](CREDITS.md), 생성 이미지 및 방향 선택은 [콘셉트 안내](concepts/README.md)에 기록한다.

## 다음 결정과 검증

사용자는 기본 KayKit/Kenney 조합 또는 위 대체 후보 중 채택할 항목과 크림·청록 UI 방향을 선택하면 된다. 비용 정책은 무료로 확정되었으며 예산 승인은 추가로 필요하지 않다. 숫자·텍스트는 최종 UI에서 폰트로 그려 다국어와 상태 변경을 지원한다.

TileSet 병합 이후 실제 Bounds·피벗·1유닛 스케일, 문 메시 분리, KayKit 리그, 필수 클립, 폰트 문자 범위, 64px 아이콘 가독성, URP 셰이더, WebGL 용량·음향을 확인한다. 문서·콘셉트만 바꾼 이번 단계의 검증은 링크·저장 경로·git 변경 범위 확인이며 Unity CompileCheck는 실행하지 않는다.
