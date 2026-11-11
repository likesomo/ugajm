# BoardShapeA planetary modeling review

Reviewed 2026-11-11 against the approved R2 stone pass. The Blender-authored low-poly environment modules use retained high-poly sources and the shared color/AO plus normal/roughness atlas. Unity captures use the orthographic review camera at pitch 50°, yaw 0°.

## Planet finishes

| Planet | Stone and silhouette | Added / clarified modeling |
| --- | --- | --- |
| moss | Pale gray granite, crystalline grain, deep cushion moss, rough fractured ledge | Preserved the approved R2 base; shared granite and curb modules remain reusable. |
| hollow | Purple-gray vesicular basalt, rounded crater crust, silver-bronze edge hardware | Staggered crater bowls with chipped lips, three deeper vents, embedded side arches, and a five-point star opening. |
| carved | Red ochre sandstone, stepped exposed beds | Nine discontinuous beds with irregular thickness, pinch-outs and cross-bed relief; enlarged pale-quartz III curb strokes. |
| frost | White-blue marble and quartz, no moss | Densified perimeter and underside crystal clusters; fixed the plain curb UV source slot so it samples frost stone rather than legacy teal/terracotta swatches. |
| joinery | Dark blue-gray slate with thin laminations; warm oak and bronze | Added a low-poly laminated slate cliff module, reduced trestle frequency for the triangle budget, and retained oak mortise-tenon frames and wedge hardware. |
| crumble | Golden limestone/travertine, porous fractured ledge | Shifted the stone atlas toward #D2A85E and spread front debris into wider gaps. |
| altar | Golden-gray sarsen-like sandstone, high free-standing menhir ring | Kept the ring upright without capstones; reduced central tile mottling while preserving edge weathering. |

## Performance

Counts include the clean board environment and all floor tiles. Environment draw count is the combined environment renderer count; material count is four for every planet.

| Planet | 8×6 | 12×5 | 10×3 | 7×7 | Max |
| --- | ---: | ---: | ---: | ---: | ---: |
| moss | 16,840 | 20,179 | 15,451 | 16,913 | 20,179 |
| hollow | 25,768 | 27,868 | 24,500 | 25,840 | 27,868 |
| carved | 20,624 | 24,448 | 19,544 | 20,696 | 24,448 |
| frost | 16,288 | 19,090 | 15,148 | 16,340 | 19,090 |
| joinery | 25,367 | 31,830 | 23,325 | 25,439 | 31,830 |
| crumble | 16,540 | 19,622 | 15,400 | 16,292 | 19,622 |
| altar | 16,105 | 18,907 | 14,965 | 16,157 | 18,907 |

The largest clean board is joinery 12×5 at 31,830 tris. All 28 chapter/board-size combinations pass the 50k build validator, preserve one environment draw, maintain one-cell perimeter clearance and pass repeated-fit checks. The builder also confirms existing art GUID stability.

## Review artifacts

- Unity captures, per-planet R2/Unity/stone triptychs, and seven-planet contact sheet: C:/unityjam/shared-assets/seven-steps/concepts/themes/stage-resources-2026-11-10/planet-modeling-review/
- The review page and 7-way comparison image are in that directory; the R2 concept index links to the page.
- Exported FBX, atlas, and Blender source binaries remain under the shared-assets tree and are not committed.
