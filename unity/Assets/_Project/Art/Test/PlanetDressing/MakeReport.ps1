$ErrorActionPreference='Stop'
$output='C:/unityjam/shared-assets/seven-steps/concepts/themes/planet-dressing-2026-11-12'
$perf=Import-Csv "$output/perf.csv"
$fits=Import-Csv "$output/stage-fit-validation.csv"
$table=@('| Planet | Stage | Before tris | After tris | Net | Dressing tris | Materials | Contrast | Largest chapter stage |','|---|---:|---:|---:|---:|---:|---:|---:|---:|')
foreach($row in ($perf | Where-Object before -eq 'False')) {
    $before=$perf | Where-Object { $_.planet -eq $row.planet -and $_.before -eq 'True' }
    $max=($fits | Where-Object planet -eq $row.planet | Measure-Object stageTris -Maximum).Maximum
    $delta=[int]$row.stageTris-[int]$before.stageTris
    $contrast=([double]$row.contrast*100).ToString('F1')
    $table+="| $($row.planet) | $($row.stage) | $($before.stageTris) | $($row.stageTris) | $delta | $($row.dressingTris) | $($row.materials) | $contrast% | $max |"
}
$header=@'
# Seven planet materials and Blender dressing - 2026-11-12

Delivered before the 06:30 KST deadline. Five distinct Blender modules per planet replace the old exterior crates, rocks and timber frames. The new 24,064-triangle crater crusts from feat/art 4c43410 are integrated without changing their geometry. The floor/blocker mesh changes from readability d504998 are preserved (included as fea5d5b in this branch).

## Review images

- `seven-planets-sheet.png`: all seven actual Unity Editor Play Mode stage fixtures, using saved production maps, Chapter presets, TileSets, sky, lighting, player and exit prefabs.
- `before-after-<planet>.png`: full-resolution 2560x790 pairs for stages 1, 8, 15, 22, 29, 36 and 43. Individual `before-*.png` and `after-*.png` are 1280x720.
- `objects-<planet>.png`: Blender source module sheets, 1500x900.
- `frost-closeup.png`: actual Unity Play Mode ice surface and modules.

The before fixture uses the same new crater crust, stage, readable tiles and camera as after, with the previous material appearance and generic exterior modules restored. This isolates the dressing/material change; it is not an archival capture of the old crust. The fixtures omit gameplay HUD and do not alter stage data. Camera: orthographic, pitch 50 degrees, yaw 0.

## Measured geometry, materials and readability

Counts include all instantiated board, environment, player, exit and camera-attached sky meshes after startup. Triangles are `GetIndexCount / 3`, counted per instance and submesh; materials are distinct shared materials, not draw calls. Dressing has 13 instances of five source modules in every stage: one enlarged hero and eight additional rotated/scaled instances beyond v1. `perf.csv` records the representative before/after fixtures; `stage-fit-validation.csv` records all 50 initial stage configurations. No frame-rate claim is made.

'@
$body=@'

Every one of the 50 stage fixtures is below 50k triangles and uses at most five materials. Every dressing library is below 8k triangles. The joinery kerb variant retains the oak bevels, mortise shoulders, brass strips and pegs, while changing the eight tiny brass fittings to square edges: 484 to 228 triangles per repeated module. This frees budget for larger stages without removing a themed object or reducing the crater mesh.

Contrast is `(floor luminance - blocker luminance) / floor luminance` in linear Rec.709. We take a 5x5-pixel patch at each ordinary floor centre and wall top, then compare the median floor and median blocker values. Walls remain raised at the existing readable height; walkable tiles remain low. The measured contrast exceeds 30% on every planet. Special gimmick materials retain their existing behavior.

## Planet identities and objects

| Planet | Textured surface | Five Blender modules |
|---|---|---|
| moss | Grey granite grains and irregular green moss | moss boulder, fern, flower clump, footprint stone, fern rock |
| hollow | Brown pumice with round mineral pores | pumice ring, arch, spires, star window, perforated boulder |
| carved | Red sandstone with broken sediment bands | rune slab, strata pillar, broken stela, spiral stele, rune tablet |
| frost | Icy crust; matte packed-snow / frosted-marble board; glossy streaked sliding ice | ice cluster, ice menhir, edge icicle fringe, quartz fan, continuous frozen stream |
| joinery | Dark slate strata, original atlas oak and brass kerb fittings | braced timber frame, gear and lever machine, scaffold, rope coil, plank stack |
| crumble | Cream travertine pores and drip staining | mineral pool, dripping stone, warm lamp, broken tiles, cascade pools |
| altar | Warm sarsen with ochre lichen | lintel, menhir ring segment, lichen stone, grass tuft, seven-star altar disc |

All assets are free procedural work authored in headless Blender 5.2. The 512x512 color and packed surface maps are generated from deterministic mineral, pore, strata, crack and lichen functions inside Blender Python. Packed detail: RG tangent normal, B roughness, A cavity. The common shader uses world projections so tile tops and crust walls keep a consistent texture scale. Prop corner colors preserve foliage, timber, brass and ice in the same chapter material. Frost uses opaque, translucent-looking ice shading with cracks, rime and glossy highlights; it does not add runtime refraction or another transparent material.

## Integration and checks

Production routing remains `Chapter_<planet> -> Sky_<planet> -> PlanetSummit_<planet>`. The existing Chapter material override replaces BaseAtlas with Environment_<planet> for the TileSet. Each summit now references a `Dressing_<planet>` set of five saved prefabs, and uses the same Environment material for the crust, kerb and modules. Existing exit, door, gate and other gimmick models are unchanged.

`BoardShapeAAssembly` omits the old generic exterior decorations and old timber frames when themed dressing is present. It retains the supplied crater mesh and existing board structure. `PlanetDressingPlacement` creates temporary colliders against the actual fitted crust, samples supported shoulders and their visibility, and removes the colliders after placement. Icicles use the actual outer hull bounds. Every prop vertex stays outside the board with a 0.20-cell margin, and the review checks each projected triangle against the full board/exit interval. No playable cell receives dressing.

All 50 stages pass projected clearance, repeated Fit at cell size 0.75 and origin (2,1,-3), minimum eleven supported instances (actual result: thirteen everywhere), and imported mesh ownership checks. TileSet/Chapter validation passes with zero errors; pre-existing warnings are four conditional-door heights and the placeholder set's missing mud. Unity Search emits its existing nonfatal index startup exception; the complete capture and validation finish with exit code 0.

## Source, storage and reproduction

Repository sources:

- `design/art/blender/build_planet_dressing.py`
- `design/art/blender/build_dressing_kerb.py`
- `unity/Assets/_Project/Art/RuinsHenge/Runtime/PlanetDressing*.cs`
- `unity/Assets/_Project/Art/Editor/PlanetDressingBuilder.cs`
- `unity/Assets/_Project/Art/Test/PlanetDressing/`

External binary assets and their stable Unity import metadata:

- `C:/unityjam/shared-assets/seven-steps/Assets/Art/RuinsHenge/PlanetDressing/`: 35 module FBXs, one kerb LOD FBX, seven color maps and seven packed detail maps.
- `C:/unityjam/shared-assets/seven-steps/Source/Blender/PlanetDressing/`: seven source libraries and the kerb source blend.

No images, FBXs, textures or blend files are committed. Unity reaches external binaries through its existing Assets/Shared/Art junction; keep the external `.meta` files with those binaries so saved GUID references remain stable.

Run both Blender scripts with `Blender 5.2/blender.exe -b --python <script>`, then run Unity 6000.6.8f1 with graphics in batch mode:

```
Unity.exe -batchmode -projectPath <workspace>/unity -executeMethod SevenSteps.Art.PlanetDressingReview.Run -logFile <workspace>/unity/Logs/dressing-review.log
powershell -ExecutionPolicy Bypass -File unity/Assets/_Project/Art/Test/PlanetDressing/MakeSheets.ps1
powershell -ExecutionPolicy Bypass -File unity/Assets/_Project/Art/Test/PlanetDressing/MakeReport.ps1
```

The review enters real Editor Play Mode, renders a 1280x720 RenderTexture and exits; it never builds a player or opens the GUI. It is excluded from player assemblies. `unity-playmode-capture.log`, `tileset-validate.log`, `blender.log`, `kerb-blender.log`, `blender-triangles.json`, `perf.csv` and `stage-fit-validation.csv` preserve the evidence beside the images.
'@
$frost=Import-Csv "$output/frost-readability.csv"
$snow=$frost | Where-Object surface -eq 'ordinary snow floor'
$ice=$frost | Where-Object surface -eq 'sliding ice'
$lighter=(([double]$snow.luminance/[double]$ice.luminance-1)*100).ToString('F1')
$glossDelta=([double]$ice.gloss-[double]$snow.gloss).ToString('F4')
$v1=Import-Csv "$output/perf-v1.csv"
$joineryNew=$perf | Where-Object { $_.planet -eq 'joinery' -and $_.before -eq 'False' }
$joineryOld=$v1 | Where-Object { $_.planet -eq 'joinery' -and $_.before -eq 'False' }
$joineryExtra=[int]$joineryNew.dressingTris-[int]$joineryOld.dressingTris
$revision=@"

## V2 review fixes: frost rule readability and dressing density

The v1 images remain beside these as *-v1.png, including seven-planets-sheet-v1.png and every before-after-<planet>-v1.png; README-v1.md and the v1 CSVs preserve the previous report.

Ordinary frost floors now read as matte white-grey packed snow / frosted marble with fine grain, without ice cracks, ice rim lighting or specular highlights. The readable wall meshes use the same dry surface with their original dark vertex pigment. Mesh UV2 role tag 7 distinguishes these board surfaces without changing geometry or adding materials; all other planets retain their previous material appearance. The crust remains blue ice with its pigment toned to 62 percent, so the snow board is brighter. The actual sliding-ice material retains its streak lines, has blue ice shading and a fixed roughness of 0.06. A dedicated diagnostic render returns the shader's gloss (1 - roughness) rather than relying on an estimate from a lit highlight.

| Frost surface | Measured linear luminance | Measured gloss |
|---|---:|---:|
| Ordinary snow floor | $($snow.luminance) | $($snow.gloss) |
| Sliding ice tile | $($ice.luminance) | $($ice.gloss) |

The ordinary snow floor is $lighter percent brighter than sliding ice in stage 22; sliding ice gloss is $glossDelta higher on the 0-1 scale. See frost-board-readability.png, frost-gloss-diagnostic.png and frost-readability.csv. Gloss is measured from the same cell-centre patches as luminance, after rendering the gloss diagnostic; the existing sliding ice alpha also composites its background. Snow roughness is 0.98, with specular contribution explicitly disabled.

Every planet now has 13 dressing instances: five original modules, one of those enlarged from scale 1.45 to 2.05 as the hero, and eight extra instances at varied scales and rotations. The hero choices are moss boulder, pumice ring, rune slab, ice cluster, timber frame, dripping stone and menhir ring segment. Extra instances reuse meshes and the existing chapter material, with actual crust raycasts, the 0.20-cell exclusion and projected board/exit clearance still enforced. Joinery adds only $joineryExtra dressing triangles; the all-stage CSV verifies the complete final total remains below 50k on every stage.
"@
$content=$header+"`r`n`r`n"+($table -join "`r`n")+"`r`n"+$revision+"`r`n"+$body
[IO.File]::WriteAllText("$output/README.md",$content,[Text.UTF8Encoding]::new($false))
Write-Output 'README.md written with current measured values.'
