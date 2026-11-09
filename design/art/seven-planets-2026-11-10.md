# Seven planet / menhir UI delivery — 2026-11-10

> Superseded visual blockouts: see [distinct seven-planet revision](seven-planets-distinct-2026-11-10.md) for current images, materials and board-clearance evidence.

Current art branch merges `origin/7lucky` at `aba812c`. All geometry and images in this update are generated locally; no paid or external assets were added. Binaries remain in the shared asset folder.

## UI source and before/after

Shared root: `C:/unityjam/shared-assets/seven-steps/`.

- Before: `concepts/ui/before-2026-11-10/{en,ko}/{title,stage-select,loading}.png`, plus `Handwritten_Background.png`.
- After: `concepts/ui/{en,ko}/{title,stage-select,loading}.png`; compare in `concepts/ui/en-ko.html`.
- Source render: `concepts/ui/menhir-joinery-background.png`.
- Blender source: `Source/Blender/SS_RuinsHenge_UI_MenhirJoinery.blend`; reproducible with `design/art/blender/build_seven_environments.py`.
- Shared Unity sprite: `Assets/UI/RuinsHenge/Handwritten_Background.png` (existing GUID preserved).
- Unity prefab: `Assets/_Project/Art/UI/MenhirJoineryBackground.prefab`. The handwritten preview scene uses the same sprite.

Three unequal independent menhirs carry one straight timber beam and a diagonal brace. Exposed tenons and cross wedges show the construction; faded wood and moss replace red lacquer. There are no paired curved lintels.

Planet selection uses the resource's normalized Dipper coordinates and edges 1–2–3–4–1 / 4–5–6–7. The chapter-star label uses its measured preferred width, with a 12 px gap to the Jua numeric run in both languages.

## Presets

All assets below are under `unity/Assets/_Project/Art/RuinsHenge/`. Each chapter references the existing shared TileSet, its own sky and lighting preset; the sky references a board-fitting summit and camera-relative star/nebula prefab.

| ID | Chapter preset | Sky preset | Lighting preset |
|---|---|---|---|
| moss | Chapter_moss | Sky_moss | Lighting_moss |
| hollow | Chapter_hollow | Sky_hollow | Lighting_hollow |
| carved | Chapter_carved | Sky_carved | Lighting_carved |
| frost | Chapter_frost | Sky_frost | Lighting_frost |
| joinery | Chapter_joinery | Sky_joinery | Lighting_joinery |
| crumble | Chapter_crumble | Sky_crumble | Lighting_crumble |
| altar | Chapter_altar | Sky_altar | Lighting_altar |

Prefab names: `PlanetSummit_<id>` and `Sky_<id>` under `Prefabs/Tiles/RuinsHenge/Env`. Surfaces, rim and distant planet colors differ per chapter; hollow has open underside sectors, carved has recessed strata, frost removes grass, crumble has irregular fragments, and altar adds taller perimeter stones. These decorations never alter gameplay collision or board coordinates.

Seven concept images and gallery: `concepts/themes/ruins-henge-planets/seven-2026-11-10/{moss,hollow,carved,frost,joinery,crumble,altar}.png` and `index.html`. Editable sources: `Source/Blender/SS_RuinsHenge_<id>.blend`. These are concept blockouts; runtime uses the shared procedural environment mesh, not a baked screenshot.

Rebuild Unity assets: `SevenSteps.Art.SevenPlanetBuilder.Prepare` (also runs GlyphCheck and BuildTools.CompileCheck). Existing GUIDs are preserved by asset reuse and prefab overwrite.

## Lead integration

Register the seven `Chapter_<id>.asset` objects in the production AppConfig chapter list and ensure ChapterStage applies sky, lighting, and `IStageEnvironment.Fit`. `chapters.json` already has matching `artPreset` strings and needs no content change. The older `Chapter01` with `moss-ruins` remains for compatibility; production should select the new `moss` preset. Use the split presets, without adding the legacy full Chapter01 environment simultaneously.

Wire `MenhirJoineryBackground.prefab` into production title/select/loading canvases and adopt the Dipper layout from `HandwrittenUiPreview`; the art preview is independent of production scene flow. All generated PNG and .blend files must accompany the shared folder when another machine builds the project. No AppConfig or production scene references are changed here.

## Validation

See the final validation supplement below for the actual run results; earlier reports describe earlier builds.

- Unity EditMode: PASS 740/740, 0 failures (`reports/seven-editmode.xml`; unity/Logs/seven-tests.log).
- Existing server tests: PASS 30/30 (concepts/ui/server-tests-2026-11-10.log).
- Initial CompileCheck: PASS (unity/Logs/seven-prepare-final.log).
- GlyphCheck: PASS, global fonts missing=0 and localized roles missing=0 (concepts/ui/font-coverage.txt).
- First UI Windows build: PASS, errors=0, 114703992 bytes (unity/Logs/seven-ui-windows.log).
- Capture required a visible render window; the first hidden-window attempts failed to capture and were replaced by a successful visible capture of all 20 images. The archived originals remain available.


- Full production WebGL build: PASS, 0 errors, 15298649 bytes (`unity/Logs/seven-webgl.log`, output `Builds/WebGL`). This validates production compilation/build; the production AppConfig still requires the art preset registration above.

- Final preset validation: PASS for all seven chapters and `artPreset` keys (`unity/Logs/seven-final-windows.log`).
- Final UI Windows build and GlyphCheck: PASS, errors=0, 114703992 bytes (`unity/Logs/seven-final-windows.log`). The final label disables automatic wrapping in both languages.
- Logs and EditMode XML are copied to shared `concepts/ui/logs/`.
- Known warnings: stripped optional DOF/Panini passes in the UI preview, plus Unity editor temporary-allocation warnings at shutdown. No test failures or build errors were reported. Seven runtime presets are reference-validated; production chapter switching still needs the lead integration above and is not claimed as a seven-chapter playtest.
