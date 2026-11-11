using System.Numerics;
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter<WallStyle>))]
enum WallStyle { Cave, Masonry, Wood }

sealed class WallData
{
    public const int Limit = 256, PointLimit = 128;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Ściana";
    public WallStyle Style { get; set; }
    public float Height { get; set; } = 3;
    public float Thickness { get; set; } = .6f;
    public bool Closed { get; set; }
    public bool Solid { get; set; } = true;
    public bool FadeWhenBlocking { get; set; } = true;
    public List<GroundPoint2> Points { get; set; } = [];
    [JsonIgnore] public GridWallCell? Cell { get; init; }

    int pathVersion;
    WallSection[]? sections;
    Vector2[][] footprints = [];
    MapData? meshMap;
    int meshVersion;
    WallMesh? mesh;
    (int X, int Z)[] chunks = [];

    public void Validate(Func<float, float, bool> contains)
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 128 || string.IsNullOrWhiteSpace(Name) || Name.Length > 80 ||
            !Enum.IsDefined(Style) || !MapData.FiniteRange(Height, .25f, 24) || !MapData.FiniteRange(Thickness, .1f, 4) ||
            Points == null || Points.Count < (Closed ? 3 : 2) || Points.Count > PointLimit ||
            Points.Any(p => p == null || !contains(p.X, p.Z)))
            throw new InvalidDataException("Ściana wymaga co najmniej dwóch punktów, poprawnej wysokości i grubości.");
        float length = 0;
        for (int i = 0; i < Points.Count - (Closed ? 0 : 1); i++)
        {
            float distance = Vector2.Distance(Points[i].Vector, Points[(i + 1) % Points.Count].Vector);
            if (distance < .1f) throw new InvalidDataException("Punkty ściany muszą być oddalone o co najmniej 0,1 m.");
            length += distance;
        }
        if (length > 8192) throw new InvalidDataException("Jedna ściana może mieć do 8192 m długości.");
        for (int i = Closed ? 0 : 1; i < Points.Count - (Closed ? 0 : 1); i++)
        {
            var incoming = Vector2.Normalize(Points[i].Vector - Points[(i + Points.Count - 1) % Points.Count].Vector);
            var outgoing = Vector2.Normalize(Points[(i + 1) % Points.Count].Vector - Points[i].Vector);
            if (Vector2.Dot(incoming, outgoing) < -.98f) throw new InvalidDataException("Ściana nie może zawracać w tym samym miejscu. Rozsuń punkty zakrętu.");
        }
    }

    void EnsurePath()
    {
        var hash = new HashCode(); hash.Add(Thickness); hash.Add(Closed);
        foreach (var p in Points) { hash.Add(p.X); hash.Add(p.Z); }
        int version = hash.ToHashCode();
        if (sections != null && pathVersion == version) return;
        pathVersion = version; sections = WallGeometry.Sample(this);
        footprints = new Vector2[Math.Max(0, sections.Length - 1)][];
        for (int i = 0; i < footprints.Length; i++)
        {
            var a = sections[i]; var b = sections[i + 1];
            footprints[i] = [a.Center + a.Side, b.Center + b.Side, b.Center - b.Side, a.Center - a.Side];
        }
        mesh = null; meshMap = null;
    }

    public WallMesh Geometry(MapData map)
    {
        if (Cell != null) return Cell.Geometry(map);
        EnsurePath();
        if (!ReferenceEquals(map, meshMap))
        {
            meshMap = map; mesh = null;
            chunks = sections!.SelectMany(s => new[] { s.Center + s.Side, s.Center - s.Side })
                .Select(p => (Math.Clamp((int)(p.X + map.Width / 2f), 0, map.Width - 1) / MapData.TerrainChunkSize,
                    Math.Clamp((int)(p.Y + map.Depth / 2f), 0, map.Depth - 1) / MapData.TerrainChunkSize)).Distinct().ToArray();
        }
        var hash = new HashCode(); hash.Add(pathVersion); hash.Add(Height); hash.Add(map.TerrainGeneration);
        foreach (var c in chunks) hash.Add(map.TerrainChunkVersion(c.X, c.Z));
        int version = hash.ToHashCode();
        if (mesh == null || meshVersion != version)
        { mesh = WallGeometry.Build(map, this, sections!); meshVersion = version; }
        return mesh;
    }

    public bool Blocks(Vector2 from, Vector2 to, float radius)
    {
        if (!Solid) return false;
        EnsurePath();
        foreach (var polygon in footprints)
        {
            // Reject distant pieces before the polygon test; the full swept path prevents tunnelling.
            if (CollisionGeometry.PointSegmentDistance((polygon[0] + polygon[2]) / 2, from, to) >
                Vector2.Distance(polygon[0], polygon[2]) / 2 + Thickness + radius) continue;
            if (CollisionGeometry.BlocksPolygon(polygon, from, to, radius)) return true;
        }
        return false;
    }
}

sealed partial class MapData
{
    public List<WallData> Walls { get; set; } = [];
    void ValidateWalls()
    {
        if (Walls == null || Walls.Count > WallData.Limit || Walls.Any(w => w == null) ||
            Walls.Select(w => w.Id).Distinct().Count() != Walls.Count)
            throw new InvalidDataException("Invalid walls.");
        foreach (var wall in Walls) wall.Validate(Contains);
    }
}

sealed partial class MapDocument
{
    public WallData ApplyWall(WallData wall, string? replaceId = null)
    {
        wall.Validate(Map.Contains);
        int index = replaceId == null ? -1 : Map.Walls.FindIndex(w => w.Id == replaceId);
        if (replaceId != null && index < 0) throw new InvalidDataException("Wybrana ściana już nie istnieje.");
        if (index < 0 && Map.Walls.Count >= WallData.Limit) throw new InvalidDataException("Limit ścian: 256.");
        if (Map.Walls.Any(w => w.Id == wall.Id && w.Id != replaceId)) throw new InvalidDataException("Wall ID already exists.");
        Checkpoint();
        var copy = ScenePrefab.Clone(wall);
        if (index < 0) Map.Walls.Add(copy); else Map.Walls[index] = copy;
        return copy;
    }
    public void DeleteWall(string id)
    {
        var wall = Map.Walls.FirstOrDefault(w => w.Id == id); if (wall == null) return;
        Checkpoint(); Map.Walls.Remove(wall);
    }
}
