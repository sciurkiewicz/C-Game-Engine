using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// A cached, bounded coverage texture gives curves soft edges without changing terrain geometry.
sealed class RoadMaterialMask : IDisposable
{
    public Texture2D Texture { get; private set; }
    public Vector4 Window { get; private set; }
    MapData? currentMap;
    TerrainBounds currentBounds;
    int revision;
    uint atlasId;
    public int BuildCount { get; private set; }
    public static Color[] Rasterize(MapData map, Vector2 origin, int width, int height, float scale, IReadOnlyDictionary<string, int> materials)
    {
        var pixels = new Color[width * height];
        foreach (var road in map.Roads)
        {
            int material = materials.GetValueOrDefault(road.Material, materials["dirt"]);
            var curve = road.Curve;
            float radius = road.Width / 2, padding = radius + 1 / scale;
            for (int i = 1; i < curve.Length; i++)
            {
                var a = curve[i - 1]; var b = curve[i];
                int minX = Math.Max(0, (int)MathF.Floor((Math.Min(a.X, b.X) - padding - origin.X) * scale));
                int minZ = Math.Max(0, (int)MathF.Floor((Math.Min(a.Y, b.Y) - padding - origin.Y) * scale));
                int maxX = Math.Min(width - 1, (int)MathF.Ceiling((Math.Max(a.X, b.X) + padding - origin.X) * scale));
                int maxZ = Math.Min(height - 1, (int)MathF.Ceiling((Math.Max(a.Y, b.Y) + padding - origin.Y) * scale));
                for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = RoadGeometry.SegmentDistance(origin + new Vector2((x + .5f) / scale, (z + .5f) / scale), a, b);
                    if (distance > padding) continue;
                    byte alpha = (byte)MathF.Round(RoadGeometry.Coverage(distance, road.Width, road.Softness) * 255);
                    int pixel = z * width + x;
                    // Transparent padding carries the same material ID for bilinear edge samples.
                    if (alpha < pixels[pixel].A) continue;
                    pixels[pixel] = new(material & 255, (material >> 8) & 255, (material >> 16) & 255, alpha);
                }
            }
        }
        return pixels;
    }
    public void Update(MapData map, TerrainBounds visible, IReadOnlyDictionary<string, int> materials, uint atlas)
    {
        if (map.Roads.Count == 0)
        {
            Window = Vector4.Zero;
            if (Texture.Id == 0)
            {
                var empty = GenImageColor(1, 1, Color.Blank);
                try { Texture = LoadTextureFromImage(empty); } finally { UnloadImage(empty); }
            }
            currentMap = null;
            return;
        }
        const int chunk = MapData.TerrainChunkSize;
        var bounds = new TerrainBounds(Math.Max(0, visible.MinX / chunk * chunk - chunk), Math.Max(0, visible.MinZ / chunk * chunk - chunk),
            Math.Min(map.Width, (visible.MaxX + chunk - 1) / chunk * chunk + chunk), Math.Min(map.Depth, (visible.MaxZ + chunk - 1) / chunk * chunk + chunk));
        int version = RoadGeometry.Version(map.Roads);
        if (ReferenceEquals(currentMap, map) && bounds == currentBounds && version == revision && atlasId == atlas) return;
        float scale = Math.Min(4, 1024f / Math.Max(bounds.MaxX - bounds.MinX, bounds.MaxZ - bounds.MinZ));
        int width = Math.Max(1, (int)MathF.Ceiling((bounds.MaxX - bounds.MinX) * scale));
        int height = Math.Max(1, (int)MathF.Ceiling((bounds.MaxZ - bounds.MinZ) * scale));
        var origin = new Vector2(bounds.MinX - map.Width / 2f, bounds.MinZ - map.Depth / 2f);
        var pixels = Rasterize(map, origin, width, height, scale, materials);
        if (Texture.Id != 0 && Texture.Width == width && Texture.Height == height) UpdateTexture(Texture, pixels);
        else
        {
            var image = GenImageColor(width, height, Color.Blank);
            try
            {
                if (Texture.Id != 0) UnloadTexture(Texture);
                Texture = LoadTextureFromImage(image); UpdateTexture(Texture, pixels); SetTextureFilter(Texture, TextureFilter.Bilinear);
                SetTextureWrap(Texture, TextureWrap.Clamp);
            }
            finally { UnloadImage(image); }
        }
        Window = new(bounds.MinX, bounds.MinZ, scale, 1);
        currentMap = map; currentBounds = bounds; revision = version; atlasId = atlas; BuildCount++;
    }
    public void Dispose() { if (Texture.Id != 0) UnloadTexture(Texture); }
}
