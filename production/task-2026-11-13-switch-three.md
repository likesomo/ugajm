# Task 2026-11-13: clearer SWITCH, why all three (lead split)

Request: `C:/unityjam/shared-assets/seven-steps/reports/user-request-2026-11-13-switch-three-brothers.md` (user: "switch 설명을 더 명확히 하세요. 셋을 다 써야하는 이유를 담도록 튜토리얼 및 컷씬의 내용을 더 보강하세요. (텍스트 설명도 컷신에 추가하세요)").

**Rule correction (lead).** The brief's SWITCH line is wrong. Use the real rule (`core.js` doStep / GDD table):
- **KEEP**: this brother stays here as a standing stone. The next brother wakes at the current start.
- **SWITCH**: no stone. The next brother wakes **here**, and this cell becomes the new start.
QA-3 confusion = "where does the next brother wake after SWITCH" -> answer: right where the last one stood.

## Done (lead, 7lucky ae441cb)
- `design/story/cutscenes.json` v2, copied to `Data/cutscenes.json`. Each page may have `rule`: `{ line(ko), text[{lang:"en", line}] }`, a plain-language caption shown under the story lines. It is on prologue 3, 4, 5, 5b and 6.
- Prologue page 5 = KEEP. New page **5b** = SWITCH; its image `cutscene.prologue.5b` is not drawn yet, so it shows a blank card until Codex delivers. Page 6 = why all three. The prologue now has **8 pages**.
- `check-cutscenes.mjs`: a rule caption is at most 40 ko / 100 en characters, and may also use digits, `:` and Latin capitals.
- Known red until gameplay-ui updates it: `CutsceneControllerTests` asserts 7 prologue pages.

## core (feat/core)
1. Tutorial SWITCH made explicit (`docs/tutorial.md` + levels.js tutorials):
   - Somewhere in T1-T3 the player must **do** a SWITCH. Pick the cheapest design that keeps 3 boards (same save ids and flow); for example, rework T3 so it needs one SWITCH and one KEEP and still has minBodies == 3.
   - Cues: `switch` -> "SWITCH: 돌 없이, 다음 형제가 바로 이 자리에서 깬다." / "SWITCH: no stone. The next brother wakes right here."; `respawn` after a SWITCH -> "여기서 깼다. 이제 여기가 출발점이다." / "Woke up right here. This is the start now."
   - Make the T2 `choose` cue name both: "KEEP은 돌로 남고, SWITCH는 여기서 다음 형제가 깬다." Shorten it if it is over about 30 characters; that wording is yours.
2. T3 goal: the `start` cue begins with "목표: 세 형제 모두 걸어서 별 셋." Add a cue when the player tries to finish with fewer brothers, for example heading to the exit before the plates are held. Add a new trigger only if needed; it is your call, documented in tutorial.md 2.3.
3. qa-play.mjs:
   - mirror the new cue texts;
   - `story` shows the `rule` caption under the lines;
   - its naive/tutorial texts match the game;
   - qa-play tests stay green.
4. Gates: check-cutscenes, levels/golden (restore CRLF-only changes), verify, EditMode.

## gameplay-ui (feat/view)
1. Cutscene rule caption:
   - add `rule` to the DTO and `CutsceneCatalog` (language pick as for lines);
   - show it under the story lines in the bottom text box, smaller and in a different tone (plain rule voice);
   - it must stay readable at 1280x720 and in WebGL;
   - update the 7 -> 8 page tests, `-newgamecheck` (more key presses), and playcapture U5Cutscene, including page 5b as a blank card.
2. SWITCH clarity in stages and tutorials:
   - KEEP/SWITCH button tooltips, or a first-use cue: "KEEP: 여기서 돌로 남고, 다음 형제는 출발점에서 깬다." / "KEEP: stay here as a stone; the next brother wakes at the start." and "SWITCH: 돌 없이, 다음 형제가 바로 이 자리에서 깬다." / "SWITCH: no stone; the next brother wakes right here."
   - On the choose screen, mark where the next brother will wake for each option: KEEP = the current start, SWITCH = here. After the respawn, briefly highlight the wake cell and show the left stone clearly.
3. ★2 nudge on the result screen, when the stage is cleared without a hint but bodiesUsed < bodies: "형제 {n}명이 모두 걸으면 별 셋." / "All {n} brothers walk: three stars." Here {n} = level.bodies.
4. Optional, report it separately: a HUD "형제 n/3" counter (n/bodies), or the ★ conditions on the stage start card.
5. Gates: EditMode, PlayMode, playcapture (cutscene + choose screen), scene-validate (GlyphCheck covers the new strings). No player build.
