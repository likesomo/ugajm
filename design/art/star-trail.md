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


## 승인 후 수정 · 별 흔적 v2 (2026-11-10)

이 절이 이전 시안의 경로·상태 표현을 대체한다. 승인된 시안의 이전 파일은 공유 `before-fix/`에 그대로 보존했고, 기존 네 장도 직교 경로로 수정했다. 추가 5장: `star-trail/v2/states.png`, `transitions.png`, `planets-variants.png`, `hole-bridge.png`, `undo-restart.png`. 정확한 셀 경로는 `routes.json`과 `route-proof.png`를 기준으로 한다. 생성 렌더의 광학적 근사와 셀 증명용 평면도를 분리한다.

### 직교 경로와 색

7걸음은 출발 0 + 도착 1–7의 **8개 위치**다. 연속 두 좌표의 Manhattan 거리는 반드시 1이다. 셀 중심에서 셀 중심으로 이동 축에 평행한 선을 잇고 방향 전환은 중심에서 한다. 왕복은 합법이며 같은 칸 재방문 시 걸음 슬롯만 별도로 남긴다. 장식 북두칠성 음각/HUD는 대각선을 포함해도 되지만 이동 경로에는 적용하지 않는다.

시안의 경로 A: (0,0)→(1,0)→(2,0)→(2,1)→(3,1)→(3,2)→(4,2)→(4,3). KEEP 후 B: (0,0)→(0,1)→(1,1)→(1,2)→(2,2)→(2,3)→(3,3)→(3,4). SWITCH 후 C: (3,4)→(2,4)→(1,4)→(1,5)→(2,5)→(3,5)→(4,5)→(5,5). A 선돌 위치를 B/C가 밟지 않는다. 총 21이동의 Manhattan 거리 1을 검증했다. 이는 연출 셀 배치 예시이며 기존 레벨의 해법 검증을 주장하지 않는다.

기준 스카프색 #568C88 / #B99A57 / #B96C59는 형제 정체성의 출처다. 이번 승인 수정의 화면색은 첫째 청록을 유지하고 둘째 밝은 노란 겨자 #D3B84E(그늘 #B99A57), 셋째 어두운 붉은 벽돌 #A54B45(그늘 #823B36)로 벌린다. 모든 키아트·스토리보드·설정 시트에서 둘째의 주황 기를 빼고 셋째를 붉고 어둡게 보정했다. VFX 림도 청록/노랑/빨강을 쓰며 밝은 코어만 공통이다. 행성 색조는 바탕과 미세 입자에만 적용하고 식별 림을 바꾸지 않는다.

### View 상태 머신

상태는 형제 경로별이며 단일 전역 상태로 덮어쓰지 않는다. 한 화면에 Fixed/Passed/Active가 동시에 존재할 수 있다.

| 상태 | 진입 이벤트/조건 | 점·선·끝점 | 다음 전이 |
|---|---|---|---|
| Empty | Rebuild/Restart | 흔적 없음 | 적용된 Step → Active |
| Active | Step | 얇은 점·직각 선, 0.8Hz ±8% 은은한 맥동 | StepIdx7 → Awakened |
| Awakened | 일곱째 Step의 실제 판정 | 음각 문양 0.18초 각성, HUD 광선 | KEEP/Stone, SWITCH, Bridge, Win별 분기 |
| Fixed (KEEP) | Keep 명령 + Stone 결과 | 낮지만 또렷한 고정 발광, 작은 별 결정, 끝 선돌 꼭대기 별 | 다음 형제 Active를 원점에서 시작 |
| Passed (SWITCH) | Switch 결과 | Fixed보다 옅은 남은 경로, 선돌 없음, 끝에 두 색 짧은 매듭 | 다음 형제 Active를 실제 새 Start에서 시작 |
| Sunken (Bridge) | Bridge 결과, 해당 hole | 다리 칸 바닥 아래 가라앉은 별, 구멍 아래 우주 | 다음 형제 경로와 공존 |
| Falling | Fall만 있고 Bridge 없음 | 일시 낙하 잔광, 고정 다리 별을 만들지 않음 | 실제 Respawn/상태로 복원 |
| Finale | Win/Clear 의미의 결과 완료 | 세 색 경로·이음매·선돌 별이 HUD로 빛 전송 | 결과 화면, Restart/Load 시 해제 |
| Restore | Undo Snap | 마지막 커밋 흔적과 연관 매듭/선돌 VFX만 회수, 앞 상태 유지 | 복원된 history의 상태 |
| Dissolve | Restart/실패 표현 | 전체가 0.25초 별가루로 흩어짐 | 새 State를 즉시 표시 |

겹친 셀에는 두 색 반반 테두리 또는 동심 이중 링을 그린다. 세 색이면 얇은 삼분할 림을 쓰고 emission을 합산하지 않는다. 새 Active와 고정 경로는 맥동/결정 모양도 달라 색만으로 구분하지 않는다. SWITCH 매듭은 두 형제의 실제 같은 endpoint에서만 생성한다.

이 표의 Step/7번째/KEEP/SWITCH/Fall/Undo/Restart/Clear는 **의미 이름**이다. 실제 API는 기존 `GameSession.Updated`의 Result.Events(Step, Stone, Switch, Fall, Bridge, Win 등)와 Command/Prev/State, `Presented`에 매핑한다. 별도 Clear 이벤트나 신규 규칙을 추가하지 않고 Win을 결과 완료로 읽는다. KEEP도 명령 수신만으로 그리지 않고 실제 Stone 판정을 확인한다. Fall은 Bridge와 반드시 구분한다. 리드가 gameplay-ui에 구현을 배분하며 이 브랜치는 시안·문서만 수정했다.

### 행성별 파라미터와 공용 텍스처

| 행성 | 바탕 색조 / 발광 모양 | 공용 패턴 아틀라스 마스크 | 파티클 형태 |
|---|---|---|---|
| moss | 회색 화강암·녹색 이끼 / 틈의 반딧불 같은 점 | row0 R별 G선 B음각 A이끼틈 | 작고 느린 점, 선택 사항 |
| hollow | 흑회 현무암·부석 / 기공 속 별·다리 아래 별 | row1 R별 G선 B기공 A깊이 | 낮게 가라앉는 점 |
| carved | 붉은 사암 / 층리 홈의 흐름·7번째 음각 | row2 R별 G선 B새김 A층리 | 납작한 미세 모래별 |
| frost | 흰 대리석·청색 석영 / 굴절 점·미끄럼 긴 잔광 | row3 R별 G선 B결정 A굴절 | 짧은 결정 조각 |
| joinery | 점판암·목재·청동 / 상감선·기어 별 | row4 R별 G선 B기어 A금속 | 절제된 금속 반짝점 |
| crumble | 크림 석회암·트래버틴 / 균열 빛·조각 잔광 | row5 R별 G선 B균열 A파편 | 깨진 작은 빛 조각 |
| altar | 모래 사르센 / 일출 금 코어·선돌 원에서 하늘로 | row6 R별 G선 B원 A모래결 | 천천히 오르는 소수 점 |

`planets-variants.png`는 위 7행 × 단일/KEEP 후/SWITCH 후 3열이다. 같은 상태 실루엣·형제 림·가독성 상한을 모든 행성에서 유지한다. 얼음 긴 잔광도 실제 Path의 수평/수직 통과선에만 생기고 별 개수는 StepIdx 기준이다. 균열·층리의 미세 빛은 셀 내부 재질 장식이며 옆 칸으로 대각선 이동하는 연결로 읽히면 안 된다.

### 추가 draw 0인 v2 데이터 제안

타일당 머티리얼을 만들지 않는다. 기존 보드 타일 패스에 공용 7행 패턴 아틀라스(예: 512×512 RGBA32 약 1MiB)와 보드 상태 텍스처 한 장을 바인딩한다. 타일의 material/planet row를 vertex/instance data로 주어 동일 셰이더·아틀라스를 공유한다. R/G/B/A 패턴 용도는 위 표이며 실제 표면 결 텍스처는 기존 재질을 유지한다.

상태 텍스처는 논리 보드 W×H에 형제 3슬롯을 나란히 배치한 3W×H RGBA32, point/clamp/no mip, linear data로 쓴다. 각 형제 texel의 R=4방향 edge bitmask, G=step index+phase packed byte, B=stone/knot/bridge flags, A=intensity. 최대 세 형제의 점과 선을 기존 타일 fragment 패스의 고정 세 샘플에서 합성하고 겹침 림은 max/분할로 그린다. 7×7 보드는 원시 데이터 588 bytes(드라이버 정렬/패딩 제외)이며 크기는 스테이지 셀 수에 비례한다. byte packing은 WebGL 지원 포맷에 맞춰 정규화 8bit로 해독하고 정수 bit 연산 없이도 가능한 수식으로 작성한다.

보드 상태가 바뀌는 이벤트 때만 dirty cell을 갱신하고 맥동은 공용 시간 uniform으로 한다. 새 Renderer·개별 데칼·LineRenderer·실시간 라이트 0개, **필수 VFX 추가 draw 0**. 선돌 별은 기존 선돌 셰이더/메시에 같은 마스크로 포함한다. 매듭은 타일 내부 패턴이다. Restart 별가루도 마스크의 시간 임계치/dither로 흩어 보이게 하며 별도 ParticleSystem은 기본안에서 쓰지 않는다. 실제 기하 파티클 선택안은 추가 draw가 생길 수 있으므로 0 draw 요구에서는 비활성이다. 숫자·기믹·남은 걸음 보호와 기본 0.8/순간1.2 emission 상한은 위 가독성 규칙을 그대로 유지한다. 예산은 구현 전 제안이며 프레임 디버거에서 배칭 유지·overdraw·샘플 비용을 검증해야 한다.
