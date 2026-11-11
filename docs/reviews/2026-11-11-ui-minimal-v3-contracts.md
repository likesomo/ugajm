# 계약 결정 + 참고 구현 검토: 최소 UI v3 (선택 UI·1회 안내·접근성·UiLayoutCheck 등록)

검토자: 아키텍트, 2026-11-11. 기준: 7lucky a300022 `production/ui-minimal-plan.md` "승인된 결정", origin/feat/art `design/art/ui-minimal.md`, `concepts/ui/minimal-2026-11-10/v3/README.md`, 참고 구현 feat/art 2cd08fb `Art/Test/UiV3`(리뷰 전용).

**결론: 계약을 정했다.** 내용은 docs/architecture.md 4.4, 11.3.2, 14.11에 있고, 15.6 문구도 정정했다. 이번 커밋의 Contracts 추가(하위 호환, 기존 API 변경 없음):
- `ChoicePreview`
- `TutorialData`·`TutorialIds`
- `SaveSystem.TutorialKey`·`Tutorial`·`TutorialSeen`·`MarkTutorialSeen`·`ChoiceConfirms`·`ExplainChoice`·`CountChoiceConfirm`
- `SettingsData.showStepNumbers`·`reduceMotion` + `SetShowStepNumbers`·`SetReduceMotion`

테스트 `MinimalUiContractTests` 7개를 더했고, `unity.sh test`가 **821/821 통과**했다.

## 리드 질문에 대한 답
- **미리보기를 `Rules.Dispatch(snapshot, Keep/Switch)` 순수 호출로 계산해도 되는가 — 된다.**
  - `Rules.Choose`는 `Clone` 기반이라 입력 State를 바꾸지 않는다. `Dispatch`도 새 State에 `Prev`만 단다.
  - `ChoicePreview.TryOf`가 이 두 호출을 감싼다. 테스트가 세 가지를 고정한다: Choose 상태의 `Rules.Key`가 그대로이고, 미리보기 결과가 세션이 실제로 적용한 결과와 `Rules.Key`로 같고, KEEP의 `Prev`가 Choose 상태다.
  - 미리보기 State는 세션·`StarTrailMap`·텔레메트리·저장에 넣지 않는다.
- **확정 연출 0.5초 이하와 `IBoardPresenter` 입력 잠금의 관계**
  - 확정 연출은 그 Keep/Switch 명령의 `Present` 안에서 재생한다(`ChoicePreview.ConfirmSecondsMax`, 움직임 줄이기면 0).
  - 잠금 규칙은 4.1 그대로다: 그 사이 입력은 큐에 들어가고, Undo·Restart는 연출을 끊는다.
  - 새 잠금이나 지연 장치는 만들지 않는다.
- **Z = 선택 전 복원**: 기존 Undo 그대로 된다. 선택 중 Z는 6걸음 상태로 돌아가고, 확정 뒤 Z는 Choose 상태로 돌아가 카드가 다시 뜬다(테스트 고정).
- **튜토리얼 seen·확정 횟수 저장**: scene-flow 제안을 그대로 채택했다.
  - 키 `seven-steps.tutorial`, ID 형식 `이름@버전`, 프로필 저장.
  - `-newplayer`가 함께 지운다.
  - 리플레이 해시·QA 줄·텔레메트리에는 섞지 않는다. 리플레이·smoke 실행에서는 띄우지 않고 세지 않는다.
- **접근성 키**: `SettingsData`(설정 키)에 둔다. 그래서 `-newplayer`가 지우지 않는다. 진행자가 테스터를 바꿀 때 꺼져 있는지 확인한다.

## 참고 구현 2cd08fb에서 정식 구현(MVC)으로 옮길 때 주의 (gameplay-ui)
- **U1 (4.1 위반, 옮기지 말 것)** `ReviewChoiceController`는 입력 처리에서 `Session.IsLocked`를 보고 입력을 버린다(135, 170, 332행).
  - 정식 규칙은 "입력은 그냥 `Send`하고, 잠금은 세션만 판단한다"이다. 그래야 연출 중 누른 걸음이 큐에 들어가 사라지지 않는다.
  - 선택 중 이동을 막는 것도 규칙(Feedback)이 한다. 방향 버튼 비활성은 표시로만 둔다.
- **U2** 확정 횟수를 `Updated`의 Present Keep/Switch로 세는 방식(60행)은 계약과 같다. 정식에서는 세션 구독 컨트롤러 한 곳이 `save.CountChoiceConfirm()`을 부른다(세션 메모리가 아님).
- **U3** 조작키 줄 문구(321행 20px)와 시안 제목 20px는 14.2(본문 22) 위반이다. 상시 줄은 없어지고 1회 안내 화면만 남는다. 그 화면도 22 이상(보조로 표시하면 18)이어야 한다.
- **U4** 보드 암부 30%를 UI `Image`로 덮으면 14.5 장식 겹침에 걸린다. 3D 쪽을 권하고, UI로 한다면 14.11의 exempt와 reason을 단다.
- **U5** 카드 배치를 "좌우 넓은 쪽"에서 "좌·우·위·아래 중 형제에 가까운 비겹침 후보"로 일반화한다(14.11). 그 후보 자리를 보드 틀 (276, 80, 728, 496)의 양옆 252 열이 보장한다.

## 남은 확인 (구현 후 리뷰)
- 14.11 등록 화면 6종 × ko·en에서 UiLayoutCheck 오류 0. 가장 넓은 판(17×3)의 선택 화면은 카드가 위·아래 후보로 가는지 본다.
- scene-flow: `TutorialIds` 사용처, 리플레이·smoke 실행에서 안내가 꺼지는지, 메뉴 모달 14.7.
- gameplay-ui: 확정 `Present` 길이 ≤ 0.5초, 움직임 줄이기 0초에서도 `done`을 정확히 한 번 부르는지(GameSessionTests 방식의 테스트 권장).
