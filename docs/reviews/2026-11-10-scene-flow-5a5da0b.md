# 검토: feat/scene-flow 1978e5f (LocalizationCheck) · 5a5da0b (Title/Stage Select 문자열 테이블)

검토자: 아키텍트, 2026-11-10. 기준: `origin/7lucky` aba812c ... `origin/feat/scene-flow` a392d33(7lucky 합침 포함). 코드 읽기 검토(배치 실행은 커밋 본문의 결과를 따름).

**결론: 수정 1건(F1) 후 승인.** F2는 같은 커밋에 넣기를 권한다. F3·F4는 선택.

## 좋은 점
- 13.6 요구 항목을 모두 덮는다: ko 필수·언어 중복·키 대칭(빠진 것·남는 것), 코드의 `.Get`/`.Format` 리터럴 키(주석 제거 후), 언어별 제목+본문 글꼴 cmap, 숫자 역할 문자 → `number` 글꼴, 한글 리터럴 WARN(→ ERROR 스위치).
- `UiText.Set(text, loc, key, role)` 한 곳에서 문구와 역할 글꼴을 같이 바꾼다. `Text` 하나에 글꼴을 섞지 않는다는 13.4 규칙과 맞는다.
- 언어 변경: `Localizer.Changed` 구독/해제(`OnDestroy`) 짝이 맞고, 씬 다시 읽기 없이 다시 그린다(13.3).
- `StageSelectView.Show`가 `Apply` 전 호출을 예외로 막는다(9장 단정적 규칙).
- 스크롤: `RectMask2D` + `ContentSizeFitter` + `Clamped`, 마지막으로 한 줄로 이동. 매 프레임 할당 없음.
- 의존 방향: Editor → Game/Contracts/Core, Game.UI → Contracts. 문제 없음.

## F1 (수정 필요) en에서 스테이지 이름이 WebGL에서 비어 보인다
`StageSelectView.Apply`가 그리드 텍스트 글꼴을 현재 언어의 본문 글꼴(en = Patrick Hand)로 바꾸는데, 이름은 여전히 `levels[k].Name`(ko)이다. Patrick Hand에는 한글이 없다.
- Windows 플레이어는 레거시 동적 글꼴이 OS 글꼴로 대체해서 그려지므로 `-lang en` 스크린샷에서는 보인다. **WebGL에는 OS 글꼴이 없어 이름이 빈칸**이 된다(13.4 근거와 같음). 이 커밋 전에는 기본 글꼴(Jua)이라 보였으므로 en 회귀다.
- 첫 실행 언어는 시스템 언어라(13.3) 영어 OS 플레이테스터는 기본으로 en이다.
- `levels.json`에는 이미 50스테이지 모두 en `text`가 있고(fa52dee), `LevelDto.TextFor(code)`도 있다(6398ca5). 커밋 본문의 "LevelDto.text 전까지"는 이제 해당하지 않는다.

고칠 것: 스테이지 선택의 이름을 현재 언어로. 13.5대로 `LevelCatalog`의 언어별 조회(씬 플로우·게임플레이/UI 담당)를 쓴다. 예: `LevelCatalog`가 로드할 때 `LevelDto[]`의 텍스트를 보관하고 `NameOf(int stage, Language lang)` → `dto.TextFor(Languages.Code(lang)).name`. Core `Level`은 바꾸지 않는다(`Level.Name`은 저장 키, 11.3.1). 번역이 없으면 `KeyNotFoundException`이 정상(빌드 전 검사가 잡음).

## F2 (권장, F1과 같은 커밋) LocalizationCheck가 en 레벨 텍스트를 검사하지 않는다
`TextsOf`가 `lang == Base`일 때만 levels.json을 넣는다. en 레벨 텍스트가 들어왔으므로 모든 언어에 대해 `dto.TextFor(code)`의 name/hint/story를 그 언어 글꼴로 검사한다(번역 누락은 export 검사가 잡으므로 `text`에 그 언어가 없으면 건너뛰어도 됨 — 챕터 처리와 같은 방식). 클래스 주석과 커밋 본문의 "until LevelDto.text" 문구도 지운다.
참고: F2만으로는 F1을 못 잡는다(F1은 ko 문자열을 en 글꼴로 그리는 문제). F1은 코드 수정으로 막는다.

## F3 (선택) `ScrollTo`의 4
`(levels.Length + 3) / 4`, `stage / 4`가 `GridLayoutGroup.constraintCount = 4`와 따로 적혀 있다. 상수 하나(`const int Columns = 4`)로 묶는다.

## F4 (선택) U+00B7 예외를 글꼴 이름으로 판단
`font.name.StartsWith("Jua")`는 글꼴 파일 이름이 바뀌면 조용히 꺼진다(그때는 오류가 더 나는 쪽이라 안전하다). 지금은 숫자 글꼴이 Jua 하나뿐이니 그대로 두어도 된다. 바꾼다면 `UiTheme.number`와 같은 글꼴인지로 판단.

## 확인한 것
- ko/en 키 11개 대칭, en subtitle은 영어 문장, best_bodies 구분자는 쉼표(Gaegu에 U+00B7 없음) — 커밋 본문과 일치.
- `TitleScreen.Awake`의 `Services.Get<Localizer>()`는 부트스트랩(8481e5c, 승인)이 등록한다. 직접 Play도 `Bootstrap.Run`을 먼저 부르므로 null 경로 없음.
- `SceneFlowValidator`는 build-win/build-webgl 전에 돌므로 `LocalizationCheck` 오류는 빌드를 막는다.
