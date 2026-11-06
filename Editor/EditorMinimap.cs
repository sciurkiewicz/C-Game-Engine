using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class TerrainOverview
{
    public static Vector2 Project(MapData map, Vector3 point, Rectangle bounds) => new(
        bounds.X + (point.X / map.Width + .5f) * bounds.Width,
        bounds.Y + (point.Z / map.Depth + .5f) * bounds.Height);
    public static Vector3 Unproject(MapData map, Vector2 point, Rectangle bounds) => map.GroundPosition(
        ((point.X - bounds.X) / bounds.Width - .5f) * map.Width,
        ((point.Y - bounds.Y) / bounds.Height - .5f) * map.Depth);
    public static Color Sample(MapData map, float u, float v, Func<string, Color> materialColor)
    {
        int x = Math.Clamp((int)(u * map.Width), 0, map.Width - 1), z = Math.Clamp((int)(v * map.Depth), 0, map.Depth - 1);
        float wx = x - map.Width / 2f + .5f, wz = z - map.Depth / 2f + .5f;
        if (map.WaterDepthAt(wx, wz) > .02f) return new(58, 126, 167, 255);
        var color = materialColor(map.Tiles[z * map.Width + x]);
        var normal = TerrainGeometry.NormalAt(map, wx, wz);
        float shade = Math.Clamp(.8f + normal.Y * .25f + normal.X * .25f + normal.Z * .15f + map.HeightAt(wx, wz) * .015f, .4f, 1.4f);
        return new((byte)Math.Min(255, color.R * shade), (byte)Math.Min(255, color.G * shade), (byte)Math.Min(255, color.B * shade), (byte)255);
    }
}

sealed class EditorOverview : IDisposable
{
    Texture2D texture;
    MapData? renderedMap;
    double refreshAt;
    readonly Dictionary<string, Color> materialColors = [];
    public Texture2D Texture => texture;
    public Color MaterialColor(string id) => materialColors.GetValueOrDefault(id, new(145, 118, 82, 255));
    public unsafe void Update(MapData map, WorldAssets assets)
    {
        if (ReferenceEquals(map, renderedMap) && GetTime() < refreshAt) return;
        renderedMap = map; refreshAt = GetTime() + .75;
        // At most 256 x 256 samples, including for a 2048 x 2048 world.
        float scale = 256f / Math.Max(map.Width, map.Depth);
        int width = Math.Max(1, (int)(map.Width * scale)), height = Math.Max(1, (int)(map.Depth * scale));
        Color Material(string id)
        {
            if (materialColors.TryGetValue(id, out var color)) return color;
            color = new(130, 120, 100, 255);
            if (assets.Terrain.TryGetValue(id, out var terrain))
            {
                var sample = LoadImageFromTexture(terrain);
                if (sample.Data != null)
                {
                    ImageResize(ref sample, 1, 1); color = GetImageColor(sample, 0, 0); UnloadImage(sample);
                }
            }
            materialColors[id] = color; return color;
        }
        foreach (var road in map.Roads) Material(road.Material);
        var image = GenImageColor(width, height, Color.Black);
        try
        {
            var pixels = new Span<Color>(image.Data, width * height);
            for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
                pixels[z * width + x] = TerrainOverview.Sample(map, (x + .5f) / width, (z + .5f) / height, Material);
            if (texture.Id != 0) UnloadTexture(texture);
            texture = LoadTextureFromImage(image); SetTextureFilter(texture, TextureFilter.Bilinear);
        }
        finally { UnloadImage(image); }
    }
    public void Dispose() { if (texture.Id != 0) UnloadTexture(texture); }
}

sealed partial class EditorApp
{
    readonly EditorOverview overview = new();
    Rectangle MinimapBounds
    {
        get
        {
            float width = Math.Clamp(viewport.Width * .3f, 144, 220);
            return new(viewport.X + viewport.Width - width - 12, viewport.Y + 12, width, width + 28);
        }
    }
    Rectangle MinimapMapBounds
    {
        get
        {
            var box = MinimapBounds;
            float available = box.Width - 16, scale = available / Math.Max(document.Map.Width, document.Map.Depth);
            float width = document.Map.Width * scale, height = document.Map.Depth * scale;
            return new(box.X + (box.Width - width) / 2, box.Y + 28 + (available - height) / 2, width, height);
        }
    }
    bool OverMinimap => preferences.ShowMinimap && play == null && CheckCollisionPointRec(GetMousePosition(), MinimapBounds);
    void ToggleMinimap() { preferences.ShowMinimap = !preferences.ShowMinimap; SaveEditorPreferences(); }
    bool UpdateMinimapInput()
    {
        if (!OverMinimap || !IsMouseButtonPressed(MouseButton.Left)) return false;
        EndEditorGesture();
        var box = MinimapBounds;
        if (GetMouseY() < box.Y + 26 && GetMouseX() > box.X + box.Width - 28) ToggleMinimap();
        else if (CheckCollisionPointRec(GetMousePosition(), MinimapMapBounds))
        { FocusCamera(TerrainOverview.Unproject(document.Map, GetMousePosition(), MinimapMapBounds)); status = "Camera moved using the minimap."; }
        return true;
    }
    void DrawMinimap()
    {
        if (!preferences.ShowMinimap || play != null) return;
        var box = MinimapBounds; var area = MinimapMapBounds; var map = document.Map;
        DrawRectangleRec(box, new(17, 23, 31, 240)); DrawRectangleLinesEx(box, 1, Border);
        Label("MAP / M", (int)box.X + 10, (int)box.Y + 9, 12, Accent);
        Label("x", (int)(box.X + box.Width - 19), (int)box.Y + 9, 12, Muted);
        var texture = overview.Texture;
        if (texture.Id != 0) DrawTexturePro(texture, new(0, 0, texture.Width, texture.Height), area, Vector2.Zero, 0, Color.White);
        BeginScissorMode((int)area.X, (int)area.Y, Math.Max(1, (int)area.Width), Math.Max(1, (int)area.Height));
        foreach (var road in map.Roads)
        {
            var curve = road.Curve; var color = overview.MaterialColor(road.Material);
            for (int i = 1; i < curve.Length; i++)
                DrawLineEx(TerrainOverview.Project(map, new(curve[i - 1].X, 0, curve[i - 1].Y), area),
                    TerrainOverview.Project(map, new(curve[i].X, 0, curve[i].Y), area), Math.Max(1, road.Width / map.Width * area.Width), color);
        }
        foreach (var entity in map.SceneEntities)
        {
            var p = TerrainOverview.Project(map, entity.Position, area);
            Color color = entity is MonsterData ? new(240, 92, 100, 255) : entity is NpcData ? new(255, 209, 108, 255) :
                entity is ChestData ? new(193, 143, 72, 255) : entity.Asset.Contains("vegetation/") ? new(59, 102, 57, 255) : new(166, 164, 153, 255);
            float size = Math.Clamp(entity.Height * area.Width / map.Width * .4f, 1.5f, 5);
            if (selectedIds.Contains(entity.Id) || selectedId == entity.Id) { color = Accent; size = Math.Max(size, 3); }
            DrawCircleV(p, size, color);
        }
        foreach (var trigger in map.Triggers)
        {
            var a = TerrainOverview.Project(map, new(trigger.X - trigger.Width / 2, 0, trigger.Z - trigger.Depth / 2), area);
            DrawRectangleLinesEx(new(a.X, a.Y, trigger.Width / map.Width * area.Width, trigger.Depth / map.Depth * area.Height), 1, new(190, 140, 255, 180));
        }
        var view = camera.View;
        var corners = new List<Vector2>();
        foreach (var point in new Vector2[] { Vector2.Zero, new(viewport.Width, 0), new(viewport.Width, viewport.Height), new(0, viewport.Height) })
        {
            var ray = GetScreenToWorldRayEx(point, view, (int)viewport.Width, (int)viewport.Height);
            var ground = TerrainGeometry.Raycast(map, ray) ?? WorldRenderer.GroundPoint(ray);
            if (ground is { } hit) corners.Add(TerrainOverview.Project(map, hit, area));
        }
        if (corners.Count == 4)
            for (int i = 0; i < 4; i++) DrawLineV(corners[i], corners[(i + 1) % 4], new(255, 240, 200, 220));
        var cameraPoint = TerrainOverview.Project(map, camera.IsFree ? view.Position : focus, area);
        DrawCircleLinesV(cameraPoint, 4, new(255, 240, 200, 255));
        var playerPoint = TerrainOverview.Project(map, map.Player.Position, area);
        DrawCircleV(playerPoint, 3, new(80, 183, 255, 255));
        EndScissorMode();
    }
}
