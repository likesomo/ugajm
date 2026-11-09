# 유적·행성 UI 테마 — 2026-11-09

시안과 적용 후보를 분리했다. 시안은 `C:/unityjam/shared-assets/seven-steps/concepts/ui/`, 갤러리는 해당 폴더 `index.html`이다. 기준 화면은1280×720, 세로는 참고용이며 아직 정식 모바일 레이아웃 결정이 아니다. 실제 Unity 적용은 아트 소유 테마/패널/리소스 후보까지만 만들고 공용 씬·UI 컨트롤러는 바꾸지 않았다.

## 화면과 정보 순서

| 화면 | 파일 | 구성 |
|---|---|---|
| 타이틀 | title-desktop-v2.png | 일곱 걸음/Seven Steps, 시작·이어하기·스테이지·설정·크레딧; 같은 실루엣3형제·선돌 제단 행성 |
| 세로 참고 | title-portrait.png | 세로 안전 영역, 메뉴 아래 배치; 엄지 터치48px 이상 목표 |
| 부트 | loading-boot.png | 최초 로딩 진행과 짧은 팁;7별 채우기 |
| 씬 전환 | loading-transition-v2.png | 기존 화면 위 짙은 페이드, 작은 진행 표시; 부트와 같은 무거운 배경을 다시 읽지 않음 |
| 챕터 진입 | chapter-entry-v2.png | 행성 이름·장 번호·형제 여정 지도;5행성은 잠정 시안 |
| 선택 | stage-select-v2.png | 행성별 그룹·스테이지별3별·장 별 합계9/30; 미해금은 잠금+비활성 |
| HUD | game-hud-v2.png | 숫자1–7+정확한 북두칠성, 사용 중/남음/선돌3색 몸 상태, 하단 버튼 |
| 결과 | result.png |1~3별 순차 노출, 다음·재시도·스테이지; **평가 기준 검토안** 표시 |
| 키트 | ui-kit.png | 버튼4상태·9-slice·아이콘·팔레트·서체 방향 |

생성 이미지의 문구·맵은 기능 명세가 아니다. 특히 첫 HUD의 '형제 모두 이동' 문구와 다른 실루엣 석상은 수정 시안v2에서 제거했으며 첫 이미지는 비교 기록이다. 제목/전환 첫 이미지의 별8개, 장 카드의 카메라 회전 안내, 선택 화면의 별 합계도v2에서 교정했다. 실제 구현의 좌표·규칙은 아래 리소스/기획을 따른다. 챕터 수는 레벨 담당50스테이지 계획과 연동해 확정하며 이 작업에서 장 경계를 바꾸지 않는다.

## 스타일과 수치

- 팔레트: 배경 `#101C32`, 글자 `#EEE6D4`, 보조 `#A0B0BA`, 놋쇠 `#C9AB6B`, 석재 `#60778A`, 이끼 `#748D57`. 형제 청록 `#568C88`·황토 `#B99A57`·적갈 `#B96C59`, 선돌 `#B9BFC0`.
- 본문은 평탄한 남색 면, 이끼·구름/천문 눈금은 프레임 바깥에만 쓴다. 색만으로 상태를 구분하지 않고 사용 중/남음/선돌 문구와 실루엣을 병기한다. 비활성 버튼은 명도와 포커스/입력 상태를 함께 바꾼다.
-1280×720에서 제목48–64px, 메뉴/중요 숫자24–28px, 본문20–22px, 보조18px 목표. 가장자리 안전 영역32px, 터치48px 이상. 긴 번역 문구는 줄바꿈을 허용하고 버튼 폭을 고정 글자 수에 맞추지 않는다.
- 칠성 좌표(화면 y 아래): Dubhe1(.12,.12), Merak2(.10,.36), Phecda3(.35,.40), Megrez4(.42,.18), Alioth5(.62,.12), Mizar6(.79,.20), Alkaid7(.98,.28). 선1–2–3–4–1 및4–5–6–7.1~6은 별만 켜지고7번째 완성 때만 금선/짧은 섬광. UI 프리팹 `Star_1..7`/`Number_1..7`가 이 좌표를 사용한다.
- 별 보상 후보:1=클리어,2=최소 몸 수,3=최단 걸음. 점수/저장/solver 기준은 구현하지 않았다. 현재 ResultModel은 최단 걸음/3별을 제공하지 않으므로 UI 담당·기획의 계약 확정이 필요하다. 장별 합계도 그 뒤 연결한다.

## 전달 리소스와 재생성

`design/art/build-ui-atlas.ps1`은 편집 가능한 코드 원본이다.1024² 투명 PNG와 슬라이스 JSON을 `Assets/Shared/UI/RuinsHenge`에 생성한다. GPT 시안의 글자나 질감을 잘라 쓰지 않고, 작은 화면에서 읽히는 단순한 프레임·아이콘을 별도로 그린 적용용1차 키트다. 추가 외부 텍스처는 없다.

`RuinsHengeUiBuilder.Build()`가 아래를 만든다(아트 빌더에서도 호출):

- `Art/UI/RuinsHengeTheme.asset`: 실제 S6 UiTheme, Jua·색·Settings/Result 패널 참조. 공용 기본테마는 수정하지 않는다.
- `Art/UI/SettingsPanel.prefab`, `ResultPanel.prefab`: 공용 프리팹을 읽어 아트 소유 복사본에9-slice/버튼 SpriteSwap4상태와 글꼴을 적용한다. 필수 직렬화 참조·이벤트 경계는 보존한다.
- `Art/UI/RuinsHengeResources.asset`: 위 테마·SpriteAtlas·버튼4·패널2·undo/restart/hint/settings/sound·빈/찬별·몸4·칠성 좌표 참조.
- `Art/UI/DipperCounter.prefab`: 선7개·별7개·숫자7개를 갖춘 배치 후보. 게임 HUD에 자동 삽입하지 않는다. UI 담당은 카운트 이벤트에서 해당 Image.sprite를 빈/찬별로 교체한다.
- `Art/UI/RuinsHengeUI.spriteatlas`: 회전/타이트 패킹 끔,4px padding, mipmaps 끔. PNG1024², bilinear/clamp/sRGB/alpha, 무손실 RGBA. 작은 UI 확대 시 계단과 압축 번짐을 피한다.
- `Art/UI/RuinsHengeTextureImport.preset`: 커밋 가능한 임포트 설정 원본. 버튼512×80/border20, 패널256×256/border32, 아이콘64²/border0. 전체17슬라이스. Shared PNG.meta는 공유 폴더의 기존 GUID를 보존한다. Unity 네이티브 에셋·프리팹·preset와 그meta를 커밋한다.

S6 UiTheme에 sprite 필드는 아직 없다. 따라서 코어 계약을 임의로 늘리지 않고 `RuinsHengeResources`로 전달했다. UI 담당이 이 리소스를 읽거나 UiTheme의 필드를 확장하면 된다. 문서의 색과 실제 UiTheme 필드는 ink/dim/gold/stone/buttonBg/panelBg/font/settingsPanel/resultPanel이며, plateA/B/C는 퍼즐 채널색이므로 형제색으로 덮어쓰지 않는다.

폰트: 적용은 기존 `Shared/Fonts/Jua-Regular.ttf`를 재사용하며 중복 복사하지 않는다. 제목 Jua, 본문 Pretendard 계열을 제안하지만 Pretendard는 다운로드/추가하지 않았다. Jua는 공유 `Jua-OFL.txt`(Copyright2018 The Jua Project Authors), Pretendard는 [공식 OFL1.1](https://github.com/orioncactus/pretendard/blob/main/LICENSE)을 따른다. 본문 전환 시 한글/기호 글리프와 legacy Text/TMP 비용을 UI 담당이 검사한다.

## 게임 씬 연결과 품질 보완

환경 프리팹과 계약 연결은 [unity-integration.md](unity-integration.md)의 UI 작업 이후 절을 따른다. 벽은 가장자리 이끼·베벨/분할 줄눈을 유지하고 석재V2/V3와 목재 변형을 좌표 기반으로 선택한다. 같은 벽 기믹을 나타내며 높이0.6m 이하/칸 중심은 유지한다. 동작/충돌은 바꾸지 않는다.

몸 공유 메시에서 상단70% 이후의 머리 폭12%/깊이8%를 키우되 최고점0.88m는 유지하고, 얼굴 UV를 어두운 청록 패치에서 밝은 중성 패치로 옮긴다. PlayerAtlas는 최소 조명.60의 아트 URP 셰이더로 변경해 두건 안쪽의 검정 뭉침을 줄인다. A/B/C/KEEP는 같은 몸 메시를 참조하며 추가 머티리얼을 만들지 않는다. TileSet.playerBodies에3색을 실제 연결했고 KEEP는 우선 중성 석상 공통 참조다.
