# Prompty dodatkowych tekstur podłoża

Wygenerowano 2026-10-29 wbudowanym narzędziem `image_gen` (skill imagegen), bez CLI/API. Każdy materiał powstał w osobnym wywołaniu. Pliki PNG mają 1254 × 1254 piksele i są wczytywane automatycznie z tego katalogu.

Pełny prompt każdego materiału to wspólna specyfikacja poniżej, po której bez dodatkowej pustej linii następuje właściwy `Primary request`.

## Wspólna specyfikacja

```text
Use case: stylized-concept
Asset type: a single square seamless tiling ground albedo PNG for an existing fantasy game editor.
Style/medium: hand-painted fantasy RPG environment texture, soft gouache-like brushwork, small chipped angular painted details, moderately muted natural colors, gentle contrast, matching a game with painterly green grass and grey-brown cobblestones.
Composition/framing: strict orthographic top-down view, flat material filling the entire square image edge to edge at a consistent scale.
Lighting: even diffuse ambient light, no directional lighting baked in.
Constraints: seamless repeat horizontally and vertically; opposite edges must join naturally with matching material features. Uniform detail density and brightness across the image. Only one material image, no atlas, no grid of samples, no border, no text, no watermark, no objects, no perspective, no horizon, no vignette, no strong shadows or dramatic bright spots. Opaque background. Create a square 1254 by 1254 pixel texture if supported.
```

## Deski dębowe — `wood_planks.png`

```text
Primary request: worn warm medium-brown oak floorboards for a rustic fantasy cottage or wooden walkway. Parallel vertical planks of slightly varying widths, staggered end joints, restrained painted wood grain and occasional tiny knots, narrow dark seams, gently worn edges. Approximately 9 planks across the square; plausible wood without nails or metal fittings.
```

## Kamienne płyty — `stone_tiles.png`

```text
Primary request: old grey slate flagstone flooring for a castle courtyard or dungeon. Moderately sized roughly rectangular stone slabs in a staggered arrangement, cool neutral grey with restrained sage and taupe variation, subtle chips and hairline wear, narrow dusty grout. About 6 slabs across the square. The surface is calm and readable, not a wall, no symbols or ornament.
```

## Ceglany bruk — `brick_paving.png`

```text
Primary request: weathered reddish terracotta brick paving for village paths and courtyards. Small rectangular bricks in a regular running bond, soft muted red-brown clay with restrained ochre variation, rounded worn corners, fine sandy beige mortar joints. Around 10 bricks across the square, matte painted surface, no moss clumps or grass.
```

## Jesienna ściółka — `autumn_leaves.png`

```text
Primary request: autumn woodland ground covered with many small fallen oak and beech leaves in muted burnt orange, warm ochre, chestnut brown and sparse dull olive over earthy brown soil. Leaf clusters distributed evenly in a natural random arrangement, partly decomposed leaf fragments and tiny twig details. Fine-scale low relief ground cover, about 25 individual leaves across the square, no single focal leaf, no large sticks, no rocks, no mushrooms.
```

## Spękana ziemia — `cracked_earth.png`

```text
Primary request: dry cracked ochre-brown clay ground for parched paths and dry riverbeds. A natural irregular network of thin darker cracks subdividing calm flat polygonal earth patches, dusty warm tan with subtle muted orange variation, fine painted granular detail and a few very tiny stones. Roughly 14 earth plates across the square. Cracks remain shallow and restrained, no deep chasms, no dramatic relief, no plants.
```


