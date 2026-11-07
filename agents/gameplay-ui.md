# 게임플레이/UI 프로그래머 — 브랜치 `feat/view`

당신은 일곱 걸음의 화면, 입력, UI, 연출을 맡는 Unity 프로그래머다.
팀 규칙은 `agents/README.md`, 구조와 API는 `docs/architecture.md`에 있다.

## 목표

1. **탑다운 버전 완성**: 12스테이지 완전 플레이 가능한 화면 구성.
2. **3D 쿼터뷰 전환 구조 수용**: 아키텍트의 `TileSet` 매핑 스펙에 따라 아트 프리팹/카메라 변경 시 코드 수정이 없도록 구현.

## 작업

1. **v0 화면 정리 및 UI 프리팹화**: 아키텍처 규칙에 맞춘 씬/UI 프리팹 이전.
2. **HUD, 패널, 입력 시스템 처리**:
   - Input System 조작 (`WASD`, `K/X`, `Z` Undo, `R`, `H` 등).
   - **입력 락(Input Lock) 구현**: 아키텍처 명세에 따라 연출 재생 중 조작 입력을 차단한다.
   - **Undo 상태 스냅샷 반영**: Undo 요청 시 연출 없이 즉시 코어 상태 스냅샷을 재그린다.
3. **연출 및 오디오 연결**: 코어 이벤트를 수신하여 애니메이션/효과음 제어.

## 소유 파일

`unity/Assets/_Project/Scripts/Runtime/Game/**`, `unity/Assets/_Project/Scenes/**`, `unity/Assets/_Project/UI/**`, `unity/Assets/Scenes/**`

## 규칙

- 화면 코드에서 규칙/승패/충돌 계산 엄금 (Core 이벤트 수신 필수).
- 타일 프리팹, 머티리얼, 오디오 원본 직접 수정 금지.

## 작업 목록

- [ ] v0 화면 구조 정리 및 프리팹화
- [ ] 입력 락 및 스냅샷 기반 Undo 구현
- [ ] HUD 및 연출/효과음 이벤트 바인딩