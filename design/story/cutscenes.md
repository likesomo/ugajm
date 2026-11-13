# 그림책 컷신 대본 / Storybook cutscenes

작성: 리드, 2026-11-12 저녁. 요청: 사용자 (`C:/unityjam/shared-assets/seven-steps/reports/user-request-2026-11-12-story-tutorial.md`).
데이터 기준: **`design/story/cutscenes.json`** (게임이 읽는 문구, 규칙 자막 `rule`, 이미지 키, 아트 프롬프트). 이 문서와 다르면 json이 맞다. 검사: `node design/story/tools/check-cutscenes.mjs` (개구 2,350자, `·` 없음, 금지어, 페이지당 1~3줄, 이야기 줄 ko 32자, 규칙 자막 ko 40자·en 100자, 자막은 숫자·`:`·영어 대문자 허용).

> **이전 규칙 변경**: "컷신 없음"(GDD 안티 필러, `seven-steps-concept.md`, `narrative-structure.md`, `seven-planets-story.md`)은 2026-11-12 사용자 요청으로 바뀌었다. 그림책 컷신은 **프롤로그**(새 게임)와 **에필로그**(선택, 엔딩 뒤)에만 쓴다. 행성 안의 이야기는 지금처럼 챕터 카드, 스테이지 한 줄, 엔딩 카드로 전한다.

## 1. 왜 필요한가

사용자: "7걸음을 다 써야 하는 이유, 3명을 다 써야 하는 이유에 대한 게임에서 명확한 설명이 없습니다."
프롤로그가 끝나면 플레이어는 이야기 안에서 이 둘을 안다.

| 알아야 할 것 | 세계의 이유 (`world-bible.md` 0.5·1장과 같음) | 페이지 |
|---|---|---|
| 일곱 걸음을 정확히 써야 한다 | 걸음마다 하늘의 별이 하나씩 켜지고, 일곱째 별이 켜질 때(정렬)만 땅이 듣는다. 일곱 번째 걸음이 닿는 칸만 바뀐다. 여섯째에 출구에 서도 열리지 않는다 | 3, 4 |
| 걸음을 다 쓰면 다음 형제 차례다 | 걸음을 다 쓰면 지도를 넘긴다. 남으면(KEEP) 선돌이 되어 길을 붙들고 다음 형제는 출발점에서 깬다. 남지 않으면(SWITCH) 돌이 없고 다음 형제가 그 자리에서 깬다 | 5, 5b |
| 세 형제를 다 써야 ★3 | 별 사이의 선은 한 사람의 걸음으로는 다 이어지지 않고, 셋이 모두 걸어야 가장 밝다. 결과 별 = 그 선의 밝기: 닿으면 ★1, 스스로 세면(풀이 보기 없이) ★2, 셋이 모두 걸으면 ★3 | 6 |

★ 규칙(`Stars.Of`, 2026-11-12 확정)과 같다: ★1 클리어, ★2 = 풀이 보기 0, ★3 = ★2 + `bodiesUsed == bodies`. 화자는 셋째("우리")다.

## 2. 흐름

- **새 게임**(처음 한 번): 타이틀 → 프롤로그 8장 → 튜토리얼 → 스테이지 1.
- 넘기기: 탭/클릭/Space/Enter. 건너뛰기 버튼(Esc). 타이틀의 "이야기 / Story"에서 다시 본다.
- 에필로그(선택): 엔딩 카드 4장 뒤 1장. 그림이 늦으면 넣지 않아도 된다.
- 튜토리얼은 프롤로그 5·6장에서 말한 것을 보드에서 직접 해 본다(코어 담당, 문서는 튜토리얼 작업이 정한다).
- 그림이 없으면 같은 자리에 빈 카드(배경색 + 문구)로 보이고 멈추지 않는다.

## 3. 아트 (Codex 전용)

- 이미지 키: `cutscene.<장면>.<페이지>`. 파일: `C:/unityjam/shared-assets/seven-steps/Assets/Art/Story/Cutscenes/<키>.png` (예: `cutscene.prologue.1.png`). git에 넣지 않는다.
- 1920×1080 (16:9). 아래 1/4은 글 상자 자리라 단순하게.
- 공통 화풍(모든 페이지 앞에 붙인다):
  > Picture-book illustration, gouache and watercolour on textured paper, soft warm light, muted palette (moss greens, warm sandstone, deep indigo night sky). Three identical small faceless hooded wanderers of the same height, told apart only by scarf colour: teal #568C88 (eldest), ochre #B99A57 (middle), terracotta #B96C59 (youngest). Standing stones are plain neutral grey granite. Seven stars in a dipper shape may appear in the sky. No text, letters, numbers, star ratings or UI in the picture; no real-world ruins, religions or symbols. 16:9, 1920x1080; keep the bottom quarter calm and low-detail (the text box sits there).
- 우선순위: 프롤로그 1~7 → 에필로그 1. 2026-11-13 추가: 프롤로그 5b(SWITCH).

## 4. 프롤로그 (8장)

2026-11-13 사용자 요청으로 고쳤다: 5장(KEEP)과 6장(셋이 다 걸어야 하는 이유)을 쉬운 말로 다시 쓰고, SWITCH를 설명하는 5b장을 더했다(새 그림 `cutscene.prologue.5b`, Codex 의뢰. 그림이 오기 전에는 빈 카드로 보인다). 이야기 줄 아래에 **규칙 자막**(`rule`)을 둔다. 같은 규칙을 한 줄로 다시 말하지만, 설명서 말투가 아니라 이야기 말투(화자 "나"=막내, "우리")로 쓴다(2026-11-13 사용자 요청 "설명적문장이 아니고 스토리적 문장으로"). KEEP, SWITCH는 버튼과 이어지도록 문장 안에 한 번 넣는다.

| # | 한국어 | English | 규칙 자막 (ko) | Rule caption (en) | ART PROMPT |
|---|---|---|---|---|---|
| 1 | 고향 하늘의 일곱 별이 해마다 흐려진다. / 별이 다 꺼지면 땅은 걸음을 잊고, 길은 모두 닫힌다. | Every year the seven stars over home grow fainter. / If they all go out, the ground forgets our steps and every path closes. |  |  | Night over a small mossy home planet. Wide shot: the three hooded brothers stand small on a mossy hill beside broken observatory pillars, looking up at a faint, half-faded seven-star dipper. A shut stone gate in the moss below them. |
| 2 | 관측소 마지막 문 너머에서 별지도를 찾았다. / 지도는 같은 밤에 태어난 우리 셋의 손에서만 빛났다. | Beyond the observatory's last door we found the star map. / It shines only in our hands, the three born on the same night. |  |  | Inside a ruined mossy observatory, lit by the map itself. Close-medium shot: the three brothers kneel around an unrolled scroll that glows softly. On it: seven star points and a small ring of stones at one edge, with no lines between the stars yet. Their three scarves hang into the glow. |
| 3 | 한 걸음마다 하늘에 별이 하나씩 켜진다. / 일곱째 별이 켜질 때에만 땅이 귀를 기울인다. | Each step lights one star in the sky. / Only when the seventh lights does the ground listen. | 그래서 우리는 저마다 꼭 일곱 걸음씩 걷는다. | So each of us walks exactly seven steps. | Side view: the middle brother (ochre scarf) walks across square mossy stone tiles carrying the scroll. Behind him, small glowing star-shaped footprints on the tiles he has stepped on. Above him the dipper is half lit: the first stars bright, the rest still dim. |
| 4 | 그래서 일곱 번째 걸음이 닿는 곳만 바뀐다. / 여섯째 걸음에 문 앞에 서도 문은 열리지 않는다. / 일곱 걸음을 꼭 맞게 세어야 한다. | So only the place the seventh step lands on changes. / Stand at the door on the sixth step and it stays shut. / All seven steps have to count, no more and no less. | 앞의 여섯 걸음은 땅이 듣지 못하고, 일곱째가 닿은 곳만 깨어난다. | The ground cannot hear the first six; only where the seventh lands wakes up. | The youngest brother (terracotta scarf) sets his foot on a glowing star mark in front of a mossy stone gate, and the gate swings open with warm light. Above, the whole dipper is lit and a thin beam of starlight runs straight down to his step (the alignment). |
| 5 | 일곱 걸음을 다 쓰면 지도를 다음 형제에게 넘긴다. / 남기로 하면 그 자리에서 선돌이 되어 길을 붙든다. / 그러면 다음 형제는 처음 출발한 자리에서 깨어난다. | When the seven steps are spent, the map goes to the next brother. / Whoever chooses to stay turns into a standing stone and holds the way. / Then the next brother wakes where we first set out. | 형이 KEEP으로 남아 돌이 되면, 나는 처음 그 자리에서 눈을 뜬다. | When my brother KEEPs his place as stone, I open my eyes back where we set out. | Split composition in one scene: on the right, the eldest (teal scarf) stands still on a stone plate beside an open gate, his lower half already turning into a plain grey granite standing stone. A ribbon of starlight carries the scroll from him to the left, where the middle brother wakes up curled in a soft pool of starlight at the starting point. |
| 5b | 남지 않고 지도만 건넬 수도 있다. / 돌은 없고, 다음 형제가 바로 그 자리에서 깨어난다. | Or he can hand on the map without staying. / Then no stone is left, and the next brother wakes right on that spot. | SWITCH로 지도만 건네면 돌은 없고, 나는 형이 섰던 자리에서 깬다. | If he SWITCHes and only hands on the map, no stone stays, and I wake where he stood. | Same mossy stone-tile board as the previous page, medium shot. In the middle of the board the eldest brother (teal scarf) calmly turns into a light ribbon of starlight drifting up toward the dipper (gentle, not sad), leaving no stone on his tile; on that very same tile the middle brother (ochre scarf) is waking up in a soft pool of starlight, already holding the scroll. Far behind them, the original starting tile glows only faintly. No standing stone anywhere in this picture. |
| 6 | 별 사이의 선은 한 사람의 걸음으로는 다 이어지지 않는다. / 셋이 모두 걸어야 선이 가장 밝게 이어진다. / 닿으면 별 하나, 스스로 세면 둘, 셋이 모두 걸으면 셋. | One walker alone cannot join the whole line between two stars. / It shines brightest only when all three of us walk it. / Arrive: one star. Count it yourselves: two. All three walk: three. | 그러니 하나도 빠지지 말자. 셋이 함께 걸어야 별 셋이 켜진다. | So none of us stays behind: three stars light only when all three walk. | Top-down-ish view of a small board of stone tiles: three trails of glowing footprints, one teal, one ochre, one terracotta, each starting from the same start and meeting at the exit, where the three brothers stand together. In the sky above, a bright line between two stars, woven from three coloured strands (teal, ochre, terracotta). |
| 7 | 지도 끝 선돌의 원까지, 일곱 별을 다시 잇는다. / 별이 다 꺼지기 전에. | All the way to the ring of stones at the map's end, we join the seven stars again. / Before they all go out. |  |  | Back view, wide: the three brothers walk away along a mossy path toward the horizon of their small planet, the scroll on the youngest's back. In the deep indigo sky the faint dipper, and far away a tiny planet crowned by a ring of standing stones. |

## 5. 에필로그 (선택, 1장, 엔딩 카드 뒤)

| # | 한국어 | English | 규칙 자막 (ko) | Rule caption (en) | ART PROMPT |
|---|---|---|---|---|---|
| 1 | 셋이 함께 걸은 길마다 별이 밝게 남았다. / 다음 바퀴에도, 일곱 걸음씩. | Every path the three of us walked together stayed bright. / Next turn too, seven steps at a time. |  |  | Night above the ring of standing stones, seen from behind three small figures sitting together on the grass. The whole dipper is bright, and lines of three woven colours (teal, ochre, terracotta) run between its stars across the sky. |

## 6. 세계관과 맞춘 점

- 프롤로그 1·2·7은 `world-bible.md` 0.5(위기, 지도는 셋의 손에서만, 타이틀 한 줄 "별이 다 꺼지기 전에, 다시 잇는다")를 그대로 쓴다.
- 3·4는 1장 "일곱 별과 걷는 사람"이다. 5는 KEEP, 5b는 SWITCH다(규칙: `core.js` "KEEP: become a statue here, next body wakes at the current start. SWITCH: leave nothing, next body wakes here"). "하늘이 맞춰지면 모두 돌아온다"는 세계관에 남고 컷신에서는 뺐다. 다리는 행성에서 배운다.
- 6의 "선은 셋이 모두 걸어야 가장 밝다"는 새로 더한 설정이다. 1장 "별자리는 걸은 사람이 그린다"와 "지도는 셋에게 건네졌다"(엔딩의 마지막 진실)를 잇는다. 결과 화면의 별 셋을 이야기 안에서 설명한다. `world-bible.md` 1장에 같은 줄을 더했다.
- 엔딩 카드 3의 마지막 진실("지도는 셋에게 건네진 것이었다")을 프롤로그 6이 미리 말하지만, 누가 건넸는지(옛 관측자)는 엔딩까지 남겨 둔다.
