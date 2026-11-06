using System.Numerics;

enum TerrainStampShape { Hill, Mountain, Ridge, Crater }

sealed partial class MapDocument
{
    // A stamp adds a complete shape once per click. The caller makes one checkpoint.
    public void StampTerrain(Vector3 center, float radius, float height, TerrainStampShape shape, int seed, float roughness = .3f)
    {
        if (!float.IsFinite(radius) || radius < 1 || radius > 64 || !float.IsFinite(height) || Math.Abs(height) > 32 ||
            !Enum.IsDefined(shape) || !float.IsFinite(roughness) || roughness < 0 || roughness > 1)
            throw new ArgumentOutOfRangeException(nameof(radius));
        var random = new Random(seed);
        float angle = random.NextSingle() * MathF.Tau, cos = MathF.Cos(angle), sin = MathF.Sin(angle);
        float offsetX = random.NextSingle() * 100, offsetZ = random.NextSingle() * 100;
        float cx = center.X + Map.Width / 2f, cz = center.Z + Map.Depth / 2f;
        for (int z = Math.Max(0, (int)MathF.Floor(cz - radius)); z <= Math.Min(Map.Depth, (int)MathF.Ceiling(cz + radius)); z++)
        for (int x = Math.Max(0, (int)MathF.Floor(cx - radius)); x <= Math.Min(Map.Width, (int)MathF.Ceiling(cx + radius)); x++)
        {
            float dx = (x - cx) / radius, dz = (z - cz) / radius;
            float u = dx * cos - dz * sin, v = dx * sin + dz * cos;
            float distance = MathF.Sqrt(u * u + v * v);
            if (distance >= 1) continue;
            float envelope = (1 - distance * distance) * (1 - distance * distance);
            float noise = StampNoise(u * 3 + offsetX, v * 3 + offsetZ, seed) * .7f +
                StampNoise(u * 7 + offsetX, v * 7 + offsetZ, seed + 1) * .3f;
            float profile = shape switch
            {
                TerrainStampShape.Hill => envelope,
                TerrainStampShape.Mountain => MathF.Pow(1 - distance, 1.5f) * (1 + .22f * MathF.Sin(MathF.Atan2(v, u) * 5 + distance * 6) * distance),
                TerrainStampShape.Ridge => MathF.Exp(-u * u * 16) * envelope,
                TerrainStampShape.Crater => (MathF.Exp(-MathF.Pow((distance - .6f) / .16f, 2)) * .65f -
                    MathF.Exp(-distance * distance * 12)) * envelope,
                _ => 0
            };
            ChangeHeight(x, z, Map.VertexHeight(x, z) + height * profile * (1 + noise * roughness));
        }
    }
    static float StampNoise(float x, float z, int seed)
    {
        int ix = (int)MathF.Floor(x), iz = (int)MathF.Floor(z);
        float u = x - ix, v = z - iz;
        u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
        float Hash(int a, int b)
        {
            uint value = unchecked((uint)(a * 374761393 + b * 668265263 + seed * 1274126177));
            value = (value ^ (value >> 13)) * 1274126177u;
            return ((value ^ (value >> 16)) & 0xffff) / 32767.5f - 1;
        }
        float a = Hash(ix, iz), b = Hash(ix + 1, iz), c = Hash(ix, iz + 1), d = Hash(ix + 1, iz + 1);
        return (a + (b - a) * u) * (1 - v) + (c + (d - c) * u) * v;
    }
}

readonly record struct ScatterAsset(string Id, float Height, bool Solid);

static class ObjectScatter
{
    // Sampling depends on stroke distance and seed, never on frame rate.
    public static SceneObject[] Place(MapData map, Vector3 center, float radius, int count, float spacing,
        float scaleVariation, IReadOnlyList<ScatterAsset> assets, int seed, bool avoidPlayer = false, Func<Vector2, bool>? canPlace = null)
    {
        if (!float.IsFinite(radius) || radius < .5f || radius > 32 || count < 1 || count > 256 ||
            !float.IsFinite(spacing) || spacing < .25f || !float.IsFinite(scaleVariation) || scaleVariation < 0 || scaleVariation > .75f ||
            assets.Count == 0 || assets.Any(a => !ProjectPaths.ValidAssetId(a.Id) || !float.IsFinite(a.Height) || a.Height < SceneObject.MinHeight || a.Height > SceneObject.MaxHeight))
            throw new ArgumentOutOfRangeException(nameof(radius));
        var random = new Random(seed);
        var placed = new List<SceneObject>();
        var buckets = new Dictionary<(int X, int Z), List<Vector2>>();
        (int X, int Z) Key(Vector2 point) => ((int)MathF.Floor(point.X / spacing), (int)MathF.Floor(point.Y / spacing));
        void Insert(Vector2 point)
        {
            var key = Key(point);
            if (!buckets.TryGetValue(key, out var bucket)) buckets[key] = bucket = [];
            bucket.Add(point);
        }
        foreach (var entity in map.SceneEntities) Insert(new(entity.X, entity.Z));
        bool Crowded(Vector2 point)
        {
            var key = Key(point);
            for (int z = key.Z - 1; z <= key.Z + 1; z++)
            for (int x = key.X - 1; x <= key.X + 1; x++)
                if (buckets.TryGetValue((x, z), out var bucket) && bucket.Any(p => Vector2.DistanceSquared(p, point) < spacing * spacing)) return true;
            return false;
        }
        for (int attempt = 0; attempt < count * 24 && placed.Count < count && map.Objects.Count < 10000; attempt++)
        {
            float angle = random.NextSingle() * MathF.Tau, distance = MathF.Sqrt(random.NextSingle()) * radius;
            float x = center.X + MathF.Cos(angle) * distance, z = center.Z + MathF.Sin(angle) * distance;
            if (!map.Contains(x, z) || map.WaterDepthAt(x, z) > .55f || TerrainGeometry.NormalAt(map, x, z).Y < .64f ||
                (avoidPlayer && Vector2.DistanceSquared(new(map.Player.X, map.Player.Z), new(x, z)) < (spacing + .5f) * (spacing + .5f)) ||
                Crowded(new(x, z)) || (canPlace != null && !canPlace(new(x, z)))) continue;
            var asset = assets[random.Next(assets.Count)];
            var item = new SceneObject
            {
                Asset = asset.Id, X = x, Z = z, Solid = asset.Solid,
                Height = Math.Clamp(asset.Height * (1 + (random.NextSingle() * 2 - 1) * scaleVariation), SceneObject.MinHeight, SceneObject.MaxHeight)
            };
            map.Objects.Add(item); placed.Add(item); Insert(new(x, z));
        }
        return placed.ToArray();
    }
}
