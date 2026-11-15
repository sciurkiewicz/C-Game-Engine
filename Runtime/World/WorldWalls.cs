using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class WallMaterial
{
    public const string FragmentShader = """
        in vec2 fragTexCoord;
        in vec4 fragColor;
        uniform int wallStyle;
        uniform float wallSilhouette;
        out vec4 finalColor;
        float grain(vec2 p) { return fract(sin(dot(p, vec2(41.7, 289.3))) * 15731.743); }
        void main()
        {
            clipWorld();
            if (wallSilhouette > 0.5) { finalColor = vec4(0.0, 0.0, 0.0, 1.0); return; }
            if (fragColor.a <= 0.001) discard;
            vec2 p = fragTexCoord;
            vec3 color;
            if (wallStyle == 2)
            {
                vec2 plank = vec2(p.x * 2.4, p.y * 0.65);
                float seam = smoothstep(0.025, 0.055, min(fract(plank.x), 1.0 - fract(plank.x)));
                float rings = sin(p.y * 33.0 + sin(p.x * 57.0) * 1.8) * 0.035;
                color = vec3(0.48, 0.32, 0.19) * (0.80 + grain(floor(plank)) * 0.28 + rings) * mix(0.38, 1.0, seam);
            }
            else
            {
                vec2 brick = p * (wallStyle == 0 ? vec2(1.15, 1.6) : vec2(1.8, 2.8));
                float row = floor(brick.y);
                brick.x += mod(row, 2.0) * 0.5;
                if (wallStyle == 0)
                    brick += vec2(sin(p.y * 6.1 + sin(p.x * 2.4)), sin(p.x * 5.7)) * 0.16;
                vec2 cell = floor(brick), edge = min(fract(brick), 1.0 - fract(brick));
                float mortar = smoothstep(0.018, 0.045, min(edge.x, edge.y));
                vec3 base = wallStyle == 0 ? vec3(0.48, 0.50, 0.51) : vec3(0.64, 0.48, 0.36);
                color = base * (0.78 + grain(cell) * 0.35) * mix(0.38, 1.0, mortar);
                color += (grain(floor(p * 90.0)) - 0.5) * 0.035;
            }
            vec3 normal = normalize(fragNormal);
            float shapeLight = 0.62 + 0.38 * max(0.0, dot(normal, normalize(vec3(-0.35, 1.0, -0.45))));
            color = atmosphere(wetGround(color * shapeLight * sceneLight()));
            finalColor = vec4(mix(color, vec3(0.0), wallSilhouette) * fragColor.rgb, fragColor.a);
        }
        """;
}

sealed partial class WorldRenderer
{
    readonly Dictionary<WallData, float> wallOpacity = [];
    readonly WallFrustum wallFrustum = new();
    readonly HashSet<WallData> blockedWalls = [];
    readonly HashSet<WallData> pendingWallFades = [];
    MapData? opacityMap;

    public WallData? PickWall(MapData map, Ray ray)
    {
        WallData? result = null; float closest = float.MaxValue;
        foreach (var wall in map.Walls)
            if (WallGeometry.RayDistance(map, wall, ray) is { } distance && distance < closest)
            { result = wall; closest = distance; }
        return result;
    }

    bool WallBlocksPlayer(MapData map, WallData wall, Camera3D camera, Vector3 player)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        for (int sample = 0; sample < 2; sample++)
        {
            float fraction = sample == 0 ? .35f : .75f;
            var body = player + Vector3.UnitY * (map.Player.Height * fraction);
            var origin = camera.Projection == CameraProjection.Orthographic
                ? body - forward * Vector3.Dot(body - camera.Position, forward) : camera.Position;
            var delta = body - origin; float distance = delta.Length();
            if (distance > .001f && WallGeometry.RayDistance(map, wall, new() { Position = origin, Direction = delta / distance }) is { } hit && hit < distance - .05f)
                return true;
        }
        return false;
    }

    void UpdateWallOpacity(MapData map, Camera3D camera, Vector3? player)
    {
        if (!ReferenceEquals(map, opacityMap) || !player.HasValue)
        {
            foreach (var wall in wallOpacity.Keys) if (wall.Cell is { } cell) assets.WallMeshes.Suppress(cell, false);
            wallOpacity.Clear(); opacityMap = map;
        }
        if (!player.HasValue) return;
        blockedWalls.Clear(); pendingWallFades.Clear();
        if (map.Environment.FadeOccluders)
        {
            foreach (var wall in map.Walls)
                if (wall.FadeWhenBlocking && WallBlocksPlayer(map, wall, camera, player.Value)) blockedWalls.Add(wall);
            var forward = Vector3.Normalize(camera.Target - camera.Position);
            for (int sample = 0; sample < 2; sample++)
            {
                var body = player.Value + Vector3.UnitY * (map.Player.Height * (sample == 0 ? .35f : .75f));
                var origin = camera.Projection == CameraProjection.Orthographic ? body - forward * Vector3.Dot(body - camera.Position, forward) : camera.Position;
                foreach (var cell in GridWallGeometry.Near(map, new(origin.X, origin.Z), new(body.X, body.Z), 0))
                {
                    if (!cell.FadeWhenBlocking || CollisionGeometry.PointSegmentDistance(new(cell.X - map.Width / 2f + .5f, cell.Z - map.Depth / 2f + .5f),
                        new(origin.X, origin.Z), new(body.X, body.Z)) > 1f) continue;
                    var wall = cell.AsWall(map);
                    if (!blockedWalls.Contains(wall) && WallBlocksPlayer(map, wall, camera, player.Value)) blockedWalls.Add(wall);
                }
            }
        }
        pendingWallFades.UnionWith(wallOpacity.Keys); pendingWallFades.UnionWith(blockedWalls);
        float step = Math.Clamp(GetFrameTime(), 1f / 144, .05f) * 4;
        foreach (var wall in pendingWallFades)
        {
            bool exists = wall.Cell is { } c ? ReferenceEquals(map.WallCellAt(c.X, c.Z), c) : map.Walls.Contains(wall);
            float current = wallOpacity.GetValueOrDefault(wall, 1), target = blockedWalls.Contains(wall) && exists ? map.Environment.OccluderOpacity : 1;
            float value = current + Math.Clamp(target - current, -step, step);
            if (!exists || value >= .999f)
            { wallOpacity.Remove(wall); if (wall.Cell is { } cell) assets.WallMeshes.Suppress(cell, false); }
            else
            { wallOpacity[wall] = value; if (wall.Cell is { } cell) assets.WallMeshes.Suppress(cell, true); }
        }
    }

    void DrawWalls(MapData map, Camera3D camera, float hour, WorldVisualFrame? visuals, bool transparent)
    {
        bool capture=visuals?.WaterCapture==true;
        if(capture && transparent)return;
        if (transparent ? wallOpacity.Count == 0 : map.RenderWallCount == 0) return;
        var shader = assets.WallShader;
        WorldLighting.Apply(shader, map, hour, camera.Target, visuals);
        WorldVisualFrame.Apply(shader, map, camera, visuals, VisualSurface.Wall);
        SetShaderValue(shader, GetShaderLocation(shader, "wallSilhouette"), 0f, ShaderUniformDataType.Float);
        if (!transparent && map.WallCells.Count > 0) assets.WallMeshes.Draw(map, wallFrustum, shader);
        BeginShaderMode(shader);
        if (transparent) { Rlgl.DrawRenderBatchActive(); Rlgl.DisableDepthMask(); }
        try
        {
            var walls = capture ? map.Walls : transparent ? wallOpacity.Keys.AsEnumerable() : map.Walls.AsEnumerable().Where(w => !wallOpacity.ContainsKey(w));
            if (!transparent) walls = walls.OrderBy(w => w.Style);
            if (transparent) walls = walls.OrderByDescending(w => Vector3.DistanceSquared((w.Geometry(map).Min + w.Geometry(map).Max) / 2, camera.Position));
            WallStyle? previousStyle = null;
            foreach (var wall in walls)
            {
                var geometry = wall.Geometry(map);
                if (!wallFrustum.Intersects(geometry.Min, geometry.Max)) continue;
                DrawWallGeometry(map, wall, Color.White, capture ? (byte)255 : (byte)(wallOpacity.GetValueOrDefault(wall, 1) * 255), previousStyle != wall.Style);
                previousStyle = wall.Style;
            }
        }
        finally
        {
            if (transparent) { Rlgl.DrawRenderBatchActive(); Rlgl.EnableDepthMask(); }
            EndShaderMode();
        }
    }

    void DrawWallGeometry(MapData map, WallData wall, Color tint, byte alpha = 255, bool changeMaterial = true)
    {
        if (changeMaterial)
        {
            Rlgl.DrawRenderBatchActive();
            SetShaderValue(assets.WallShader, GetShaderLocation(assets.WallShader, "wallStyle"), (int)wall.Style, ShaderUniformDataType.Int);
        }
        Rlgl.SetTexture(0); Rlgl.Begin((int)DrawMode.Quads);
        Rlgl.Color4ub(tint.R, tint.G, tint.B, Math.Min(tint.A, alpha));
        foreach (var face in wall.Geometry(map).Faces)
        {
            var normal = face.Normal; Rlgl.Normal3f(normal.X, normal.Y, normal.Z);
            Vertex(face.A, face.UvA.X, face.UvA.Y); Vertex(face.B, face.UvB.X, face.UvB.Y);
            Vertex(face.C, face.UvC.X, face.UvC.Y); Vertex(face.D, face.UvD.X, face.UvD.Y);
        }
        Rlgl.End();
    }

    public void DrawWallPreview(MapData map, Camera3D camera, WallData wall, Color tint)
    {
        var shader = assets.WallShader;
        WorldLighting.Apply(shader, map, map.Environment.TimeOfDay, camera.Target);
        WorldVisualFrame.Apply(shader, map, camera, null, VisualSurface.Wall);
        SetShaderValue(shader, GetShaderLocation(shader, "wallSilhouette"), 0f, ShaderUniformDataType.Float);
        BeginShaderMode(shader);
        Rlgl.DrawRenderBatchActive(); Rlgl.DisableDepthMask();
        try { DrawWallGeometry(map, wall, tint, 105); }
        finally { Rlgl.DrawRenderBatchActive(); Rlgl.EnableDepthMask(); EndShaderMode(); }
    }

    public void DrawWallOutline(MapData map, WallData wall, Color color)
    {
        foreach (var face in wall.Geometry(map).Faces)
        {
            var offset = face.Normal * .015f;
            DrawLine3D(face.A + offset, face.B + offset, color); DrawLine3D(face.C + offset, face.D + offset, color);
        }
        foreach (var point in wall.Points)
        {
            var ground = map.GroundPosition(point.X, point.Z);
            DrawLine3D(ground, ground + Vector3.UnitY * wall.Height, color);
        }
    }
}
