using System.Numerics;

static class EditorWallFragmentChecks
{
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Wall fragment check failed: " + message); }
    public static void Run()
    {
        var map = MapData.CreateEmpty(24, 24);
        map.Walls.Add(new() { Name = "Drewno", Style = WallStyle.Wood, Height = 2.5f, Thickness = .4f, Solid = false, FadeWhenBlocking = false, Points = [new(-6, -6), new(-2, -6)] });
        map.Walls.Add(new() { Name = "Pokój", Style = WallStyle.Masonry, Closed = true, Points = [new(-7, -5), new(-3, -5), new(-3, -2), new(-7, -2)] });
        map.Walls.Add(new() { Name = "Przecięta", Points = [new(-10, -1), new(8, -1)] });
        map.WallCells.Add(new() { X = 5, Z = 6, Style = WallStyle.Cave, Roughness = .9f });
        map.Validate();
        var fragment = MapFragment.Capture(map, new(4, 4, 8, 8));
        Check(fragment.Walls.Count == 3 && fragment.Walls.Single(w => w.Name == "Pokój").Closed, "capture includes complete rooms and all wall styles");
        Check(fragment.WallCells.Count == 1 && fragment.WallCells[0].X == 1 && fragment.WallCells[0].Z == 2 && fragment.WallCells[0].Roughness == .9f, "capture includes local dungeon grid coordinates and roughness");
        var clipped = fragment.Walls.Single(w => w.Name == "Przecięta");
        Check(clipped.Points.All(p => fragment.Contains(p.X, p.Z)) && clipped.Points[0].X == 0 && clipped.Points[^1].X > 7.99f,
            "a wall crossing selection boundaries is clipped to the selected rectangle");
        var wood = fragment.Walls.Single(w => w.Name == "Drewno");
        Check(wood.Style == WallStyle.Wood && wood.Height == 2.5f && wood.Thickness == .4f && !wood.Solid && !wood.FadeWhenBlocking, "wall settings survive capture");
        var document = new MapDocument(map, true); string before = map.Serialize();
        var destination = new MapRegion(14, 14, 8, 8);
        document.PasteFragment(fragment, destination, new());
        Check(document.Map.Walls.Count == 6 && document.Map.Walls.Select(w => w.Id).Distinct().Count() == 6, "pasting creates independent wall IDs");
        Check(document.Map.WallCellAt(15, 16)?.Roughness == .9f && document.Map.WallCellAt(5, 6) != null, "pasting translates dungeon cells without moving the source");
        var copy = document.Map.Walls.Last(w => w.Name == "Drewno");
        Check(copy.Points[0].Vector == new Vector2(4, 4) && copy.Id != map.Walls[0].Id, "pasting translates wall coordinates into the destination");
        string pasted = document.Map.Serialize();
        document.Undo(); Check(document.Map.Serialize() == before, "wall and terrain paste is one undo step");
        document.Redo(); Check(document.Map.Serialize() == pasted, "redo restores exactly the same copied walls");

        var replacement = MapData.CreateEmpty(24, 24);
        replacement.Walls.Add(new() { Name = "Cel", Points = [new(-11, 6), new(11, 6)] });
        var replace = new MapDocument(replacement, true);
        replace.PasteFragment(fragment, destination, new() { ReplaceContents = true });
        var outside = replace.Map.Walls.Where(w => w.Name == "Cel").ToArray();
        Check(outside.Length == 2 && outside.Select(w => w.Id).Distinct().Count() == 2 &&
            outside.All(w => w.Points.All(p => p.X <= 2.001f || p.X >= 9.999f)), "replacement preserves wall portions outside the destination");
        replacement = MapData.CreateEmpty(24, 24);
        replacement.Walls.Add(new() { Name = "Zostaje", Points = [new(-11, 6), new(11, 6)] });
        replace = new MapDocument(replacement, true);
        replace.PasteFragment(fragment, destination, new() { Walls = false, ReplaceContents = true });
        Check(replace.Map.Walls.Count == 1 && replace.Map.Walls[0].Points.Count == 2 && replace.Map.Walls[0].Points[0].X == -11,
            "disabling Walls preserves all destination walls during replacement");
        var onlyWalls = new FragmentPasteOptions { Terrain = false, Actors = false, Roads = false, WorldFeatures = false };
        var empty = new MapDocument(MapData.CreateEmpty(24, 24), true);
        empty.PasteFragment(fragment, destination, onlyWalls);
        Check(empty.Map.Walls.Count == 3 && empty.Map.Tiles.All(t => t == "grass"), "walls can be pasted as a separate category");
        onlyWalls.Walls = false;
        Check(MapDocument.FragmentPasteIssue(empty.Map, fragment, destination, onlyWalls) != null, "all categories disabled is rejected");
        var full = MapData.CreateEmpty(24, 24);
        for (int i = 0; i < WallData.Limit; i++) full.Walls.Add(new() { Points = [new(-8, -8), new(-7, -8)] });
        var limited = new MapDocument(full, true); before = full.Serialize();
        try { limited.PasteFragment(fragment, destination, new()); throw new Exception("Wall limit accepted."); } catch (InvalidDataException) { }
        Check(limited.Map.Serialize() == before && !limited.CanUndo, "wall limit failure is atomic and leaves no undo entry");
        Console.WriteLine("Wall fragment checks passed: rooms, clipping, independent copies, categories, replacement, limits and undo/redo.");
    }
}
