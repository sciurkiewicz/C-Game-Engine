# Ujednolicenie wszystkich tekstur podłoża

Specyfikacja edycji z 2026-10-29. Tryb: wbudowane narzędzie `image_gen`, osobne wywołanie dla każdego materiału, `transparent_background: false`.

Cel każdego wywołania: `Editor/terrain/<nazwa>.png` (obraz 1, wersja sprzed podmiany). Wspólny wzorzec stylu: `Editor/terrain/stone_tiles_ink.png` (obraz 2). Materiały są podmieniane pod dotychczasowymi nazwami, aby zapisane mapy korzystały z ujednoliconego stylu. Istniejące warianty `_ink` zachowują swoje pliki. `stone_tiles.png` korzysta z identycznej kopii zaakceptowanego `stone_tiles_ink.png`.

Pełny prompt każdej edycji to wspólna specyfikacja zakończona nowym wierszem, a następnie właściwy `Material-specific direction`.

## Wspólna specyfikacja

```text
Use case: style-transfer
Asset type: a single square seamless top-down terrain albedo PNG in a consistent game texture library.
Primary request: redraw input image 1 in the same Don't Starve inspired dark pen-and-ink visual style as the approved input image 2.
Input image 1: edit target. Preserve its ground material identity, approximate feature scale and overall material color family.
Input image 2: approved style reference ONLY (stone_tiles_ink.png). Match its dark irregular contours, visible scratchy crosshatching, imperfect doubled strokes, matte flat fills and dark whimsical handmade character. Do not copy its stones or paving layout into other materials.
Style/medium: hand-drawn 2D ink illustration, uneven bold near-black and dark-brown outlines defining the material's natural shapes, sparse expressive grouped hatch marks, crooked angular details, restrained muted flat colors. Clearly visible dark ink at game zoom, as strong as the reference. Redraw the whole surface into the illustrated style rather than merely overlaying a few hairline marks on painterly detail.
Composition: strictly orthographic top-down view of a flat continuous material surface, square and edge-to-edge with evenly distributed detail and no focal subject.
Lighting: diffuse flat albedo, no cast shadows or directional highlights.
Invariants: retain the material identity and color family of input 1; match the ink style and line contrast of input 2; keep the texture usable as flat terrain.
Output: one opaque square PNG, 1254 x 1254 pixels. Seamless horizontal and vertical tiling: opposite edges must join naturally with continuous material shapes, no frame or edge darkening.
Avoid: realism, 3D rendering, polished bevels, photographic grain, dense speckled noise, soft painterly brushwork, gradients, vignette, neon colors, strong illumination, perspective, scene, props, characters, lettering, watermark, grid of samples, parchment sheet, paper backdrop, borders or transparent margins.
```

## `cobblestone.png`

```text
Material-specific direction: irregular rounded polygonal cobblestones in dusty grey and taupe with a little muted sage. About 9 to 11 stones across. Bold crooked dark ink borders around each stone and deep inked joints, small angular chips and sparse diagonal crosshatch groups. Uneven natural paving, not rectangular bricks or a wall.
```

## `dirt.png`

```text
Material-specific direction: compact brown dirt path ground. Muted warm umber and dusty ochre flat earth patches with dark scratchy soil contours, short loose grouped hatch strokes and occasional tiny black-outlined pebbles. Most of the texture is fine compact soil, not large rocks or a network of dried clay polygons.
```

## `grass.png`

```text
Material-specific direction: green grass ground, retaining a recognizably fresh natural green family but with more muted olive and moss-green flat fills. Small irregular grass clusters defined by strong scratchy near-black pointed blade outlines and grouped hatches, evenly scattered in a quiet green ground carpet. Around 10 to 14 small tufts across. No large plants, flowers, stones or huge blades.
```

## `stone.png`

```text
Material-specific direction: broad natural bedrock ground in grey-brown and muted neutral grey. Uneven connected flat stone plates with jagged dark hand-inked cracks, chipped contours, sparse diagonal hatch clusters and a few tiny angular stone fragments. Geological continuous bedrock, not laid paving or ochre cracked clay.
```

## `grass_calm.png`

```text
Material-specific direction: calm low-contrast short grass, subdued medium olive and moss-green flat fills with gently varied patches. Clear dark irregular ink outlines on small low grass tufts and sparse crosshatch groups; leave more breathing room between tufts than on normal grass. Still distinctly black-inked, not blurred or unoutlined. No flowers, rocks or large plants.
```

## `grass_dry.png`

```text
Material-specific direction: dry meadow grass, dusty straw ochre, desaturated tan and dull olive flat fills. Crooked short straw tufts and bent pointed blades traced with strong dark brown-black ink and loose scratchy hatching. About 12 small clusters across, even all-over density. No flowers, stones or bright yellow glow.
```

## `moss.png`

```text
Material-specific direction: soft low moss carpet, muted deep forest green and dull olive cushions. Flatten the small lobed cushion shapes into hand-drawn matte colored patches with wobbly dark ink scalloped contours, a few short curly strokes and little crosshatch groups. Make it clearly moss, not grass blades, bushes or rocks.
```

## `forest_floor.png`

```text
Material-specific direction: dark woodland floor, muted umber earth with small scattered brown and ochre decaying leaves, thin pine-needle groups and tiny twig fragments. Draw leaves and needles in irregular dark ink outlines with quick veins and scratchy hatching; use quiet flat earthy fills. Keep the scattered fine-scale sparse litter character and visible soil, not a solid thick orange autumn-leaf blanket.
```

## `mud.png`

```text
Material-specific direction: damp soft mud in dark muted brown and grey-umber. Flat irregular rounded wet clay swirls and shallow puddle rims outlined with loose dark ink contours, occasional short crosshatch groups and small smeared soil marks. Matte stylized damp ground, not glossy photorealistic reflections, dried polygon cracks, stones, footprints or wheel tracks.
```

## `gravel.png`

```text
Material-specific direction: loose gravel of many small irregular angular pebbles in dusty grey, taupe, muted tan and a few dull ochre pieces. Each readable pebble has a crooked near-black outline and occasional small hatch marks, scattered over dull brown soil. About 18 to 24 small pebbles across. Keep pebbles much smaller than cobblestones, not large laid paving.
```

## `sand.png`

```text
Material-specific direction: dry sand in muted sandy ochre and warm tan flat fills. Short dark hand-inked wavy wind-ripple contours, small grouped curved hatch strokes, sparse scratchy grain marks, evenly spaced without a focal dune. Calm sand surface with clear dark drawing; no stones, footprints, vegetation or huge dunes.
```

## `sand_wet.png`

```text
Material-specific direction: wet shore sand in darker muted cool taupe and grey-brown flat fills. Loose near-black curved tidal-ripple lines and broken shallow wash contours, small grouped scratchy hatches, subtle restrained tonal variation. Keep it distinct from warm dry sand. No bright reflections, standing water, shells, footprints or vegetation.
```

## `snow.png`

```text
Material-specific direction: snowy ground with off-white and pale desaturated blue-grey flat fills. Shallow irregular soft snow drifts defined by broken dark charcoal ink contours and sparse short scratchy hatch clusters along their depressions. Keep broad light areas so it reads as snow but ensure the same visible hand-drawn ink language as the style reference. No snowflake icons, footprints, rocks, ice blocks or black fill areas.
```

## `water.png`

```text
Material-specific direction: a repeatable flat water surface, muted slate-blue and dull blue-green flat fills with dark hand-drawn little ripple arcs, crooked short wave contours and sparse curved crosshatch groups evenly distributed. The tiny original is a material identity and color reference; redraw at full requested 1254 x 1254 size. It was fully opaque, so retain an opaque background. No shore, foam, rocks, fish, sky reflection or ocean scene.
```
