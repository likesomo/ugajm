# 검토: feat/scene-flow cf7f0a7 (S7a 메뉴 시안: 타이틀·행성 지도·행성 스테이지 목록·첫 실행 언어 선택, 글꼴 라이선스)

검토자: 아키텍트, 2026-11-10. 기준: 7lucky eaacf8a. 코드 읽기 + 임시 병합 시도. 씬 플로우 보고: test 749, scene-validate 0, build-win+smoke 7/7, smoke-webgl 콘솔 0, WebGL 키보드 탐색.

**결론: 승인 — 단, 최신 7lucky(eaacf8a)를 받아 충돌을 정리한 뒤 병합(M1).** N1은 작은 수정 권장.

## M1 (병합 전 필수) 7lucky eaacf8a와 충돌 3곳
e487ebc 이후 들어온 feat/view 93a3a89·db530fa(설정·크레딧 문구, 텔레메트리 상태 줄) 때문이다.
- `Strings/ko.json`·`en.json`: 키 합집합(settings.*, credits.* + 이 브랜치의 title/planet/language 키).
- `TitleScreen.cs`: 7lucky에 생긴 `creditsPanel.Apply(localizer)`(93a3a89)를 이 브랜치의 `ApplyText()`에 넣는다 — 언어 전환 때 크레딧 패널도 다시 그려야 한다. 이 줄이 빠지면 크레딧 제목·닫기가 프리팹 빈 문구로 남는다.
- 정리 후 test·scene-validate 다시(LocalizationCheck가 합친 키의 글꼴을 본다).

## 계약 대조
- **13.3 첫 실행 언어 선택**: `!save.LanguageChosen && launch.Language == null && flow.State == Title`일 때만 연다. "계속" = `save.SetLanguage(localizer.Current)`(바꾸지 않았어도) — 아키텍트 13.3 규칙 그대로. 언어 버튼은 각자 그 언어의 본문 글꼴(13.4), "English / 한국어" 줄은 숫자 글꼴 Jua(라틴·한글 모두 있음).
- **UiTheme 메뉴 스킨**: 모두 선택 필드, 비면 단색(기존 모양). `Validate`가 버튼 4상태 전부/없음, 별 둘 다/없음, dipper 7점/없음, 배경은 UI 프리팹(RectTransform). `FontFor`와 분리되어 있고 글꼴 규칙(13.4)을 건드리지 않는다. `RuinsHengeTheme.asset`은 참조만(아트 에셋 그대로, 리드 승인).
- **LevelCatalog**: `Chapters`(chapters.json 순서), `StagesOf(key)`(levels.json 순서, `ChapterKeyOf` 기준) — `Load` 전 호출은 예외. 화면 열 때만 호출되어 LINQ 할당 문제없음.
- **TitleFlowController**: `BeginJourney`(1스테이지), `ContinueJourney`/`CanContinue`(`Progress.lastStage` 이름, 11.3.1 — 없어진 스테이지면 1스테이지). 새 전이는 없다(기존 `GoToGame`).
- **L1 마무리**: `SceneFlowValidator.RequireFontLicenses` — UiTheme이 쓰는 글꼴(`font`, `number`, 언어별 title/body)마다 `TitleScreen.licenses`에 `<Family>-OFL`이 있어야 오류 없음. 이 브랜치의 `Title.unity`가 OFL 세 개를 연결해 db530fa 검토의 남은 항목이 해결된다. 이름 규칙(`Gaegu-Regular` → `Gaegu-OFL`)은 공유 폴더 파일명과 일치.
- 오버레이가 모두 닫히면 아래 화면에 키보드 포커스를 돌려준다(uGUI 탐색, WebGL 키보드 확인).

## N1 (권장) 언어 미리보기 뒤 Esc
언어 버튼은 `localizer.Set`으로 바로 미리 보여 주고 저장은 "계속"에서 한다. 선택 화면을 Esc로 닫으면 미리 본 언어가 이번 실행 동안 남고 저장은 안 된다(다음 실행엔 다시 선택 화면, 그 사이 텔레메트리 `lang`도 미리 본 언어). Esc로 닫을 때 `localizer.Set(launch.Language ?? save.Language)`로 되돌리면 "Esc = 저장 안 함"이 화면에도 맞는다.

## 재확인: 0d163b2 (M1·N1 반영) — 승인, 병합 가능
- M1: Strings 합집합(93키), `ApplyText()`에 `creditsPanel.Apply(localizer)` — 해결.
- N1: `OnOverlaysChanged`에서 언어 패널이 "계속" 없이 닫히면 `launch.Language ?? save.Language`로 되돌림 — 해결.
- 직접 확인(임시 브랜치 = 최신 7lucky 016893c + feat/scene-flow 0d163b2, 충돌 없음): EditMode 757/757, scene-validate OK(ERROR 0, WARN 1 = `SettingsPanel` NativeName "한국어", 의도된 예외).
