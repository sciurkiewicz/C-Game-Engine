# Dodatkowe warianty trawy — 2026-10-30

Tryb: wbudowane narzędzie `image_gen` (skill imagegen), pięć osobnych wywołań typu style-transfer, `transparent_background: false`. Każda edycja tworzy nowy wariant pod osobną nazwą.

Obrazy wejściowe, w kolejności: `Editor/terrain/grass.png` (cel wariantu i główny wzorzec stylu), `Editor/terrain/grass_calm.png` (pomocniczy wzorzec kreski). Obrazy wejściowe zachowują swoje pliki. Wyniki: osobne PNG w `Editor/terrain/`, 1254 × 1254 px.

Pełny prompt każdego wariantu składa się ze wspólnej specyfikacji zakończonej nowym wierszem, a następnie odpowiedniego `Variant-specific direction`.

## Wspólna specyfikacja

```text
Use case: style-transfer
Asset type: single seamless square grass ground texture PNG, an additional material in the existing fantasy game editor.
Primary request: make a new distinct grass variant from input image 1, in exactly the approved Don't Starve inspired ink drawing style.
Input image 1: edit target and primary style reference (grass.png). Keep the dark scratchy ink language, little tuft scale and overhead ground function; create the requested new grass variant with a fresh natural distribution of tufts.
Input image 2: supporting style reference only (grass_calm.png), showing matching irregular dark ground contours and grouped crosshatching.
Style/medium: hand-drawn 2D game texture with clearly visible near-black and dark-brown angular grass-blade outlines, imperfect varied-weight ink strokes, occasional doubled contours, short expressive crosshatch clusters and muted matte colored fills. Dark whimsical illustrated survival-game look, consistent with both references. Distinct dark ink remains readable at game zoom.
Composition/framing: strictly orthographic top-down flat ground; small grass clumps scattered evenly across the full square at the same approximate scale as the references. Roughly 10 to 14 little tufts across the width. No central focal subject, no large plants.
Lighting: evenly lit flat albedo, no baked cast shadows or directional highlights.
Invariants: preserve the reference drawing style, line weight, material scale and top-down terrain usability. Keep the material identifiable as grass. Make the requested variant visibly distinct through its palette, ground coverage and blade shapes.
Output: one opaque full-bleed square PNG, 1254 x 1254 pixels. Seamless horizontal and vertical tiling with matching material features across opposite edges, evenly distributed brightness and no dark frame.
Avoid: photorealism, 3D render, shiny highlights, soft gouache painting, photographic grain, dense speckled noise, neon colors, gradients, vignette, perspective, scenic view, horizon, characters, large props, big rocks, paths, paper sheet, background margin, outer border, text, logos, watermark, atlas or sample grid.
```

## Trawa leśna — `grass_forest.png`

```text
Variant-specific direction: shaded woodland grass. A cooler darker palette of muted deep moss green, grey forest green and restrained dark sage. Relatively dense little crooked pointed grass clusters, narrow uneven blades, dark ink contours and hatch groups over continuous low turf. A few tiny clover-like ground leaves can merge into the carpet. Clearly grass rather than moss cushions or bushes. No flowers, leaf litter or fallen branches.
```

## Jasna trawa łąkowa — `grass_meadow.png`

```text
Variant-specific direction: fresh short meadow grass. A visibly lighter palette of muted sage green, soft yellow-green and grey spring green, with gentle warm olive variations. Small jaunty pointed tufts and low broad grass patches, strong dark ink blade outlines and little broken hatch groups. Brightest green of this set but still desaturated and handmade, never fluorescent. No flowers, bushes or bare soil patches.
```

## Trawa z drobnymi kwiatami — `grass_flowers.png`

```text
Variant-specific direction: an olive-green meadow grass carpet with a few very tiny wildflowers mixed among the short ink-drawn tufts. Keep at least 95 percent of the surface ordinary grass. Only about 15 to 20 tiny scattered flower heads total, each a few simple cream or dusty muted violet petals with a crooked dark outline and small ochre centre, never a focal bloom. Muted medium olive and moss greens, no large flowers or tall stems. Same ground-level grass scale and strong scratchy black hatching as the references.
```

## Rzadka trawa na ziemi — `grass_sparse.png`

```text
Variant-specific direction: sparse worn grass on exposed soil. About 55 to 65 percent muted dusty umber and grey-brown compact earth showing between small isolated dull olive-green tufts. Irregular clumps have strong crooked dark ink blade contours, the earth has sparse short hatch groups and a few tiny outlined dirt clods. Distribute grass islands evenly without forming a trail or central clearing. No large stones, flowers, dried clay crack polygons or grass borders.
```

## Przywiędła trawa — `grass_withered.png`

```text
Variant-specific direction: withered late-autumn grass, distinctly browner and cooler than golden dry grass. Flattened bent short blades and uneven low tuft clusters in muted grey-umber, dusty straw brown, dull tobacco tan and a little faded olive. Crooked near-black ink outlines, angular bent tips and occasional scratchy crosshatching. A continuous worn grassy carpet with only small hints of grey-brown soil. No orange fallen leaves, flowers, bright golden yellow or bare cracked-earth patches.
```
