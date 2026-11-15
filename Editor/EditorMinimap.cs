using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class TerrainOverview
{
    // Match the camera's horizontal right/backward axes. The default view looks along -X,
    // so world -Z belongs on the right of both the viewport and its overview.
    static (Vector2 Right, Vector2 Down) Basis(float yaw) =>
        (new(MathF.Cos(yaw),-MathF.Sin(yaw)),new(MathF.Sin(yaw),MathF.Cos(yaw)));
    public static Vector2 Size(MapData map, float yaw = FollowCamera.DefaultYaw)
    {
        var (right,down)=Basis(yaw);
        return new(Math.Abs(right.X)*map.Width+Math.Abs(right.Y)*map.Depth,
            Math.Abs(down.X)*map.Width+Math.Abs(down.Y)*map.Depth);
    }
    public static Rectangle FitBounds(MapData map, Rectangle available, float yaw = FollowCamera.DefaultYaw)
    {
        var size=Size(map,yaw); float scale=Math.Min(available.Width/size.X,available.Height/size.Y);
        return new(available.X+(available.Width-size.X*scale)*.5f,available.Y+(available.Height-size.Y*scale)*.5f,size.X*scale,size.Y*scale);
    }
    public static Vector2 Project(MapData map, Vector3 point, Rectangle bounds, float yaw = FollowCamera.DefaultYaw)
    {
        var (right,down)=Basis(yaw); var size=Size(map,yaw); var ground=new Vector2(point.X,point.Z);
        return new(bounds.X+(Vector2.Dot(ground,right)/size.X+.5f)*bounds.Width,
            bounds.Y+(Vector2.Dot(ground,down)/size.Y+.5f)*bounds.Height);
    }
    public static Vector3 Unproject(MapData map, Vector2 point, Rectangle bounds, float yaw = FollowCamera.DefaultYaw)
    {
        var (right,down)=Basis(yaw); var size=Size(map,yaw);
        var ground=right*((point.X-bounds.X)/bounds.Width-.5f)*size.X+down*((point.Y-bounds.Y)/bounds.Height-.5f)*size.Y;
        return map.GroundPosition(ground.X,ground.Y);
    }
    public static Color Sample(MapData map, float u, float v, Func<string, Color> materialColor)
    {
        int x = Math.Clamp((int)(u * map.Width), 0, map.Width - 1), z = Math.Clamp((int)(v * map.Depth), 0, map.Depth - 1);
        float wx = x - map.Width / 2f + .5f, wz = z - map.Depth / 2f + .5f;
        float depth=map.WaterDepthAt(wx,wz);
        if (depth > .02f)
        {
            var water=map.WaterAppearance; float t=Math.Clamp(depth/water.Clarity,0,1);
            return new((int)MathF.Round(water.R+(water.DeepR-water.R)*t),(int)MathF.Round(water.G+(water.DeepG-water.G)*t),
                (int)MathF.Round(water.B+(water.DeepB-water.B)*t),255);
        }
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
            // Bake the grid into the cached overview instead of issuing one draw per cell every frame.
            // A minimum of one pixel preserves narrow corridors on large maps.
            foreach (var cell in map.WallCells)
            {
                int px = cell.X * width / map.Width, pz = cell.Z * height / map.Depth;
                int pw = Math.Max(1, (cell.X + 1) * width / map.Width - px), ph = Math.Max(1, (cell.Z + 1) * height / map.Depth - pz);
                var color = cell.Style == WallStyle.Cave ? new Color(112, 121, 125, 255) : new Color(177, 151, 121, 255);
                for (int z = pz; z < Math.Min(height, pz + ph); z++)
                    pixels.Slice(z * width + px, Math.Min(pw, width - px)).Fill(color);
            }
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
    float MinimapYaw
    {
        get
        {
            // Free navigation keeps the overview aligned to the last fixed map view.
            var view=camera.TerrainTextureView; var heading=view.Position-view.Target;
            return MathF.Atan2(heading.X,heading.Z);
        }
    }
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
            float available = box.Width - 16;
            return TerrainOverview.FitBounds(document.Map,new(box.X+8,box.Y+28,available,available),MinimapYaw);
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
        {
            var point=TerrainOverview.Unproject(document.Map,GetMousePosition(),MinimapMapBounds,MinimapYaw);
            // Isometric overviews have empty corners around the rotated world rectangle.
            if(document.Map.Contains(point.X,point.Z))
            { FocusCamera(point); status = "Camera moved using the minimap."; }
        }
        return true;
    }
    void DrawMinimap()
    {
        if (!preferences.ShowMinimap || play != null) return;
        var box = MinimapBounds; var area = MinimapMapBounds; var map = document.Map;
        float yaw=MinimapYaw, scale=area.Width/TerrainOverview.Size(map,yaw).X;
        Vector2 Project(Vector3 point)=>TerrainOverview.Project(map,point,area,yaw);
        DrawRectangleRec(box, new(17, 23, 31, 240)); DrawRectangleLinesEx(box, 1, Border);
        Label("MAP / M", (int)box.X + 10, (int)box.Y + 9, 12, Accent);
        Label("x", (int)(box.X + box.Width - 19), (int)box.Y + 9, 12, Muted);
        var texture = overview.Texture;
        DrawRectangleRec(area,new(24,32,43,255));
        if (texture.Id != 0)
        {
            // Rotate the terrain, baked grid walls and all overlays with the same transform.
            void Corner(float x,float z,float u,float v)
            { var p=Project(new(x,0,z)); Rlgl.TexCoord2f(u,v); Rlgl.Vertex2f(p.X,p.Y); }
            Rlgl.SetTexture(texture.Id); Rlgl.Begin((int)DrawMode.Quads); Rlgl.Color4ub(255,255,255,255);
            Corner(-map.Width*.5f,-map.Depth*.5f,0,0); Corner(-map.Width*.5f,map.Depth*.5f,0,1);
            Corner(map.Width*.5f,map.Depth*.5f,1,1); Corner(map.Width*.5f,-map.Depth*.5f,1,0);
            Rlgl.End(); Rlgl.SetTexture(0);
        }
        BeginScissorMode((int)area.X, (int)area.Y, Math.Max(1, (int)area.Width), Math.Max(1, (int)area.Height));
        foreach (var road in map.Roads)
        {
            var curve = road.Curve; var color = overview.MaterialColor(road.Material);
            for (int i = 1; i < curve.Length; i++)
                DrawLineEx(Project(new(curve[i - 1].X, 0, curve[i - 1].Y)),
                    Project(new(curve[i].X, 0, curve[i].Y)), Math.Max(1, road.Width * scale), color);
        }
        foreach (var wall in map.Walls)
        {
            var color = wall.Id == editingWallId && tool == EditTool.Walls ? Accent : new Color(185, 172, 157, 255);
            for (int i = 0; i < wall.Points.Count - (wall.Closed ? 0 : 1); i++)
            {
                var a = wall.Points[i]; var b = wall.Points[(i + 1) % wall.Points.Count];
                DrawLineEx(Project(new(a.X, 0, a.Z)),Project(new(b.X, 0, b.Z)),Math.Max(1.5f,wall.Thickness*scale),color);
            }
        }
        foreach (var entity in map.SceneEntities)
        {
            var p = Project(entity.Position);
            Color color = entity is MonsterData ? new(240, 92, 100, 255) : entity is NpcData ? new(255, 209, 108, 255) :
                entity is ChestData ? new(193, 143, 72, 255) : entity.Asset.Contains("vegetation/") ? new(59, 102, 57, 255) : new(166, 164, 153, 255);
            float size = Math.Clamp(entity.Height * scale * .4f, 1.5f, 5);
            if (selectedIds.Contains(entity.Id) || selectedId == entity.Id) { color = Accent; size = Math.Max(size, 3); }
            DrawCircleV(p, size, color);
        }
        foreach (var trigger in map.Triggers)
        {
            Vector3[] points=[new(trigger.X-trigger.Width/2,0,trigger.Z-trigger.Depth/2),new(trigger.X+trigger.Width/2,0,trigger.Z-trigger.Depth/2),
                new(trigger.X+trigger.Width/2,0,trigger.Z+trigger.Depth/2),new(trigger.X-trigger.Width/2,0,trigger.Z+trigger.Depth/2)];
            for(int i=0;i<4;i++)DrawLineV(Project(points[i]),Project(points[(i+1)%4]),new(190,140,255,180));
        }
        var view = camera.View;
        var corners = new List<Vector2>();
        foreach (var point in new Vector2[] { Vector2.Zero, new(viewport.Width, 0), new(viewport.Width, viewport.Height), new(0, viewport.Height) })
        {
            var ray = GetScreenToWorldRayEx(point, view, (int)viewport.Width, (int)viewport.Height);
            var ground = TerrainGeometry.Raycast(map, ray) ?? WorldRenderer.GroundPoint(ray);
            if (ground is { } hit) corners.Add(Project(hit));
        }
        if (corners.Count == 4)
            for (int i = 0; i < 4; i++) DrawLineV(corners[i], corners[(i + 1) % 4], new(255, 240, 200, 220));
        var cameraPoint = Project(camera.IsFree ? view.Position : focus);
        DrawCircleLinesV(cameraPoint, 4, new(255, 240, 200, 255));
        var playerPoint = Project(map.Player.Position);
        DrawCircleV(playerPoint, 3, new(80, 183, 255, 255));
        EndScissorMode();
    }
}
