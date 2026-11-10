# 이야기를 게임에 담는 구조 / Story Integration

작성: 스토리 디렉터, 2026-11-10 (`agents/story.md` 3단계). 상태: **제안.** 계약(`StoryCatalog`, `StoryRules`, `StoryDirector`, `story.json`, 저장 키, `ResultModel` 필드)은 **아키텍트 검토**를 거친다. 공용 계약을 바꾸는 항목은 "API·인터페이스 변경 고지" 절차를 따른다.
기준: `narrative-structure.md`(형식, 글자 수), `world-bible.md`(내용), `docs/architecture.md` 3·4·6·9·11·13·14·15장.

> **블라인드 QA**: 풀이 정보가 없다.

리드 요청(2026-11-10)이 들어간 곳은 다음과 같다.
- (a) 트리거는 `GameSession` 이벤트와 `State`로만 판정한다. 별 흔적과의 연결 지점 표: 4.2
- (b) 글자 수 한도의 최종 판정은 `UiLayoutCheck`다: 3.3
- (c) `ksx1001` 검사를 돌릴 곳: 3.4

---

## 0. 최소 UI 개정 메모 (2026-11-10, 4단계 전, `production/ui-minimal-plan.md`)

사용자 지시로 UI를 최소화한다(시안 승인 전, 구현은 승인 뒤). 이 장이 아래 1~9장보다 우선한다. 1~9장의 구조(데이터, 검증, 트리거 원칙, MVC)는 그대로 두고, **전달 지점과 배치만** 바꾼다.

### 0.1 바뀌는 것

| 항목 | 1~9장 (이전) | 최소 UI (지금) | 담당 |
|---|---|---|---|
| 스테이지 한 줄 | HUD 왼쪽 열에 늘 보임 | **스테이지 시작 때 한 줄만 잠깐**(첫 입력이나 약 3초 뒤 사라짐, 시도마다 한 번). 이야기 줄과 힌트 중 **하나만**. 새 기믹 소개 스테이지는 힌트를 세계의 말로 쓴 줄 | gameplay-ui(표시), level-design(스테이지마다 고름) |
| 순간 문구 (KEEP·SWITCH·다리 등) | HUD 오른쪽 열 `MomentLineView`, 시도당 1회 | **잼에서 뺀다.** 선돌 위 별(형제 색 테), 이음매 별, 가라앉은 별이 대신한다(별 흔적). `on` 값은 스키마에 남기되 필수 entry에서 뺀다 | — |
| 첫 7번째 걸음 문구 | `seventhFirst` | **뺀다.** 발치 일곱째 별 맥동 + 별 각성 | — |
| 피날레 결과 문구 `FinaleLine` | 결과 모달, 별 위 | **결과 화면에 두지 않는다.** 행성 마지막 스테이지 클리어 때, 보드 위 별자리 피날레 연출(약 1.2초) 동안 시작 한 줄과 **같은 잠깐 줄 자리**에 한 번 보이고, 결과 화면이 열리면 사라진다. 결과 화면은 별·별자리·다음 버튼만 | gameplay-ui |
| 별자리 완성 문구 `ConstellationLine` | 결과 모달 | **뺀다.** 별지도에서 그 행성의 별이 밝아지는 것으로 대신한다 | scene-flow(별지도) |
| 별지도 머리말 `map` | 상단 띠 | **뺀다.** 별지도의 선이 이야기를 그린다: 행성 피날레를 깬 순서대로 1→2→3→4, frost에서 4→1(국자가 닫힘), 4→5→6→7 | scene-flow |
| 행성 설명 `tagline` | 카드마다 늘 | 고른 행성 하나에만 | scene-flow |
| 행성 진입 카드 | 이름 + intro 2줄 | 이미지 + 이름 + intro **한 줄**(ko 18 / en 40) | scene-flow, level-design(문구) |
| 로딩 새김 | 팁과 번갈아 | **잼에서 뺀다**(일곱 별 진행만) | — |
| 실패 | 제목 + 본문 | 아이콘 위 한마디(ko 10 / en 24) | gameplay-ui |
| 엔딩 | 이미지 4 + 카드마다 2줄까지 | 이미지 4 + 카드마다 **한 줄**(ko 18 / en 40). 갈래(형제, 선돌 수)는 그대로 | scene-flow |
| 타이틀 한 줄 | 14자 | 10자 | scene-flow |

### 0.2 잠깐 줄 (`TransientLineView`) 하나로 모은다

- 게임 안에서 글이 나오는 곳은 이 한 군데다. 스테이지 시작 한 줄과 피날레 한 줄이 같은 View, 같은 자리, 같은 판을 쓴다.
  - 자리는 시안을 따른다. 제안은 보드 위쪽 가운데이고, `BoardFrame`과 겹치지 않게 한다.
  - 보이는 시간은 `max(2.5초, 글자 수 × 0.12초)`다.
- 사라지는 때: 첫 이동 입력, Undo/Restart(`Snap`), 스테이지 이동(`Rebuild`), 결과 화면이 열릴 때. 리플레이·스모크·flowcheck·ArtPerf에서는 나오지 않는다.
- 다시 보기: 최소 HUD 메뉴 안에 둔다(풀이 보기와 같은 메뉴). 힌트와 이야기 줄 중 그 스테이지에 고른 것만 다시 보인다.
- `UiLayoutCheck` 대상은 가장 긴 시작 줄과 가장 긴 피날레 줄이다(ko/en, 두 해상도). 최소 UI의 새 HUD 프레임 기준은 아키텍트가 14장을 갱신한 뒤 정한다.

### 0.3 데이터 영향 (제안, 소유자 결정)

- `levels.js`에 스테이지마다 `startLine: "story" | "hint" | ""`(없으면 `"story"`, 그 줄이 비면 아무것도 안 보임)을 둔다. 레벨 디자인 소유, 코어의 `LevelDto` 필드 추가가 필요하다(고지).
  - 대안: 소개 스테이지는 `story`를 비우고 `hint`만 두는 데이터 관례로 처리한다. 필드는 늘지 않는다.
- `story.json` 필수 entry(2.3)를 줄인다: `title`(2), `finale`(7), `ending`(카드 1: 형제 3, 카드 2·3: 선돌 3씩, 카드 4: 1), `lose`(1). `map`, `loading`, `keep`/`switch`/`bridge`/`seal`/`fall`/`vanish`, `seventhFirst`, `constellation`, `stillStar`는 선택 entry로 남긴다. 잼 이후 다시 켜도 스키마가 바뀌지 않는다.
- 글자 수 상한은 `narrative-structure.md` 4.1의 최소 UI 표를 따른다. `check-story.mjs`가 같은 표를 쓴다.
- `ResultModel.FinaleLine`·`ConstellationLine`은 만들지 않는다. 피날레 줄은 `StoryDirector`가 `Presented(Win)`에서 `TransientLineView`로 보낸다. 결과 지연(4.3)은 그대로 필요하다. 별자리 피날레와 피날레 줄이 결과 화면 막에 가리지 않게 하기 위해서다.

### 0.4 이야기를 대신 전하는 시각 연출 (요청 목록, 별 흔적·최소 UI 작업 안에서)

| 이야기 | 시각 | 담당 | 별도 작업인가 |
|---|---|---|---|
| 형제가 선돌로 남았다 (누가) | 선돌 위 별에 **그 형제 색의 가는 테** | 아트(셰이더), gameplay-ui(`OfStatue` 비트, 이미 15.2에 있음) | 아니오(별 흔적 안) |
| 지도를 넘겼다 | 이음매 별(15.5 비트 0) | 같음 | 아니오 |
| 몸이 다리가 되었다 | 가라앉은 별(15.5 비트 1) | 같음 | 아니오 |
| 셋이 함께 길을 냈다 | 클리어 별자리(세 색) | gameplay-ui | 아니오 |
| 행성을 다 걸었다, 별이 이어진다, 국자가 닫힌다 | 별지도의 행성 별 밝기 + 피날레를 깬 행성 사이 선(1→2→3→4→1, 4→5→6→7) | scene-flow(별지도). 저장에 있는 클리어 기록만 쓴다 | **예**(작은 새 작업. 선 7개, 피날레 클리어 여부로 켬) |
| 실패해도 아무도 죽지 않는다 | 실패 때 별 흔적이 흐려지고(0.20초), 출발점에서 형제가 다시 깨어나는 짧은 몸짓 | gameplay-ui | 아니오 |

---

## 1. 한눈에

```
design/story/story.json  (원고, 스토리 디렉터)          design/levels/chapters.json, levels.js
          │  unity.sh levels → story-check (node)          │ (행성 카드 intro·tagline, 스테이지 story — 레벨 디자인)
          ▼                                                ▼
Assets/_Project/Data/story.json ──────────────┐   Data/chapters.json, levels.json
                                              ▼
                     ┌──────────── Model (Contracts/Story, 순수 C#) ────────────┐
                     │ StoryCatalog  : 로드·단정 검증, Lines(cue)                │
                     │ StoryRules    : SessionUpdate·State → StoryCue (순수 함수)│
                     │ StoryProgress : 본 횟수·엔딩 봄 (SaveSystem 키 story.v1)  │
                     └───────────────────────────────────────────────────────────┘
                                              ▲ 읽기
  GameSession.Updated / Presented ──► StoryDirector (Controller, Game/Story) ──► Views
  AppFlow·ChapterIntro·GameFlowController.OnWon ─┘        │                      ├ MomentLineView (HUD)
                                                         │                      ├ ResultView (+ finale·constellation 줄)
  StarTrailView (gameplay-ui, 같은 이벤트) ◄── 연결 표 4.2 ┘                      ├ EndingView (scene-flow EndingController)
                                                                                ├ ChapterEntryView (+ 이미지)
                                                                                ├ LoadingView (관측자 새김)
                                                                                └ Title / StageSelect (머리말 갈래)
```

- **규칙 코드(`SevenSteps.Core`, `core.js`)는 바꾸지 않는다.** 이야기는 규칙이 이미 내는 이벤트와 상태를 읽기만 한다.
- 스테이지 한 줄(`levels.js` `story`)과 행성 카드(`chapters.json` `intro`·`tagline`)는 지금처럼 레벨 데이터에 둔다. 이미 화면에 연결되어 있다. 나머지 이야기 문구는 모두 `story.json`에 둔다.

## 2. 데이터: `story.json`

### 2.1 위치와 흐름

| 파일 | 소유 | 성격 |
|---|---|---|
| `design/story/story.json` | 스토리 디렉터 | 원고(원본). 4단계에서 채운다 |
| `unity/Assets/_Project/Data/story.json` | 생성물 (`unity.sh levels`가 복사) | `chapters.json`과 같은 방식. 손으로 고치지 않는다. 검사를 통과해야만 쓴다 |
| `GameAssets.storyJson` (TextAsset) | 아키텍트(필드), 게임플레이/UI(연결) | 런타임 입력 |
| `GameAssets.storyImages` (`StoryImageSet`, ScriptableObject: 키 → Sprite) | 아키텍트(타입), 아트(값) | 이미지 키 해석. 비어 있으면 이미지 없이 글만 보인다 |

### 2.2 스키마 (JsonUtility로 읽을 수 있는 모양: 딕셔너리와 null 없음, 빈 값은 `""`/`0`/`-1`)

```json
{
  "about": "Story lines (design/story/story-integration.md 2). ko base + text[] like chapters.json (architecture 13.5).",
  "version": 1,
  "languages": ["ko", "en"],
  "brothers": [
    { "n": 1, "name": "첫째", "text": [{ "lang": "en", "name": "the eldest" }] },
    { "n": 2, "name": "둘째", "text": [{ "lang": "en", "name": "the middle one" }] },
    { "n": 3, "name": "셋째", "text": [{ "lang": "en", "name": "the youngest" }] }
  ],
  "beats": [
    { "chapter": "moss", "star": 1, "part": "bowl", "beat": "departure", "focus": 1 }
  ],
  "images": ["card.moss", "ending.1", "ending.2", "ending.3", "ending.4", "title.key"],
  "entries": [
    {
      "id": "moment.keep.b1",
      "on": "keep",
      "chapter": "", "brother": 1, "stones": -1, "card": 0, "after": "",
      "image": "",
      "lines": [
        { "id": "1", "line": "여기는 내가 붙들게.", "text": [{ "lang": "en", "line": "I'll hold this one." }] }
      ]
    },
    {
      "id": "ending.2.s1",
      "on": "ending", "chapter": "", "brother": 0, "stones": 1, "card": 2, "after": "",
      "image": "ending.2",
      "lines": [ { "id": "1", "line": "…", "text": [{ "lang": "en", "line": "…" }] } ]
    }
  ]
}
```

- **entry** 하나는 "언제(`on` + 한정자) 무엇을 보여 줄지"를 정한다. `lines`는 그 자리의 문장 풀(1개 이상)이다.
- **한정자**: 값이 비면 "아무거나"다.

  | 한정자 | 값 |
  |---|---|
  | `chapter` | 챕터 id 또는 `""` |
  | `brother` | 1~3, 0 = 아무나 |
  | `stones` | 0~2, −1 = 아무거나 |
  | `card` | 엔딩 카드 1~4, 0 = 해당 없음 |
  | `after` | `""`, `"ending"`(엔딩을 본 뒤), `"allStars"`(150별) |

- 고르는 법은 `StoryCatalog.Find(cue)`가 맡는다.
  - 한정자가 모두 맞는 entry 가운데 **가장 구체적인 것**(빈 값이 아닌 한정자가 가장 많은 것)을 고른다.
  - 같은 구체성의 entry가 둘이면 검사 오류다(3.1).
- **줄 고르기**는 난수 없이 `lines[StoryProgress.Count(entry.id) % lines.Length]`다. 보여 줄 때 센다. 직전 줄은 다시 나오지 않는다(풀이 2개 이상이면 저절로 그렇다). 같은 저장 상태에서는 같은 줄이 나온다.
- `beats`는 런타임에 쓰지 않는다. 텔레메트리 분석과 문서 대조용 메타데이터다(행성 = 별, 국자/자루, 비트, 중심 형제).
- 변수: 문장 안 치환은 `{brother}` 하나만 허용한다(`narrative-structure.md` 4.4). 숫자 치환은 쓰지 않는다.

### 2.3 `on` 값 (전달 지점)

| `on` | 지점 (`narrative-structure.md` 4.1 번호) | 한정자 | 필요한 entry (검사) |
|---|---|---|---|
| `title` | 1 타이틀 한 줄 | `after` | `after ""` 1개, `"ending"` 1개 |
| `map` | 2 별지도 머리말 | `after` | `""`, `"ending"`, `"allStars"` 각 1개 |
| `loading` | 11 관측자 새김 | — | 1개, 줄 7개 이상 |
| `seventhFirst` | 6 첫 7번째 걸음 | `brother` | 1개 (`brother 0`) |
| `keep` `switch` `bridge` | 7 순간 문구 | `brother` | 각 형제 1~3, 줄 2개 이상 |
| `seal` `fall` `vanish` | 7 순간 문구 | `brother` | 각 1개 이상 (`brother 0` 허용) |
| `lose` | 12 실패 | — | 1개 (제목·본문은 줄 2개: `id` `title`, `body`) |
| `finale` | 8 피날레 결과 | `chapter` | 7챕터 각 1개 (`altar`는 49) |
| `constellation` | 9 별자리 완성 | `chapter` | 7챕터 각 1개 |
| `ending` | 10 엔딩 | `card`, `brother`, `stones` | 카드 1: 형제 1~3. 카드 2·3: 선돌 0~2. 카드 4: 1개 |
| `stillStar` | 엔딩 뒤 북극성 줄 | `after "allStars"` | 1개 |

스테이지 한 줄(지점 5)과 행성 카드(4)·행성 설명(3)은 `levels.js`/`chapters.json`에 있다. 같은 검사 도구가 그 파일들의 해당 필드에도 글자 수와 글자 검사를 한다(3.1).

## 3. 검증

### 3.1 export 단정 검사 (`story-check`, node, Unity 없이)

`design/story/tools/check-story.mjs`(스토리 디렉터가 만든다)가 검사한다. 하나라도 실패하면 exit 1이고, `unity.sh levels`는 `Data/story.json`을 쓰지 않는다.

| # | 검사 | 실패 예 |
|---|---|---|
| 1 | JSON 모양, `version == 1`, `languages`가 `chapters.json`과 같음 | 필드 누락 |
| 2 | entry `id`가 모두 다르고 `[a-z0-9.]+` 꼴, line `id`는 entry 안에서 다름 | 중복 id |
| 3 | 모든 line에 ko(`line`)와 `languages`의 모든 번역(`text[].line`)이 있고 비어 있지 않음 | en 빠짐 |
| 4 | 글자 수 상한(지점별, 언어별, `narrative-structure.md` 4.1 표). 카드는 줄당 상한 | ko 31자 스테이지 줄 |
| 5 | ko 한글 음절이 모두 KS X 1001 2,350자 안 (`ksx1001.mjs`) | Gaegu에 없는 글자 |
| 6 | 허용 기호만 씀 (`… · , . ? ! “ ” ‘ ’` 와 공백), 아라비아 숫자 없음 | `~`, `7` |
| 7 | 금지어 없음 (`narrative-structure.md` 4.2 목록, ko/en) | "희생" |
| 8 | `on`이 2.3 표에 있음. 한정자 범위가 맞음: `chapter`가 `chapters.json` id, `brother` 0~3, `stones` −1~2, `card` 0~4, `after` 값 | 없는 챕터 |
| 9 | 2.3 "필요한 entry"가 모두 있음(갈래가 빠짐없이 채워짐) | 엔딩 카드 2 선돌 0 갈래 없음 |
| 10 | 같은 (`on`, 한정자) 조합이 둘 이상 없음 | 모호한 트리거 |
| 11 | 치환은 `{brother}`만. ko `brothers[].name`이 모두 받침 없음(`(code − 0xAC00) % 28 == 0`) | 받침 있는 호칭 |
| 12 | `image` 키가 `images` 목록에 있음 | 오타 |
| 13 | (레벨 데이터) `levels.js` `story`, `chapters.json` `intro`·`tagline`에 4~7번 검사를 적용함. 지금 데이터는 **경고**로 시작하고, 4단계 원고 반영 뒤 오류로 바꿈 | — |

### 3.2 Unity 쪽 (scene-flow `SceneFlowValidator`, 빌드 전 오류)

- `StoryCatalog.FromJson`이 로드할 때 단정 검사를 한다.
  - 2.3 필수 entry, 한정자 범위, 번역 누락을 검사하고, 어기면 `InvalidDataException`(메시지에 entry id와 이유)을 던진다.
  - EditMode 테스트가 실제 `Data/story.json`을 로드한다.
- `LocalizationCheck` 범위에 `story.json`을 더한다. 각 언어의 모든 line을 그 언어 **제목·본문 글꼴 둘 다**의 cmap으로 검사한다(GlyphCheck, 13.4).
- `StoryImageSet`은 `images`의 키마다 Sprite가 있는지 본다. 아트 납품 전에는 경고, 납품 뒤에는 오류다(아키텍트가 정하는 플래그).

### 3.3 글자 수의 최종 판정 = `UiLayoutCheck` (리드 요청 b)

- `narrative-structure.md` 4.1의 글자 수 상한은 **원고를 쓸 때 쓰는 예측값**이다. 화면에 들어가는지는 `UiLayoutCheck`(`architecture.md` 14.9)가 최종 판정한다.
  - 검사 범위: ko·en 각각, 1280×720·1920×1080, 위반은 **오류**.
  - 글자 수 검사(3.1 #4)를 통과해도 `UiLayoutCheck`가 실패하면 실패다.
- `UiLayoutCheck`가 렌더해야 하는 이야기 화면과 문자열은 다음과 같다(14.9 "가장 긴 값"). 화면 소유자가 Edit Mode 빌드 경로를 만든다.

  | 화면 | 넣을 문자열 | 소유 |
  |---|---|---|
  | HUD | 가장 긴 스테이지 한 줄 + 가장 긴 힌트(같은 열) + 가장 긴 순간 문구 | 게임플레이/UI |
  | 챕터 진입 카드 | 가장 긴 행성 이름 + 가장 긴 intro | 씬 플로우 |
  | 결과 | 가장 긴 피날레 문구 + 가장 긴 별자리 문구 + 별 조건 | 게임플레이/UI(내용), 씬 플로우(흐름) |
  | 엔딩 | 가장 긴 엔딩 카드 줄(모든 갈래 중) | 씬 플로우 |
  | 로딩 | 가장 긴 새김 | 씬 플로우 |
  | 타이틀, 별지도 | 가장 긴 `title`·`map`·`stillStar` 줄 | 씬 플로우 |

- 상한이 화면과 맞지 않으면 상한을 고친다. 문서(`narrative-structure.md` 4.1)와 `check-story.mjs`의 표를 같이 고치고, `UiLayoutCheck` 결과를 근거로 적는다.

### 3.4 `ksx1001` 검사를 돌릴 곳 (리드 요청 c)

| 어디서 | 무엇을 | 왜 |
|---|---|---|
| **`unity.sh story-check`** (새 명령, 리드 소유 `unity.sh`) | `node design/story/tools/check-story.mjs`: 3.1 전부. ksx1001은 `story.json`, `levels.js` story·name·hint, `chapters.json` 텍스트, `Data/Strings/ko.json`에 적용 | Unity 없이 몇 초. 스토리와 레벨 디자인 작업 공간에서 에디터를 닫지 않고 돌릴 수 있다 |
| **`unity.sh levels`** 안 (export 첫 단계) | `story-check`를 먼저 부르고, 실패하면 아무 파일도 쓰지 않음 | 생성물(`Data/*.json`)이 검사를 거치지 않고 만들어지는 일이 없게 |
| **리드 병합 체크리스트** (`agents/README.md` "합치기 전 확인") | `story-check`를 `test`·`scene-validate` 옆에 추가 | 지금 CI 서버가 없으므로 병합 전 확인이 곧 CI다. GitHub Actions를 두게 되면 node만 있는 첫 작업으로 같은 명령을 돌린다(Unity 라이선스 불필요) |
| `scene-validate` (Unity) | 실제 Gaegu cmap 검사(GlyphCheck) | 최종 판정. ksx1001은 그보다 빠른 1차 거름망이다(Gaegu = 완성형 2,350자이므로 두 결과는 같아야 한다. 다르면 글꼴 파일이 바뀐 것) |

---

## 4. 트리거

### 4.1 원칙 (리드 요청 a)

1. **판정 입력은 `SessionUpdate`(Kind, Command, Prev, State, Result.Events)와 `State`뿐이다.** 그 밖의 입력은 흐름 쪽 사실(챕터 진입, 클리어 기록, 로딩)뿐이다. 규칙 코드와 `core.js`는 그대로 둔다.
2. 판정은 **순수 함수** `StoryRules`(Contracts/Story)가 한다. 화면, 시간, 난수를 모른다. 출력은 문자열이 아니라 `StoryCue`(on, chapter, brother, stones, card)다. 문자열은 `StoryCatalog` + `Localizer`가 만든다.
3. **형제 번호**는 `BodyIndex.Current(update.Prev)`다. 명령을 낸 몸, 1부터 센다. 선돌 형제는 `BodyIndex.OfStatue`다. 별 흔적(`architecture.md` 15.3)과 같은 함수이므로 색과 목소리가 어긋나지 않는다.
4. **보여 주는 때는 연출 뒤다.** `Updated(Present)`에서 cue를 만들어 토큰과 함께 보류하고, `GameSession.Presented`가 오면 보여 준다. 결과 패널과 같은 규칙이다(4.2).
5. **숨기는 규칙**
   - `Snap`(Undo, Restart, Replay), `Rebuild`(Load)가 오면 보류 중인 cue를 버리고 보이는 순간 문구를 즉시 숨긴다.
   - 리플레이 실행(`LaunchOptions.Replay`), `-flowcheck`, `SmokeView`, `ArtPerf`에서는 순간 문구와 엔딩 자동 재생을 끈다. 행성 카드는 지금처럼 리플레이·스모크에서 나오지 않는다.
   - `QueuedCount > 0`(입력이 밀려 빨리 감는 중)이면 순간 문구를 건너뛴다.
   - 같은 (on, brother)는 시도마다 한 번만 보여 준다. 시도 경계는 `architecture.md` 6.5와 같다.
   - 큐 명령만 지우는 Undo는 업데이트가 없으니 아무것도 하지 않는다.
6. **Undo로 되돌아간 상태에서 다시 KEEP** 해도 "한 시도에 한 번" 규칙으로 다시 나오지 않는다. 그래서 문구로 Undo를 벌주지 않는다.

### 4.2 이벤트 → 전달 지점 표 (별 흔적 연결 포함)

`StarTrailView`/`StarTrail.Of`(gameplay-ui, `architecture.md` 15.3·15.5)는 **보드 위 시각**을 맡고, `StoryDirector`는 **글**만 맡는다. 둘은 같은 업데이트를 따로 구독한다. 서로를 부르지 않으며, 같은 판정 함수(`BodyIndex`)와 같은 이벤트 이름을 쓴다. 겹치는 일(같은 것을 둘이 그리기)이 없도록 열을 나눈다.

| 출처 (Kind / 조건) | 판정 (StoryRules) | 이야기 지점 (`on`) | 보이는 때 | 별 흔적 쪽 (gameplay-ui, 참고) | 겹침 방지 |
|---|---|---|---|---|---|
| `Rebuild` (Load) | 스테이지 n, 챕터 | 스테이지 한 줄(`levels.json` story) | 즉시, HUD 왼쪽 | 텍스처 새로 만들기 | 보류 cue 버림 |
| `Present`, `Step` StepIdx 1~6 | 없음 | — | — | 별 점 커밋, 최신 칸 맥동 | 글 없음 |
| `Present`, StepIdx 7 이벤트(`Choose`·`Win`·`Bridge`·`Seal`·`Vanish`·`Stone`), 게임 전체 처음 | `StoryProgress.Count("seventhFirst") == 0` | `seventhFirst` | Presented 뒤 2초 | 일곱째 각성(0.18초) | 글은 각성이 끝난 뒤에만 |
| `Present`, Command Keep → `Stone` | 형제 = `BodyIndex.Current(Prev)` | `keep` | Presented 뒤 | 선돌 위 고정 별(`OfStatue`), 굳은 별자리 | 선돌 별은 별 흔적만 그린다. 글은 HUD 칸에만 |
| `Present`, Command Switch → `Switch` | 형제 | `switch` | Presented 뒤 | 바통 이음매 비트(층 1 A 비트 0) | 같음 |
| `Present`, `Bridge` | 형제 | `bridge` | Presented 뒤 | 가라앉은 별(층 1 A 비트 1) | 같음 |
| `Present`, `Seal` | 형제 | `seal` | Presented 뒤 | 가라앉은 별 | 같음 |
| `Present`, `Fall` (1~6) | 형제 | `fall` | Presented 뒤 | 낙하 표시(비트 2, v1 선택) | 같음 |
| `Present`, `Vanish` | 형제 | `vanish` | Presented 뒤 | 사라짐(비트 3) | 같음 |
| `Present`, `Stone` 자동(마지막 몸) → `Lose` | 없음(순간 문구 안 냄) | `lose` 문구는 실패 패널이 냄 | 실패 패널 | 패배 단계(0.20초 풀림) | 실패 패널 하나만 |
| `Present`, `Win` (보통 스테이지) | 없음 | — (결과 화면의 기존 문구) | — | 승리 단계, 세 색 별자리 피날레(1.2초) | — |
| `Present`, `Win`, **행성 마지막 스테이지** (7·14·21·28·35·42·49) | 챕터 | `finale` | 결과 패널(피날레 별자리 연출이 끝난 뒤 열림, 4.3) | 피날레 별자리 | 별자리는 보드에, 문구는 결과 패널에 |
| 위 + 이번 클리어로 그 행성 7스테이지 모두 클리어 | `GameFlowController.OnWon`의 `Progress`로 판정해 넘김 | `constellation` | 결과 패널, 피날레 문구 아래 | (행성 별 밝기는 별지도가 그림) | — |
| `Present`, `Win`, **스테이지 50** | 갈래: 형제 = `BodyIndex.Current(State)`, 선돌 = `min(2, State.Statues.Length)` | `ending` 카드 1~4 | 결과 패널(`journey_done`) → 엔딩 | 피날레 별자리 | 엔딩 카드 3의 별자리 장면은 정지 이미지다. 보드 연출과 섞지 않는다 |
| `Snap` (Undo·Restart·Replay) | 없음 | 숨김 | 즉시 | 다시 채우기, Undo 0.12·Restart 0.25초 | 순간 문구 숨김 |
| `Feedback` (Bump) | 없음 | — | — | 없음 | — |
| 흐름: Title 진입 | `after` = 엔딩 봄 여부 | `title` | 타이틀 | — | — |
| 흐름: StageSelect 지도 | `after` = 엔딩 봄 / 150별 | `map`, (`stillStar`) | 별지도 | — | — |
| 흐름: 로딩(0.3초 넘음) | 로드 횟수 | `loading` | 로딩 화면, 규칙 팁과 번갈아 | — | — |
| 흐름: 챕터 진입(`ChapterIntroController`) | 챕터 | 카드(`chapters.json` intro) + 이미지 `card.<id>` | 카드 모달 | — | — |

### 4.3 순서와 보류

- 결과 패널은 지금 `Presented` 뒤에 열린다. **행성 마지막 스테이지와 50**에서는 별 흔적 피날레(`StarTrailPreset` 피날레 시간, 기본 1.2초)를 막(알파 0.6) 아래에서 가리지 않도록 결과 패널을 그 시간만큼 늦게 연다.
  - 늦추는 쪽은 `GameFlowController`(씬 플로우)다.
  - 시간 값은 `StarTrailPreset`에서 읽는다.
  - 입력은 막지 않는다. Enter를 누르면 바로 연다.
  - 별 흔적 기능이 없는 빌드에서는 0초다.
- 순간 문구는 한 번에 하나다. 새 cue가 오면 앞의 것을 바꿔 쓴다(큐에 쌓지 않음).

## 5. 코드 구조 제안 (`architecture.md` 9장: MVC, 단정적 프로그래밍)

### 5.1 Model — `Contracts/Story/` (아키텍트, 순수 C#, 엔진 없는 부분은 EditMode 테스트)

```csharp
namespace SevenSteps.Contracts.Story {
    public enum StoryOn { Title, Map, Loading, SeventhFirst, Keep, Switch, Bridge, Seal, Fall, Vanish, Lose,
                          Finale, Constellation, Ending, StillStar }          // 끝에만 추가
    public readonly struct StoryCue {                                         // 무엇을 보여 줄지 (문자열 아님)
        public readonly StoryOn On; public readonly string Chapter;           // "" = 아무거나
        public readonly int Brother, Stones, Card;                            // 0 / -1 / 0 = 아무거나
    }
    public sealed class StoryCatalog {
        public static StoryCatalog FromJson(string json, IReadOnlyList<string> chapterIds); // 단정 검사(3.2), 실패 = InvalidDataException
        public StoryEntry Find(StoryCue cue);                                  // 가장 구체적인 entry, 없으면 KeyNotFoundException
        public string Line(StoryEntry e, int index, Language lang);           // 번역 없음 = KeyNotFoundException
        public IReadOnlyList<string> ImageKeys { get; }
    }
    public static class StoryRules {                                          // 순수 함수, 상태 없음
        public static bool TryMoment(in SessionUpdate u, out StoryCue cue);   // 4.2 Present 행
        public static StoryCue Ending(State win, int card);                   // 50 갈래: 형제·선돌 수
        public static StoryCue Finale(string chapterId);
    }
    public sealed class StoryProgress {                                       // 저장: SaveSystem 키 "story.v1" (JSON)
        public int Count(string entryId); public void MarkShown(string entryId);
        public bool EndingSeen { get; } public void MarkEndingSeen();
    }
}
```

- `StoryProgress`는 기존 `ProgressData`를 바꾸지 않도록 **따로 저장 키**(`story.v1`)를 쓰자고 제안한다.
  - 손상된 값은 기본값으로 돌리고 경고 로그를 낸다(외부 입력 예외 규칙).
  - 키 이름과 `SaveSystem` API는 아키텍트가 정한다.
- `StoryRules`는 Core의 `State`, `GameEventType`, `BodyIndex`만 쓴다. 엔진과 Localizer를 모른다.

### 5.2 Controller — `Game/Story/StoryDirector` (게임플레이/UI)

- 씬 루트가 `Services`에서 `GameSession`, `Localizer`, `StoryCatalog`, `StoryProgress`, `LaunchOptions`를 꺼내 생성자로 넘긴다. `StoryDirector`는 `Services`를 부르지 않는다.
- `Updated`에서 `StoryRules.TryMoment`로 cue를 만들고 보류 토큰을 둔다. `Presented`에서 보여 주고, `Snap`/`Rebuild`에서 버린다(4.1).
- 시도마다 본 (on, brother) 집합을 둔다. `Load`에서 비운다.
- `MomentLineView.Show(string)`/`Hide()`를 부른다. 보이는 시간은 View가 정한다(2초).
- 흐름 쪽 지점(타이틀, 별지도, 로딩, 결과, 엔딩)은 각 화면의 컨트롤러가 `StoryCatalog`에 직접 묻는다. `StoryDirector`는 보드 세션만 맡는다.

### 5.3 View

| View | 소유 | 받는 것 | 비고 |
|---|---|---|---|
| `MomentLineView` (HUD 오른쪽 열) | 게임플레이/UI | 문자열 | 판(알파 0.75 이상), 22pt 이상, `HudView.BoardFrame` 밖, 형제 색 작은 점 장식(대비 3:1) |
| `ResultView` | 게임플레이/UI | `ResultModel.FinaleLine`, `ConstellationLine` (string, ""이면 숨김) | `result.chapter_done` 대체. `ResultModel` 필드 추가는 씬 플로우 계약(고지) |
| `EndingView` / `EndingController` | 씬 플로우 | `EndingModel { Line[4], ImageKey[4] }` | 지금 WIP(ko/en 키 7개)를 `story.json` `ending` entry로 옮긴다. 모달 오버레이(`OverlayStack`), 새 `AppState` 없이 Game 씬 안에서. Enter·클릭은 다음, Esc는 건너뛰기, 끝나면 StageSelect |
| `ChapterEntryView` | 씬 플로우(틀), 게임플레이/UI(프리팹) | `ChapterCardModel` + `Sprite`(card.<id>) | 이미지가 없으면 지금 모습 그대로 |
| `LoadingView` | 씬 플로우 | 새김 문자열 | `loading.tip.*` 규칙 팁과 번갈아 |
| Title / StageSelect | 씬 플로우 | `title`·`map`·`stillStar` 문자열 | 엔딩 뒤 갈래 |

### 5.4 공용 계약 변경 (아키텍트 검토, 변경 고지 대상)

1. 새 `Contracts/Story/*`(5.1)와 `SevenSteps.Contracts` asmdef 안 폴더
2. `GameAssets.storyJson`(TextAsset), `GameAssets.storyImages`(`StoryImageSet`)
3. `SaveSystem`에 `story.v1` 키(또는 아키텍트가 정하는 다른 위치)
4. `ResultModel`에 `FinaleLine`, `ConstellationLine`, (50) `EndingModel` 넘기기 (씬 플로우)
5. `unity.sh`에 `story-check`(리드), `export-levels.mjs`가 `story.json` 복사·검사 호출(레벨 디자인)

## 6. 화면별 배치

모든 화면은 `architecture.md` 14장을 따른다. 판 0.75 이상, 여백 8, 대비 4.5:1, 안전 영역 24, 모달 막 0.6. **보드와 숫자를 가리지 않는다.**

| 화면 | 이야기 요소 | 자리 | 주의 |
|---|---|---|---|
| 타이틀 | `title` 한 줄 | 게임 이름 바로 아래(지금 `title.tagline` 자리) | 키 아트 왼쪽 1/3 가독성(시안 확인점) |
| 별지도 | `map` 머리말, 행성 카드의 `tagline`, `stillStar` | 머리말은 상단 띠 판. tagline은 행성 카드 안. `stillStar`는 50 노드 옆 카드 | 별 개수 숫자는 Jua 별도 Text(13.4) |
| 로딩 | 관측자 새김 | 지금 팁 자리 | 0.3초 이하 로드에는 안 나옴 |
| 챕터 진입 카드 | 이미지 + 행성 이름 + intro 2줄 | 이미지는 카드 패널 안 위쪽(16:9를 잘라 씀), intro는 지금 판(1000×92) | 이미지에 글자, 숫자, 별 개수를 굽지 않음(art-bible) |
| HUD | 스테이지 한 줄(왼쪽 열, 지금), 순간 문구(오른쪽 열 메시지 아래, 새 판) | `BoardFrame` 밖 | 풀이(H) 메시지와 같은 판을 쓰지 않음. 풀이가 보이는 동안 순간 문구는 숨김 |
| 결과 | 피날레 문구(별 위), 별자리 문구(피날레 아래) | 결과 모달 | 별 조건 문구(본문 22) 줄과 겹치지 않음 |
| 엔딩 | 정지 이미지(전체) + 한 줄(하단 판) + "건너뛰기 Esc" 보조 글 | 모달 | 이미지 하단 30%는 판 자리로 비워 둔다(아트 요청) |
| 실패 | `lose` 제목·본문 | 실패 패널(지금) | `result.lost_*` 대체 |

### 6.1 아트(Codex)에 필요한 이미지 (코디네이터 경유)

시안(`concepts/world-2026-11-10/`)을 바탕으로 만든다. 글자, 숫자, 별 개수를 굽지 않는다. 바이너리는 공유 폴더에 둔다.

| 키 | 내용 | 근거 시안 | 우선 |
|---|---|---|---|
| `ending.1` | 선돌의 원 한가운데 가는 빛기둥, 일곱 별 정렬. 형제는 작게 또는 없음(갈래와 무관하게) | 새로 | 잼 |
| `ending.2` | 원 안에서 세 형제가 다시 함께 선 모습(머플러 3색). 선돌 수와 무관한 구도 | `storyboard/09-ending.png` 재회 부분 | 잼 |
| `ending.3` | 원의 바닥과 선돌 위로 이어지는 별 흔적 별자리. 새 선돌은 그림자나 원경으로만(0/1/2 갈래 모두에 맞게) | `star-trail/finale-constellation.png` | 잼 |
| `ending.4` | 뒤집힌 두루마리의 빈 면, 테라코타 머플러의 손끝에 첫 점 하나 | `storyboard/09-ending.png` 지도 부분 | 잼 |
| `card.moss` … `card.altar` (7) | 행성 진입 카드 배경 16:9, 하단 30% 판 자리 | `storyboard/01, 03–08` (moss는 01-departure) | 잼: 시안을 그대로 쓸 수 있는지 아트 판단. 아니면 이후 |
| `title.key` | 타이틀 키 아트(왼쪽 1/3 비움) | `key-art.png` | 이후(지금 메뉴 배경 유지) |

## 7. 텔레메트리 제안 (백엔드/분석 결정, `docs/telemetry.md`)

개인정보는 넣지 않는다. 문구 자체는 보내지 않고 id만 보낸다.

| 이벤트 | 필드 | 왜 |
|---|---|---|
| `ending_view` | `firstTime`, `cardsSeen`(1~4), `skipped`(bool), `durationMs`, `stones`(0~2), `winner`(1~3) | 엔딩까지 간 비율, 엔딩을 끝까지 보는지, 갈래 분포 |
| `story_line` (선택) | `id`(entry id), `point`(`finale`·`constellation`·`card`), `level` | 저빈도 지점만. 순간 문구는 보내지 않음(빈도가 높고, `keep`·`switch` 이벤트로 이미 알 수 있음) |
| `session_start`에 기존 필드 | 바꿀 것 없음 | — |

## 8. 이행 계획

### 8.1 단계와 담당

| 단계 | 누가 | 무엇 | 테스트·확인 |
|---|---|---|---|
| S0 승인 | 리드 → 사용자 | 1~3단계 문서 | — |
| S1 원고 | **스토리 디렉터** | `design/story/story.json`(모든 entry ko+en), 50스테이지 한 줄·행성 카드 intro·tagline **원고**(`design/story/copy-*.md`), `check-story.mjs` | `node check-story.mjs` 통과(3.1) |
| S2 계약 | 아키텍트 | `Contracts/Story/*`, `GameAssets` 필드, 저장 키, 변경 고지 | EditMode: `FromJson` 단정 검사(빠진 갈래, 모호한 트리거, 번역 누락), `Find` 구체성, `StoryRules` 골든 트레이스(KEEP/SWITCH/Bridge/Fall/Vanish/자동 선돌/Undo 뒤), `Ending` 갈래 |
| S3 데이터 | 레벨 디자인 | `export-levels.mjs`가 `story.json` 복사 + `story-check` 호출. 원고의 스테이지 줄·카드를 `levels.js`/`chapters.json`에 반영(f79eb9b 다시 맞춤) | `unity.sh levels`, `story-check` |
| S3 명령 | 리드 | `unity.sh story-check`, 병합 체크리스트에 추가 | — |
| S4 보드 쪽 | 게임플레이/UI | `StoryDirector`, `MomentLineView`, 결과 문구(`FinaleLine`·`ConstellationLine`), HUD Edit Mode 빌드 경로 | EditMode: 보류·Presented 표시·Snap 숨김·시도당 1회·리플레이 끔. `UiLayoutCheck` HUD·결과 |
| S4 흐름 쪽 | 씬 플로우 | 엔딩 WIP를 `story.json`으로, 결과 지연(4.3), 로딩 새김, 타이틀·별지도 갈래, `LocalizationCheck`에 `story.json`, `UiLayoutCheck` 엔딩·로딩·카드 | `scene-validate`, `smoke`(flowcheck에 50 → 엔딩 → StageSelect 추가), ko/en 스크린샷 |
| S5 이미지 | 아트(Codex) | 6.1 이미지, `StoryImageSet` 값 | `scene-validate`(이미지 키) |
| S6 텔레메트리 | 백엔드/분석 | `ending_view`(+선택 `story_line`) 스키마·골든 | `unity.sh golden` |
| S7 병합 | 아키텍트 검토 → 리드 | test, story-check, scene-validate, build-win + smoke, build-webgl + smoke-webgl | 기존 기준 수치 유지 |

### 8.2 잼 범위와 이후

| 잼 안 (우선순위 순) | 이후로 미룸 |
|---|---|
| 1. 피날레 결과 문구 7 + 50 엔딩 4장(글, 갈래 포함) — 이미 배분된 일(gameplay-ui, scene-flow)을 `story.json`에 맞춰 마무리 | 설정의 "이야기 문구" 토글 |
| 2. 행성 카드 intro·tagline, 스테이지 한 줄 다듬기(레벨 디자인 반영) | 행성 카드 이미지 본제작(시안이 쓸 만하지 않으면) |
| 3. 순간 문구 KEEP·SWITCH·다리(형제별) + 첫 7번째 | `seal`·`fall`·`vanish` 순간 문구 |
| 4. 별자리 완성 문구, 로딩 새김, 실패 문구 | 북극성 150별 줄, 타이틀 키 아트 |
| 5. 엔딩 이미지 4장 | 별지도에 행성별 별자리 그림(저장된 경로 필요, 저장 형식 변경) |
| 6. `ending_view` 텔레메트리 | `story_line` 텔레메트리 |

블라인드 QA가 끝나기 전에는 스테이지 **이름**을 바꾸지 않는다(이름 = 저장·텔레메트리·QA 키).

### 8.3 보류 중인 일과의 관계 (리드 메시지 2026-11-10)

- **level-design f79eb9b 챕터 카드 intro**: S1 원고에서 다시 맞춘다.
  - altar의 "빈자리가 둘 있다"는 엔딩 갈래(선돌 0~2)와 어긋나고 50의 풀이를 암시할 수 있어서 대안을 낸다.
  - frost, joinery, crumble의 기존 유지 근거도 존중한다. 깨달음을 미리 알려 주지 않고, 게임 용어를 쓰지 않는다. 이 둘은 4.2의 형식 규칙에도 들어 있다.
- **scene-flow 엔딩 WIP**(`EndingController`, 키 7개): 흐름 코드는 그대로 쓴다. 문구 출처만 `Strings` 키에서 `story.json` `ending` entry로 바꾼다.
  - 바꾸는 시점은 둘 중 하나다. S2 계약이 들어오면 바로 바꾸거나, WIP를 먼저 병합한 뒤 바꾼다(키 7개는 그때 지운다).
  - 엔딩 1~3의 갈래(형제, 선돌 수)는 `StoryRules.Ending`이 준다.
- **gameplay-ui 피날레 결과 행성별 문구**(미착수): `Strings` 키를 7개 늘리지 말고 `ResultModel.FinaleLine`으로 받는다(S2 이후). 급하면 임시로 `Strings`에 두고, S4에서 옮긴다.
- **별 흔적**(gameplay-ui 진행 중): 4.2 표가 연결 지점이다. 이야기 쪽은 별 흔적 코드를 부르지 않는다. 요청은 4.3의 결과 패널 지연 하나뿐이다.

## 9. 열린 질문 (승인 때 정할 것)

1. 엔딩 처음 재생은 50 결과 패널의 "다음"을 누를 때인가, 결과 패널 대신 바로인가? 제안은 **결과 패널의 "다음" 버튼이 "끝까지 보기"로 바뀌는 것**이다.
2. 순간 문구를 리플레이와 스모크 말고도 "★3 도전" 중에 숨길지. 제안은 **숨기지 않는 것**이다. 시도당 1회라 방해가 적다.
3. `title.tagline`을 `story.json`으로 옮길지, `Strings`에 두고 엔딩 뒤 갈래만 `story.json`에서 덮을지. 제안은 **잼 동안은 덮기만** 하는 것이다.
4. 로딩 화면의 새김과 규칙 팁을 번갈아 보일지, 새김만 보일지. 제안은 **번갈아 보이기**다. 처음 하는 플레이어에게는 팁이 필요하다.
