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
    public Shader SpriteShader { get; }
    TerrainBlending? terrainBlending;
    public TerrainBlending TerrainBlending => terrainBlending ??= new();
    TerrainChunkCache? terrainMeshes;
    public TerrainChunkCache TerrainMeshes => terrainMeshes ??= new();
    public WorldAssets()
    {
        SpriteShader = LoadShaderFromMemory(WorldLighting.VertexShader, WorldLighting.FragmentPrelude + """
            in vec2 fragTexCoord;
            in vec4 fragColor;
            uniform sampler2D texture0;
            uniform vec4 colDiffuse;
            out vec4 finalColor;
            void main()
            {
                vec4 color = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
                // Invisible PNG pixels must not write depth and cut holes in the water pass.
                if (color.a <= 0.01) discard;
                finalColor = vec4(color.rgb * sceneLight(), color.a);
            }
            """);
        if (!IsShaderValid(SpriteShader) || SpriteShader.Id == Rlgl.GetShaderIdDefault())
            throw new InvalidOperationException("Cannot load sprite transparency shader.");
        UiText.Load();
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
        UiText.Unload();
        terrainBlending?.Dispose();
        terrainMeshes?.Dispose();
        UnloadShader(SpriteShader);
        foreach (var sprite in Sprites.Values) UnloadTexture(sprite.Texture);
        foreach (var texture in Terrain.Values) UnloadTexture(texture);
    }
}

sealed partial class WorldRenderer(WorldAssets assets)
{
    public static void Screenshot(string path)
    {
        var image = LoadImageFromScreen();
        try { if (!ExportImage(image, path)) throw new IOException("Could not save screenshot: " + path); }
        finally { UnloadImage(image); }
    }
    public void Draw(MapData map, Camera3D camera, bool grid, Vector3? playerPosition = null, int viewWidth = 0, int viewHeight = 0, float? timeOfDay = null)
    {
        float hour = timeOfDay ?? map.Environment.TimeOfDay;
        var bounds = TerrainVisibility.GetBounds(map, camera, viewWidth > 0 ? viewWidth : GetScreenWidth(), viewHeight > 0 ? viewHeight : GetScreenHeight());
        DrawTerrain(map, bounds, camera.Target, hour);
        Vector3 player = playerPosition ?? map.GroundPosition(map.Player.X, map.Player.Z);
        DrawShadows(map, player, hour, bounds);
        if (grid)
        {
            for (int x = bounds.MinX; x <= bounds.MaxX; x++)
            for (int z = bounds.MinZ; z < bounds.MaxZ; z++)
                DrawLine3D(map.Vertex(x, z) + new Vector3(0, .02f, 0), map.Vertex(x, z + 1) + new Vector3(0, .02f, 0), new Color(220, 230, 220, x % 5 == 0 ? 95 : 35));
            for (int z = bounds.MinZ; z <= bounds.MaxZ; z++)
            for (int x = bounds.MinX; x < bounds.MaxX; x++)
                DrawLine3D(map.Vertex(x, z) + new Vector3(0, .02f, 0), map.Vertex(x + 1, z) + new Vector3(0, .02f, 0), new Color(220, 230, 220, z % 5 == 0 ? 95 : 35));
        }
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var items = map.SceneEntities.Select(o => (Entity: (SceneObject?)o, o.Asset, Position: map.GroundPosition(o), o.Height,
                Fallback: o is NpcData ? new Color(244, 192, 96, 255) : o is MonsterData ? new Color(222, 75, 86, 255) : o is ChestData ? new Color(190, 140, 70, 255) : Color.Magenta))
            .Append((null, map.Player.Asset ?? "", player, map.Player.Height, new Color(80, 183, 255, 255)))
            .OrderByDescending(o => Vector3.Dot(o.Item3 - camera.Position, forward));
        WorldLighting.Apply(assets.SpriteShader, map, hour, camera.Target);
        BeginShaderMode(assets.SpriteShader);
        try
        {
            foreach (var item in items)
            {
                byte alpha = item.Entity == null ? (byte)255 : ObstacleAlpha(map, camera, item.Entity, playerPosition);
                if (alpha < 255) { Rlgl.DrawRenderBatchActive(); Rlgl.DisableDepthMask(); }
                DrawSpriteGeometry(camera, item.Asset, item.Position, item.Height, item.Fallback, alpha);
                if (alpha < 255) { Rlgl.DrawRenderBatchActive(); Rlgl.EnableDepthMask(); }
            }
        }
        finally { EndShaderMode(); }
        if (obstacleOpacity.Count > map.Objects.Count + map.Chests.Count + map.Npcs.Count + map.Monsters.Count + 32)
        { var ids = map.SceneEntities.Select(o => o.Id).ToHashSet(); foreach (var id in obstacleOpacity.Keys.Where(id => !ids.Contains(id)).ToArray()) obstacleOpacity.Remove(id); }
        DrawWater(map, camera, bounds, (float)GetTime(), hour);
    }
    public void DrawSprite(Camera3D camera, string id, Vector3 position, float height, Color fallback, byte alpha = 255)
    {
        BeginShaderMode(assets.SpriteShader);
        try { DrawSpriteGeometry(camera, id, position, height, fallback, alpha); }
        finally { EndShaderMode(); }
    }
    void DrawSpriteGeometry(Camera3D camera, string id, Vector3 position, float height, Color fallback, byte alpha = 255)
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
    public void DrawOutline(Camera3D camera, SceneObject item, Color color, MapData? map = null)
    {
        var quad = GetSpriteQuad(camera, item.Asset, map?.GroundPosition(item) ?? item.Position, item.Height);
        var offset = quad.Normal * .01f;
        DrawLine3D(quad.BottomLeft + offset, quad.BottomRight + offset, color);
        DrawLine3D(quad.BottomRight + offset, quad.TopRight + offset, color);
        DrawLine3D(quad.TopRight + offset, quad.TopLeft + offset, color);
        DrawLine3D(quad.TopLeft + offset, quad.BottomLeft + offset, color);
    }
    public SceneObject? Pick(MapData map, Camera3D camera, Ray ray)
    {
        SceneObject? result = null; float closest = float.MaxValue;
        foreach (var item in map.SceneEntities)
        {
            var quad = GetSpriteQuad(camera, item.Asset, map.GroundPosition(item), item.Height);
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
    public static float CollisionRadius(SceneObject item) => CollisionGeometry.Radius(item);
    public static bool CanWalk(MapData map, Vector3 p, Vector3? from = null, string? ignoreId = null, float radius = .25f)
    {
        if (!map.Contains(p.X, p.Z) || map.WaterDepthAt(p.X, p.Z) > .55f || TerrainGeometry.NormalAt(map, p.X, p.Z).Y < .64f) return false;
        if (from is { } previous && Math.Abs(map.HeightAt(p.X, p.Z) - previous.Y) > .3f + Vector2.Distance(new(p.X, p.Z), new(previous.X, previous.Z)) * 1.2f) return false;
        return !map.SceneEntities.Any(o => o.Id != ignoreId && CollisionGeometry.Blocks(o,
            from is { } start ? new(start.X, start.Z) : new(p.X, p.Z), new(p.X, p.Z), radius));
    }
}
