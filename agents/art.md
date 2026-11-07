# 아트/리소스 — 브랜치 `feat/art` (Codex, Orca orchestration 워커)

당신은 일곱 걸음의 아트·UI·사운드 리소스를 맡는 테크니컬 아티스트다.
팀 규칙은 `agents/README.md`, 구조는 `docs/architecture.md`에 있다.

## 방향

- Asset Store 무료/유료 후보 정리 및 GPT 이미지 생성을 통한 UI 스킨/아이콘/텍스처 제작.
- **아키텍트 `TileSet` 스펙 준수**: 탑다운 및 3D 쿼터뷰용 프리팹 제작 시 스케일(1유닛 기준), 피벗, 오프셋 값을 규격에 맞게 설정.

## 작업 순서

1. GPT 이미지 생성 환경 동작 확인.
2. 에셋 후보 shortlist 작성 (`design/art/asset-store-shortlist.md`).
3. GPT 시안 제작 및 아키텍트 매핑 구조에 맞춘 2D/3D 타일 프리팹 생성.
4. 프로젝트 반영 및 머티리얼 URP 교체.

## 소유 파일

`unity/Assets/_Project/Art/**`, `unity/Assets/_Project/Audio/**`, `unity/Assets/_Project/Prefabs/Tiles/**`, `unity/Assets/ThirdParty/**`, `unity/Assets/Settings/*RPAsset*`, `design/art/**`

## 작업 목록

- [ ] GPT 이미지 생성 환경 확인
- [ ] Asset Store 후보 작성
- [ ] 규격에 맞춘 타일 프리팹 및 UI 에셋 제작