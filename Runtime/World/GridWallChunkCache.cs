using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Static wall vertices stay on the GPU. Editing invalidates only the affected 8x8 chunks.
sealed unsafe class GridWallChunkCache : IDisposable
{
    sealed class Batch(Mesh mesh, WallStyle style, Dictionary<GridWallCell, (int Start, int Count)> ranges)
    {
        public readonly Mesh Mesh = mesh;
        public readonly WallStyle Style = style;
        public readonly Dictionary<GridWallCell, (int Start, int Count)> Ranges = ranges;
        public bool ColorsDirty;
    }
    sealed class Chunk(long revision, int terrainVersion, Batch[] batches, Vector3 min, Vector3 max)
    {
        public readonly long Revision = revision;
        public readonly int TerrainVersion = terrainVersion;
        public readonly Batch[] Batches = batches;
        public readonly Vector3 Min = min, Max = max;
        public long Used;
    }
    readonly Dictionary<(int X, int Z), Chunk> chunks = [];
    readonly HashSet<GridWallCell> suppressed = [];
    MapData? currentMap;
    GridWallIndex? currentIndex;
    object? fadeOwner;
    long generation, frame;
    long indexRevision = -1;
    Material material = LoadMaterialDefault();
    public int BuildCount { get; private set; }
    public int DrawCount { get; private set; }
    public int CachedChunkCount => chunks.Count;
    public int VisibleCellCount { get; private set; }

    public void UseFadesFor(object renderer)
    {
        if (ReferenceEquals(fadeOwner, renderer)) return;
        foreach (var cell in suppressed.ToArray()) Suppress(cell, false);
        fadeOwner = renderer;
    }

    public void Suppress(GridWallCell cell, bool value)
    {
        if (!(value ? suppressed.Add(cell) : suppressed.Remove(cell))) return;
        if (!chunks.TryGetValue((cell.X / GridWallIndex.ChunkSize, cell.Z / GridWallIndex.ChunkSize), out var chunk)) return;
        foreach (var batch in chunk.Batches)
        {
            if (!batch.Ranges.TryGetValue(cell, out var range)) continue;
            for (int i = range.Start; i < range.Start + range.Count; i++) batch.Mesh.Colors[i * 4 + 3] = value ? (byte)0 : (byte)255;
            batch.ColorsDirty = true;
        }
    }
    public void Synchronize(MapData map)
    {
        var index = map.GridWalls;
        if (!ReferenceEquals(map, currentMap) || !ReferenceEquals(index, currentIndex) || generation != map.TerrainGeneration)
        {
            Clear(); currentMap = map; currentIndex = index; generation = map.TerrainGeneration;
            // Fades belong to the renderer and may have been prepared before the first upload.
            suppressed.RemoveWhere(c => !ReferenceEquals(map.WallCellAt(c.X, c.Z), c));
        }
        if (indexRevision != index.Revision)
        {
            foreach (var key in chunks.Keys.Where(k => !index.Chunks.ContainsKey(k)).ToArray())
            { Release(chunks[key]); chunks.Remove(key); }
            indexRevision = index.Revision;
        }
    }
    public void Draw(MapData map, WallFrustum frustum, Shader shader)
    {
        Synchronize(map); var index = map.GridWalls;
        frame++; DrawCount = VisibleCellCount = 0;
        material.Shader = shader;
        Rlgl.DrawRenderBatchActive();
        foreach (var pair in index.Chunks)
        {
            int cx = pair.Key.X, cz = pair.Key.Z, size = GridWallIndex.ChunkSize;
            var broadMin = new Vector3(cx * size - map.Width / 2f - .22f, map.MinimumHeight - .1f, cz * size - map.Depth / 2f - .22f);
            var broadMax = new Vector3(Math.Min((cx + 1) * size, map.Width) - map.Width / 2f + .22f, map.MaximumHeight + index.ChunkHeight(cx, cz) + .01f,
                Math.Min((cz + 1) * size, map.Depth) - map.Depth / 2f + .22f);
            if (!frustum.Intersects(broadMin, broadMax)) continue;
            var hash = new HashCode();
            int tx0 = Math.Max(0, (cx * size - 1) / MapData.TerrainChunkSize), tz0 = Math.Max(0, (cz * size - 1) / MapData.TerrainChunkSize);
            int tx1 = Math.Min(map.Width - 1, (cx + 1) * size) / MapData.TerrainChunkSize;
            int tz1 = Math.Min(map.Depth - 1, (cz + 1) * size) / MapData.TerrainChunkSize;
            for (int z = tz0; z <= tz1; z++) for (int x = tx0; x <= tx1; x++) hash.Add(map.TerrainChunkVersion(x, z));
            int terrainVersion = hash.ToHashCode(); long revision = index.ChunkVersion(cx, cz);
            if (!chunks.TryGetValue(pair.Key, out var chunk) || chunk.Revision != revision || chunk.TerrainVersion != terrainVersion)
            {
                if (chunk != null) Release(chunk);
                chunk = Build(map, pair.Value, revision, terrainVersion); chunks[pair.Key] = chunk; BuildCount++;
            }
            if (!frustum.Intersects(chunk.Min, chunk.Max)) continue;
            chunk.Used = frame;
            VisibleCellCount += pair.Value.Count;
            foreach (var batch in chunk.Batches)
            {
                if (batch.ColorsDirty)
                { UpdateMeshBuffer(batch.Mesh, 3, batch.Mesh.Colors, batch.Mesh.VertexCount * 4, 0); batch.ColorsDirty = false; }
                SetShaderValue(shader, GetShaderLocation(shader, "wallStyle"), (int)batch.Style, ShaderUniformDataType.Int);
                DrawMesh(batch.Mesh, material, Matrix4x4.Identity); DrawCount++;
            }
        }
        foreach (var key in chunks.Where(c => c.Value.Used != frame).OrderBy(c => c.Value.Used)
            .Take(Math.Max(0, chunks.Count - 96)).Select(c => c.Key).ToArray())
        { Release(chunks[key]); chunks.Remove(key); }
    }
    Chunk Build(MapData map, List<GridWallCell> cells, long revision, int terrainVersion)
    {
        var batches = new List<Batch>(); var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        try
        {
            foreach (var group in cells.GroupBy(c => c.Style))
            {
                var walls = group.Select(c => (Cell: c, Geometry: c.Geometry(map))).ToArray();
                int count = walls.Sum(w => w.Geometry.Faces.Length) * 6;
                var mesh = new Mesh { VertexCount = count, TriangleCount = count / 3,
                    Vertices = (float*)MemAlloc((uint)(count * 3 * sizeof(float))), TexCoords = (float*)MemAlloc((uint)(count * 2 * sizeof(float))),
                    Normals = (float*)MemAlloc((uint)(count * 3 * sizeof(float))), Colors = (byte*)MemAlloc((uint)(count * 4)) };
                var ranges = new Dictionary<GridWallCell, (int Start, int Count)>();
                var batch = new Batch(mesh, group.Key, ranges); batches.Add(batch);
                if (mesh.Vertices == null || mesh.TexCoords == null || mesh.Normals == null || mesh.Colors == null) throw new OutOfMemoryException();
                int vertex = 0;
                foreach (var wall in walls)
                {
                    int start = vertex; byte alpha = suppressed.Contains(wall.Cell) ? (byte)0 : (byte)255;
                    min = Vector3.Min(min, wall.Geometry.Min); max = Vector3.Max(max, wall.Geometry.Max);
                    foreach (var face in wall.Geometry.Faces)
                    {
                        var normal = face.Normal;
                        void Put(Vector3 p, Vector2 uv)
                        {
                            mesh.Vertices[vertex * 3] = p.X; mesh.Vertices[vertex * 3 + 1] = p.Y; mesh.Vertices[vertex * 3 + 2] = p.Z;
                            mesh.TexCoords[vertex * 2] = uv.X; mesh.TexCoords[vertex * 2 + 1] = uv.Y;
                            mesh.Normals[vertex * 3] = normal.X; mesh.Normals[vertex * 3 + 1] = normal.Y; mesh.Normals[vertex * 3 + 2] = normal.Z;
                            mesh.Colors[vertex * 4] = mesh.Colors[vertex * 4 + 1] = mesh.Colors[vertex * 4 + 2] = 255;
                            mesh.Colors[vertex * 4 + 3] = alpha; vertex++;
                        }
                        Put(face.A, face.UvA); Put(face.B, face.UvB); Put(face.C, face.UvC);
                        Put(face.A, face.UvA); Put(face.C, face.UvC); Put(face.D, face.UvD);
                    }
                    ranges[wall.Cell] = (start, vertex - start);
                }
                UploadMesh(ref mesh, true);
                // UploadMesh fills the VBO/VAO fields on the struct.
                batches[^1] = new(mesh, group.Key, ranges);
            }
            return new(revision, terrainVersion, batches.ToArray(), min, max);
        }
        catch { foreach (var batch in batches) UnloadMesh(batch.Mesh); throw; }
    }
    static void Release(Chunk chunk)
    {
        foreach (var batch in chunk.Batches)
        {
            UnloadMesh(batch.Mesh);
            foreach (var cell in batch.Ranges.Keys) cell.ReleaseGeometry();
        }
    }
    void Clear() { foreach (var chunk in chunks.Values) Release(chunk); chunks.Clear(); }
    public void Dispose() { Clear(); suppressed.Clear(); MemFree(material.Maps); material = default; }
}
