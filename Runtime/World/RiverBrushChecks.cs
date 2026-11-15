using System.Numerics;

static class RiverBrushChecks
{
    public static void Run()
    {
        var map = MapData.CreateEmpty(32, 24);
        map.Objects.Add(new() { Asset = "objects/rocks/large/rock_01.png", X = 10, Z = 8 });
        var document = new MapDocument(map, true);
        string initial = map.Serialize();
        string[] materials = (string[])map.Tiles.Clone();
        Check(document.PaintRiverSegment(new(-8, 0, -3), new(0, 0, -3), 4, 1.5f, checkpoint: true), "a segment cuts its channel and adds local water");
        Check(document.PaintRiverSegment(new(0, 0, -3), new(0, 0, 6), 4, 1.5f), "a second segment joins the river around a bend");
        for (float x = -8; x <= 0; x += .5f) Check(map.WaterDepthAt(x, -3) > 1, "a fast horizontal stroke has no dry gaps");
        for (float z = -3; z <= 6; z += .5f) Check(map.WaterDepthAt(0, z) > 1, "the bend and second segment remain connected");
        Check(map.WaterDepthAt(10, 5) == 0 && map.HeightAt(10, 5) == 0, "painting does not flood or sculpt unrelated ground");
        Check(map.Tiles.SequenceEqual(materials) && map.Objects.Count == 1 && map.Objects[0].X == 10, "the river preserves materials and authored objects");
        Check(document.Dirty && map.TerrainChunkVersion(0, 0) > 0, "river edits mark the document and terrain chunks as changed");
        string river = map.Serialize();
        Check(!document.PaintRiverSegment(new(-8, 0, -3), new(0, 0, -3), 4, 1.5f, checkpoint: true) && map.Serialize() == river,
            "painting the same course again neither deepens it nor adds an undo checkpoint");
        Check(MapData.Parse(river).Serialize() == river, "existing map JSON preserves the complete river");
        document.Undo();
        Check(document.Map.Serialize() == initial && !document.Dirty && !document.CanUndo, "one undo removes the whole multi-segment stroke");
        document.Redo(); Check(document.Map.Serialize() == river && document.Dirty, "redo restores the complete river");

        var narrow = new MapDocument(MapData.CreateEmpty(16, 16), true);
        Check(narrow.PaintRiverSegment(new(-4.5f, 0, .5f), new(4.5f, 0, .5f), 2, .25f), "the minimum brush works between terrain vertices");
        for (float x = -4.5f; x <= 4.5f; x += .25f)
            Check(narrow.Map.WaterDepthAt(x, .5f) > .05f, "the minimum-width channel is continuously wet");
        var wider = new MapDocument(MapData.CreateEmpty(16, 16), false);
        wider.PaintRiverSegment(new(-4, 0, 0), new(4, 0, 0), 8, 2);
        Check(wider.Map.WaterDepthAt(0, 3) > .5f && narrow.Map.WaterDepthAt(0, 3) == 0, "width changes the river footprint");
        Check(Math.Abs(wider.Map.WaterDepthAt(0, 0) - 2) < .001f, "depth controls the centre of the bed");

        var deepMap = MapData.CreateEmpty(16, 16); deepMap.SetVertexHeight(8, 8, -5); deepMap.SetVertexHeight(2, 2, -3);
        var deep = new MapDocument(deepMap, true);
        deep.PaintRiverSegment(new(-4, 0, 0), new(4, 0, 0), 4, 1);
        Check(deep.Map.VertexHeight(8, 8) == -5 && deep.Map.WaterDepthAt(-6, -6) == 0,
            "existing deep beds remain deep while remote depressions stay unfilled");
        var edge = new MapDocument(MapData.CreateEmpty(16, 16), true);
        Check(edge.PaintRiverSegment(new(-100, 0, -7), new(100, 0, -7), 4, 1), "a stroke crossing map bounds is clipped safely");
        edge.Map.Validate();
        Check(edge.Map.WaterDepthAt(-7.9f, -7) > .5f && edge.Map.WaterDepthAt(7.9f, -7) > .5f, "the channel reaches both map boundaries");
        string unchanged = edge.Map.Serialize();
        Check(!edge.PaintRiverSegment(new(100, 0, 100), new(110, 0, 110), 4, 1, checkpoint: true) && edge.Map.Serialize() == unchanged,
            "a completely outside stroke makes no changes");
        ExpectInvalid(() => edge.PaintRiverSegment(Vector3.Zero, new(float.NaN, 0, 0), 4, 1));
        ExpectInvalid(() => edge.PaintRiverSegment(Vector3.Zero, Vector3.One, 1, 1));
        ExpectInvalid(() => edge.PaintRiverSegment(Vector3.Zero, Vector3.One, 4, float.PositiveInfinity));
        Check(edge.Map.Serialize() == unchanged, "invalid arguments are rejected before modifying the map");

        var large = new MapDocument(MapData.CreateEmpty(2048, 2048), false);
        Check(large.PaintRiverSegment(new(-5, 0, -4), new(5, 0, 4), 4, 1), "the brush supports a large map");
        Check(large.Map.TerrainChunkVersion(64, 64) > 0 && large.Map.TerrainChunkVersion(10, 10) == 0 &&
            large.Map.Heights.Count(h => h != 0) < 200, "a large map only rebuilds the locally painted terrain");
        Console.WriteLine("River brush checks passed: continuous bends, width/depth, minimum size, no repeat deepening, local water, map bounds, validation, undo/redo, JSON and large-map locality.");
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("River brush check failed: " + message); }
    static void ExpectInvalid(Action action)
    { try { action(); } catch (ArgumentOutOfRangeException) { return; } throw new InvalidOperationException("Invalid river brush arguments accepted."); }
}
