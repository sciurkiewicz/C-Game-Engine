// The editable grid, collision lookup and GPU chunk revisions share the same index.
// Painting touches only the changed cells and their immediate neighbours.
sealed class GridWallIndex
{
    public const int ChunkSize = 8;
    readonly List<GridWallCell> source;
    readonly Dictionary<(int X, int Z), int> positions = [];
    readonly Dictionary<(int X, int Z), long> versions = [];
    readonly Dictionary<(int X, int Z), List<GridWallCell>> chunks = [];
    readonly Dictionary<(int X, int Z), float> maximumHeights = [];
    int knownCount;
    public long Revision { get; private set; }
    public ulong ContentHash { get; private set; }
    public IReadOnlyDictionary<(int X, int Z), List<GridWallCell>> Chunks => chunks;
    public bool Matches(List<GridWallCell> cells) => ReferenceEquals(source, cells) && knownCount == cells.Count;
    public GridWallIndex(List<GridWallCell> cells)
    {
        source = cells;
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i]; positions.Add((cell.X, cell.Z), i); ContentHash ^= Hash(cell);
            var key = (cell.X / ChunkSize, cell.Z / ChunkSize);
            if (!chunks.TryGetValue(key, out var chunk)) chunks[key] = chunk = [];
            chunk.Add(cell);
            maximumHeights[key] = Math.Max(maximumHeights.GetValueOrDefault(key), Top(cell));
        }
        knownCount = cells.Count;
    }
    public GridWallCell? At(int x, int z) => positions.TryGetValue((x, z), out int index) ? source[index] : null;
    public long ChunkVersion(int x, int z) => versions.GetValueOrDefault((x, z));
    public float ChunkHeight(int x, int z) => maximumHeights.GetValueOrDefault((x, z));
    static float Top(GridWallCell cell) => cell.Height * (1 + (cell.Style == WallStyle.Cave ? cell.Roughness * .15f : 0));
    public void Set(int x, int z, GridWallCell? value)
    {
        var key = (x, z); var chunkKey = (x / ChunkSize, z / ChunkSize);
        if (positions.TryGetValue(key, out int index))
        {
            var old = source[index]; ContentHash ^= Hash(old); chunks[chunkKey].Remove(old);
            if (value == null)
            {
                var last = source[^1]; source[index] = last; positions[(last.X, last.Z)] = index;
                source.RemoveAt(source.Count - 1); positions.Remove(key);
            }
            else source[index] = value;
        }
        else if (value != null) { positions[key] = source.Count; source.Add(value); }
        else return;
        if (value != null)
        {
            if (!chunks.TryGetValue(chunkKey, out var chunk)) chunks[chunkKey] = chunk = [];
            chunk.Add(value); ContentHash ^= Hash(value);
        }
        else if (chunks[chunkKey].Count == 0) chunks.Remove(chunkKey);
        if (chunks.TryGetValue(chunkKey, out var remaining)) maximumHeights[chunkKey] = remaining.Max(Top);
        else maximumHeights.Remove(chunkKey);
        knownCount = source.Count; Revision++;
        void Touch(int tx, int tz)
        {
            var c = (tx / ChunkSize, tz / ChunkSize); versions[c] = versions.GetValueOrDefault(c) + 1;
        }
        Touch(x, z); Touch(x - 1, z); Touch(x + 1, z); Touch(x, z - 1); Touch(x, z + 1);
    }
    static ulong Hash(GridWallCell cell)
    {
        ulong result = 14695981039346656037UL;
        void Add(uint value) { result ^= value; result *= 1099511628211UL; }
        Add((uint)cell.X); Add((uint)cell.Z); Add((uint)cell.Style);
        Add(BitConverter.SingleToUInt32Bits(cell.Height)); Add(BitConverter.SingleToUInt32Bits(cell.Roughness));
        Add(cell.Solid ? 1u : 0u); Add(cell.FadeWhenBlocking ? 1u : 0u);
        return result;
    }
}

sealed partial class MapDocument
{
    GridWallCell[] savedWallCells = saved ? initial.WallCells.ToArray() : [];
    ulong savedWallHash = saved ? initial.GridWalls.ContentHash : 0;
    GridWallIndex? checkedWallIndex;
    long checkedWallRevision = -1;
    bool wallsDirty;
    bool GridWallsDirty
    {
        get
        {
            var index = Map.GridWalls;
            if (ReferenceEquals(index, checkedWallIndex) && checkedWallRevision == index.Revision) return wallsDirty;
            checkedWallIndex = index; checkedWallRevision = index.Revision;
            wallsDirty = Map.WallCells.Count != savedWallCells.Length || index.ContentHash != savedWallHash;
            if (!wallsDirty)
                foreach (var savedCell in savedWallCells)
                    if (index.At(savedCell.X, savedCell.Z) is not { } cell || !cell.SameSettings(savedCell)) { wallsDirty = true; break; }
            return wallsDirty;
        }
    }
    void SaveWallState()
    {
        savedWallCells = Map.WallCells.ToArray(); savedWallHash = Map.GridWalls.ContentHash;
        checkedWallIndex = null; checkedWallRevision = -1;
    }
}
