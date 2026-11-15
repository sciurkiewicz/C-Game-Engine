using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class WorldRenderer
{
    void DrawTerrain(MapData map, TerrainBounds bounds, Camera3D camera, float hour, Camera3D terrainTextureView, WorldVisualFrame? visuals)
    {
        if (bounds.TileCount == 0) return;
        var blending = assets.TerrainBlending;
        blending.Update(assets.Terrain, map, bounds, terrainTextureView);
        WorldLighting.Apply(blending.Shader, map, hour, camera.Target, visuals);
        WorldVisualFrame.Apply(blending.Shader, map, camera, visuals, VisualSurface.Terrain);
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
    void DrawWater(MapData map, Camera3D camera, TerrainBounds bounds, float time, float hour, WorldVisualFrame? visuals)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var cells = bounds.Indices(map.Width).Where(i => map.WaterMask(i) != 0)
            .OrderByDescending(i => Vector3.Dot(new Vector3(i % map.Width - map.Width / 2f + .5f, map.WaterLevel,
                i / map.Width - map.Depth / 2f + .5f) - camera.Position, forward)).ToArray();
        if (cells.Length == 0) return;
        var appearance=map.WaterAppearance;var shader=assets.WaterShader;
        bool captured=assets.WaterSurfaces.Matches(map,camera);
        WorldLighting.Apply(shader,map,hour,camera.Target,visuals);
        WorldVisualFrame.Apply(assets.WaterShader, map, camera, visuals, VisualSurface.Water);
        SetShaderValue(shader,GetShaderLocation(shader,"shallowColor"),appearance.ShallowColor,ShaderUniformDataType.Vec3);
        SetShaderValue(shader,GetShaderLocation(shader,"deepColor"),appearance.DeepColor,ShaderUniformDataType.Vec3);
        SetShaderValue(shader,GetShaderLocation(shader,"waterOptics"),new Vector4(appearance.Clarity,appearance.ReflectionStrength,
            appearance.RefractionStrength,appearance.FoamStrength),ShaderUniformDataType.Vec4);
        SetShaderValue(shader,GetShaderLocation(shader,"waterSurface"),new Vector3(time,map.WaveHeight,captured?1:0),ShaderUniformDataType.Vec3);
        // Both captures retain the viewport resolution so underwater detail stays sharp.
        SetShaderValue(shader,GetShaderLocation(shader,"waterViewport"),new Vector2(waterRenderWidth,waterRenderHeight),ShaderUniformDataType.Vec2);
        BeginShaderMode(assets.WaterShader);
        Rlgl.DrawRenderBatchActive(); Rlgl.EnableDepthMask(); Rlgl.DisableBackfaceCulling();
        void BindCaptures()
        {
            if(!captured)return;
            SetShaderValueTexture(shader,GetShaderLocation(shader,"reflectionMap"),assets.WaterSurfaces.Reflection.Texture);
            SetShaderValueTexture(shader,GetShaderLocation(shader,"refractionMap"),assets.WaterSurfaces.Refraction.Texture);
        }
        void WaterVertex(Vector3 point)
        {
            // Water uses this varying for the local current; Y keeps velocity recoverable if normalized.
            var flow=map.SampleWaterFlow(point.X,point.Z);
            Rlgl.Normal3f(flow.X,1,flow.Y);
            Rlgl.TexCoord2f(Math.Max(0,map.WaterLevel-map.HeightAt(point.X,point.Z)),0);
            Rlgl.Vertex3f(point.X,point.Y,point.Z);
        }
        BindCaptures();Rlgl.SetTexture(0);
        foreach (int cell in cells)
        foreach (var triangle in WaterGeometry.BuildTile(map, cell % map.Width, cell / map.Width, time))
        {
            // A batch rollover clears Raylib's extra texture bindings even though the shader stays active.
            if(Rlgl.CheckRenderBatchLimit(3))BindCaptures();
            Rlgl.Begin((int)DrawMode.Triangles);Rlgl.Color4ub(255,255,255,255);
            WaterVertex(triangle.A);WaterVertex(triangle.B);WaterVertex(triangle.C);Rlgl.End();
        }
        Rlgl.DrawRenderBatchActive(); Rlgl.EnableBackfaceCulling(); Rlgl.EnableDepthMask();
        EndShaderMode();
    }
}
