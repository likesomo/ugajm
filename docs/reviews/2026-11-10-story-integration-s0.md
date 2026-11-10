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
- **A6 결과 패널 지연(4.3)**:
  - `GameFlowController`가 `ChapterPreset.starTrail ?? StarTrailPreset.Default`의 `finaleSeconds`를 읽는다(15.5).
  - 리플레이·`-flowcheck`·smoke·`ArtPerf`에서는 0초라 기존 smoke 시간과 FlowCheck가 그대로다.
  - Enter·클릭은 지연을 건너뛴다. 별 흔적 쪽 요청은 이것 하나로 충분하다.
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
