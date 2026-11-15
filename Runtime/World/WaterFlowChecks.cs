using System.Numerics;
using System.Text.Json.Nodes;

static class WaterFlowChecks
{
    public static void Run()
    {
        var map = WetMap(32, 24);
        map.Objects.Add(new() { Asset = "objects/rocks/large/rock_01.png", X = 10, Z = 8 });
        var document = new MapDocument(map, true);
        string initial = map.Serialize();
        var materials = (string[])map.Tiles.Clone();
        var heights = (float[])map.Heights.Clone();
        var masks = (byte[])map.WaterCells.Clone();
        Check(document.PaintWaterFlowSegment(new(-8, 0, -4), new(0, 0, -4), 4, 2, checkpoint: true),
            "a dragged brush paints velocity along the first segment");
        Check(document.PaintWaterFlowSegment(new(0, 0, -4), new(0, 0, 6), 4, 2), "the current follows a bend");
        for (float x = -8; x <= -3; x += .25f)
        {
            var flow = map.SampleWaterFlow(x, -4);
            Check(flow.X > 1.9f && Math.Abs(flow.Y) < .001f, "a fast horizontal stroke has a continuous eastward current");
        }
        for (float z = -1; z <= 6; z += .25f)
        {
            var flow = map.SampleWaterFlow(0, z);
            Check(flow.Y > 1.9f && Math.Abs(flow.X) < .001f, "the next segment carries water around the corner");
        }
        Check(map.SampleWaterFlow(9, 8) == Vector2.Zero, "the current stays local to the painted route");
        Check(map.Heights.SequenceEqual(heights) && map.Tiles.SequenceEqual(materials) && map.WaterCells.SequenceEqual(masks) &&
            map.Objects.Count == 1 && map.Objects[0].X == 10 && map.TerrainChunkVersion(0, 0) == 0,
            "flow editing preserves terrain, materials, water masks, objects and terrain revisions");
        Check(document.Dirty, "flow metadata marks the map as changed");
        string painted = map.Serialize();
        Check(MapData.Parse(painted).Serialize() == painted, "flow vectors survive the existing JSON save format");
        var copy = MapSnapshot.Capture(map).Restore();
        Check(copy.Serialize() == painted && !ReferenceEquals(copy.WaterFlow, map.WaterFlow), "history and Play snapshots own independent flow dictionaries");
        int copiedCell = copy.WaterFlow.Keys.First(); copy.WaterFlow[copiedCell] = new(0, -1);
        Check(map.Serialize() == painted, "editing a restored copy cannot change the authored current");
        document.Undo();
        Check(document.Map.Serialize() == initial && !document.Dirty && !document.CanUndo, "one Undo restores the whole multi-segment flow stroke");
        document.Redo(); Check(document.Map.Serialize() == painted && document.Dirty, "Redo restores every painted vector");

        var reverse = new MapDocument(WetMap(16, 16), true);
        reverse.PaintWaterFlowSegment(new(-4, 0, 0), new(4, 0, 0), 4, 2);
        reverse.PaintWaterFlowSegment(new(4, 0, 0), new(-4, 0, 0), 4, 3);
        Check(reverse.Map.SampleWaterFlow(0, 0).X < -2.99f, "dragging the other way reverses the current and updates speed");
        Check(reverse.PaintWaterFlowSegment(new(-4, 0, 0), new(4, 0, 0), 4, 0) && reverse.Map.SampleWaterFlow(0, 0) == Vector2.Zero,
            "a zero-speed moving stroke stops the current");
        reverse.PaintWaterFlowSegment(new(-4, 0, 0), new(4, 0, 0), 4, 2);
        Check(reverse.PaintWaterFlowSegment(Vector3.Zero, Vector3.Zero, 4, 1, erase: true) && reverse.Map.SampleWaterFlow(0, 0) == Vector2.Zero &&
            reverse.Map.SampleWaterFlow(3, 0).X > 1.9f, "a stationary eraser clears only its own footprint");
        Check(reverse.PaintWaterFlowSegment(new(-4, 0, 0), new(4, 0, 0), 4, 1, erase: true) && reverse.Map.WaterFlow.Count == 0,
            "erasing the entire route removes its sparse entries");
        Check(!reverse.PaintWaterFlowSegment(Vector3.Zero, Vector3.Zero, 4, 2, checkpoint: true), "a directionless normal brush does not edit water");

        var smooth = WetMap(8, 8);
        smooth.WaterFlow[Cell(smooth, -.5f, -.5f)] = new(2, 0);
        smooth.WaterFlow[Cell(smooth, .5f, -.5f)] = new(0, 2);
        smooth.WaterFlow[Cell(smooth, -.5f, .5f)] = new(2, 0);
        smooth.WaterFlow[Cell(smooth, .5f, .5f)] = new(0, 2);
        Check(Near(smooth.SampleWaterFlow(0, 0), new(1, 1)), "bilinear sampling blends local directions between all four cell centres");
        var before = smooth.SampleWaterFlow(-.0001f, 0); var after = smooth.SampleWaterFlow(.0001f, 0);
        Check(Vector2.Distance(before, after) < .001f, "sampling crosses cell boundaries without a seam");
        smooth.WaterFlow[0] = new(1, 0);
        Check(Near(smooth.SampleWaterFlow(-3.99f, -3.99f), new(1, 0)) && smooth.SampleWaterFlow(4, 0) == Vector2.Zero &&
            smooth.SampleWaterFlow(float.NaN, 0) == Vector2.Zero, "sampling clamps edge cell centres and safely rejects outside or invalid positions");

        var falloff = new MapDocument(WetMap(16, 16), true);
        falloff.PaintWaterFlowSegment(new(-4, 0, .25f), new(4, 0, .25f), 4, 2);
        float centre = falloff.Map.SampleWaterFlow(.5f, .5f).Length();
        float edge = falloff.Map.SampleWaterFlow(.5f, -1.5f).Length();
        Check(centre > 1.99f && edge > 0 && edge < centre, "the outside of the brush blends softly into the existing current");
        var narrow = new MapDocument(WetMap(16, 16), true);
        narrow.PaintWaterFlowSegment(new(-4, 0, 0), new(4, 0, 0), 2, 4);
        for (float x = -4; x <= 4; x += .25f)
            Check(narrow.Map.SampleWaterFlow(x, 0).X > 3.99f, "the minimum width remains continuous between grid cells at the maximum speed");
        narrow.Map.Validate();

        var dry = new MapDocument(MapData.CreateEmpty(16, 16), true);
        string dryInitial = dry.Map.Serialize();
        Check(!dry.PaintWaterFlowSegment(new(-6, 0, 0), new(6, 0, 0), 4, 1, checkpoint: true) &&
            dry.Map.Serialize() == dryInitial && !dry.Dirty && !dry.CanUndo, "painting dry ground neither carves, floods nor creates an empty Undo");
        dry.Map.WaterCells = Enumerable.Repeat((byte)3, dry.Map.Tiles.Length).ToArray();
        Check(!dry.PaintWaterFlowSegment(new(-6, 0, 0), new(6, 0, 0), 4, 1), "a dry masked surface is not mistaken for actual water");
        var partial = MapData.CreateEmpty(8, 8);
        int partialCell = 4 * partial.Width + 4;
        partial.WaterCells = new byte[partial.Tiles.Length]; partial.WaterCells[partialCell] = 1;
        partial.SetVertexHeight(4, 5, -1);
        var partialDocument = new MapDocument(partial, true);
        Check(partialDocument.PaintWaterFlowSegment(new(-1, 0, .5f), new(2, 0, .5f), 2, 1) &&
            partial.WaterFlow.Count == 1 && partial.SampleWaterFlow(.1f, .8f).X > 0 && partial.SampleWaterFlow(.8f, .1f) == Vector2.Zero,
            "partially submerged triangle cells accept flow while the dry half stays dry");

        var bounds = new MapDocument(WetMap(16, 16), true);
        Check(bounds.PaintWaterFlowSegment(new(-100, 0, -7.5f), new(100, 0, -7.5f), 2, 1, checkpoint: true) &&
            bounds.Map.SampleWaterFlow(-7.99f, -7.5f).X > .99f && bounds.Map.SampleWaterFlow(7.99f, -7.5f).X > .99f,
            "a stroke crossing both map bounds is clipped safely");
        string bounded = bounds.Map.Serialize();
        Check(!bounds.PaintWaterFlowSegment(new(100, 0, 100), new(110, 0, 110), 4, 1, checkpoint: true), "a completely outside stroke does nothing");
        ExpectInvalid(() => bounds.PaintWaterFlowSegment(Vector3.Zero, new(float.NaN, 0, 0), 4, 1));
        ExpectInvalid(() => bounds.PaintWaterFlowSegment(Vector3.Zero, Vector3.One, 1, 1));
        ExpectInvalid(() => bounds.PaintWaterFlowSegment(Vector3.Zero, Vector3.One, 33, 1));
        ExpectInvalid(() => bounds.PaintWaterFlowSegment(Vector3.Zero, Vector3.One, 4, -1));
        ExpectInvalid(() => bounds.PaintWaterFlowSegment(Vector3.Zero, Vector3.One, 4, 5));
        ExpectInvalid(() => bounds.PaintWaterFlowSegment(Vector3.Zero, Vector3.One, 4, float.PositiveInfinity));
        Check(bounds.Map.Serialize() == bounded, "invalid brush arguments are rejected before changing data or history");
        bounds.Undo(); Check(bounds.Map.WaterFlow.Count == 0 && !bounds.CanUndo, "no-op and invalid strokes add no checkpoints");

        var legacy = JsonNode.Parse(initial)!.AsObject(); legacy.Remove("WaterFlow");
        Check(MapData.Parse(legacy.ToJsonString()).WaterFlow.Count == 0, "older maps default to still water without a schema migration");
        var invalid = WetMap(8, 8); invalid.WaterFlow[-1] = new(1, 0); ExpectInvalidMap(invalid);
        invalid.WaterFlow.Clear(); invalid.WaterFlow[invalid.Tiles.Length] = new(1, 0); ExpectInvalidMap(invalid);
        invalid.WaterFlow.Clear(); invalid.WaterFlow[0] = new(4, 4); ExpectInvalidMap(invalid);
        invalid.WaterFlow[0] = new(float.NaN, 0); ExpectInvalidMap(invalid);
        invalid.WaterFlow = null!; ExpectInvalidMap(invalid);

        var largeMap = MapData.CreateEmpty(2048, 2048); largeMap.WaterLevel = 1;
        largeMap.WaterCells = new byte[largeMap.Tiles.Length];
        for (int z = 1020; z < 1028; z++)
        for (int x = 1018; x < 1030; x++) largeMap.WaterCells[z * largeMap.Width + x] = 3;
        var large = new MapDocument(largeMap, false);
        Check(large.PaintWaterFlowSegment(new(-5, 0, -2), new(5, 0, 2), 4, 1) && largeMap.WaterFlow.Count is > 0 and < 100,
            "large maps keep only the locally painted flow cells");
        Check(largeMap.Heights.Length == 0 && largeMap.TerrainChunkVersion(64, 64) == 0, "flow never rebuilds terrain chunks on a large map");
        largeMap.Validate();
        Console.WriteLine("Water flow checks passed: local directions and bends, speed, interpolation, soft edges, erasing, real-water-only editing, bounds, validation, one-step undo/redo, JSON, old maps, independent snapshots and sparse large maps.");
    }

    static MapData WetMap(int width, int depth)
    {
        var map = MapData.CreateEmpty(width, depth);
        map.WaterLevel = 1; map.WaterCells = Enumerable.Repeat((byte)3, width * depth).ToArray();
        return map;
    }
    static int Cell(MapData map, float x, float z) => (int)(z + map.Depth / 2f) * map.Width + (int)(x + map.Width / 2f);
    static bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .001f;
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Water flow check failed: " + message); }
    static void ExpectInvalid(Action action)
    { try { action(); } catch (ArgumentOutOfRangeException) { return; } throw new InvalidOperationException("Invalid water flow brush arguments accepted."); }
    static void ExpectInvalidMap(MapData map)
    { try { map.Validate(); } catch (InvalidDataException) { return; } throw new InvalidOperationException("Invalid water flow map accepted."); }
}
