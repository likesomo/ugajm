# 별의 흔적 · 시안과 WebGL 기술 메모

2026-11-10. **시안만 제작했으며 런타임·규칙·셰이더를 변경하지 않았다. 구현은 승인 후 한다.** 공유 `concepts/world-2026-11-10/star-trail/`의 effect-sheet.png, block-states.png, finale-constellation.png, readability.png와 상위 index.html을 본다.

## 표현과 가독성

1–6걸음은 판석 모서리의 작은 별 점. 타일 폭 대비 지름 0.035→0.065, 코어 opacity 0.22→0.42로 조금씩 증가한다. 테두리는 청록 #568C88, 황토 #B99A57, 테라코타 #B96C59이고 색 이외에 림 끊김 1·2·3개로도 구분한다. 7번째에는 얕은 음각의 원작 북두칠성 문양이 켜지고 0.18초 가는 광선으로 하늘 HUD의 일곱째 별에 연결된다. 정확한 문양은 국자 1–2–3–4–1, 자루 4–5–6–7이다.

실제 형제별 1–7걸음 경로는 가는 선으로 잇고 셋이 모인 경로를 결과 피날레에서 해당 행성의 별자리로 보여 준다. 실제 해법의 경로를 북두칠성 모양으로 억지 변형하지 않는다. KEEP 선돌 꼭대기의 작은 고정 별은 흔적이 도구임을 말한다.

초기 밝기 상한 제안: 톤매핑 전 emission 기본 0.8, 각성 1.2를 0.18초만 허용. 최종 화면에서 흔적 휘도는 흰색 HUD 최대의 65% 이하, 점유 면적 타일 8% 이하. 타일 중앙 40% 숫자 영역, 기믹 실루엣, 남은 걸음 HUD는 마스크로 보호한다. Bloom은 필수가 아니다. 수치는 엔진·톤매퍼에 맞춰 실기기에서 확정할 초기값이다. 겹친 림은 분할하며 밝기를 더해 포화시키지 않는다.

실패는 0.20초 작은 점이 어두워지며 풀림, Restart는 0.25초 전체 잔광이 사라져 빈 보드로 스냅, Undo는 0.12초 마지막 커밋된 별·선분만 걷히고 앞선 흔적은 유지. 상태 스냅이나 입력을 연출 때문에 지연하지 않는다. readability.png 좌상 숫자 보호 / 우상 각성 / 좌하 초기화 / 우하 마지막 흔적 되감기 순서다.

## 실제 GameSession 계약에 붙이는 View 설계

확인한 소스는 `unity/Assets/_Project/Scripts/Runtime/Contracts/GameSession.cs`와 `Core/State.cs`. 별도 가상 Step/Seventh 이벤트 API를 가정하지 않고 `Updated(SessionUpdate)`, `Presented(State)`를 구독한다.

| 조건 | 읽는 데이터 | View 동작 |
|---|---|---|
| Present + Step | Result.Events의 Step.At/StepIdx, Prev/State | 점·선분 커밋 |
| 일곱째 | StepIdx==7, State.Used, Seal/Choose/Win 실제 결과 | 각성·기믹 연출 동기화 |
| KEEP | CommandType.Keep + Stone 이벤트/Statues diff | 선돌 위 별 고정 |
| SWITCH | Switch 이벤트·Bodies·Trail | 다음 색과 경로로 전환 |
| Undo | Snap + CommandType.Undo | State에서 캐시 복원, 마지막 차이만 되감기 |
| Restart | Snap + CommandType.Restart | 캐시 초기화·잔광 취소 |
| Load/Replay | Rebuild / Snap | 새 상태로 즉시 재구축 |
| Feedback/Bump | 상태 불변 | 흔적 추가 안 함 |
| Win 피날레 | Presented + Win | 보드 연출 완료 후 최종 별자리 |

얼음 Result.Path는 여러 통과 칸이어도 한 걸음일 수 있다. 통과선은 보조이며 별 수는 Step 이벤트/Trail의 걸음 번호로 정한다. 진흙은 한 칸 두 걸음일 수 있으므로 칸 수로 걸음 수를 재계산하지 않고 같은 칸의 별을 작은 림 슬롯으로 분리한다. View 캐시는 State 참조·Undo history에 대응하여 보관/복원하고 Load에서 해제한다. 규칙 State는 수정하지 않는다.

GameSession.Send는 아직 실행되지 않은 큐 명령만 Undo로 취소하면 Updated를 발행하지 않는다. 이때 커밋된 흔적을 지우면 안 된다. 큐 미리보기와 실제 경로를 분리한다. Snap/Rebuild 때 취소 토큰으로 이전 VFX 콜백을 무효화한다.

## WebGL 예산 제안

기본안: 기존 타일 공유 셰이더에 발광 마스크 채널을 넣고 vertex color 또는 instance data로 단계/형제색을 전달한다. 공유 머티리얼을 복제하거나 Renderer.material을 타일마다 만들지 않는다. 배칭 구조에 따라 보드 데이터 텍스처 하나 또는 vertex channel을 사용하고 이벤트 때만 갱신한다. 별 모양은 석질 아틀라스의 남는 채널 또는 공용 R8 마스크에 둔다. 추가 실시간 라이트 0, 추가 draw 0이 목표다.

대안: 공용 데칼 텍스처 1장(256² RGBA)과 보드당 결합 quad mesh 하나. 이 경우 추가 1 draw이므로 추가 draw 0 요구에는 기본 셰이더안을 선택한다. 선도 기존 타일 패스에서 셀 경계 좌표로 그리거나 같은 결합 메시로 묶는다. 별마다 LineRenderer/데칼/라이트를 만들지 않는다. 실제 Built-in/URP 배칭 호환은 구현 전 확인한다.

파티클은 7번째·리셋 순간 공유 풀 12개 이하, 공용 머티리얼로 선택적 추가 1 draw, 최저 옵션은 0개. 보드 데이터 텍스처를 쓰면 512² RGBA32 1MiB 이내, 작은 R8/vertex channel을 우선한다. CPU는 이벤트 때만 갱신한다. 이 값은 측정 결과가 아닌 승인 전 예산 제안이며 적용 뒤 프레임 디버거와 WebGL 저사양 실기기로 검증한다.

생성 시안의 별 개수·문양·카메라 각도는 근사치다. 정확한 HUD·50도 투영·밝기는 이 문서와 코드 도식 기준으로 제작한다.
