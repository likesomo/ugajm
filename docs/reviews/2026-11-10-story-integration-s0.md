# 사전 검토(S0): design/story/story-integration.md (7lucky e6b7b4f, 스토리 디렉터 60e6067)

검토자: 아키텍트, 2026-11-10. 사용자 승인 전 계약 검토. 구현 없음.

**결론: 조건부 승인.** 구조는 그대로 받는다.
- Model(Contracts, 순수)·Controller(Game)·View로 나눈다.
- 판정 입력은 `SessionUpdate`와 `State`뿐이고 규칙 코드는 그대로다.
- 원고는 생성물로 복사하고, node 검사가 1차, Unity 검사가 최종이다.
- 별 흔적과 열을 나눴다.

아래 A1~A8을 S2(계약) 때 이렇게 고정한다. 문서에 반영해 달라(스토리 디렉터).

## 계약 결정 (아키텍트)
- **A1 네임스페이스**: 폴더는 `Contracts/Story/`이지만 네임스페이스는 기존처럼 평평한 `SevenSteps.Contracts`다(`Chapters/`, `Save/`도 그렇다). `SevenSteps.Contracts.Story`는 쓰지 않는다.
- **A2 형제 번호**: `StoryCue.Brother`는 1~3(0 = 아무나) = `BodyIndex` + 1이다. 별 흔적(15장)과 `BodyIndex`는 0부터 센다. 변환은 `StoryRules` 한 곳에서만 한다. 문서 4.1-3의 "1부터 센다"는 `StoryCue`에 대한 말임을 적는다.
- **A3 `StoryRules`는 상태 없음**: 4.2 `seventhFirst` 행의 `StoryProgress.Count(...) == 0` 조건은 저장 상태라 순수 함수에 넣지 않는다.
  - `StoryRules`는 "일곱째 걸음" cue를 늘 내고, `StoryDirector`가 `StoryProgress`로 거른다.
  - "시도당 한 번"과 `QueuedCount` 건너뛰기도 Director가 한다(문서대로).
  - `StoryRules` 시그니처는 `TryMoment(SessionUpdate u, out StoryCue cue)`다. `SessionUpdate`는 이미 클래스라 `in`이 필요 없다.
- **A4 행성 마지막 스테이지는 데이터에서**: 4.2의 "7·14·21·28·35·42·49"와 "그 행성 7스테이지 모두"는 숫자로 굳히지 않는다. altar는 43~50의 8스테이지다.
  - 경계는 `LevelCatalog.ChapterKeyOf(n) != ChapterKeyOf(n+1)`로 정한다(FlowCheck가 이미 쓰는 식). 마지막 챕터만 예외로, 마지막 스테이지(50)는 `ending`이고 `finale`은 그 바로 앞 스테이지다.
  - "행성 완성"(`constellation`)은 그 챕터의 모든 스테이지가 `Progress`에서 클리어인지로 본다.
  - 레벨 디자인이 스테이지 수를 바꿔도 그대로 맞는다.
- **A5 저장**: `StoryProgress`는 `SaveSystem` 안에 둔다(`SaveSystem.Story`, `SaveStory()`).
  - 키는 기존 이름 규칙에 따라 `seven-steps.story`, 값은 JSON `{ "version": 1, "counts": [{"id","n"}], "endingSeen": false }`. 문서의 `story.v1` 대신이다.
  - 이유: 같은 `ISaveStore`(`SaveStores.For(launch)`, `-savekey` 접미사)를 쓰고, `StartNewPlayer`(`-newplayer`)가 이 값도 지우게 하려는 것이다. 다음 테스터가 엔딩 뒤 갈래로 시작하지 않는다. 손상·새 버전 처리는 `ProgressData`와 같다(기본값과 경고, 새 버전은 덮어쓰지 않음).
  - 쓰기 시점: `MarkShown`은 메모리만 바꾼다. `SaveStory()`는 결과 패널, 엔딩 끝, 앱 종료 때 부른다. WebGL에서 `PlayerPrefs.Save`는 IndexedDB 쓰기라 순간 문구마다 하지 않는다.
- **A6 결과 패널 지연(4.3), feat/view 26f01e0 뒤 수정**: 별 흔적 v1이 승리 피날레(`finaleSeconds`)를 보드 Present 안에서 기다린다. 그래서 `Presented`와 결과 패널이 저절로 그 뒤에 열리므로 `GameFlowController`가 따로 늦출 필요가 없다. 4.3은 "Presented 뒤에 연다(지금 그대로)"로 고친다. 피날레 중 Enter·클릭으로 건너뛰기와 리플레이·smoke 단축은 별 흔적 쪽(gameplay-ui) 일이다.
- **A7 `GameAssets` 필드와 이미지**:
  - `GameAssets.storyJson`(TextAsset)과 `storyImages`(`StoryImageSet`, Contracts SO: `Entry[] { key, Sprite }`)를 둔다.
  - "납품 전 경고, 뒤 오류"는 `StoryImageSet.required`(bool, 기본 false) 하나로 정한다. 아트가 다 채우고 켠다.
  - 런타임에 키가 없으면 이미지 없이 글만 보인다(문서대로).
- **A8 `StoryCatalog.FromJson`의 단정**: 런타임 입력이 빌드에 들어간 생성물이라 예외로 멈춰도 된다(levels.json과 같다). 다만 Boot에서 로드할 때 실패하면 이야기만 끄고 게임은 계속하는 쪽을 권한다(오류 로그). 데이터는 빌드 전 `scene-validate`가 이미 거른다.

## 별 흔적과 겹침 (4.2 표)
- **문제없음**: 열 구분이 15장과 맞다.
  - 선돌 별, 바통 이음매, 가라앉은 별, 낙하, 사라짐은 별 흔적이 텍스처 비트로만 그린다. 이야기는 글만 낸다.
  - 각성이 끝난 뒤 글을 내는 것과, 피날레 별자리는 보드에·문구는 결과 패널에 두는 것도 15.5와 같다.
  - `seal`은 15.5에서 다리 비트(1)와 같은 "가라앉은 별"이다.
- 둘은 서로를 부르지 않는다. 공유는 `BodyIndex`, `SessionUpdate`, `StarTrailPreset.finaleSeconds`뿐이다. 좋다.
- **Undo 뒤 다시 KEEP**: 별 흔적은 State에서 다시 그리고, 문구는 시도당 1회라 다시 나오지 않는다. 의도대로 어긋나지 않는다.

## 그 밖 (막지 않음)
- **B1 블라인드 QA**: `Data/story.json`은 `design/story/`의 복사본이다. 엔딩 갈래(선돌 수)와 피날레 문구가 들어간다. `qa-play-plan.md`의 금지 파일에 `unity/Assets/_Project/Data/story.json`을 더한다(리드). 하네스는 이 파일을 읽지 않는다.
- **B2 `unity.sh levels`**: `story-check` 실패가 `levels.json` 생성까지 막으면 레벨 디자인 작업이 이야기 원고에 묶인다. 실패하면 `story.json`만 복사하지 않고 종료 코드 1로 알리는 쪽을 권한다(레벨 디자인·리드 결정).
- **B3 `ResultModel`**: `ResultModel`은 Game/UI(`ResultView.cs`)에 있다. `FinaleLine`·`ConstellationLine` 추가는 gameplay-ui·씬 플로우 사이의 변경 고지면 되고 Contracts 변경이 아니다.
- **B4 HUD 순간 문구 판**: 14장 대상이다. `UiLayoutCheck`의 HUD 변형에 "가장 긴 순간 문구"를 넣는 것(3.3 표)이 오류 판정의 근거가 된다. 풀이 메시지가 보이는 동안 숨기는 규칙도 좋다.
- **9장 열린 질문**: 1(다음 → 끝까지 보기), 3(잼 동안 덮기만), 4(번갈아)는 제안대로 동의. 2는 사용자 판단.

## 추가분 (2026-11-11): 0.5 플레이 목적 장치 (스토리 디렉터 dca699f, 7lucky 91bf521)

**결론: 조건부 승인(A1~A8에 이어 A9~A13).** 판정을 이미 있는 클리어 기록에서 끌어내고 새 저장은 연출 플래그 둘만 두는 방향이 좋다. HUD 발치 고리를 걸음 수 전용으로 두는 8번은 15.6과 맞는다.

- **A9 `FinaleCleared(k)`는 데이터 경계로 정한다(A4와 같다).**
  - 문서의 "행성 안 7번째, altar는 49"는 숫자를 굳힌 것이다. 피날레 스테이지는 `LevelCatalog.ChapterKeyOf` 경계에서 정한다: 챕터의 마지막 스테이지이고, 마지막 챕터만 게임의 마지막 스테이지(50) 바로 앞이다. 지금 데이터로는 7·14·21·28·35·42·49가 나와 문서와 같지만, 스테이지 수가 바뀌어도 맞는다.
  - 클리어 여부는 `ProgressData.IsCleared(level)`(스테이지 이름 키, 11.3.1)로 본다. 번호가 아니라 이름이라 레벨 순서가 바뀌어도 기록이 맞다.
  - 순수 함수로 둔다(Game `LevelCatalog` 옆 또는 `StoryTruths`). 저장하지 않는다.
- **A10 `Truths == 7`도 숫자로 쓰지 않는다**: `Truths == 챕터 수`로 쓴다(chapters.json). 별지도·타이틀의 별 7개도 챕터 목록을 따른다.
- **A11 `StoryCue`에 `After`가 필요하다.** 5.1의 `StoryCue`에는 `On`/`Chapter`/`Brother`/`Stones`/`Card`만 있는데, `title`/`map`/`stillStar`와 이번 카드 3(`after "allTruths"`)은 `after` 한정자로 고른다.
  - `StoryCue.After`(string, `""` = 아무거나)를 더하고, `StoryRules.Ending(win, card, truths)`는 카드 3에서 `truths == 챕터 수`이면 `After = "allTruths"`를 넣는다.
  - `check-story.mjs`의 `after` 허용값에 `allTruths`를 더한다.
- **A12 저장(A5 확인)**: `CarvingSeen`과 `FinalTruthSeen`은 `SaveSystem.Story`(키 `seven-steps.story`, 문서의 `story.v1` 대신)에 둔다.
  - `CarvingSeen`은 챕터 **id 목록**(`string[]`)이다. 번호 k가 아니므로 챕터 순서가 바뀌어도 맞다.
  - `-newplayer`가 이 값도 지운다. 연출 플래그라 손상되면 기본값(아무것도 안 봄)으로 돌아가고, 다시 한 번 보여 주는 것뿐이다.
- **A13 별지도 선의 모양은 데이터로**: "k→k+1, frost는 4→1과 4→5, altar는 7→원"을 scene-flow 코드에 분기로 넣지 말고 `story.json`에 둔다. 예: `"dipper": [{"from":"moss","to":"hollow"}, …, {"from":"altar","to":"circle"}]`. `beats`는 런타임에 안 쓴다고 했으니 별도 배열로 두고, `check-story.mjs`가 챕터 id를 검사한다.
- **텔레메트리 `ending_view.truths`**: 백엔드 스키마와 골든(`unity.sh golden`)을 같이 바꾼다. 막을 일은 없다.
- **이미지 키**: `carving.<id>` 7개는 A7의 `StoryImageSet`에 들어간다. 키 목록은 챕터 id에서 만들고, 검사는 `images` 목록 대조(3.1 #12)로 충분하다.
