using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// GPU meshes contain only geometry/UVs. Painting changes the blend lookup, not these buffers.
sealed unsafe class TerrainChunkCache : IDisposable
{
    sealed record Chunk(Mesh Mesh, long Revision) { public long Used; }
    readonly Dictionary<(int X, int Z), Chunk> chunks = [];
    MapData? currentMap;
    long generation, frame;
    Material material = LoadMaterialDefault();
    public int BuildCount { get; private set; }
    public int CachedChunkCount => chunks.Count;
    public void Draw(MapData map, TerrainBounds bounds, TerrainBlending blending)
    {
        if (!ReferenceEquals(currentMap, map) || generation != map.TerrainGeneration)
        { Clear(); currentMap = map; generation = map.TerrainGeneration; }
        frame++;
        blending.ConfigureMaterial(ref material);
        Rlgl.DrawRenderBatchActive();
        for (int cz = bounds.MinZ / MapData.TerrainChunkSize; cz <= (bounds.MaxZ - 1) / MapData.TerrainChunkSize; cz++)
        for (int cx = bounds.MinX / MapData.TerrainChunkSize; cx <= (bounds.MaxX - 1) / MapData.TerrainChunkSize; cx++)
        {
            long revision = map.TerrainChunkVersion(cx, cz);
            if (!chunks.TryGetValue((cx, cz), out var chunk) || chunk.Revision != revision)
            {
                if (chunk != null) UnloadMesh(chunk.Mesh);
                chunk = new(Build(map, cx, cz), revision); chunks[(cx, cz)] = chunk; BuildCount++;
            }
            chunk.Used = frame;
            DrawMesh(chunk.Mesh, material, Matrix4x4.Identity);
        }
        // Bound retained GPU memory when panning across a 2048 x 2048 world.
        foreach (var key in chunks.Where(c => c.Value.Used != frame).OrderBy(c => c.Value.Used)
            .Take(Math.Max(0, chunks.Count - 256)).Select(c => c.Key).ToArray())
        { UnloadMesh(chunks[key].Mesh); chunks.Remove(key); }
    }
    static Mesh Build(MapData map, int cx, int cz)
    {
        int minX = cx * MapData.TerrainChunkSize, minZ = cz * MapData.TerrainChunkSize;
        int maxX = Math.Min(map.Width, minX + MapData.TerrainChunkSize), maxZ = Math.Min(map.Depth, minZ + MapData.TerrainChunkSize);
        int count = (maxX - minX) * (maxZ - minZ) * 6;
        var mesh = new Mesh
        {
            VertexCount = count, TriangleCount = count / 3,
            Vertices = (float*)MemAlloc((uint)(count * 3 * sizeof(float))),
            TexCoords = (float*)MemAlloc((uint)(count * 2 * sizeof(float))),
            Normals = (float*)MemAlloc((uint)(count * 3 * sizeof(float))), Colors = (byte*)MemAlloc((uint)(count * 4))
        };
        try
        {
            if (mesh.Vertices == null || mesh.TexCoords == null || mesh.Normals == null || mesh.Colors == null) throw new OutOfMemoryException();
            int vertex = 0;
            var sun = Vector3.Normalize(new Vector3(-.35f, 1, -.45f));
            void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
            {
                var normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                byte shade = (byte)Math.Clamp((.6f + .45f * Math.Max(0, Vector3.Dot(normal, sun))) * 255, 0, 255);
                void Put(Vector3 p, Vector2 uv)
                {
                    mesh.Vertices[vertex * 3] = p.X; mesh.Vertices[vertex * 3 + 1] = p.Y; mesh.Vertices[vertex * 3 + 2] = p.Z;
                    mesh.TexCoords[vertex * 2] = uv.X; mesh.TexCoords[vertex * 2 + 1] = uv.Y;
                    mesh.Normals[vertex * 3] = normal.X; mesh.Normals[vertex * 3 + 1] = normal.Y; mesh.Normals[vertex * 3 + 2] = normal.Z;
                    mesh.Colors[vertex * 4] = mesh.Colors[vertex * 4 + 1] = mesh.Colors[vertex * 4 + 2] = shade;
                    mesh.Colors[vertex * 4 + 3] = 255; vertex++;
                }
                Put(a, ua); Put(b, ub); Put(c, uc);
            }
            for (int z = minZ; z < maxZ; z++)
            for (int x = minX; x < maxX; x++)
            {
                var a = map.Vertex(x, z); var b = map.Vertex(x, z + 1); var c = map.Vertex(x + 1, z + 1); var d = map.Vertex(x + 1, z);
                Triangle(a, b, c, new(x, z), new(x, z + 1), new(x + 1, z + 1));
                Triangle(a, c, d, new(x, z), new(x + 1, z + 1), new(x + 1, z));
            }
            UploadMesh(ref mesh, false); return mesh;
        }
        catch { UnloadMesh(mesh); throw; }
    }
    void Clear() { foreach (var chunk in chunks.Values) UnloadMesh(chunk.Mesh); chunks.Clear(); }
    public void Dispose() { Clear(); MemFree(material.Maps); material = default; }
}
