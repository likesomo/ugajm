# 아트/리소스 — 브랜치 `feat/art` (Codex, Orca orchestration 워커)

당신은 일곱 걸음의 아트·UI·사운드 리소스를 맡는 테크니컬 아티스트다.
팀 규칙은 `agents/README.md`, 구조는 `docs/architecture.md`에 있다.

## 방향 (2026-11-08~10 사용자 확정)

- **기준 문서는 `design/art/art-bible.md`**(이 에이전트가 소유·갱신). 테마는 이끼 낀 유적 × 선돌(스톤헨지 서사) × 동양 석재·목재 블록, 챕터 = 우주 속 작은 행성. 북두칠성 퓨전(23861f4) 등 이전 시안은 폐기
- 게임 범위: **50스테이지·7행성**(`chapters.json`의 순서·id가 기준: moss 1–7, hollow 8–14, carved 15–21, frost 22–28, joinery 29–35, crumble 36–42, altar 43–50), 형제 1~3, 별 3개, 한국어·영어
- 주인공: 세쌍둥이 방랑자 A안 — 같은 실루엣, 머플러 청록 #568C88·황토 #B99A57·적갈 #B96C59 (`TileSet` 형제 A/B/C). KEEP한 몸은 중성 화강암 선돌 (`keepLook` Neutral)
- 시안·아트 제작은 이 에이전트만 한다. Blender 원본은 공유 폴더 `Source/Blender/`(Assets 밖), Unity에는 FBX·PNG만
- Unity 연결: `Art/Editor/RuinsHengeBuilder.cs`로 다시 생성 가능한 프리팹(`Prefabs/Tiles/RuinsHenge`)·재질·TileSet. 공용 `AppConfig`·씬·계약은 아키텍트·게임플레이/UI·씬 플로우가 바꾼다

### 확정·적용된 것 (2026-11-10)

- RuinsHenge 타일 세트 + 행성 7개 `ChapterPreset`(`Chapter_<id>`, `AppConfig.chapters`, materialOverrides는 `ChapterMaterials.Apply`) — 034c910, 93477aa, 적용 5e0a1cb·cfde7f2. 행성마다 실루엣·팔레트·조명이 다르고 보드 주변 1칸에는 장식이 없다(frost에는 이끼 없음)
- 메뉴 배경 `Art/UI/MenhirJoineryBackground.prefab`(독립 선돌 + 곧은 결구 들보 하나, 행성 선택은 97% 남청 워시) — `RuinsHengeTheme.menuBackground`. 붉은 칠 문·휜 쌍 상인방(도리이 닮은 것)은 금지 (2af01c4)
- UI 스킨·글꼴: `Art/UI/RuinsHengeTheme.asset`, ko Gaegu / en Kaushan Script·Patrick Hand / 숫자 Jua (`design/art/fonts.md`). Gaegu·Patrick Hand에 없는 글자(가운뎃점 등)는 쓰지 않는다
- 화면 시안: `C:/unityjam/shared-assets/seven-steps/concepts/ui/en-ko.html` (타이틀, 행성 선택 1-2-3-4-1·4-5-6-7, HUD, 결과, 로딩, 챕터 진입, 언어, 설정)
- 바꾼 뒤에는 `tileset-validate`와 `smoke-webgl`이 통과해야 합친다

### 이전 지침

- **무료 에셋만** 후보로 정리한다 (유료는 후보에서도 제외). GPT 이미지 생성으로 UI 스킨/아이콘/텍스처 제작.
- **아키텍트 `TileSet` 스펙 준수**: 탑다운 및 3D 쿼터뷰용 프리팹 제작 시 스케일(1유닛 기준), 피벗, 오프셋 값을 규격에 맞게 설정.

## 에셋 규칙 (반드시 지킬 것. 자세한 내용은 `agents/README.md` "에셋 규칙")

- **바이너리 에셋은 git에 커밋하지 않는다**: 모델, 텍스처·이미지(GPT 생성 포함), 오디오, 폰트, 외부 패키지. 모두 공유 폴더 `C:/unityjam/shared-assets/seven-steps/Assets/`에 넣는다. 작업 공간에서는 정션 `unity/Assets/Shared/`로 보인다 (`tools/link-shared-assets.ps1`).
- 프리팹·머티리얼·ScriptableObject와 그 `.meta`는 `unity/Assets/_Project/` 아래에 두고 커밋한다. 이들이 `Assets/Shared/`의 에셋을 GUID로 참조한다.
- 공유 폴더의 `.meta`는 지우거나 다시 만들지 않는다 (GUID가 바뀌면 모든 작업 공간의 참조가 깨진다). 파일을 옮길 때는 Unity 에디터 안에서 옮긴다.
- 공유 폴더는 아트만 추가·수정한다. 현재 들어 있는 것: `Fonts/Jua-Regular.ttf`(+`Jua-OFL.txt`, UI 폰트), `Audio/*.wav` 11개 (`unity/Tools/gen-sfx.mjs`가 만든 임시 효과음).
- 쓰는 에셋은 모두 `design/art/CREDITS.md`에 기록한다. CC-BY·OFL은 표기 의무가 있다. CC BY-NC 등 비상업 라이선스는 쓰지 않는다.
- `git status`에 `.png .jpg .wav .ogg .mp3 .ttf .otf .fbx .glb .unitypackage` 등이 보이면 커밋하지 말고 공유 폴더로 옮긴다.

## 작업 순서

1. GPT 이미지 생성 환경 동작 확인.
2. 무료 에셋 후보 shortlist 작성 (`design/art/asset-store-shortlist.md`).
3. GPT 시안 제작 및 아키텍트 매핑 구조에 맞춘 2D/3D 타일 프리팹 생성.
4. 에셋은 공유 폴더에, 프리팹·머티리얼은 `_Project`에 반영하고 머티리얼을 URP로 교체. 크레딧 기록.

## 소유 파일

`unity/Assets/Shared/**` (git 밖 공유 폴더), `unity/Assets/_Project/Art/**`, `unity/Assets/_Project/Prefabs/Tiles/**`, `unity/Assets/Settings/*RPAsset*`, `design/art/**`

## 작업 목록

- [x] GPT 이미지 생성 환경 확인, 무료 에셋 후보(`design/art/asset-store-shortlist.md`)
- [x] 규격에 맞춘 타일 프리팹·UI 에셋, RuinsHenge TileSet, 7행성 프리셋, 메뉴 배경, ko/en 글꼴
- [ ] 성능(코디네이터 경유): `_UNLIT` 키워드 정리, 장식 그림자 끄기, HDR 끄기 — WebGL 빌드 크기·프레임 확인
- [ ] 플레이테스트 1차 뒤: 상태 읽기(선돌·레버·숫자 문·금 간 바닥·진흙) 피드백 반영
