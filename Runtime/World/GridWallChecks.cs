using System.Numerics;

static class GridWallChecks
{
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Dungeon wall check failed: " + message); }
    public static void Run()
    {
        var document = new MapDocument(MapData.CreateEmpty(24, 24, MapType.Cave), true);
        var stone = new GridWallCell { Style = WallStyle.Masonry };
        document.PaintWallCells(GridWallGeometry.Rectangle((4, 4), (18, 18)), stone, false);
        Check(document.Map.WallCells.Count == 56 && document.Map.WallCellAt(10, 10) == null, "room outline paints its perimeter and leaves an empty interior");
        string room = document.Map.Serialize();
        document.Undo(); Check(document.Map.WallCells.Count == 0 && !document.Dirty, "room creation is one undo step");
        document.Redo(); Check(document.Map.Serialize() == room, "redo restores the room");
        document.PaintWallCells(GridWallGeometry.Stroke((11, 4), (11, 18)), stone, false);
        Check(!WorldRenderer.CanWalk(document.Map, new(3, 0, -.5f), new(-3, 0, -.5f)), "a divider blocks movement between rooms");
        document.PaintWallCells([(11, 11)], stone, true);
        Check(WorldRenderer.CanWalk(document.Map, new(3, 0, -.5f), new(-3, 0, -.5f)), "erasing a cell opens a one-metre corridor");
        document.Undo(); Check(!WorldRenderer.CanWalk(document.Map, new(3, 0, -.5f), new(-3, 0, -.5f)), "undo closes a carved passage");

        document = new(MapData.CreateEmpty(24, 24, MapType.Cave), true);
        var cave = new GridWallCell { Style = WallStyle.Cave, Roughness = 1 };
        document.PaintWallCells(GridWallGeometry.Stroke((10, 4), (10, 18)), cave, false);
        document.PaintWallCells(GridWallGeometry.Stroke((12, 4), (12, 18)), cave, false);
        for (int i = 0; i < 40; i++)
        {
            float z = -7 + i * .3f;
            Check(WorldRenderer.CanWalk(document.Map, new(-.5f, 0, z + .3f), new(-.5f, 0, z)), "uneven cave walls preserve a walkable one-cell corridor");
        }
        Check(!WorldRenderer.CanWalk(document.Map, new(-3, 0, -.5f), new(3, 0, -.5f)), "long steps cannot tunnel through grid walls");
        var cell = document.Map.WallCellAt(10, 10)!;
        var mesh = cell.Geometry(document.Map);
        Check(mesh.Max.Y - mesh.Min.Y > 2.7f && mesh.Faces.SelectMany(f => new[] { f.A.Y, f.B.Y, f.C.Y, f.D.Y }).Where(y => y > 2).Distinct().Count() > 8,
            "cave geometry has an irregular crest");
        Check(cell.Footprint(document.Map).Any(p => Math.Abs(p.X - MathF.Round(p.X)) > .04f && Math.Abs(p.Y - MathF.Round(p.Y)) > .04f), "cave wall sides leave the perfect grid outline");
        Check(ReferenceEquals(mesh, cell.Geometry(document.Map)), "unchanged cave geometry is cached");
        var restored = MapSnapshot.Capture(document.Map).Restore().WallCellAt(10, 10)!;
        Check(restored.Geometry(document.Map).Faces.SequenceEqual(mesh.Faces), "cave variation stays deterministic after save and restore");

        document = new(MapData.CreateEmpty(12, 12), true);
        document.PaintWallCells([(4, 4)], stone, false);
        var first = document.Map.WallCellAt(4, 4)!; int standalone = first.Geometry(document.Map).Faces.Length;
        document.PaintWallCells([(5, 4)], stone, false, false);
        Check(first.Geometry(document.Map).Faces.Length < standalone, "neighbouring grid blocks omit hidden internal faces");
        document.PaintWallCells([(5, 4)], stone, true, false);
        Check(first.Geometry(document.Map).Faces.Length == standalone, "erasing a neighbour restores its visible boundary");
        document.PaintWallCells(GridWallGeometry.Stroke((4, 4), (8, 4)), stone, false, false);
        Check(document.Map.WallCells.Count == 5, "a fast brush movement fills all intermediate cells");
        document.Undo(); Check(document.Map.WallCells.Count == 0, "a continuous brush stroke can share one checkpoint");
        document.Redo(); Check(document.Map.WallCells.Count == 5, "redo restores the complete stroke");
        Check(!document.PaintWallCells(GridWallGeometry.Stroke((4, 4), (8, 4)), stone, false), "painting unchanged cells is a no-op");
        var regular = document.Map.WallCellAt(4, 4)!.Geometry(document.Map);
        Check(regular.Max.Y == 3 && regular.Faces.All(f => float.IsFinite(f.Normal.Y)), "building walls remain flat and have valid normals");
        string before = document.Map.Serialize();
        try { document.PaintWallCells([(1, 1)], new() { Roughness = float.NaN }, false); throw new Exception("Invalid brush accepted."); } catch (InvalidDataException) { }
        Check(document.Map.Serialize() == before, "invalid paint parameters leave the map untouched");
        var invalid = MapSnapshot.Capture(document.Map).Restore(); invalid.WallCells.Add(invalid.WallCells[0]);
        try { invalid.Validate(); throw new Exception("Duplicate wall cells accepted."); } catch (InvalidDataException) { }

        string path = Path.Combine(Path.GetTempPath(), "wildwood-dungeon-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            document.Save(path); var loaded = MapData.Load(path);
            Check(loaded.Serialize() == document.Map.Serialize(), "wall cells round-trip through map JSON");
            var play = new PlaySession(loaded);
            Check(play.Map.WallCells.Count == 5 && !WorldRenderer.CanWalk(play.Map, new(-1.5f, 0, -1.5f)), "Runtime loads solid grid walls");
            play.Map.WallCells.Clear(); Check(loaded.WallCells.Count == 5, "Play uses its own grid copy");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        Console.WriteLine("Dungeon wall checks passed: room outlines, painting/erasing, corridors, rough geometry, adjacency, swept collision, snapshots, undo/redo and Runtime.");
    }
}
