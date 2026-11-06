using System.Numerics;

static class EditorFragmentChecks
{
    static MapRegion Source => new(8, 6, 8, 6);
    static MapData Fixture()
    {
        var map = MapData.CreateEmpty(32, 24); map.Player.X = 13; map.Player.Z = 9; map.WaterLevel = 3; map.WaveHeight = .1f;
        for (int z = 6; z <= 12; z++)
        for (int x = 8; x <= 16; x++) map.SetVertexHeight(x, z, (x - 8) * .25f + (z - 6) * .5f);
        for (int z = 6; z < 12; z++)
        for (int x = 8; x < 16; x++) map.Tiles[z * 32 + x] = (x + z) % 2 == 0 ? "dirt" : "stone";
        map.WaterCells = new byte[map.Tiles.Length]; map.WaterCells[7 * 32 + 9] = 1; map.WaterCells[8 * 32 + 10] = 2;
        map.Objects.Add(new() { Id = "door", Asset = "objects/door.png", X = -6, Z = -4, IsDoor = true, Collider = new() { Kind = ColliderKind.Box, Width = 2, Depth = .5f, Rotation = 30 } });
        map.Objects.Add(new() { Id = "tree", Asset = "objects/vegetation/tree.png", X = -7, Z = -5, Height = 5 });
        map.Objects.Add(new() { Id = "outside", Asset = "objects/rock.png", X = 12, Z = 9 });
        map.Npcs.Add(new() { Id = "npc", Name = "Strażnik Żółw", X = -5, Z = -3 });
        var chest = new ChestData { Id = "chest", X = -4, Z = -3 }; chest.Contents.Add("sword"); chest.Contents.Add("potion"); map.Chests.Add(chest);
        map.Monsters.Add(new() { Id = "monster", X = -3, Z = -2, MaxHealth = 75, Damage = 12 });
        map.Lights.Add(new() { Id = "light", X = -7, Z = -4, Height = 2.5f });
        map.Sounds.Add(new() { Id = "sound", X = -5, Z = -5, Clip = "water" });
        map.Triggers.Add(new() { Id = "door-trigger", X = -7, Z = -4, Action = TriggerAction.ToggleDoor, TargetId = "door" });
        map.Triggers.Add(new() { Id = "npc-trigger", X = -5, Z = -5, Action = TriggerAction.Dialogue, TargetId = "npc" });
        map.Triggers.Add(new() { Id = "local-teleport", X = -4, Z = -5, Action = TriggerAction.Teleport, DestinationX = -3, DestinationZ = -2 });
        map.Triggers.Add(new() { Id = "external-teleport", X = -2, Z = -5, Action = TriggerAction.Teleport, DestinationX = 13, DestinationZ = 9 });
        map.Triggers.Add(new() { Id = "change-map", X = -7, Z = -2, Action = TriggerAction.ChangeMap, MapFile = "second.json", DestinationX = 999, DestinationZ = -600 });
        map.Roads.Add(new() { Id = "road", Name = "Trail", Width = 1, Softness = .25f, Points = [new(-7, -5), new(-4, -4), new(-1, -5)] });
        map.Roads.Add(new() { Id = "crossing", Name = "Crossing", Width = 1, Points = [new(-12, -1), new(8, -1)], Softness = .25f });
        map.Validate(); return map;
    }
    public static void Run()
    {
        CaptureAndLibrary(); PasteAndLinks(); Replacement(); WaterAndNoop(); BoundsAndAtomicity();
        Console.WriteLine("Map fragment checks passed: bounded capture, frozen clipboard/library, terrain/water, clipped roads, actors/inventory/dialogues, link remapping, independent copies, replacement, limits, atomic failures and undo/redo.");
    }
    static void CaptureAndLibrary()
    {
        var map = Fixture(); string original = map.Serialize();
        var fragment = MapFragment.Capture(map, Source);
        Check(map.Serialize() == original && fragment.Width == 8 && fragment.Depth == 6, "capture is read-only and keeps rectangular dimensions");
        for (int z = 0; z < 6; z++)
        for (int x = 0; x < 8; x++)
            Check(fragment.Tiles[z * 8 + x] == map.Tiles[(z + 6) * 32 + x + 8] && fragment.WaterMask(z * 8 + x) == map.WaterMask((z + 6) * 32 + x + 8), "capture copies every material and water triangle");
        Check(fragment.VertexHeight(8, 6) == map.VertexHeight(16, 12) && fragment.Actors.Entities.Count() == 5 && fragment.Lights.Count == 1 && fragment.Triggers.Count == 5,
            "capture includes boundary vertices, actors and world features");
        var crossing = fragment.Roads.Single(r => r.Name == "Crossing");
        Check(fragment.Roads.Count == 2 && !crossing.Smooth && crossing.Points.All(p => fragment.Contains(p.X, p.Z)) && crossing.Curve.All(p => p.X >= 0 && p.X < 8), "crossing roads are clipped to the selected area");
        Check(fragment.Roads.Single(r => r.Name == "Trail").Smooth, "fully contained roads retain editable smooth control points");
        Check(fragment.Triggers.Single(t => t.Data.Id == "local-teleport").LocalDestination && !fragment.Triggers.Single(t => t.Data.Id == "external-teleport").LocalDestination,
            "local and external destinations are distinguished");
        map.Tiles[6 * 32 + 8] = "sand"; map.Npcs[0].Name = "Changed"; map.SetVertexHeight(16, 12, 50);
        Check(fragment.Tiles[0] != "sand" && fragment.Actors.Npcs[0].Name == "Strażnik Żółw" && fragment.VertexHeight(8, 6) == 5, "clipboard does not alias source terrain or actors");
        string directory = Path.Combine(Path.GetTempPath(), "wildwood-fragments-" + Guid.NewGuid().ToString("N"));
        try
        {
            fragment.Name = "Leśny obóz";
            var loaded = MapFragment.Load(fragment.Save(directory));
            Check(loaded.Name == fragment.Name && loaded.Tiles.SequenceEqual(fragment.Tiles) && loaded.Heights.SequenceEqual(fragment.Heights) && loaded.Roads[1].Curve.SequenceEqual(crossing.Curve), "fragment library preserves content and clipped curves");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        var wide = MapData.CreateEmpty(2048, 4); wide.SetVertexHeight(2048, 4, 4);
        var edge = MapFragment.Capture(wide, new(2047, 3, 1, 1));
        Check(edge.VertexHeight(1, 1) == 4, "one-tile fragments include the far edge on a 2048-wide map");
        var reverse = MapRegion.FromDrag(map, new(-.1f, 0, -.1f), new(-7.9f, 0, -5.9f));
        Check(reverse == Source, "reverse dragging selects the same full tiles");
    }
    static void PasteAndLinks()
    {
        var map = Fixture(); var document = new MapDocument(map, true); var fragment = MapFragment.Capture(map, Source);
        string before = map.Serialize();
        var destination = new MapRegion(18, 14, 8, 6); var result = document.PasteFragment(fragment, destination, new());
        Check(result.Changed && result.Actors.Length == 5 && result.DisabledTriggers == 0 && document.Dirty, "paste adds the full fragment as one edit");
        for (int z = 0; z < fragment.Depth; z++)
        for (int x = 0; x < fragment.Width; x++)
            Check(map.Tiles[(14 + z) * 32 + 18 + x] == fragment.Tiles[z * 8 + x] && map.WaterMask((14 + z) * 32 + 18 + x) == fragment.WaterMask(z * 8 + x), "paste reproduces materials and water masks");
        Check(map.VertexHeight(26, 20) == 5 && map.Player.X == 13 && map.Player.Z == 9 && map.WaterLevel == 3 && map.WaveHeight == .1f, "paste preserves heights and global map settings/player start");
        var copiedDoor = result.Actors.Single(e => e.IsDoor); var copiedNpc = result.Actors.OfType<NpcData>().Single(); var copiedChest = result.Actors.OfType<ChestData>().Single();
        Check(copiedDoor.Id != "door" && copiedDoor.X == 4 && copiedDoor.Z == 4 && copiedDoor.Collider?.Rotation == 30, "actors get new IDs, translated positions and preserved colliders");
        Check(map.Triggers.Single(t => t.Id != "door-trigger" && t.Action == TriggerAction.ToggleDoor).TargetId == copiedDoor.Id &&
            map.Triggers.Single(t => t.Id != "npc-trigger" && t.Action == TriggerAction.Dialogue).TargetId == copiedNpc.Id, "pasted triggers refer to pasted doors and NPCs");
        Check(map.Triggers.Single(t => t.Id != "local-teleport" && t.Action == TriggerAction.Teleport && t.DestinationX != 13).DestinationX == 7 &&
            map.Triggers.Count(t => t.Action == TriggerAction.Teleport && t.DestinationX == 13 && t.DestinationZ == 9) == 2, "local destinations move and external destinations stay fixed");
        Check(map.Triggers.Where(t => t.Action == TriggerAction.ChangeMap).All(t => t.DestinationX == 999 && t.DestinationZ == -600 && t.MapFile == "second.json"), "cross-map entrances never become local destinations");
        Check(copiedChest.Contents.Items.All(item => fragment.Actors.Chests[0].Contents.Items.All(original => item.Id != original.Id)), "copied chest items have independent IDs");
        copiedNpc.Name = "Independent";
        Check(fragment.Actors.Npcs[0].Name == "Strażnik Żółw" && map.Npcs[0].Name == "Strażnik Żółw", "pasted actors do not alias clipboard or originals");
        string after = map.Serialize(); document.Undo(); Check(document.Map.Serialize() == before && !document.Dirty, "one undo restores all data before paste");
        document.Redo(); Check(document.Map.Serialize() == after, "redo exactly restores pasted IDs and contents");
        var second = document.PasteFragment(fragment, destination, new());
        Check(!second.Actors.Select(e => e.Id).Intersect(result.Actors.Select(e => e.Id)).Any() && document.Map.SceneEntities.Select(e => e.Id).Distinct().Count() == document.Map.SceneEntities.Count(), "repeated overlapping pastes create independent actors");
        document.Map.Validate();
    }
    static void Replacement()
    {
        var map = Fixture(); var fragment = MapFragment.Capture(map, Source);
        map.Objects.Add(new() { Id = "replace-me", Asset = "objects/prop.png", X = 3, Z = 3 });
        map.Roads.Add(new() { Id = "target-road", Name = "Target road", Width = 1, Softness = .25f, Points = [new(0, 4), new(14, 4)] });
        var document = new MapDocument(map, true); string before = map.Serialize();
        document.PasteFragment(fragment, new(18, 14, 8, 6), new() { ReplaceContents = true });
        Check(map.Objects.All(e => e.Id != "replace-me") && map.Objects.Any(e => e.Id == "outside"), "replacement removes selected destination contents and keeps outside actors");
        Check(map.Roads.Count(r => r.Name == "Target road") == 2 && map.Roads.Where(r => r.Name == "Target road").All(r => r.Curve.All(p => p.X <= 2.001f || p.X >= 9.999f)), "replacement preserves road portions outside the pasted rectangle");
        document.Undo(); Check(document.Map.Serialize() == before, "replacement is wholly undoable");
        var tiles = (string[])document.Map.Tiles.Clone(); var heights = (float[])document.Map.Heights.Clone();
        var options = new FragmentPasteOptions { Terrain = false, Roads = false, WorldFeatures = false };
        document.PasteFragment(fragment, new(18, 14, 8, 6), options);
        Check(document.Map.Tiles.SequenceEqual(tiles) && document.Map.Heights.SequenceEqual(heights) && document.Map.Roads.Count == 3 && document.Map.Triggers.Count == 5, "paste categories can copy actors only");
    }
    static void WaterAndNoop()
    {
        var fragment = MapFragment.Capture(Fixture(), Source);
        var map = MapData.CreateEmpty(16, 16); map.WaterLevel = 7; map.WaveHeight = 0;
        var document = new MapDocument(map, true); var options = new FragmentPasteOptions();
        var result = document.PasteFragment(fragment, new(2, 2, 8, 6), options);
        Check(options.ElevationShift(fragment, map) == 4 && map.VertexHeight(2, 2) == 4 && map.WaterLevel == 7 && map.WaveHeight == 0, "copied basins align to target water without changing global water settings");
        Check(result.DisabledTriggers == 1 && map.Triggers.Single(t => t.Action == TriggerAction.Teleport && !t.Enabled).DestinationX < 8, "external out-of-bounds destinations are disabled and kept editable");
        var missing = MapFragment.Capture(Fixture(), new(10, 6, 2, 2));
        var destination = new MapDocument(MapData.CreateEmpty(16, 16), true);
        var links = destination.PasteFragment(missing, new(1, 1, 2, 2), new() { Terrain = false, Actors = false, Roads = false });
        Check(links.DisabledTriggers >= 1 && destination.Map.Triggers.Any(t => t.Action == TriggerAction.Dialogue && !t.Enabled), "missing external actor links cannot operate on unrelated destination actors");
        var source = MapData.CreateEmpty(4, 4); source.Tiles[0] = "water"; source.WaterCells = new byte[16];
        var dry = MapFragment.Capture(source, new(0, 0, 1, 1));
        var target = new MapDocument(MapData.CreateEmpty(4, 4), true); target.PasteFragment(dry, new(1, 1, 1, 1), new());
        Check(!dry.HasWater && target.Map.WaterMask(5) == 0, "dry legacy-water materials remain dry when pasted");
        source.WaterCells = []; source.WaterLevel = 2;
        var wet = MapFragment.Capture(source, new(0, 0, 1, 1));
        Check(wet.HasWater && wet.WaterMask(0) == 3, "legacy implicit water is captured explicitly");
        var blank = MapFragment.Capture(MapData.CreateEmpty(4, 4), new(0, 0, 1, 1));
        var unchangedMap = MapData.CreateEmpty(4, 4); unchangedMap.WaterLevel = 2; unchangedMap.WaveHeight = 0;
        var unchanged = new MapDocument(unchangedMap, true);
        Check(!unchanged.PasteFragment(blank, new(0, 0, 1, 1), new()).Changed && !unchanged.Dirty && !unchanged.CanUndo, "identical pastes do not create history, including custom water settings");
        unchanged.Place("objects/tree.png", new(.5f, 0, .5f), 2, true); unchanged.Undo();
        unchanged.PasteFragment(blank, new(0, 0, 1, 1), new()); Check(unchanged.CanRedo, "no-op paste preserves redo history");
    }
    static void BoundsAndAtomicity()
    {
        var map = Fixture(); var document = new MapDocument(map, true); var fragment = MapFragment.Capture(map, Source); string before = map.Serialize();
        foreach (var region in new[] { new MapRegion(-1, 0, 8, 6), new MapRegion(30, 20, 8, 6) })
        {
            try { document.PasteFragment(fragment, region, new()); throw new Exception("Out-of-bounds paste accepted."); } catch (InvalidDataException) { }
            Check(map.Serialize() == before && !document.CanUndo, "invalid destination preserves data and history");
        }
        try { document.PasteFragment(fragment, new(18, 14, 8, 6), new() { HeightOffset = 96 }); throw new Exception("Invalid height accepted."); } catch (InvalidDataException) { }
        Check(map.Serialize() == before && !document.CanUndo, "height limits are validated before editing");
        var crowded = MapData.CreateEmpty(32, 32);
        for (int i = 0; i < 501; i++) crowded.Objects.Add(new() { Asset = "objects/tree.png", X = i % 10 - 5, Z = i / 10 % 10 - 5 });
        var large = MapFragment.Capture(crowded, new(0, 0, 32, 32));
        var target = new MapDocument(MapData.CreateEmpty(64, 64), true); target.PasteFragment(large, new(0, 0, 32, 32), new());
        Check(target.Map.Objects.Count == 501, "map fragments are not restricted to the 500-actor prefab limit");
        var limited = MapData.CreateEmpty(16, 16);
        for (int i = 0; i < 1000; i++) limited.Npcs.Add(new() { X = 0, Z = 0 });
        var limitDocument = new MapDocument(limited, true); string initial = limited.Serialize();
        try { limitDocument.PasteFragment(fragment, new(0, 0, 8, 6), new() { Terrain = false, Roads = false, WorldFeatures = false }); throw new Exception("Actor limit accepted."); } catch (InvalidDataException) { }
        Check(limited.Serialize() == initial && !limitDocument.CanUndo, "actor limits fail atomically");
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Map fragment check failed: " + message); }
}
