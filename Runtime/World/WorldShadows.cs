using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class WorldShadows
{
    public const string FragmentShader = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        out vec4 finalColor;
        void main()
        {
            vec2 offset = fragTexCoord * 2.0 - 1.0;
            float radiusSquared = dot(offset, offset);
            if (radiusSquared >= 1.0) discard;
            float softness = 1.0 - smoothstep(0.0, 1.0, radiusSquared);
            finalColor = vec4(fragColor.rgb, fragColor.a * softness);
        }
        """;
}

sealed partial class WorldRenderer
{
    void DrawShadows(MapData map, Camera3D camera, Vector3 player, float hour, TerrainBounds bounds)
    {
        if (!map.Environment.Shadows || bounds.TileCount == 0) return;
        // Match the sprite's horizontal axis in both camera projections.
        var forward = new Vector2(camera.Target.X - camera.Position.X, camera.Target.Z - camera.Position.Z);
        forward = forward.LengthSquared() > .000001f ? Vector2.Normalize(forward) : new(0, -1);
        var right = new Vector2(-forward.Y, forward.X);
        float strength = WorldLighting.Ambient(map, hour).Y;
        byte opacity = (byte)(100 * strength);
        void Shadow(string id, Vector3 feet, float height)
        {
            // Use the cropped art's actual size, independently of collision shapes or solidity.
            float width = SpriteWidth(id, height);
            float radiusX = Math.Max(.025f, width * .55f), radiusZ = Math.Max(.025f, Math.Max(width * .32f, height * .10f));
            float extentX = MathF.Sqrt(right.X * right.X * radiusX * radiusX + forward.X * forward.X * radiusZ * radiusZ);
            float extentZ = MathF.Sqrt(right.Y * right.Y * radiusX * radiusX + forward.Y * forward.Y * radiusZ * radiusZ);
            float tx = feet.X + map.Width / 2f, tz = feet.Z + map.Depth / 2f;
            int minX = Math.Clamp((int)MathF.Floor(tx - extentX), bounds.MinX, bounds.MaxX);
            int maxX = Math.Clamp((int)MathF.Ceiling(tx + extentX), bounds.MinX, bounds.MaxX);
            int minZ = Math.Clamp((int)MathF.Floor(tz - extentZ), bounds.MinZ, bounds.MaxZ);
            int maxZ = Math.Clamp((int)MathF.Ceiling(tz + extentZ), bounds.MinZ, bounds.MaxZ);
            void Put(Vector3 point)
            {
                var offset = new Vector2(point.X - feet.X, point.Z - feet.Z);
                Rlgl.TexCoord2f(.5f + Vector2.Dot(offset, right) / (2 * radiusX), .5f + Vector2.Dot(offset, forward) / (2 * radiusZ));
                Rlgl.Vertex3f(point.X, point.Y + .015f, point.Z);
            }
            for (int z = minZ; z < maxZ; z++)
            for (int x = minX; x < maxX; x++)
            {
                // Reuse terrain triangles so the shadow follows ridges and hollows without floating.
                var a = map.Vertex(x, z); var b = map.Vertex(x, z + 1);
                var c = map.Vertex(x + 1, z + 1); var d = map.Vertex(x + 1, z);
                Rlgl.CheckRenderBatchLimit(6);
                Rlgl.Begin((int)DrawMode.Triangles);
                Rlgl.Color4ub(0, 0, 0, opacity);
                Put(a); Put(b); Put(c); Put(a); Put(c); Put(d);
                Rlgl.End();
            }
        }
        BeginShaderMode(assets.ShadowShader);
        Rlgl.DisableDepthMask();
        Rlgl.SetTexture(0);
        try
        {
            foreach (var item in map.SceneEntities) Shadow(item.Asset, map.GroundPosition(item), item.Height);
            Shadow(map.Player.Asset ?? "", player, map.Player.Height);
        }
        finally
        {
            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableDepthMask();
            EndShaderMode();
        }
    }
}
