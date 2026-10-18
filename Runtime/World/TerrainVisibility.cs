using System.Numerics;
using Raylib_cs;

readonly record struct TerrainBounds(int MinX, int MinZ, int MaxX, int MaxZ)
{
    public int TileCount => (MaxX - MinX) * (MaxZ - MinZ);
    public IEnumerable<int> Indices(int width)
    {
        for (int z = MinZ; z < MaxZ; z++)
            for (int x = MinX; x < MaxX; x++) yield return z * width + x;
    }
}

static class TerrainVisibility
{
    public static TerrainBounds GetBounds(MapData map, Camera3D camera, int viewWidth, int viewHeight)
    {
        var full = new TerrainBounds(0, 0, map.Width, map.Depth);
        if (viewWidth <= 0 || viewHeight <= 0) return full;
        bool orthographic = camera.Projection == CameraProjection.Orthographic;
        if (!orthographic && camera.Projection != CameraProjection.Perspective) return full;
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, camera.Up));
        var up = Vector3.Cross(right, forward);
        float tangent = MathF.Tan(camera.FovY * MathF.PI / 360), aspect = viewWidth / (float)viewHeight;
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (int x in new[] { -1, 1 })
        foreach (int y in new[] { -1, 1 })
        {
            var origin = orthographic ? camera.Position +
                (right * (x * aspect) + up * y) * (camera.FovY / 2) : camera.Position;
            var direction = orthographic ? forward : forward + right * (x * aspect * tangent) + up * (y * tangent);
            // Our cameras look down onto the ground. Fall back conservatively if a custom camera sees the horizon.
            if (!float.IsFinite(direction.Y) || direction.Y >= -.0001f || origin.Y <= map.MinimumHeight) return full;
            foreach (float height in new[] { Math.Min(map.MinimumHeight, map.WaterLevel - map.WaveHeight),
                Math.Min(Math.Max(map.MaximumHeight, map.WaterLevel + map.WaveHeight), origin.Y - .001f) })
            {
                var point = origin + direction * ((height - origin.Y) / direction.Y);
                minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
                minZ = Math.Min(minZ, point.Z); maxZ = Math.Max(maxZ, point.Z);
            }
        }
        // Keep a tile of padding to avoid clipping the edge of the viewport or grid.
        return new(
            Math.Clamp((int)MathF.Floor(minX + map.Width / 2f) - 1, 0, map.Width),
            Math.Clamp((int)MathF.Floor(minZ + map.Depth / 2f) - 1, 0, map.Depth),
            Math.Clamp((int)MathF.Ceiling(maxX + map.Width / 2f) + 1, 0, map.Width),
            Math.Clamp((int)MathF.Ceiling(maxZ + map.Depth / 2f) + 1, 0, map.Depth));
    }
}
