# 검토: feat/view db530fa (HUD 문구 자동 축소, 설정 화면 텔레메트리 상태 줄, 글꼴 크레딧 L1)

검토자: 아키텍트, 2026-11-10. 코드 읽기 검토.

**결론: 승인.** L1은 절반 해결(크레딧 줄), 라이선스 원문 연결은 씬 플로우 몫으로 남음.

- **HUD 자동 축소**: 이야기·힌트·메시지 줄에 레거시 `Text` best fit(최소·최대 크기), 메시지 줄은 줄바꿈 + 넘치면 자름. `HudLayoutBuilder`(1회용 프리팹 편집) 경로로 프리팹에 저장 — 런타임 코드 변경 없음. best fit은 글자가 바뀔 때만 다시 계산하므로 비용 문제없음.
- **텔레메트리 상태 줄**(진행자용): `HttpTelemetry.SentEvents`(2xx 받은 이벤트 수)·`PendingEvents`(큐)·`LastStatus`(-1 없음, 0 연결 오류) 읽기 전용 추가, 전송 동작은 그대로. 설정 패널이 열려 있는 동안 1초에 한 번 문자열을 만든다(매 프레임 할당 없음). `NullTelemetry`·꺼짐이면 "보내지 않음". 플레이어 id는 앞 8자만 — 익명 UUID라 화면 표시 문제없음. 테스트 2개(ko/en, 꺼짐/켜짐).
- 문구 3키 ko/en 대칭, 내용은 숫자·영숫자·한글/영문 단어라 각 언어 본문 글꼴로 그려진다(LocalizationCheck 대상).
- **L1(93a3a89 검토)**: `UI/Credits.txt`와 `design/art/CREDITS.md`에 Gaegu·Patrick Hand·Kaushan Script 줄 추가 — 해결. **남은 것**: `Title.unity`의 `TitleScreen.licenses`는 여전히 `Jua-OFL.txt` 하나(feat/view, feat/scene-flow 모두). `Shared/Fonts/Gaegu-OFL.txt`·`PatrickHand-OFL.txt`·`KaushanScript-OFL.txt` 연결은 씬 플로우(Title.unity 소유).
