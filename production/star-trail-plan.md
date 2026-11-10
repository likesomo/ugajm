# 별 발자국 (북두칠성 흔적) 구현 배분 — 2026-11-10

작성: 리드. 사용자 지시: "북두칠성 컨셉이 흐려졌다. 형제들이 밟은 곳에 별의 흔적이 남도록 이펙트와 블록 변화를 추가하라."
상태: **계획만.** Codex 시안과 `design/art/star-trail.md`가 나오고 사용자가 승인한 뒤 시작한다. 규칙 코드(`SevenSteps.Core`, `prototypes/seven-steps/src/core.js`)는 바꾸지 않는다.

## 시안 항목 → 담당
| 시안 항목 | 데이터 출처 (이미 있음) | 담당 |
|---|---|---|
| 별 발자국: 밟은 칸마다 작은 별 | 현재 몸 `State.Trail`(칸, 걸음 번호) | gameplay-ui (View) |
| 7번째 걸음 칸 각성 | `Footprint.Step == 7`, `StepResult.Events`의 `Step`(StepIdx 7) | gameplay-ui (View), 발광은 아트 |
| 형제 경로 별자리: 앞선 몸의 경로가 선으로 남음 | `State.Prev` 사슬을 거슬러 각 몸의 마지막 `Trail`(Respawn에서 Trail이 새로 시작) | gameplay-ui (View) |
| KEEP 선돌의 별 | `State.Statues`, `GameEventType.Stone` | gameplay-ui (View), 선돌 발광은 아트 |
| 피날레 별자리 (클리어 시 전체 경로가 북두칠성처럼 이어짐) | `Status.Win` + `GameSession.Presented` 뒤 | gameplay-ui (View), HUD Dipper와 연결은 gameplay-ui |
| 셰이더 발광 마스크, 별 스프라이트/메시, 블록 변화 머티리얼 | — | 아트 (Codex, `Art/**`, `Shared/**`) |
| 이벤트 계약 확인·리뷰 | — | architect |

## 원칙 (architect 확인 대상)
1. **흔적은 State에서 계산한다.** View는 `GameSession.Updated`를 구독하되, 그릴 내용은 이벤트를 누적하지 않고 매번 `State`(현재 `Trail` + `Prev` 사슬)에서 다시 만든다. 그래서 Undo/Restart(`SessionUpdateKind.Snap`)와 리플레이가 자동으로 맞는다. 애니메이션(별이 켜지는 연출)만 `Present`의 `StepResult`를 쓴다.
2. **새 계약이 필요하면 architect가 판단한다.** 지금 보기로는 `GameSession.Updated`/`Presented`, `State.Trail`/`Prev`/`Statues`, `StepResult.Events`로 충분하다. 앞선 몸 경로를 `Prev` 사슬로 매 갱신마다 거스르는 비용이 문제면(스테이지당 최대 3몸×7걸음, 작음) View 안에서 캐시하고, Core에 필드를 더하지 않는다.
   - architect 의견(2026-11-10): 계약 변경 필요 없음. 단 `Prev` 사슬은 명령 경계의 State만 남긴다. 7걸음을 다 쓴 몸은 Choose 상태 `Trail`에 7칸이 다 있지만, **낙하한 몸의 마지막 칸(구덩이)은 어느 State의 `Trail`에도 없고** 그 이동의 `StepResult`(Fall 이벤트, `Path`)에만 있다. State에는 명령이 저장되지 않으므로, 낙하 경로를 구덩이까지 그리려면 View가 `prev`에서 4방향 `Rules.Dispatch`를 시도해 `Rules.Key`가 다음 State와 같은 것의 `Path`를 쓴다(architect 권장 (b): 순수 함수, 4회라 저렴, Snap·리플레이에도 맞아 원칙 1 유지). Present의 `StepResult.Path`를 결과 State 참조로 캐시하는 (a)는 Snap/리플레이로 들어온 상태에 캐시가 없어 쓰지 않는다. 어느 쪽도 계약 변경 없음. 낙하 경로를 그릴지는 시안 승인 때 정한다.
3. **TileVisual/TileSet 확장**(별 머티리얼, 각성 발광 등)이 필요하면 `labelScale`처럼 0/null이면 기존 동작인 선택 필드로 더하고, architect 리뷰 + `tileset-validate` 검사를 붙인다.
4. **가독성 우선**: 지금 칸 위 숫자 발자국(BoardView `footLabels`)과 겹쳐 읽기 어려워지면 안 된다. 별과 숫자 중 무엇을 남길지는 시안 승인 때 정한다. 보드 대비(moss/altar 3:1)와 UiLayoutCheck(보드 영역)는 그대로 통과해야 한다.
5. **성능**: 표준 Chrome 1/29/50에서 60 FPS, 드로우콜 증가는 시안 기술 메모의 예산 안. 발광은 HDR/블룸 없이(WebGL HDR 끔, A1) 머티리얼 발광 + 가산 스프라이트로.

## 순서
1. Codex 시안 + `design/art/star-trail.md` → 사용자 승인 (코디네이터).
2. architect: 기술 메모를 보고 계약 변경 필요 여부 확정(위 2·3).
3. 아트(Codex): 별 스프라이트/메시, 발광 마스크, 블록 변화 머티리얼 → `feat/art`.
4. gameplay-ui: View 구현(위 표), EditMode 테스트 — Undo/Restart/리플레이 뒤 흔적이 State에서 다시 만든 것과 같은지, 3몸 경로·7번째 칸·KEEP 선돌·Win 피날레.
5. architect 리뷰 → 리드 병합: test, scene-validate, tileset-validate, build-win, smoke, build-webgl, smoke-webgl, 표준 Chrome FPS(1/29/50).
6. 사람 플레이테스트 1차 일정과 겹치면 최종 빌드에 넣을지는 사용자에게 묻는다(기본: 승인·검증이 끝난 것만 넣음).
