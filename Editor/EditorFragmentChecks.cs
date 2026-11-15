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
        map.WaterFlow[7 * 32 + 9] = new(1, 0); map.WaterFlow[8 * 32 + 10] = new(0, 1.5f);
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
        CaptureAndLibrary(); PasteAndLinks(); Replacement(); WaterAndNoop(); WaterFlow(); BoundsAndAtomicity();
        Console.WriteLine("Map fragment checks passed: bounded capture, frozen clipboard/library, terrain/water/current, clipped roads, actors/inventory/dialogues, link remapping, independent copies, replacement, limits, atomic failures and undo/redo.");
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
            Check(loaded.Name == fragment.Name && loaded.Tiles.SequenceEqual(fragment.Tiles) && loaded.Heights.SequenceEqual(fragment.Heights) &&
                loaded.WaterFlow.OrderBy(pair => pair.Key).SequenceEqual(fragment.WaterFlow.OrderBy(pair => pair.Key)) &&
                loaded.Roads[1].Curve.SequenceEqual(crossing.Curve), "fragment library preserves content, current vectors and clipped curves");
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
    static void WaterFlow()
    {
        var source = MapData.CreateEmpty(8, 8); source.WaterLevel = 0;
        for (int z = 0; z <= source.Depth; z++)
        for (int x = 0; x <= source.Width; x++) source.SetVertexHeight(x, z, -2);
        source.WaterCells = Enumerable.Repeat((byte)3, source.Tiles.Length).ToArray();
        var first = new WaterFlowVector(1, .5f); var last = new WaterFlowVector(-.25f, 2);
        source.WaterFlow[1 * 8 + 2] = first; source.WaterFlow[2 * 8 + 4] = last;
        source.WaterFlow[1 * 8 + 1] = new(0, 1);
        source.WaterCells[1 * 8 + 3] = 0; source.WaterFlow[1 * 8 + 3] = new(2, 0);
        for (int z = 1; z <= 2; z++)
        for (int x = 4; x <= 5; x++) source.SetVertexHeight(x, z, 1);
        source.WaterFlow[1 * 8 + 4] = new(2, 0);
        string original = source.Serialize(); var fragment = MapFragment.Capture(source, new(2, 1, 3, 2));
        Check(source.Serialize() == original && fragment.WaterFlow.Count == 2 && fragment.WaterFlow[0] == first && fragment.WaterFlow[5] == last,
            "capture reindexes sparse currents and omits outside, drained and elevated dry cells");
        source.WaterFlow[1 * 8 + 2] = new(3, 0);
        Check(fragment.WaterFlow[0] == first && !ReferenceEquals(fragment.WaterFlow, source.WaterFlow), "captured currents do not alias the source dictionary");

        string json = System.Text.Json.JsonSerializer.Serialize(fragment);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<MapFragment>(json)!; roundTrip.Validate();
        Check(roundTrip.WaterFlow.OrderBy(pair => pair.Key).SequenceEqual(fragment.WaterFlow.OrderBy(pair => pair.Key)) &&
            !ReferenceEquals(roundTrip.WaterFlow, fragment.WaterFlow), "fragment JSON preserves independent local current vectors");
        var legacyJson = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject(); legacyJson.Remove(nameof(MapFragment.WaterFlow));
        var legacy = System.Text.Json.JsonSerializer.Deserialize<MapFragment>(legacyJson.ToJsonString())!; legacy.Validate();
        Check(legacy.WaterFlow.Count == 0, "fragments saved before local currents load as still water");
        Dictionary<int, WaterFlowVector>[] invalid = [null!, new() { [-1] = first }, new() { [6] = first },
            new() { [0] = new(float.NaN, 0) }, new() { [0] = new(0, float.PositiveInfinity) }, new() { [0] = new(4, .1f) }];
        var valid = roundTrip.WaterFlow;
        foreach (var currents in invalid)
        {
            roundTrip.WaterFlow = currents; bool rejected = false;
            try { roundTrip.Validate(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "fragment validation rejects null, out-of-bounds, non-finite and excessive current vectors");
        }
        roundTrip.WaterFlow = valid;

        var target = MapData.CreateEmpty(12, 8); target.WaterLevel = 0;
        target.WaterAppearance = target.WaterAppearance with { R = 83, ReflectionStrength = .34f };
        for (int z = 0; z <= target.Depth; z++)
        for (int x = 0; x <= target.Width; x++) target.SetVertexHeight(x, z, -2);
        target.WaterCells = Enumerable.Repeat((byte)3, target.Tiles.Length).ToArray();
        var destination = new MapRegion(5, 3, 3, 2); int firstCell = 3 * 12 + 5, lastCell = 4 * 12 + 7;
        // Interleaved keys expose accidental remove/reinsert changes in otherwise identical pastes.
        target.WaterFlow[0] = new(0, -.5f); target.WaterFlow[firstCell] = new(-1, 0);
        target.WaterFlow[95] = new(.5f, 0); target.WaterFlow[lastCell] = new(0, -1);
        target.WaterFlow[firstCell + 1] = new(1, 0); target.WaterFlow[firstCell + 2] = new(1, 0);
        target.WaterFlow[lastCell - 1] = new(1, 0);
        // Stale vectors are permitted in stored fragments but must not create currents on dry terrain.
        fragment.WaterFlow[1] = new(2, 0); fragment.WaterFlow[2] = new(2, 0);
        var options = new FragmentPasteOptions { Actors = false, Roads = false, Walls = false, WorldFeatures = false };
        var document = new MapDocument(target, true); string before = target.Serialize();
        var result = document.PasteFragment(fragment, destination, options);
        Check(result.Changed && document.Dirty && target.WaterFlow.Count == 4 && target.WaterFlow[firstCell] == first && target.WaterFlow[lastCell] == last &&
            target.WaterFlow[0] == new WaterFlowVector(0, -.5f) && target.WaterFlow[95] == new WaterFlowVector(.5f, 0),
            "terrain paste reindexes currents, replaces the entire footprint and preserves currents outside it");
        fragment.WaterFlow[0] = new(3, 0);
        Check(target.WaterFlow[firstCell] == first && !ReferenceEquals(target.WaterFlow, fragment.WaterFlow), "pasted currents do not alias the clipboard");
        fragment.WaterFlow[0] = first;
        string after = target.Serialize();
        Check(MapData.Parse(after).Serialize() == after, "pasted sparse currents round trip through map JSON");
        document.Undo(); Check(document.Map.Serialize() == before && !document.Dirty, "one undo restores terrain and every replaced or cleared current");
        document.Redo(); Check(document.Map.Serialize() == after, "redo restores the exact pasted current JSON");

        var unchanged = new MapDocument(MapData.Parse(after), true); var unchangedFlow = unchanged.Map.WaterFlow;
        Check(!unchanged.PasteFragment(fragment, destination, options).Changed && !unchanged.Dirty && !unchanged.CanUndo &&
            ReferenceEquals(unchanged.Map.WaterFlow, unchangedFlow) && unchanged.Map.Serialize() == after,
            "matching currents and custom water appearance make terrain paste a true no-op without reordering dictionary keys");
        unchanged.Place("objects/tree.png", new(.5f, 0, .5f), 2, true); unchanged.Undo();
        Check(!unchanged.PasteFragment(fragment, destination, options).Changed && unchanged.CanRedo, "a no-op current paste preserves redo history");

        var currentOnlyMap = MapData.Parse(after); currentOnlyMap.WaterFlow[firstCell] = new(-1, 0);
        var currentOnly = new MapDocument(currentOnlyMap, true); string currentBefore = currentOnlyMap.Serialize();
        Check(currentOnly.PasteFragment(fragment, destination, options).Changed && currentOnly.Dirty && currentOnly.Map.WaterFlow[firstCell] == first,
            "a current-only difference is detected even when terrain and water masks already match");
        currentOnly.Undo(); Check(currentOnly.Map.Serialize() == currentBefore && !currentOnly.Dirty, "current-only paste creates a complete undo checkpoint");
        currentOnly.Redo(); Check(currentOnly.Map.Serialize() == after, "current-only redo restores the saved current values and ordering");

        var cleared = new MapDocument(MapData.Parse(after), true);
        Check(cleared.PasteFragment(legacy, destination, options).Changed && cleared.Map.WaterFlow.Count == 2 &&
            !cleared.Map.WaterFlow.ContainsKey(firstCell) && !cleared.Map.WaterFlow.ContainsKey(lastCell) &&
            cleared.Map.WaterCells.SequenceEqual(target.WaterCells) && cleared.Map.Heights.SequenceEqual(target.Heights),
            "pasting a wet fragment without currents clears stale destination currents without changing matching geometry");
        cleared.Undo(); Check(cleared.Map.Serialize() == after, "clearing currents through an older fragment is undoable");

        var dry = MapFragment.Capture(MapData.CreateEmpty(4, 4), new(0, 0, 3, 2)); dry.WaterFlow[0] = new(2, 0);
        var dryTarget = new MapDocument(MapData.Parse(after), true);
        Check(dryTarget.PasteFragment(dry, destination, options).Changed && dryTarget.Map.WaterFlow.Count == 2 &&
            Enumerable.Range(0, 6).All(cell => dryTarget.Map.WaterMask((destination.Z + cell / 3) * 12 + destination.X + cell % 3) == 0),
            "dry terrain paste clears the whole destination current footprint even if source data contains stale vectors");
        dryTarget.Undo(); Check(dryTarget.Map.Serialize() == after, "dry replacement restores water and currents together on undo");
        var raised = new MapDocument(MapData.Parse(after), true);
        Check(raised.PasteFragment(fragment, destination, options with { AlignWater = false, HeightOffset = 8 }).Changed &&
            raised.Map.WaterFlow.Count == 2 && !raised.Map.HasWaterSurface(firstCell), "height offsets that leave the pasted bed dry do not retain currents");

        dry.Actors.Objects.Add(new() { Asset = "objects/tree.png", X = .5f, Z = .5f });
        var actorsOnly = new MapDocument(MapData.Parse(after), true); var actorFlow = actorsOnly.Map.WaterFlow;
        var actorWater = actorsOnly.Map.WaterCells.ToArray(); var actorHeights = actorsOnly.Map.Heights.ToArray();
        Check(actorsOnly.PasteFragment(dry, destination, options with { Terrain = false, Actors = true }).Changed &&
            ReferenceEquals(actorFlow, actorsOnly.Map.WaterFlow) && actorsOnly.Map.WaterFlow.OrderBy(pair => pair.Key).SequenceEqual(target.WaterFlow.OrderBy(pair => pair.Key)) &&
            actorsOnly.Map.WaterCells.SequenceEqual(actorWater) && actorsOnly.Map.Heights.SequenceEqual(actorHeights),
            "actor-only paste preserves destination current values, dictionary, water masks and terrain");
        actorsOnly.Undo(); Check(actorsOnly.Map.Serialize() == after, "actor-only undo preserves the authored current JSON");
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
