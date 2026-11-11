using System.Numerics;
using System.Text.Json.Serialization;

// Sparse, one-metre wall cells. Empty cells are the dungeon's walkable corridors.
sealed class GridWallCell
{
    public const int Limit = 65536;
    public int X { get; init; }
    public int Z { get; init; }
    public WallStyle Style { get; init; }
    public float Height { get; init; } = 3;
    public float Roughness { get; init; } = .75f;
    public bool Solid { get; init; } = true;
    public bool FadeWhenBlocking { get; init; } = true;
    WallData? view;
    MapData? geometryMap;
    WallMesh? mesh;
    int meshVersion;
    Vector2[]? footprint;
    internal void ReleaseGeometry() => mesh = null;

    public GridWallCell Copy(int x, int z) => new() { X = x, Z = z, Style = Style, Height = Height, Roughness = Roughness, Solid = Solid, FadeWhenBlocking = FadeWhenBlocking };
    public bool SameSettings(GridWallCell other) => Style == other.Style && Height == other.Height && Roughness == other.Roughness && Solid == other.Solid && FadeWhenBlocking == other.FadeWhenBlocking;
    public void Validate(int width, int depth)
    {
        if (X < 0 || X >= width || Z < 0 || Z >= depth || !Enum.IsDefined(Style) || !MapData.FiniteRange(Height, .5f, 12) || !MapData.FiniteRange(Roughness, 0, 1))
            throw new InvalidDataException("Invalid dungeon wall cell.");
    }
    public WallData AsWall(MapData map) => view ??= new WallData {
        Id = $"grid:{X}:{Z}", Name = "Ściana lochu", Style = Style, Height = Height, Thickness = 1,
        Solid = Solid, FadeWhenBlocking = FadeWhenBlocking, Cell = this };
    public Vector2[] Footprint(MapData map)
    {
        if (!ReferenceEquals(geometryMap, map)) { footprint = null; mesh = null; geometryMap = map; }
        return footprint ??= GridWallGeometry.Outline(map, this);
    }
    public WallMesh Geometry(MapData map)
    {
        var outline = Footprint(map);
        var hash = new HashCode(); hash.Add(map.TerrainGeneration);
        foreach (var p in outline)
        {
            int cx = Math.Clamp((int)(p.X + map.Width / 2f), 0, map.Width - 1) / MapData.TerrainChunkSize;
            int cz = Math.Clamp((int)(p.Y + map.Depth / 2f), 0, map.Depth - 1) / MapData.TerrainChunkSize;
            hash.Add(map.TerrainChunkVersion(cx, cz));
        }
        foreach (var direction in GridWallGeometry.Directions)
        {
            var neighbour = map.WallCellAt(X + direction.X, Z + direction.Z);
            hash.Add(neighbour?.Style); hash.Add(neighbour?.Height); hash.Add(neighbour?.Roughness);
        }
        int version = hash.ToHashCode();
        if (mesh == null || version != meshVersion) { mesh = GridWallGeometry.Build(map, this, outline); meshVersion = version; }
        return mesh;
    }
}

sealed partial class MapData
{
    List<GridWallCell> wallCells = [];
    GridWallIndex? wallIndex;
    public List<GridWallCell> WallCells
    {
        get => wallCells;
        set { wallCells = value; wallIndex = null; }
    }
    [JsonIgnore] public GridWallIndex GridWalls
    {
        get
        {
            if (wallIndex == null || !wallIndex.Matches(WallCells)) wallIndex = new(WallCells);
            return wallIndex;
        }
    }
    public GridWallCell? WallCellAt(int x, int z) => GridWalls.At(x, z);
    [JsonIgnore] public IEnumerable<WallData> RenderWalls => Walls.Concat(WallCells.Select(c => c.AsWall(this)));
    [JsonIgnore] public int RenderWallCount => Walls.Count + WallCells.Count;
    void ValidateWallCells()
    {
        if (WallCells == null || WallCells.Count > GridWallCell.Limit || WallCells.Any(c => c == null) || WallCells.Select(c => (c.X, c.Z)).Distinct().Count() != WallCells.Count)
            throw new InvalidDataException("Invalid dungeon wall grid.");
        foreach (var cell in WallCells) cell.Validate(Width, Depth);
        if (wallIndex != null && (!wallIndex.Matches(WallCells) || WallCells.Any(c => !ReferenceEquals(wallIndex.At(c.X, c.Z), c)))) wallIndex = null;
    }
}

sealed partial class MapDocument
{
    public bool PaintWallCells(IEnumerable<(int X, int Z)> positions, GridWallCell settings, bool erase, bool checkpoint = true)
    {
        settings.Copy(0, 0).Validate(Map.Width, Map.Depth);
        var points = positions.Where(p => p.X >= 0 && p.X < Map.Width && p.Z >= 0 && p.Z < Map.Depth).Distinct().ToArray();
        var index = Map.GridWalls;
        var changes = new List<((int X, int Z) Position, GridWallCell? Cell)>();
        int count = Map.WallCells.Count;
        foreach (var p in points)
        {
            var old = index.At(p.X, p.Z);
            if (erase && old != null) { changes.Add((p, null)); count--; }
            else if (!erase && (old == null || !old.SameSettings(settings)))
            { changes.Add((p, settings.Copy(p.X, p.Z))); if (old == null) count++; }
        }
        if (count > GridWallCell.Limit) throw new InvalidDataException("Limit ścian lochu: 65536 kratek.");
        if (changes.Count == 0) return false;
        if (checkpoint) Checkpoint();
        foreach (var change in changes) index.Set(change.Position.X, change.Position.Z, change.Cell);
        return true;
    }
}
