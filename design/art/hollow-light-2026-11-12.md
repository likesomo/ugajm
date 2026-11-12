# Hollow lighting readability - 2026-11-12

Branch: `feat/art-hollow-light`, based on `origin/7lucky` at `3c1a627` fetched when this task began. Production changes are restricted to `Lighting_hollow.asset` and `Environment_hollow.mat`; no sky files, shaders, geometry, game rules or other planets were changed.

## Change

The hollow key light previously came from behind the board (yaw 125 degrees). It now comes from the front left (yaw -35, pitch 48), so brother silhouettes, exit fittings and block fronts receive direct light. Key intensity is 1.25, keeping floor tops in the requested luminance range. A neutral cool fill at 0.6 replaces the violet fill, a restrained ochre rim at 0.55 replaces the very dark purple rim, and ambient light is a lighter neutral grey. The existing three-light, no-shadow setup is retained.

The material retains its original brown pumice color map, pore/cavity detail, atlas recipe and roughness. Normal strength changes from 0.60 to 0.38, reducing excessively dark micro-normal shading while retaining the porous identity. No new materials or textures are introduced.

## Actual Game camera measurements

All six primary images are 1280x720 captures of the production Game scene in real Editor Play Mode, with the U5 UI and the production board camera fit. They are not art review fixtures or player builds. Initial states avoid trails and clear overlays over the sampled floor faces. We compare the median of 5x5-pixel patches at ordinary floor centres and raised wall tops, using linear Rec.709 luminance; contrast is `(floor - blocker) / floor`.

| Stage | Before floor | After floor | Before blocker | After blocker | Before contrast | After contrast |
|---|---:|---:|---:|---:|---:|---:|
| 8 | 0.1949 | 0.2152 | 0.0311 | 0.0332 | 84.1% | 84.6% |
| 11 | 0.2150 | 0.2256 | 0.0311 | 0.0320 | 85.5% | 85.8% |
| 14 | 0.2060 | 0.2217 | 0.0309 | 0.0319 | 85.0% | 85.6% |

Every after floor lies within 0.18-0.25, and every blocker contrast exceeds 30%. The baseline initial-state floor patches already met those numeric thresholds; the primary visual improvement is better frontal illumination and more visible brother/exit detail, while maintaining those thresholds. The texture's pores remain dark.

The supplied reference is a stage 14 clear state. Additional `before-clear-stage-14.png` and `after-clear-stage-14.png` capture that state through the actual clear flow. Its existing UI scrim and trails remain intact; their pixel measurements are included separately in `luminance.csv`, and are not used as isolated material-face measurements. No UI or sky asset was modified to brighten the pictures.

## Images and evidence

Output folder: `C:/unityjam/shared-assets/seven-steps/concepts/themes/planet-dressing-2026-11-12/hollow-light/`.

- `before-stage-08.png`, `before-stage-11.png`, `before-stage-14.png`: original hollow preset, 1280x720.
- `after-stage-08.png`, `after-stage-11.png`, `after-stage-14.png`: revised preset, 1280x720.
- `before-after-stage-08.png`, `before-after-stage-11.png`, `before-after-stage-14.png`: labelled comparison pairs, 2560x760.
- `before-clear-stage-14.png`, `after-clear-stage-14.png`: additional clear-state captures, 1280x720.
- `luminance.csv`, capture logs, `compile.log`, `scene-validate.log`, `tileset-validate.log`, `EditMode.log`, and `editmode-results.xml`.

## Verification

All checks used Unity 6000.6.8f1 in headless batch mode in the art-dressing worktree.

- Compile: exit 0 (`BuildTools.CompileCheck`).
- Scene validation: exit 0, zero errors; 19 existing missing story-image sprite warnings, with 480 UI layout screen builds checked.
- TileSet validation: exit 0, zero errors; six existing art TileSet warnings and one placeholder TileSet warning. Chapter presets: zero errors and zero warnings.
- EditMode: 854/854 passed, zero failed, zero skipped.
- New capture-script `.meta` contains the complete MonoImporter record and a terminating newline. All committed files are text.

Capture source is `Assets/_Project/Art/Editor/HollowLightReview.cs`. Reproduce the after captures with:

```
Unity.exe -batchmode -projectPath <workspace>/unity -executeMethod SevenSteps.Art.HollowLightReview.Run -level 8 -ui5 -artperf -hollow-phase after -logFile <output>/capture-after.log
```

The method captures stages 8, 11 and 14 sequentially through the actual game flow and exits. To capture the clear state, use `-level 14 -hollow-phase after-clear`; it replays the stored solution except the final move, then sends that move through the board controller to show the actual clear UI. Captures need graphics, so omit `-nographics`; compile, validators and EditMode use it. `RunChecks.ps1` beside the evidence records the four verification invocations used here. No binary assets are committed.
