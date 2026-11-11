# 4단계 원고 / Stage 4 Copy

작성: 스토리 디렉터, 2026-11-11. 상태: **원고(사용자 승인 2026-11-11, 코디네이터 경유).** 통합은 리드가 한다.
기준: `world-bible.md` 0.5·5.0·6장(내용), `narrative-structure.md` 2.5·4.1·4.2(형식, 글자 수), `story-integration.md` 0.5·2·3(데이터, 검사). 아키텍트 S0 A1~A13을 따른다.

> **블라인드 QA**: 풀이 정보가 없다. 스테이지 한 줄은 "무엇이 있는지·무엇이 뒤집혔는지"만 말하고, 누가 무엇이 되어야 하는지(몸 수, 순서)는 말하지 않는다. 기존 줄 가운데 풀이를 말하던 줄(예: 50 "두 형제가 원의 선돌로 서고…")은 바꾸었다.

## 0. 파일

| 파일 | 소유 | 내용 | 런타임 |
|---|---|---|---|
| `design/story/story.json` | 스토리 디렉터 | 타이틀 2, 북극성 1(선택), 피날레 7(= 일곱 진실), 엔딩 9갈래, 실패 1(줄 3), `dipper` 8선, 이미지 키 19 | `unity.sh levels`가 검사 뒤 `Data/story.json`으로 복사(리드 통합) |
| `design/story/copy-levels.json` | 스토리 디렉터(**제안**) | 스테이지 한 줄 50(ko/en), 행성 카드 intro·tagline 7(ko/en), `startLine` 제안 | 읽지 않음. 레벨 디자인이 `levels.js`·`chapters.json`에 옮긴다 |
| `design/story/tools/check-story.mjs` | 스토리 디렉터 | `story-integration.md` 3.1 검사 #1~#14 | `node design/story/tools/check-story.mjs` (exit 1 = 실패) |

검사 결과(이 커밋): `OK story.json 20 entries / 22 lines, dipper 8, copy-levels 50 stages + 7 chapters`. ksx1001 `OK`. 지금 `levels.js`·`chapters.json`의 기존 문구는 최소 UI 상한을 넘어 **경고 164건**(#13)이 나온다. 레벨 디자인이 이 원고를 옮기면 0이 되고, 그때 `--strict-levels`(오류)로 바꾼다.

## 1. `story.json` 문구

괄호 안은 글자 수(공백·문장부호 포함). 상한: 타이틀·북극성 20/44, 피날레 16/36, 엔딩 18/40, 실패 10/24.

| id | ko | en |
|---|---|---|
| `title` | 별이 다 꺼지기 전에, 다시 잇는다. (20) | Join the stars again before they go out. (40) |
| `title.ending` | 일곱 별이 다시 이어졌다. (14) | The Seven Stars are joined again. (33) |
| `stillstar` | 움직이지 않는 별 아래, 다음 지도. (20) | Under the still star, the next map. (35) |
| `finale.moss` | 지도는 셋의 손에서만 빛났다. (16) | The map shone only in their hands. (34) |
| `finale.hollow` | 건넨 자리에서 다음이 깼다. (15) | Each woke where the map was handed. (35) |
| `finale.carved` | 옛 선돌은 누군가의 자리였다. (16) | Someone stood in every old stone. (33) |
| `finale.frost` | 바퀴마다 누군가 다시 걸었다. (16) | Every turn, someone walked again. (33) |
| `finale.joinery` | 장치는 뒤에 올 손을 알았다. (16) | The gears were made for those after. (36) |
| `finale.crumble` | 지나가는 것도 돕는 일이었다. (16) | Moving on was a way to help, too. (33) |
| `finale.altar` | 원은 지도를 건네는 곳이었다. (16) | The map changed hands at the circle. (36) |
| `ending.1.b1` | 첫째가 디딘 곳에 별이 맞춰졌다. (18) | The eldest's step lined up the stars. (37) |
| `ending.1.b2` | 둘째가 닿자, 일곱 별이 섰다. (17) | The middle one ran in. The sky lined up. (40) |
| `ending.1.b3` | 셋째가 끝까지 세고 디뎠다. (15) | The youngest counted, then stepped in. (38) |
| `ending.2.s0` | 셋이 섰다. 남은 돌은 없었다. (17) | The three stood. No stone remained. (35) |
| `ending.2.s1` | 셋이 섰다. 돌 하나가 남았다. (17) | The three stood. One stone remained. (36) |
| `ending.2.s2` | 셋이 섰다. 돌 둘이 남았다. (16) | The three stood. Two stones remained. (37) |
| `ending.3` | 아직 흐린 새김이 있다. (13) | Some carvings are still dim. (28) |
| `ending.3.all` | 지도는 셋에게 건네진 것이었다. (17) | The map had been handed to them. (32) |
| `ending.4` | 다음 길은 우리가 그린다. (14) | The next road is ours to draw. (30) |
| `lose` | 별이 덜 모였다. (9) | Not all the stars came. (23) |
| `lose` | 하늘이 아직이었다. (10) | The sky wasn't ready. (21) |
| `lose` | 셋은 다시 센다. (9) | The three count again. (22) |

- **타이틀**: 엔딩 전에는 "왜 걷는가"(위기 + 할 일), 엔딩 뒤에는 복원(`after "ending"`).
- **피날레 = 진실**: 홀로 읽어도 뜻이 통하게 썼다(순서 잠금 없음, `narrative-structure.md` 2.5-4). 3(carved)과 6(crumble)이 두 반전이고, 둘이 엔딩 카드 3의 마지막 진실을 받친다. 별지도 새김을 누르면 같은 줄이 다시 나온다(0.5 표 3번).
- **엔딩 카드 1**: 출구에 닿은 형제마다 한 벌(`brother` = `BodyIndex` + 1). 첫째는 단단하게, 둘째는 달려 들어오고, 셋째는 끝까지 센다.
- **엔딩 카드 2**: 선돌 0/1/2 갈래. 같은 틀에 바뀌는 낱말만 문장 끝에 둔다(2.3).
- **엔딩 카드 3**: `after ""` = "아직 흐린 새김이 있다", `after "allTruths"`(`Truths == 챕터 수`, A10·A11) = 마지막 진실.
- **실패**: 줄 풀 3개를 차례로 돈다(`Count % 3`). 이전 스키마의 제목·본문 두 줄은 최소 UI의 "아이콘 위 한마디"로 바뀌었다.
- **선택 entry**: `map`, `loading`, 순간 문구, `seventhFirst`, `constellation`은 잼에서 쓰지 않아 넣지 않았다. `stillStar`(150별 뒤 북극성 줄)만 넣었다. 잼에서 화면이 없으면 쓰이지 않을 뿐이다.

### 1.1 `dipper` (A13)

`moss→hollow→carved→frost`, `frost→moss`(국자가 닫힘), `frost→joinery→crumble→altar`, `altar→circle`(50). 선은 `from` 챕터의 `FinaleCleared`가 참일 때 그린다(`story-integration.md` 0.5 표 2번).

### 1.2 이미지 키 (`StoryImageSet`, 지금은 `required = false`)

`card.<챕터 id>` 7, `carving.<챕터 id>` 7, `ending.1`~`ending.4`, `title.key`. 엔딩 이미지 3장은 Codex 한도 때문에 11/14 이후로 미룬다(코디네이터 2026-11-11). 그동안 이미지가 없으면 글만 보인다. 아트가 다 채운 뒤 `required`를 켠다(A7).

## 2. 행성 카드 (`chapters.json`, 레벨 디자인 반영)

intro = **가야 하는 이유**(moss만 위기의 대가), tagline = 장소의 감각(명사구). 상한 intro 18/40, tagline 12/26.

| id | intro ko | intro en | tagline ko | tagline en |
|---|---|---|---|---|
| moss | 별이 꺼지면 땅은 걸음을 잊는다. (18) | Without stars, no step will count. (34) | 이끼 덮인 고향 관측소 (12) | Home, a mossy observatory (25) |
| hollow | 구멍 너머로 잇는 셈을 찾는다. (17) | Find the count that crosses holes. (34) | 별이 비치는 구멍의 달 (12) | A moon of holes and stars (25) |
| carved | 원의 셈은 새김돌에 적혀 있다. (17) | The circle's count is carved here. (34) | 셈이 새겨진 기둥 숲 (11) | A forest of carved pillars (26) |
| frost | 국자를 닫아야 자루를 뻗는다. (16) | Close the bowl before going onward. (35) | 미끄러운 서리의 달 (10) | A slippery frost moon (21) |
| joinery | 남은 길은 옛 장치로만 열린다. (17) | Only the old gears open the rest. (33) | 나무와 놋쇠의 장치 (10) | Gears of timber and brass (25) |
| crumble | 원으로 가는 마지막 디딤이다. (16) | The last foothold before the circle. (36) | 밟으면 갈라지는 땅 (10) | A crumbling stone world (23) |
| altar | 한 밤 안에 원을 맞춰야 한다. (17) | The circle must align this one night. (37) | 늪 너머 선돌의 원 (10) | The circle past the bog (23) |

- altar 카드는 이전 초안의 "빈자리가 둘"을 쓰지 않는다. 빈자리의 수는 풀이 정보이기 때문이다. 기한(이번 바퀴의 한 밤)을 말한다. 빈자리는 카드 이미지(`card.altar`)가 보여 주고, 몇 자리인지는 말하지 않는다(`world-bible.md` 7장).
- en에서 "handle"은 joinery의 손잡이(레버의 세계 말)에만 쓴다. 북두칠성의 자루(the handle)와 겹치지 않게 frost 카드 en은 "handle"을 쓰지 않았다.

## 3. 스테이지 시작 한 줄 50 (`levels.js` `story`, 레벨 디자인 반영)

상한 ko 20자·7어절, en 42자·7단어. 두 구(준비 / 전환), 3인칭 현재, "형"은 쓰지 않는다. **소개** = 새 기믹을 처음 보이는 스테이지이고, 그 줄은 힌트를 세계의 말로 쓴 것이다(`narrative-structure.md` 4.0-3). 피날레 스테이지(7·14·21·28·35·42·49)의 시작 줄은 진실을 미리 말하지 않고 다음 별로 가는 감정만 연다. 진실은 클리어 뒤 피날레 줄이 말한다.

| n | 행성 | 표시 | ko | en |
|---|---|---|---|---|
| 1 | moss | 소개 | 하나부터 센다. 일곱째에 열린다. (18) | Count from one. The seventh step opens. (39) |
| 2 | moss |  | 문이 코앞이어도, 별은 아직이다. (18) | The door is near. The stars aren't. (35) |
| 3 | moss |  | 길은 많아도, 일곱 걸음 길은 하나. (20) | Many roads. Only one is seven steps. (36) |
| 4 | moss | 소개 · hint 제안 | 일곱째에 서면 돌이 되어 붙든다. (18) | Stay on the seventh step, as stone. (35) |
| 5 | moss |  | 발판은 서 있는 이를 기다린다. (17) | The plate waits for someone to stand. (37) |
| 6 | moss |  | 붙든 돌이, 지름길도 막는다. (16) | The stone that holds also blocks. (33) |
| 7 | moss |  | 마지막 문 너머에서 길이 시작된다. (19) | Past the last door, the road begins. (36) |
| 8 | hollow | 소개 · hint 제안 | 그 자리에서 지도를 건넬 수도 있다. (20) | Or hand the map over, right there. (34) |
| 9 | hollow |  | 혼자는 못 가도, 셋은 잇는다. (17) | Too far for one. Not for three. (31) |
| 10 | hollow | 소개 | 일곱째 걸음에만 구멍이 다리가 된다. (20) | Only a seventh step bridges a hole. (35) |
| 11 | hollow |  | 다리가 될까, 돌이 될까. (14) | To be a bridge, or a stone? (27) |
| 12 | hollow |  | 건넌 자리에서, 다음이 깨어난다. (18) | Across the hole, the next one wakes. (36) |
| 13 | hollow |  | 발밑의 별이 더 깊어진다. (14) | The stars below grow deeper. (28) |
| 14 | hollow |  | 구멍 너머, 다음 별이 비친다. (17) | Past the holes, the next star shines. (37) |
| 15 | carved | 소개 | 문에 새긴 수는 지나갈 걸음의 차례. (20) | The carved number says which step passes. (41) |
| 16 | carved |  | 새김 너머에, 붙들 자리가 있다. (18) | Beyond the carving, a place to hold. (36) |
| 17 | carved |  | 가까운 길에도, 새김이 지킨다. (17) | Even the short way is carved. (29) |
| 18 | carved |  | 먼 문 앞에서, 처음부터 다시 센다. (20) | A far door. Count again from one. (33) |
| 19 | carved |  | 다리를 건너는 걸음도 셈에 든다. (18) | Steps over a bridge count too. (30) |
| 20 | carved |  | 어디서 건네느냐가 다음 셈을 정한다. (20) | Where it's handed sets the next count. (38) |
| 21 | carved |  | 옛 선돌의 키가, 셋과 같다. (16) | The old stones are just their height. (37) |
| 22 | frost | 소개 | 얼음 위에선 미끄러져도 한 걸음. (18) | On ice, one slide is one step. (30) |
| 23 | frost |  | 바닥만으로는 일곱이 맞지 않는다. (18) | The floor alone won't make seven. (33) |
| 24 | frost |  | 미끄러진 걸음도 새김은 센다. (16) | The carvings count slides too. (30) |
| 25 | frost | 소개 | 벽이 없으면, 서 있는 돌이 멈춘다. (20) | No wall? A standing stone stops you. (36) |
| 26 | frost |  | 멈춰 준 돌이, 다음 길을 막는다. (19) | The stone that stops may also block. (36) |
| 27 | frost |  | 미끄러져 닿은 곳이 구멍이라면. (17) | If the slide ends at a hole… (28) |
| 28 | frost |  | 얼음 너머로 고향 별이 보인다. (17) | Past the ice, the home star shows. (34) |
| 29 | joinery | 소개 | 일곱째의 손잡이가 벽을 뒤집는다. (18) | On the seventh, the handle flips walls. (39) |
| 30 | joinery |  | 하나가 열리면, 하나가 닫힌다. (17) | One opens, and another closes. (30) |
| 31 | joinery | 소개 | 올라온 벽도 미끄럼을 멈춘다. (16) | A risen wall can stop a slide. (30) |
| 32 | joinery |  | 손잡이는 한 번, 발판은 계속. (17) | A handle turns once. A plate holds. (35) |
| 33 | joinery |  | 벽이 바뀌면 새김까지 길도 바뀐다. (19) | Move the walls, and the count shifts. (37) |
| 34 | joinery |  | 놋쇠 손잡이가, 셋의 손 높이다. (18) | The brass handles sit at their height. (38) |
| 35 | joinery |  | 마지막 톱니가 금 간 별을 가리킨다. (20) | The last gear points at cracked ground. (39) |
| 36 | crumble | 소개 | 지난 곳은 무너지고 선 곳은 굳는다. (20) | Passed floors fall, but a stop holds. (37) |
| 37 | crumble |  | 무너질 길과 남길 길을 고른다. (17) | Choose which road falls and which stays. (40) |
| 38 | crumble | 소개 | 문은 서 있는 선돌의 수를 센다. (18) | The gate counts the stones still standing. (42) |
| 39 | crumble |  | 막다른 곳도 쓸모가 있다. (14) | Nowhere roads have their use too. (33) |
| 40 | crumble | 소개 | 아무도 서지 않아야 열리는 문. (17) | This gate opens while no stone stands. (38) |
| 41 | crumble |  | 굳은 바닥은 길일 뿐, 돌이 아니다. (20) | Sealed floor is road, not stone. (32) |
| 42 | crumble |  | 돌아갈 길은 없다. 앞으로만 센다. (19) | No way back. Only counting forward. (35) |
| 43 | altar | 소개 | 늪의 진흙은 한 칸에 두 걸음. (17) | Bog mud takes two steps a square. (33) |
| 44 | altar |  | 진흙을 밟으면 셈 하나를 건너뛴다. (19) | Mud skips a number in the count. (32) |
| 45 | altar |  | 얼음은 빠르고, 진흙은 무겁다. (17) | Ice runs fast. Mud weighs heavy. (32) |
| 46 | altar |  | 배운 셈을, 이 방에서 다 쓴다. (18) | Every count they learned, in one room. (38) |
| 47 | altar |  | 닫힌 문 하나가 길의 전부는 아니다. (20) | A shut gate isn't the only way. (31) |
| 48 | altar |  | 흔적 위로, 마지막 걸음이 지난다. (19) | Over the traces, the last steps pass. (37) |
| 49 | altar |  | 일곱 칸 일곱 줄, 원이 가까워진다. (20) | Seven by seven. The circle draws near. (38) |
| 50 | altar |  | 이번 바퀴의 한 밤, 원 한가운데로. (20) | One night this turn. Into the circle. (37) |

**`startLine` 제안** (레벨 디자인 결정, 0.3): 4(KEEP)와 8(SWITCH)은 화면 버튼 이름을 알아야 하므로 `hint`를 제안한다. 나머지는 기본 `story`다. 소개 스테이지의 story 줄이 이미 세계의 말로 된 힌트이기 때문이다. 한 줄만 보이는 최소 UI에서 기존 힌트는 대부분 20자를 넘으므로, `hint`를 고른 스테이지는 힌트 길이도 같이 줄여야 한다.

## 4. 넘기는 일

| 누구 | 무엇 |
|---|---|
| 리드 | `story.json` 통합: `unity.sh story-check`(= `node design/story/tools/check-story.mjs`)와 `levels` 안 복사(3.4). 병합 체크리스트에 `story-check`. B1(QA 금지 파일에 `Data/story.json`) |
| 아키텍트 | S2 계약: `StoryCatalog`·`StoryRules`(`After`, `Ending(win, card, truths, chapterCount)`)·`StoryProgress`(`SaveSystem.Story`)·`StoryImageSet`(`required = false`)·`StoryTruths` |
| 레벨 디자인 | `copy-levels.json`을 `levels.js` `story`/`text[].story`와 `chapters.json` `intro`/`tagline`에 옮긴다. `startLine` 결정. 옮긴 뒤 `check-story.mjs --strict-levels`가 통과해야 한다 |
| 씬 플로우 | 타이틀 한 줄·하늘, 별지도 새김·`dipper` 선, 엔딩 카드 9갈래, 피날레 줄 다시 보기 |
| 게임플레이/UI | 잠깐 줄(`TransientLineView`): 스테이지 시작 줄, 피날레 줄. 실패 한마디 |
| 아트(Codex, 코디네이터 경유) | `ending.1`~`4`(11/14 이후), `carving.<id>` 7, 별지도 국자 기울기, 타이틀 별 두 상태 |

## 5. 블라인드 QA 1차 관찰 대응: 이야기 줄과 힌트가 겹침 (2026-11-11)

**관찰**(리드 전달): 게임 화면 왼쪽 패널에서 이야기 줄과 힌트가 거의 같은 말을 한다(예: 29 "일곱째의 손잡이가 벽을 뒤집는다"와 레버 힌트).

**원인**: 3장의 소개 스테이지 줄은 최소 UI(시작 때 둘 중 **하나만** 보임, `startLine`)를 전제로 **힌트를 세계의 말로 쓴 것**이다. 지금 빌드처럼 패널에 둘을 함께 보이면 겹친다. 소개가 아닌 스테이지 가운데도 기존 힌트와 내용이 가까운 줄이 있다(19, 41, 44 등).

**결론 (리드, 2026-11-11, gameplay-ui 재현)**: (가) 문구는 그대로 둔다. 지금 빌드의 왼쪽 판에는 이야기 줄 하나만 보이고, 레벨 `hint`는 런타임에 나오지 않는다. S4에서는 `startLine`에 따라 story, hint, 없음 가운데 하나만 잠깐 띄운다. 그래서 `alt`를 `story`로 옮기지 않는다.

**대안 줄** (`copy-levels.json`의 `alt`, 검사 통과): 힌트를 되풀이하지 않는 장면 줄이다. 런타임에 쓰지 않고, 단계형 힌트를 설계할 때 참고 자료로 남긴다.

| n | 지금 story | alt ko | alt en |
|---|---|---|---|
| 1 | 하나부터 센다. 일곱째에 열린다. | 고향의 땅이 걸음에 늦게 대답한다. (19) | Home's ground answers steps slowly now. (39) |
| 3 | 길은 많아도, 일곱 걸음 길은 하나. | 갈림길마다 셋이 걸음을 맞춰 본다. (19) | At every fork, the three count again. (37) |
| 4 | 일곱째에 서면 돌이 되어 붙든다. | 첫 이별, 하늘이 맞으면 돌아온다. (19) | The first goodbye. Back when stars align. (41) |
| 6 | 붙든 돌이, 지름길도 막는다. | 고향의 첫 선돌이 관측소를 지킨다. (19) | The first stone keeps watch at home. (36) |
| 8 | 그 자리에서 지도를 건넬 수도 있다. | 남지 않고도 도울 수 있다. (15) | One can help without staying. (29) |
| 10 | 일곱째 걸음에만 구멍이 다리가 된다. | 구멍 아래로 별이 흐른다. (14) | Stars drift below the holes. (28) |
| 15 | 문에 새긴 수는 지나갈 걸음의 차례. | 기둥마다 관측자의 손이 남아 있다. (19) | The watchers' hands live on the pillars. (40) |
| 19 | 다리를 건너는 걸음도 셈에 든다. | 구멍 위로 새김이 이어진다. (15) | The carvings run on across the holes. (37) |
| 22 | 얼음 위에선 미끄러져도 한 걸음. | 차가운 달, 발이 멈추지 않는다. (18) | A cold moon. Feet won't stay put. (33) |
| 23 | 바닥만으로는 일곱이 맞지 않는다. | 얼음은 셈을 흔든다. (11) | The ice shakes the count. (25) |
| 25 | 벽이 없으면, 서 있는 돌이 멈춘다. | 얼음 위에서 셋이 서로를 붙든다. (18) | The three steady each other on ice. (35) |
| 26 | 멈춰 준 돌이, 다음 길을 막는다. | 기대 선 형제 곁으로 얼음이 흐른다. (20) | Ice flows past a brother leaning still. (39) |
| 29 | 일곱째의 손잡이가 벽을 뒤집는다. | 나무와 놋쇠가 숨을 죽이고 있다. (18) | Timber and brass wait, holding still. (37) |
| 30 | 하나가 열리면, 하나가 닫힌다. | 다 열 수는 없는 장치다. (14) | Not every door can stand open. (30) |
| 31 | 올라온 벽도 미끄럼을 멈춘다. | 반쯤 내려앉은 벽이 기다린다. (16) | The sunken walls are waiting. (29) |
| 32 | 손잡이는 한 번, 발판은 계속. | 놋쇠는 차갑고, 나무는 따뜻하다. (18) | Cold brass, warm timber. (24) |
| 36 | 지난 곳은 무너지고 선 곳은 굳는다. | 발자국마다 금이 퍼진다. (13) | Cracks spread from every footprint. (35) |
| 38 | 문은 서 있는 선돌의 수를 센다. | 옛 문이 무언가를 세고 있다. (16) | The old gate is counting something. (35) |
| 40 | 아무도 서지 않아야 열리는 문. | 이 문은 텅 빈 원을 품고 있다. (18) | This gate bears an empty ring. (30) |
| 41 | 굳은 바닥은 길일 뿐, 돌이 아니다. | 흔적에도 여러 모양이 있다. (15) | Traces come in many shapes. (27) |
| 43 | 늪의 진흙은 한 칸에 두 걸음. | 늪의 진흙이 발을 붙잡는다. (15) | Bog mud grabs at their feet. (28) |
| 44 | 진흙을 밟으면 셈 하나를 건너뛴다. | 늪이 걸음을 무겁게 센다. (14) | The bog counts every step heavy. (32) |
| 45 | 얼음은 빠르고, 진흙은 무겁다. | 빠른 얼음과 무거운 늪 사이. (16) | Between swift ice and heavy bog. (32) |
| 49 | 일곱 칸 일곱 줄, 원이 가까워진다. | 원이 보인다. 마지막 방이다. (16) | The circle in sight. One last room. (35) |

- 참고(레벨 디자인 몫): 지금 일부 힌트는 풀이를 말한다(예: 48 "두 형제가 모두 길이 된다", 50 "두 형제가 선돌로 서면 제단이 열린다"). 이야기 줄은 그렇게 쓰지 않았다.
- **스테이지 이름 변경(23·37)**: `story.json`과 `copy-levels.json`은 스테이지 이름을 쓰지 않는다(번호와 챕터 id만). 맞출 곳이 없다. `seven-planets-story.md`의 옛 이름은 이름 변경을 권한 검토 기록이라 그대로 둔다.
