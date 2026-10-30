using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class WorldRenderer
{
    void DrawTerrain(MapData map, TerrainBounds bounds, Vector3 focus, float hour)
    {
        if (bounds.TileCount == 0) return;
        var blending = assets.TerrainBlending;
        blending.Update(assets.Terrain, map, bounds);
        WorldLighting.Apply(blending.Shader, map, hour, focus);
        assets.TerrainMeshes.Draw(map, bounds, blending);
    }
    public void DrawBrush(MapData map, Vector3 center, float radius, Color color, bool square, float? level = null)
    {
        const int segments = 64;
        Vector3 Point(int i)
        {
            float x, z;
            if (square)
            {
                float t = i / (float)segments * 4;
                if (t < 1) { x = -radius + 2 * radius * t; z = -radius; }
                else if (t < 2) { x = radius; z = -radius + 2 * radius * (t - 1); }
                else if (t < 3) { x = radius - 2 * radius * (t - 2); z = radius; }
                else { x = -radius; z = radius - 2 * radius * (t - 3); }
            }
            else { float angle = i * MathF.Tau / segments; x = MathF.Cos(angle) * radius; z = MathF.Sin(angle) * radius; }
            x = Math.Clamp(center.X + x, -map.Width / 2f, map.Width / 2f);
            z = Math.Clamp(center.Z + z, -map.Depth / 2f, map.Depth / 2f);
            var ground = map.GroundPosition(x, z);
            return new(x, Math.Max(ground.Y, level ?? ground.Y) + .06f, z);
        }
        for (int i = 0; i < segments; i++) DrawLine3D(Point(i), Point(i + 1), color);
    }
    static readonly Vector3 Sun = Vector3.Normalize(new Vector3(-.35f, 1, -.45f));
    static void DrawWater(MapData map, Camera3D camera, TerrainBounds bounds, float time, float hour)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var cells = bounds.Indices(map.Width).Where(i => map.WaterMask(i) != 0)
            .OrderByDescending(i => Vector3.Dot(new Vector3(i % map.Width - map.Width / 2f + .5f, map.WaterLevel,
                i / map.Width - map.Depth / 2f + .5f) - camera.Position, forward)).ToArray();
        if (cells.Length == 0) return;
        var ambient = WorldLighting.Ambient(map, hour);
        var lights = WorldLighting.Nearby(map, camera.Target);
        Rlgl.DrawRenderBatchActive(); Rlgl.DisableDepthMask(); Rlgl.DisableBackfaceCulling();
        Rlgl.SetTexture(0); Rlgl.Begin((int)DrawMode.Triangles);
        foreach (int cell in cells)
        foreach (var triangle in WaterGeometry.BuildTile(map, cell % map.Width, cell / map.Width, time))
        {
            var normal = Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A);
            if (normal.LengthSquared() < 1e-10f) continue;
            normal = Vector3.Normalize(normal);
            float light = Math.Max(0, Vector3.Dot(normal, Sun));
            float glint = MathF.Pow(Math.Max(0, Vector3.Dot(Vector3.Reflect(-Sun, normal), -forward)), 24);
            float depth = Math.Clamp(triangle.Depth / 3, 0, 1);
            byte r = (byte)Math.Clamp(25 + light * 26 + glint * 145 - depth * 12, 0, 255);
            byte g = (byte)Math.Clamp(95 + light * 48 + glint * 120 - depth * 32, 0, 255);
            byte b = (byte)Math.Clamp(135 + light * 52 + glint * 90 - depth * 28, 0, 255);
            var illumination = WorldLighting.Evaluate(map, (triangle.A + triangle.B + triangle.C) / 3, ambient, lights);
            Rlgl.Color4ub((byte)Math.Clamp(r * illumination.X, 0, 255), (byte)Math.Clamp(g * illumination.Y, 0, 255), (byte)Math.Clamp(b * illumination.Z, 0, 255), (byte)(175 + depth * 45));
            Rlgl.Normal3f(normal.X, normal.Y, normal.Z);
            Rlgl.Vertex3f(triangle.A.X, triangle.A.Y, triangle.A.Z);
            Rlgl.Vertex3f(triangle.B.X, triangle.B.Y, triangle.B.Z);
            Rlgl.Vertex3f(triangle.C.X, triangle.C.Y, triangle.C.Z);
        }
        Rlgl.End(); Rlgl.DrawRenderBatchActive(); Rlgl.EnableBackfaceCulling(); Rlgl.EnableDepthMask();
    }
}
