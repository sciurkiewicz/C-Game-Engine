# Miejskie tekstury podłoża — 2026-10-30

Tryb: wbudowane narzędzie `image_gen` (skill imagegen), sześć osobnych wywołań typu style-transfer, `transparent_background: false`. Każde wywołanie tworzy nowy materiał pod osobną nazwą; obrazy wejściowe zachowują swoje pliki.

Wyniki: osobne PNG 1254 × 1254 px w `Editor/terrain/`. Pełny prompt każdego materiału składa się ze wspólnej specyfikacji zakończonej nowym wierszem, a następnie odpowiedniego `Variant-specific direction`.

## Wspólna specyfikacja

```text
Use case: style-transfer
Asset type: single seamless square city ground texture PNG for the existing game editor.
Primary request: turn input image 1 into a new city paving material specified below, preserving the approved Don't Starve inspired dark pen-and-ink art direction.
Input image 1: edit target for a new sibling material. Redraw its surface arrangement and material according to the variant direction; keep its square flat terrain function and handmade ink rendering.
Input image 2: supporting approved style reference ONLY. Match the scratchy near-black contours, crooked varied-weight lines, sparse diagonal crosshatch clusters, irregular chipped corners and matte muted colored fills. Do not copy its paving arrangement or color into the requested new material.
Style/medium: visibly hand-drawn 2D ink game texture, dark whimsical survival-game look, imperfect doubled outlines and expressive short hatches. Dark outlines should be as bold and readable as the references at game zoom. Keep surface detail restrained so roads and courtyards remain readable.
Composition/framing: strictly orthographic directly overhead view of a flat continuous ground surface. Material fills the full square edge to edge at uniform scale and detail density. No focal motif or central subject.
Lighting: evenly lit flat albedo, no cast shadows, no directional highlights.
Invariants: preserve the approved dark ink line language and terrain usability; use the requested material and arrangement rather than preserving the old pattern.
Output: one opaque full-bleed PNG, 1254 x 1254 pixels. Seamless horizontal and vertical repeat with matching opposite edges, continuous paving joints and even brightness.
Avoid: photorealism, realistic 3D stone relief, raised bevels, glossy highlights, dense photographic grain, smooth painterly gouache, neon colors, vignette, gradients, scene perspective, horizon, buildings, characters, large props, central medallion, road markings, manholes, curbs, borders, paper sheet, transparent margins, text, watermark, logos, atlas or sample grid.
```

## Płyty chodnikowe — `city_sidewalk.png`

Obrazy wejściowe w kolejności: `Editor/terrain/stone_tiles_ink.png` (cel edycji), `Editor/terrain/cobblestone.png` (pomocniczy wzorzec stylu).

```text
Variant-specific direction: old square concrete sidewalk slabs, about five slabs across, laid in a straight aligned square grid. Pale muted warm-grey and chalky stone-beige flat fills, narrow dark uneven joints, slightly chipped edges, occasional hairline drawn cracks and sparse crosshatch clusters near corners. Clearly broad plain concrete squares, distinct from irregular medieval flagstones. No grass, moss, brickwork or decorative patterns.
```

## Kostka granitowa — `city_granite_setts.png`

Obrazy wejściowe w kolejności: `Editor/terrain/cobblestone.png` (cel edycji), `Editor/terrain/stone_tiles_ink.png` (pomocniczy wzorzec stylu).

```text
Variant-specific direction: a city street paved in many small roughly square granite setts, about 13 to 16 stones across, arranged in gently wonky staggered straight rows. Muted cool blue-grey, neutral grey and occasional taupe flat fills. Strong crooked dark ink joints, little chipped corners and sparse expressive hatch groups. Stones much smaller and more regular than the source cobblestones. No fans, large slabs, grass or ornament.
```

## Bruk wachlarzowy — `city_fan_paving.png`

Obrazy wejściowe w kolejności: `Editor/terrain/cobblestone.png` (cel edycji), `Editor/terrain/stone_tiles_ink.png` (pomocniczy wzorzec stylu).

```text
Variant-specific direction: traditional small granite cobblestones laid in repeated interlocking fan-shaped arcs, the curved rows spanning and continuing across the whole texture. Several fan motifs repeat evenly; small stones about 15 to 20 across. Muted neutral grey, dusty taupe and a few darker grey pieces. Scratchy near-black outlines, occasional crosshatched stone faces. A practical city square paving pattern, not a single circular medallion, radial centrepiece, mosaic picture or decorative border.
```

## Cegła w jodełkę — `city_brick_herringbone.png`

Obrazy wejściowe w kolejności: `Editor/terrain/brick_paving_ink.png` (cel edycji), `Editor/terrain/stone_tiles_ink.png` (pomocniczy wzorzec stylu).

```text
Variant-specific direction: worn clay paving bricks laid in a continuous interlocking 90-degree herringbone, the overall zigzag pattern running diagonally across the square. Narrow rectangular bricks in alternating perpendicular directions, about 12 brick lengths across. Muted rusty terracotta, earthy red-brown, dusty ochre and dirty beige joints. Crooked dark ink outlines, little corner chips and sparse hatch clusters. Change the original horizontal running-bond pattern fully into herringbone. No long parallel horizontal rows or grass.
```

## Kocie łby — `city_cobbles_round.png`

Obrazy wejściowe w kolejności: `Editor/terrain/cobblestone.png` (cel edycji), `Editor/terrain/stone_tiles_ink.png` (pomocniczy wzorzec stylu).

```text
Variant-specific direction: old town pavement of rounded smooth river cobbles, a dense irregular all-over arrangement of small oval and rounded egg-shaped stone faces, about 12 to 16 cobbles across. Dusty muted grey, grey-brown, taupe and a few dull beige fills; dark winding ink seams, scratchy stone contour lines and occasional short curved hatches. Clearly more rounded and smaller than the angular source cobblestones. Flat illustrated stone faces, no 3D pebble shadows, orderly rectangular rows, grass or large bare-soil gaps.
```

## Popękany asfalt — `city_asphalt_cracked.png`

Obrazy wejściowe w kolejności: `Editor/terrain/stone_tiles_ink.png` (cel edycji), `Editor/terrain/cobblestone.png` (pomocniczy wzorzec stylu).

```text
Variant-specific direction: aged urban asphalt surface, a continuous matte muted charcoal-grey and dusty slate-grey ground with a sparse irregular network of jagged near-black ink cracks and occasional short scratchy crosshatch groups. Keep most of the surface calm grey with only a few tiny hand-drawn aggregate flecks. Cracks must read against the grey, no pitch-black overall fill. Entirely replace the source paving slabs with continuous asphalt: no stone faces or tile joints, no exposed brown clay, road markings, potholes, weeds, painted lines, litter or street fixtures.
```
