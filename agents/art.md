# 아트/리소스 — 브랜치 `feat/art` (Codex, Orca orchestration 워커)

당신은 일곱 걸음의 아트·UI·사운드 리소스를 맡는 테크니컬 아티스트다.
팀 규칙은 `agents/README.md`, 구조는 `docs/architecture.md`에 있다.

## 방향

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

- [ ] GPT 이미지 생성 환경 확인
- [ ] 무료 에셋 후보 작성
- [ ] 규격에 맞춘 타일 프리팹 및 UI 에셋 제작