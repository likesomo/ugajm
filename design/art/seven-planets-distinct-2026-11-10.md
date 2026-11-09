# Seven planets: distinct silhouettes and board clearance — 2026-11-10 revision

This supersedes the first seven-planet concept blockouts in `seven-2026-11-10`. The previously approved menhir/timber UI source remains unchanged. No binary asset is committed.

## Review artifacts

Shared root: `C:/unityjam/shared-assets/seven-steps/`.

- Blender comparison sheet: `concepts/themes/ruins-henge-planets/distinct-2026-11-10/contact-sheet.png` (seven equal 448x336 images in one horizontal row, labels below).
- Actual Unity preset comparison: `concepts/themes/ruins-henge-planets/distinct-2026-11-10/unity/contact-sheet.png`.
- Full images: the same folders contain `moss.png`, `hollow.png`, `carved.png`, `frost.png`, `joinery.png`, `crumble.png`, `altar.png`.
- Before: `concepts/themes/ruins-henge-planets/seven-2026-11-10/` and `concepts/ui/before-distinct-2026-11-10/`.
- Updated UI: `concepts/ui/en/stage-select.png`, `concepts/ui/ko/stage-select.png`, and `concepts/ui/en-ko.html`.
- Editable Blender sources: `Source/Blender/SS_RuinsHenge_<id>.blend`.

## Seven directions

| ID | Main hue / sky | Large form and surface | Distant world / key |
|---|---|---|---|
| moss | teal / green nebula | rounded green crust, moss banks on lateral wings | celadon / cool green daylight |
| hollow | purple / violet nebula | open crust with large exterior windows, hollow lower shell | lilac satellites, rings in concept / lavender |
| carved | ochre / amber nebula | broad alternating stepped strata, exterior tally pillars | amber / warm sandstone |
| frost | ice blue / deep blue nebula | crystalline icy body, tall lateral crystal clusters, no moss | icy white / cold blue |
| joinery | red brown / rust nebula | timber hull, crossed underside joists and exposed exterior tenons | copper / warm amber |
| crumble | grey brown / smoky nebula | jagged crust silhouette, detached floating pieces | muted stone / neutral warm |
| altar | gold / golden nebula | concentric circular terraces and tall standing stones on the outer wings | gold / pale gold |

Every `Chapter_<id>`, `Sky_<id>`, `Lighting_<id>`, `PlanetSummit_<id>` and sky prefab is regenerated with its own palette. `Environment_<id>.mat` replaces BaseAtlas through ChapterPreset.materialOverrides; this replaces one of the six shared material roles rather than adding a seventh role. Frost disables `_UseMoss`, uses an icy edge color and generates zero grass/moss props.

## Board visibility

A board cell is 1 metre and the visible 5x5 board bounds are +/-2.5. Blender asserts that every prop bounding-box vertex is outside the expanded +/-3.5 rectangle. Unity asserts every generated exterior prop vertex against the board bounds expanded by 1 metre, including 5x5 and 9x7 validation cases. Tall decorations are confined to lateral wings; neither rear stones nor rails project over the board. Hull and underside timber are structural surfaces entirely below the playable plane.

The art review scene renders the actual saved Unity presets with the same clean board, camera and resolution for all seven chapters. This independently checks the runtime look instead of presenting Blender images as Unity renders. The art-only capture runner then loads the bilingual UI scene and captures all 20 screens.

Stage selection uses a 97% navy wash over the approved UI background. This keeps the background board faint enough that the seven nodes, connection lines and labels read clearly. The approved single-line chapter-star alignment remains.

## Reproduction

- Blender: `blender -b --python-exit-code 1 -P design/art/blender/build_distinct_planets.py` (`-- --only <id>` renders one).
- Sheet: `powershell -ExecutionPolicy Bypass -File design/art/build-planet-contact-sheet.ps1`; pass `-Root <.../unity>` for the actual Unity sheet.
- Presets / CompileCheck / GlyphCheck: `SevenSteps.Art.SevenPlanetBuilder.Prepare`.
- Review player: `SevenSteps.Art.SevenPlanetBuilder.ReviewWindows`; run with `-planet-capture <folder> -ui-capture <folder>` in a visible render window.

Production integration is unchanged: register `Chapter_<id>` in AppConfig, apply materialOverrides/sky/lighting, and call IStageEnvironment.Fit. `chapters.json` already has the seven matching artPreset strings. The new material overrides must be applied to the board as well so frost never inherits a green moss material.

## Validation

Final run results are recorded below.

Merged current `origin/7lucky` at `548f3fd` before the final checks.

- Final CompileCheck and GlyphCheck: PASS (`unity/Logs/distinct-final-prepare.log`), all localized glyph roles missing=0.
- Geometry clearance and seven preset references: PASS for 5x5 and 9x7 boards (`distinct-final-prepare.log`); Blender prop bounds also PASS (`distinct-2026-11-10/clearance.json`).
- Existing server tests on the merged base: PASS 30/30 (`concepts/ui/distinct-server-final.log`).
- An initial review-runner compile attempt failed because TileVisual is a struct; fixed by using HasPrefab/prefab and the contract's offset/rotation/scale. Subsequent review builds passed. Optional DOF/Panini stripping and editor shutdown temporary-allocation warnings remain; they are not hidden as render or test failures.

- Final merged-base Unity EditMode tests: PASS 744/744 (`reports/distinct-merged-editmode.xml`, `unity/Logs/distinct-merged-tests.log`).
- Final art review Windows build: PASS, errors=0 (`unity/Logs/distinct-review-merged.log`).
- Final actual Unity capture: PASS seven preset images plus 20 EN/KO UI images, no capture failures or exceptions (`distinct-2026-11-10/unity-capture-final.log`). Both comparison sheets were inspected at thumbnail scale; every board cell remains visible. No seven-chapter gameplay completion is claimed by this art-only review.

- Final merged-base full WebGL build: PASS, 0 errors, 15307943 bytes (`unity/Logs/distinct-merged-webgl.log`, output `Builds/WebGL`).
- Logs/XML copied to shared `concepts/ui/logs/`; final capture log is alongside the Unity images.
- `git diff --check`: PASS. Build-only ProjectSettings, URP stripping, generated Credits and scene serialization changes were restored; no binary files are included in the commit.
