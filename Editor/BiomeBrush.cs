using System.Numerics;

enum BiomeKind { Forest, Meadow, Rocky }
readonly record struct BiomeTerrain(string Id, float Weight);
sealed record BiomeDefinition(string Name, BiomeTerrain[] Terrain, ScatterAsset[] Objects, string SteepTerrain);
sealed class BiomeBrushSettings(float density, float spacing, float variation)
{
    public float Radius { get; set; } = 6;
    public float Density { get; set; } = density;
    public float Spacing { get; set; } = spacing;
    public float Variation { get; set; } = variation;
}

static class Biomes
{
    public static BiomeDefinition Create(BiomeKind kind, IEnumerable<string> terrainIds, IEnumerable<SpriteAsset> sprites)
    {
        var terrain = terrainIds.ToHashSet();
        var all = sprites.Where(s => s.Id.StartsWith("objects/") && s.Id != ChestData.DefaultAsset).OrderBy(s => s.Id).ToArray();
        string Material(string desired, string fallback) => terrain.Contains(desired) ? desired : fallback;
        var rocks = all.Where(s => s.Id.Contains("/rocks/"));
        var plants = all.Where(s => s.Id.Contains("/vegetation/"));
        var trees = plants.Where(s => s.Id.Contains("/trees/")).ToArray();
        var grasses = plants.Where(s => s.Id.Contains("/grass/") || s.Id.Contains("/flowers/")).ToArray();
        var bushes = plants.Where(s => s.Id.Contains("/bushes/")).ToArray();
        var chosen = kind switch
        {
            BiomeKind.Forest => (trees.Length > 0 ? trees.AsEnumerable() : plants).SelectMany(s => Enumerable.Repeat(s, 4))
                .Concat(trees.Length > 0 ? bushes.SelectMany(s => Enumerable.Repeat(s, 2)) : []).Concat(rocks.Take(2)),
            BiomeKind.Meadow => grasses.AsEnumerable(),
            BiomeKind.Rocky => rocks,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        // A small library can still paint materials; forest uses available vegetation when trees are absent.
        if (kind == BiomeKind.Forest && !chosen.Any()) chosen = plants;
        BiomeTerrain[] materials = kind switch
        {
            BiomeKind.Forest => [new(Material("terrain/forest_floor.png", "dirt"), .55f), new(Material("terrain/grass_forest.png", "grass"), .35f), new("dirt", .1f)],
            BiomeKind.Meadow => [new(Material("terrain/grass_meadow.png", "grass"), .55f), new(Material("terrain/grass_flowers.png", "grass"), .3f), new(Material("terrain/grass_sparse.png", "dirt"), .15f)],
            _ => [new("stone", .55f), new(Material("terrain/gravel.png", "dirt"), .3f), new(Material("terrain/grass_sparse.png", "grass"), .15f)]
        };
        return new(kind.ToString(), materials, chosen.Select(s => new ScatterAsset(s.Id, Math.Clamp(s.DefaultHeight, SceneObject.MinHeight, SceneObject.MaxHeight), s.DefaultSolid)).ToArray(), "stone");
    }
}

sealed partial class MapDocument
{
    // The editor owns a single checkpoint for an entire brush stroke.
    public SceneObject[] PaintBiome(Vector3 center, float radius, BiomeDefinition biome, float density, float spacing, float variation, int seed, int scatterSeed)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Z) || !MapData.FiniteRange(radius, 1, 32) ||
            !MapData.FiniteRange(density, 0, .3f) || !MapData.FiniteRange(spacing, .25f, 16) || !MapData.FiniteRange(variation, 0, .75f) ||
            biome.Terrain.Length == 0 || biome.Terrain.Any(t => !ProjectPaths.ValidAssetId(t.Id) || !float.IsFinite(t.Weight) || t.Weight <= 0) ||
            !ProjectPaths.ValidAssetId(biome.SteepTerrain)) throw new ArgumentOutOfRangeException(nameof(radius));
        float cx = center.X + Map.Width / 2f, cz = center.Z + Map.Depth / 2f;
        float total = biome.Terrain.Sum(t => t.Weight);
        for (int z = Math.Max(0, (int)MathF.Floor(cz - radius)); z < Math.Min(Map.Depth, (int)MathF.Ceiling(cz + radius)); z++)
        for (int x = Math.Max(0, (int)MathF.Floor(cx - radius)); x < Math.Min(Map.Width, (int)MathF.Ceiling(cx + radius)); x++)
        {
            var position = new Vector3(x + .5f - Map.Width / 2f, 0, z + .5f - Map.Depth / 2f);
            float distance = Vector2.Distance(new(position.X, position.Z), new(center.X, center.Z));
            if (distance >= radius || Map.WaterDepthAt(position.X, position.Z) > .55f) continue;
            float edge = Math.Clamp((radius - distance) / Math.Max(.75f, radius * .2f), 0, 1);
            float edgeNoise = (StampNoise(position.X * 1.7f + 50, position.Z * 1.7f + 50, seed + 21) + 1) * .5f;
            if (edgeNoise > edge * edge * (3 - 2 * edge)) continue;
            float choice = (StampNoise(position.X * .19f, position.Z * .19f, seed) + 1) * .5f * total;
            string material = biome.Terrain[^1].Id;
            foreach (var entry in biome.Terrain) { choice -= entry.Weight; if (choice <= 0) { material = entry.Id; break; } }
            if (TerrainGeometry.NormalAt(Map, position.X, position.Z).Y < .64f) material = biome.SteepTerrain;
            Paint(position, 0, material);
        }
        int count = Math.Min(256, (int)MathF.Round(MathF.PI * radius * radius * density));
        if (biome.Objects.Length == 0 || count == 0) return [];
        var roads = Map.Roads.Select(r => (Radius: r.Width / 2 + .2f, Curve: r.Curve)).ToArray();
        return ObjectScatter.Place(Map, center, radius, count, spacing, variation, biome.Objects, scatterSeed, true,
            point => !roads.Any(r => RoadGeometry.Distance(point, r.Curve) < r.Radius));
    }
}
