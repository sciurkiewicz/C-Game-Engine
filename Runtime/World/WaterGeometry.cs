using System.Numerics;

readonly record struct WaterTriangle(Vector3 A, Vector3 B, Vector3 C, float Depth);

static class WaterGeometry
{
    public static float SurfaceHeight(MapData map, float x, float z, float time)
    {
        if(map.WaveHeight==0)return map.WaterLevel;
        float depth = Math.Max(0, map.WaterLevel - map.HeightAt(x, z));
        if(depth==0)return map.WaterLevel;
        float wave;
        var flow=map.SampleWaterFlow(x,z);
        float amount=Math.Clamp(flow.Length()/.15f,0,1);amount=amount*amount*(3-2*amount);
        if(amount==0)wave=WaveNoise(x,z,time);
        else
        {
            // Match the bounded, overlapping advection phases of the surface shader.
            float phase=time*.25f-MathF.Floor(time*.25f),other=(phase+.5f)%1;
            float weight=1-Math.Abs(phase*2-1);
            float moving=Lerp(WaveNoise(x-flow.X*other*4,z-flow.Y*other*4,time*.025f),
                WaveNoise(x-flow.X*phase*4,z-flow.Y*phase*4,time*.025f),weight);
            wave=amount==1?moving:Lerp(WaveNoise(x,z,time),moving,amount);
        }
        wave = Math.Clamp(wave, -1, 1);
        return map.WaterLevel + wave * map.WaveHeight * Math.Clamp(depth / .4f, 0, 1);
    }
    static float WaveNoise(float x,float z,float time)
    {
        // All scales use world coordinates so tile boundaries never restart the pattern.
        return SmoothNoise(x * .38f + time * .45f + 7.3f, z * .38f - time * .21f + 15.9f) * .55f +
            SmoothNoise((x * .74f - z * .67f) * .67f - time * .37f + 19.3f,
                (x * .67f + z * .74f) * .67f + time * .29f - 7.1f) * .30f +
            SmoothNoise((x * .41f + z * .91f) * 1.13f + time * .19f - 31.7f,
                (-x * .91f + z * .41f) * 1.13f - time * .61f + 42.8f) * .15f;
    }
    static float SmoothNoise(float x, float z)
    {
        int cellX = (int)MathF.Floor(x), cellZ = (int)MathF.Floor(z);
        float u = x - cellX, v = z - cellZ;
        u = u * u * u * (u * (u * 6 - 15) + 10);
        v = v * v * v * (v * (v * 6 - 15) + 10);
        float a = Lerp(Lattice(cellX, cellZ), Lattice(cellX + 1, cellZ), u);
        float b = Lerp(Lattice(cellX, cellZ + 1), Lattice(cellX + 1, cellZ + 1), u);
        return Lerp(a, b, v);
    }
    static float Lerp(float a, float b, float amount) => a + (b - a) * amount;
    static float Lattice(int x, int z)
    {
        uint hash = unchecked((uint)x * 0x8da6b343u ^ (uint)z * 0xd8163841u ^ 0xcb1ab31fu);
        hash ^= hash >> 16; hash = unchecked(hash * 0x7feb352du);
        hash ^= hash >> 15; hash = unchecked(hash * 0x846ca68bu);
        hash ^= hash >> 16;
        return (hash & 0xffffffu) / 16777215f * 2 - 1;
    }
    public static List<WaterTriangle> BuildTile(MapData map, int x, int z, float time)
    {
        var triangles = new List<WaterTriangle>();
        byte mask = map.WaterMask(z * map.Width + x);
        if (mask == 0) return triangles;
        var a = map.Vertex(x, z); var b = map.Vertex(x, z + 1); var c = map.Vertex(x + 1, z + 1); var d = map.Vertex(x + 1, z);
        void Surface(Vector3 p, Vector3 q, Vector3 r)
        {
            var polygon = new List<Vector3>();
            var input = new[] { p, q, r };
            for (int i = 0; i < 3; i++)
            {
                var start = input[i]; var end = input[(i + 1) % 3];
                bool inside = start.Y < map.WaterLevel, endInside = end.Y < map.WaterLevel;
                if (inside) polygon.Add(start);
                if (inside != endInside) polygon.Add(Vector3.Lerp(start, end, (map.WaterLevel - start.Y) / (end.Y - start.Y)));
            }
            if (polygon.Count < 3) return;
            float depth = polygon.Average(v => Math.Max(0, map.WaterLevel - v.Y));
            for (int i = 0; i < polygon.Count; i++)
            {
                var v = polygon[i]; polygon[i] = new(v.X, SurfaceHeight(map, v.X, v.Z, time), v.Z);
            }
            for (int i = 1; i < polygon.Count - 1; i++) triangles.Add(new(polygon[0], polygon[i], polygon[i + 1], depth));
        }
        if ((mask & 1) != 0) Surface(a, b, c);
        if ((mask & 2) != 0) Surface(a, c, d);
        return triangles;
    }
}
