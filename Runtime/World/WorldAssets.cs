using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed record SpriteAsset(string Id, Texture2D Texture)
{
    public string Name => Path.GetFileNameWithoutExtension(Id).Replace('_', ' ');
    public string Category => Id.StartsWith("objects/") ? Id.Split('/')[1] : "characters";
    public float DefaultHeight => Id.Contains("/trees/") ? 5 : Id.Contains("/grass/") ? .9f : Id.Contains("/monuments/") ? 3 : 1.8f;
    public bool DefaultSolid => !Id.Contains("/grass/") && !Id.Contains("/effects/");
}

sealed class WorldAssets : IDisposable
{
    public Dictionary<string, SpriteAsset> Sprites { get; } = [];
    public Dictionary<string, Texture2D> Terrain { get; } = [];
    public List<string> Warnings { get; } = [];
    public WorldAssets()
    {
        foreach (var directory in new[] { "objects", "characters", "terrain" })
        {
            string root = Path.Combine(ProjectPaths.AssetRoot, directory);
            if (!Directory.Exists(root)) continue;
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(p => Path.GetExtension(p).Equals(".png", StringComparison.OrdinalIgnoreCase)).Order())
            {
                var img = LoadImage(path);
                if (img.Width <= 0 || img.Height <= 0) { Warnings.Add(Path.GetFileName(path)); continue; }
                if (directory != "terrain") ImageAlphaCrop(ref img, .02f);
                if (Math.Max(img.Width, img.Height) > 768)
                {
                    float scale = 768f / Math.Max(img.Width, img.Height);
                    ImageResize(ref img, Math.Max(1, (int)(img.Width * scale)), Math.Max(1, (int)(img.Height * scale)));
                }
                var texture = LoadTextureFromImage(img);
                UnloadImage(img);
                if (!IsTextureValid(texture)) { Warnings.Add(Path.GetFileName(path)); continue; }
                SetTextureFilter(texture, TextureFilter.Bilinear);
                string id = Path.GetRelativePath(ProjectPaths.AssetRoot, path).Replace('\\', '/');
                if (directory == "terrain") Terrain.Add(id, texture);
                else Sprites.Add(id, new(id, texture));
            }
        }
        // Small repeatable bitmap materials until ground textures are supplied.
        var colors = new Dictionary<string, Color>
        {
            ["grass"] = new(87, 108, 72, 255), ["dirt"] = new(139, 112, 78, 255),
            ["sand"] = new(190, 175, 123, 255), ["stone"] = new(108, 119, 129, 255),
            ["water"] = new(58, 108, 138, 255)
        };
        foreach (var (name, color) in colors)
        {
            var img = GenImageColor(64, 64, color);
            var random = new Random(42);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int noise = random.Next(-12, 13);
                    if (name == "water") noise += (int)(MathF.Sin(y * .6f + x * .15f) * 6);
                    ImageDrawPixel(ref img, x, y, new Color(color.R + noise, color.G + noise, color.B + noise, 255));
                }
            Terrain[name] = LoadTextureFromImage(img);
            UnloadImage(img);
        }
    }
    public void Dispose()
    {
        foreach (var sprite in Sprites.Values) UnloadTexture(sprite.Texture);
        foreach (var texture in Terrain.Values) UnloadTexture(texture);
    }
}

sealed class WorldRenderer(WorldAssets assets)
{
    public static void Screenshot(string path)
    {
        var image = LoadImageFromScreen();
        try { if (!ExportImage(image, path)) throw new IOException("Could not save screenshot: " + path); }
        finally { UnloadImage(image); }
    }
    public void Draw(MapData map, Camera3D camera, bool grid, Vector3? playerPosition = null)
    {
        DrawTerrain(map);
        if (grid)
        {
            for (int x = 0; x <= map.Width; x++)
                DrawLine3D(new(x - map.Width / 2f, .015f, -map.Depth / 2f), new(x - map.Width / 2f, .015f, map.Depth / 2f), new Color(220, 230, 220, x % 5 == 0 ? 95 : 35));
            for (int z = 0; z <= map.Depth; z++)
                DrawLine3D(new(-map.Width / 2f, .015f, z - map.Depth / 2f), new(map.Width / 2f, .015f, z - map.Depth / 2f), new Color(220, 230, 220, z % 5 == 0 ? 95 : 35));
        }
        Vector3 player = playerPosition ?? map.Player.Position;
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var items = map.Objects.Select(o => (o.Asset, o.Position, o.Height, Player: false))
            .Append((map.Player.Asset ?? "", player, map.Player.Height, true))
            .OrderByDescending(o => Vector3.Dot(o.Item2 - camera.Position, forward));
        foreach (var item in items)
            DrawSprite(camera, item.Item1, item.Item2, item.Item3, item.Item4 ? new Color(80, 183, 255, 255) : Color.Magenta);
    }
    void DrawTerrain(MapData map)
    {
        foreach (var group in map.Tiles.Select((id, index) => (id, index)).GroupBy(t => t.id))
        {
            var texture = assets.Terrain.GetValueOrDefault(group.Key, assets.Terrain["grass"]);
            Rlgl.SetTexture(texture.Id);
            Rlgl.Begin((int)DrawMode.Quads);
            Rlgl.Color4ub(255, 255, 255, 255);
            Rlgl.Normal3f(0, 1, 0);
            foreach (var tile in group)
            {
                float x = tile.index % map.Width - map.Width / 2f, z = tile.index / map.Width - map.Depth / 2f;
                Rlgl.TexCoord2f(0, 0); Rlgl.Vertex3f(x, 0, z);
                Rlgl.TexCoord2f(0, 1); Rlgl.Vertex3f(x, 0, z + 1);
                Rlgl.TexCoord2f(1, 1); Rlgl.Vertex3f(x + 1, 0, z + 1);
                Rlgl.TexCoord2f(1, 0); Rlgl.Vertex3f(x + 1, 0, z);
            }
            Rlgl.End();
            Rlgl.SetTexture(0);
        }
    }
    public void DrawSprite(Camera3D camera, string id, Vector3 position, float height, Color fallback, byte alpha = 255)
    {
        var quad = GetSpriteQuad(camera, id, position, height);
        if (assets.Sprites.TryGetValue(id, out var asset))
        {
            Rlgl.SetTexture(asset.Texture.Id);
            Rlgl.Begin((int)DrawMode.Quads);
            Rlgl.Color4ub(255, 255, 255, alpha);
            var normal = quad.Normal;
            Rlgl.Normal3f(normal.X, normal.Y, normal.Z);
            Vertex(quad.BottomLeft, 0, 1);
            Vertex(quad.BottomRight, 1, 1);
            Vertex(quad.TopRight, 1, 0);
            Vertex(quad.TopLeft, 0, 0);
            Rlgl.End();
            Rlgl.SetTexture(0);
        }
        else
        {
            fallback.A = Math.Min(fallback.A, alpha);
            DrawTriangle3D(quad.BottomLeft, quad.BottomRight, quad.TopRight, fallback);
            DrawTriangle3D(quad.BottomLeft, quad.TopRight, quad.TopLeft, fallback);
        }
    }
    static void Vertex(Vector3 point, float u, float v)
    {
        Rlgl.TexCoord2f(u, v);
        Rlgl.Vertex3f(point.X, point.Y, point.Z);
    }
    public SpriteQuad GetSpriteQuad(Camera3D camera, string id, Vector3 position, float height)
    {
        float width = assets.Sprites.TryGetValue(id, out var asset) ? height * asset.Texture.Width / asset.Texture.Height : height * .6f;
        return BillboardGeometry.Create(camera, position, width, height);
    }
    public void DrawOutline(Camera3D camera, SceneObject item, Color color)
    {
        var quad = GetSpriteQuad(camera, item.Asset, item.Position, item.Height);
        var offset = quad.Normal * .01f;
        DrawLine3D(quad.BottomLeft + offset, quad.BottomRight + offset, color);
        DrawLine3D(quad.BottomRight + offset, quad.TopRight + offset, color);
        DrawLine3D(quad.TopRight + offset, quad.TopLeft + offset, color);
        DrawLine3D(quad.TopLeft + offset, quad.BottomLeft + offset, color);
    }
    public SceneObject? Pick(MapData map, Camera3D camera, Ray ray)
    {
        SceneObject? result = null; float closest = float.MaxValue;
        foreach (var item in map.Objects)
        {
            var quad = GetSpriteQuad(camera, item.Asset, item.Position, item.Height);
            var hit = GetRayCollisionQuad(ray, quad.BottomLeft, quad.BottomRight, quad.TopRight, quad.TopLeft);
            if (hit.Hit && hit.Distance < closest) { closest = hit.Distance; result = item; }
        }
        return result;
    }
    public static Vector3? GroundPoint(Ray ray)
    {
        if (Math.Abs(ray.Direction.Y) < .0001f) return null;
        float t = -ray.Position.Y / ray.Direction.Y;
        return t >= 0 ? ray.Position + ray.Direction * t : null;
    }
    public static float CollisionRadius(SceneObject item) => Math.Clamp(item.Height * .13f, .2f, .8f);
    public static bool CanWalk(MapData map, Vector3 p) => !map.Objects.Any(o => o.Solid &&
        Vector2.Distance(new(p.X, p.Z), new(o.X, o.Z)) < CollisionRadius(o) + .25f);
}
