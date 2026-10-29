# Tekstury w stylu Don’t Starve

Wygenerowano 2026-10-29 wbudowanym narzędziem `image_gen` (skill imagegen), bez CLI/API. Pięć osobnych edycji typu style-transfer. Każda używała `transparent_background: false` i jednego obrazu wejściowego `Editor/terrain/<nazwa>.png` jako celu edycji. Wyniki zapisano jako `<nazwa>_ink.png`, zachowując wcześniejsze pliki.

Pełny prompt każdej edycji to wspólna specyfikacja poniżej, po której bez dodatkowej pustej linii następuje właściwy `Material-specific direction`.

## Wspólna specyfikacja

```text
Use case: style-transfer
Asset type: single square repeatable top-down terrain texture PNG for a 2D game.
Primary request: redraw the supplied floor texture in the unmistakable hand-drawn visual style of Don't Starve.
Input image 1: edit target; use it ONLY to retain the type of ground material and approximate feature scale. Completely replace its rendering style.
Style/medium: scratchy black and dark-brown pen-and-ink drawing, crooked angular outlines with varied line weight, occasional imperfect doubled contours, sparse grouped crosshatching and quick expressive hatch marks. Dark whimsical gothic survival-game art. Flat, muted, dirty colors inside the ink shapes. Deliberately stylized and asymmetrical, visibly drawn by hand. Strong readable material silhouettes at game zoom.
Composition/framing: perfectly orthographic directly overhead. The material is a continuous flat surface that fills the entire square edge to edge. Spread details and colors evenly with no focal subject.
Lighting: flat diffuse albedo; all dark marks are drawn ink, not cast shadows.
Invariants: preserve the ground material identity, square dimensions, top-down orientation and approximately the feature scale. Transform the entire drawing style, including surface details and edges.
Constraints: seamless tiling in X and Y with matching features across opposite edges; not a finite rectangular patch. Opaque full bleed image, 1254 x 1254 pixels. One texture only.
Avoid: realistic material rendering, painterly gouache strokes, smooth 3D shading, bevels, glossy highlights, photorealistic grain, fine speckled noise, gradients, vignette, dramatic illumination. No scene, characters, props, paper sheet, parchment background, frame, outer border, labels, text, watermark, sample grid, perspective or transparent margin.
```

## `wood_planks_ink.png`

Obraz wejściowy: `Editor/terrain/wood_planks.png`.

```text
Material-specific direction: rustic wooden floorboards. A handful of tall warped planks with crooked black ink seams and staggered joins, angular splits, comically knotted wood grain drawn as loose dark whorls. Dusty desaturated umber and tan flat fills, sparse scratchy hatch groups. No golden polished wood, no nails.
```

## `stone_tiles_ink.png`

Obraz wejściowy: `Editor/terrain/stone_tiles.png`.

```text
Material-specific direction: dungeon flagstones. Uneven angular grey stone slabs with thick sketchy charcoal outlines, charmingly wonky proportions and a few exaggerated hand-drawn cracks. Muted grey-green and dirty taupe flat fills, a few short crosshatched corners. No smooth realistic stone, no ornament.
```

## `brick_paving_ink.png`

Obraz wejściowy: `Editor/terrain/brick_paving.png`.

```text
Material-specific direction: old brick paving. Wobbly narrow rectangular bricks in a staggered bond, uneven hand-inked black outlines, tiny drawn corner chips. Desaturated dirty brick-red and dusty rusty-brown flat fills, sparse scribbled hatching. Clear overhead floor texture, no wall perspective, no realistic mortar grain.
```

## `autumn_leaves_ink.png`

Obraz wejściowy: `Editor/terrain/autumn_leaves.png`.

```text
Material-specific direction: autumn leaf litter. Simplified angular curled oak and beech leaves, sharp crooked black outlines and quick central vein scribbles over a flat dark umber ground. Dirty muted ochre, rusty brown, dull olive, very restrained orange. Leaves form an even all-over surface with sparse crosshatching. No photographic leaf detail, no large twigs or objects.
```

## `cracked_earth_ink.png`

Obraz wejściowy: `Editor/terrain/cracked_earth.png`.

```text
Material-specific direction: dry cracked earth. Wonky polygonal plates of dusty desaturated ochre and grey-brown clay separated by crooked dark ink cracks. Sparse clusters of loose hand-drawn scratch marks and a few simple small stone doodles. Flat 2D shapes, no deep canyons or realistic pebbly noise.
```
