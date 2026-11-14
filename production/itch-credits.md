# itch.io credits + Generative AI disclosure (draft, lead, 2026-11-13)

Text to paste on the itch.io page. The license facts come from `design/art/CREDITS.md` and the shared `Assets/Audio/AUDIO-CREDITS.md`.
Confirmed by the coordinator from the shared-folder notes (2026-11-13): only the 9 cutscene pages are AI images; the U5 icons are procedural (GenerateKit.cs).

---

## Credits

**Seven Steps**: a picture-book grid puzzle about three brothers and the seven-star dipper.

Made by somon

Music (CC0, credit given voluntarily)
- "musicbox2_cute_tune" (4 Music Box Tracks) by Aureolus_Omicron: https://opengameart.org/content/4-music-box-tracks
- "Somnium" by Adiutorium: https://opengameart.org/content/somnium (excerpt, looped)
- "Lullaby Stories" by Earl Beat: https://opengameart.org/content/lullaby-stories

Sound effects
- Kenney (www.kenney.nl), Interface Sounds / RPG Audio / Music Jingles, CC0 (trimmed and edited)
- Step, stone, switch and other board sounds: synthesized by the team (`gen-sfx.mjs`)

Fonts (SIL Open Font License 1.1)
- Gaegu by The Gaegu Project Authors
- Patrick Hand by Patrick Wagesreiter
- Kaushan Script by Pablo Impallari and Igino Marini
- Jua by The Jua Project Authors

Engine: Unity 6 (URP).

## Generative AI disclosure

This game was made with generative AI tools under human direction. A human chose the concept, picked between alternatives and approved every change. What was AI-generated:

- **Code, level tools, game text and story:** written with AI coding agents (Anthropic Claude via Claude Code, OpenAI Codex), reviewed and tested by automated checks and play sessions. Every one of the 50 stages is checked by a solver.
- **Images:** the 9 storybook cutscene illustrations were generated with OpenAI image generation (through Codex) from our own prompts, with no third-party images as input, and selected by the user. Everything else you see is procedural: the menu icons are drawn by code, and the 3D planets, ruins, board pieces and textures are Blender/Python models and renders, built by scripts the AI agents wrote.
- **Concept art:** early concept mock-ups were AI-generated (GPT image) and used only as reference; none of them ship in the game.
- **Not AI:** all music and the new sound effects are human-made CC0 recordings (listed above). The fonts are the open-source fonts listed above.

## Short version (for the game's credits screen)

```
Music: Aureolus_Omicron, Adiutorium, Earl Beat (OpenGameArt, CC0)
Sound effects: Kenney (kenney.nl, CC0)
Fonts: Gaegu, Patrick Hand, Kaushan Script, Jua (SIL OFL 1.1)
Made with generative AI: code and text (Claude, Codex), storybook illustrations (OpenAI image generation). Music and sounds are not AI.
```
