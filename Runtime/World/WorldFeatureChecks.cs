using System.Numerics;
using System.Text.Json.Nodes;

static class WorldFeatureChecks
{
    public static void Run()
    {
        MapTypeChecks.Run();
        const string tree = "objects/vegetation/trees/tree1.png";
        var map = MapData.CreateEmpty(16, 16);
        var door = new SceneObject { Asset = tree, X = 4, IsDoor = true, Collider = new() { Kind = ColliderKind.Box, Width = 3, Depth = .2f, Rotation = 30 } };
        map.Objects.Add(door); map.Lights.Add(new()); map.Sounds.Add(new()); map.Triggers.Add(new()); map.Validate();
        Check(MapData.Parse(map.Serialize()).Serialize() == map.Serialize() && MapSnapshot.Capture(map).Restore().Serialize() == map.Serialize(), "world features survive JSON and snapshots");
        var old = JsonNode.Parse(map.Serialize())!.AsObject();
        foreach (string property in new[] { "Environment", "Lights", "Sounds", "Triggers" }) old.Remove(property);
        Check(MapData.Parse(old.ToJsonString()).Environment.TimeOfDay == 12, "older maps get a daytime environment");
        Check(CollisionGeometry.Blocks(door, new(2, 0), new(6, 0), .25f), "swept collision catches thin rotated walls");
        Check(!CollisionGeometry.Blocks(door, new(4, 0), new(4, -.2f), .25f), "an actor can escape an overlapping closed door");
        door.DoorOpen = true;
        Check(!CollisionGeometry.Blocks(door, new(2, 0), new(6, 0), .25f), "open doors stop blocking movement"); door.DoorOpen = false;
        var polygon = new ColliderData { Kind = ColliderKind.Polygon, Points = [new(-2, -2), new(2, -2), new(2, -1), new(-1, -1), new(-1, 2), new(-2, 2)] };
        polygon.Validate(); var obstacle = new SceneObject { Asset = tree, Collider = polygon };
        Check(CollisionGeometry.Blocks(obstacle, new(-1.5f, .5f), new(-1.5f, .5f), .1f) &&
            !CollisionGeometry.Blocks(obstacle, new(.5f, .5f), new(.5f, .5f), .1f), "concave polygons preserve walkable cutouts");
        ExpectInvalid(() => new ColliderData { Kind = ColliderKind.Polygon, Points = [new(-1, -1), new(1, 1), new(-1, 1), new(1, -1)] }.Validate(), "self-intersecting polygon");
        var chest = new ChestData { X = -2, Z = 1 }; chest.Contents.Add("sword"); map.Chests.Add(chest);
        map.Npcs.Add(new() { X = -1, Z = 2 });
        var prefab = ScenePrefab.Capture("Camp", map.SceneEntities);
        var document = new MapDocument(map, true);
        var instances = document.PlacePrefab(prefab, new(-2, 0, -2));
        Check(instances.Count == 3 && map.SceneEntities.Select(e => e.Id).Distinct().Count() == 6 &&
            map.Chests.SelectMany(c => c.Contents.Items).Select(i => i.Id).Distinct().Count() == 2, "prefabs preserve types and regenerate actor and item IDs");
        Check(instances.OfType<SceneObject>().First().Collider?.Kind == ColliderKind.Box, "prefab includes collider data");
        document.Undo(); Check(document.Map.SceneEntities.Count() == 3, "prefab placement is one undo step");
        document.Redo(); Check(document.Map.SceneEntities.Count() == 6, "prefab redo restores the entire group");
        string unchanged = document.Map.Serialize();
        ExpectInvalid(() => document.PlacePrefab(prefab, new(-7, 0, 7)), "prefab outside map");
        Check(document.Map.Serialize() == unchanged, "failed placement preserves the document");

        var triggers = MapData.CreateEmpty(16, 16);
        var gate = new SceneObject { Asset = tree, X = 5, IsDoor = true }; triggers.Objects.Add(gate);
        triggers.Triggers.Add(new() { Action = TriggerAction.ToggleDoor, TargetId = gate.Id, Once = false });
        var play = new PlaySession(triggers); play.AdvanceWorld(.1f);
        Check(play.Map.Objects[0].DoorOpen, "entering a trigger opens its door"); play.AdvanceWorld(.1f);
        Check(play.Map.Objects[0].DoorOpen, "repeatable triggers fire on entry rather than every frame");
        play.TryMovePlayer(new(0, 0, 4)); play.AdvanceWorld(.1f); play.TryMovePlayer(Vector3.Zero); play.AdvanceWorld(.1f);
        Check(!play.Map.Objects[0].DoorOpen, "re-entering a repeatable area fires again");
        triggers = MapData.CreateEmpty(16, 16); triggers.Triggers.Add(new() { Action = TriggerAction.SpawnMonster, DestinationX = 4, DestinationZ = 4 });
        play = new(triggers); play.AdvanceWorld(.1f); play.AdvanceWorld(.1f);
        Check(play.Monsters.Count() == 1 && play.FiredTriggers.Count == 1, "once-only spawn is registered with combat and fires once");
        triggers = MapData.CreateEmpty(16, 16); triggers.Triggers.Add(new() { Action = TriggerAction.Teleport, DestinationX = -3, DestinationZ = -3 });
        play = new(triggers); play.AdvanceWorld(.1f);
        Check(play.Player == new Vector3(-3, 0, -3) && Vector3.Distance(play.Camera.View.Target, play.Player + Vector3.UnitY * 1.5f) < .001f, "teleport moves player and camera together");
        triggers = MapData.CreateEmpty(16, 16); var npc = new NpcData { X = 3, Z = 3 }; triggers.Npcs.Add(npc);
        triggers.Triggers.Add(new() { Action = TriggerAction.Dialogue, TargetId = npc.Id });
        play = new(triggers); play.AdvanceWorld(.1f); Check(play.Dialogue.Active && play.Dialogue.Speaker?.Id == npc.Id, "trigger starts the configured conversation");
        triggers.Environment.DayNightCycle = true; triggers.Environment.TimeOfDay = 6; triggers.Environment.DayLengthSeconds = 24;
        triggers.Triggers.Clear(); play = new(triggers); play.AdvanceWorld(1);
        Check(Math.Abs(play.TimeOfDay - 7) < .001f && WorldLighting.Ambient(12) == Vector3.One && WorldLighting.Ambient(0).X < .2f, "day/night advances deterministically with daytime and nighttime lighting");

        string directory = Path.Combine(ProjectPaths.Root, "Editor", "obj", "world-feature-checks"); Directory.CreateDirectory(directory);
        string progressPath = Path.Combine(directory, "progress.json");
        string secondName = "world-check-" + Guid.NewGuid().ToString("N") + ".json";
        string secondPath = Path.Combine(ProjectPaths.Root, "Runtime", "Maps", secondName);
        try
        {
            string prefabPath = prefab.Save(directory); Check(ScenePrefab.Load(prefabPath).Entities.Count() == 3, "prefab library round trip"); File.Delete(prefabPath);
            var source = MapData.CreateEmpty(16, 16);
            var container = new ChestData { Z = 1 }; container.Contents.Add("sword"); source.Chests.Add(container);
            source.Objects.Add(new() { Asset = tree, X = 5, IsDoor = true });
            source.Monsters.Add(new() { X = .8f }); source.Monsters.Add(new() { X = 1.4f });
            source.Triggers.Add(new() { Text = "Welcome" });
            string editorState = source.Serialize(); play = new(source);
            Check(play.OpenChest(play.Map.Chests[0]), "open chest before save");
            Check(InventoryGrid.Transfer(play.OpenedChest!.Contents, play.Inventory, play.OpenedChest.Contents.Items[0].Id, 0, 0), "loot before save");
            play.CloseInventory(); play.Map.Objects[0].DoorOpen = true;
            play.Monsters.First().Health = 7; play.UpdateCombat(.1f, true); play.Monsters.Single().Health = 13; play.AdvanceWorld(.1f);
            play.Monsters.Single().Actor.X = 3;
            int health = play.PlayerHealth; string state = play.Map.Serialize();
            play.SaveProgress(progressPath); play.Inventory.Items.Clear(); play.Map.Objects[0].DoorOpen = false;
            play.LoadProgress(progressPath);
            Check(play.Map.Serialize() == state && play.PlayerHealth == health && play.Monsters.Single().Health == 13 &&
                play.FiredTriggers.Count == 1 && play.Map.Chests[0].Opened, "save restores loot, opened chests/doors, killed monsters, remaining HP and triggers");
            Check(Math.Abs(play.Monsters.Single().Spawn.X - 1.4f) < .001f && play.Monsters.Single().Actor.X == 3,
                "load preserves a chasing monster's original home as well as its current position");
            Check(play.ChangeMap(Path.GetFileName(ProjectPaths.MapFile), 0, 0) && play.Monsters.Single().Health == 13 && play.Map.Objects[0].DoorOpen,
                "same-map transitions preserve live combat and door state");
            Check(source.Serialize() == editorState, "saving/loading progress preserves the editor map");
            var corrupt = JsonNode.Parse(File.ReadAllText(progressPath))!;
            corrupt["Worlds"]![play.CurrentMapPath]!["Health"] = -1; File.WriteAllText(progressPath, corrupt.ToJsonString());
            ExpectInvalid(() => play.LoadProgress(progressPath), "invalid progress health");
            Check(play.Map.Serialize() == state && play.PlayerHealth == health, "failed progress load is transactional");
            play.Restart(); Check(play.Map.Monsters.Count == 2 && play.Inventory.Items.Count == 0 && !play.Map.Chests[0].Opened, "restart restores the original map after loading progress");
            var destination = MapData.CreateEmpty(8, 8); destination.Objects.Add(new() { Asset = tree, X = -3, IsDoor = true }); destination.Save(secondPath);
            play.Inventory.Add("dagger");
            Check(play.ChangeMap(secondName, 1, 2) && play.Player == new Vector3(1, 0, 2) && play.Inventory.Items.Count == 1, "map transition carries inventory and uses the entrance");
            play.Map.Objects[0].DoorOpen = true;
            Check(play.ChangeMap(Path.GetFileName(ProjectPaths.MapFile), 0, 0), "return to previously visited map");
            Check(play.ChangeMap(secondName, 1, 2) && play.Map.Objects[0].DoorOpen, "visited maps retain world state");
            play.SaveProgress(progressPath); play.LoadProgress(progressPath);
            Check(play.Map.Objects[0].DoorOpen && play.Inventory.Items.Count == 1 && !play.ChangeMap("../outside.json", 0, 0), "multi-map progress and relative path validation");
        }
        finally { if (File.Exists(secondPath)) File.Delete(secondPath); if (File.Exists(progressPath)) File.Delete(progressPath); }
        var revisions = MapData.CreateEmpty(64, 64); revisions.SetVertexHeight(16, 16, 1);
        Check(revisions.TerrainChunkVersion(0, 0) > 0 && revisions.TerrainChunkVersion(1, 1) > 0 && revisions.TerrainChunkVersion(3, 3) == 0,
            "sculpting invalidates neighbouring chunks at shared boundaries");
        long revision = revisions.TerrainChunkVersion(0, 0); new MapDocument(revisions, true).Paint(Vector3.Zero, 0, "sand");
        Check(revisions.TerrainChunkVersion(0, 0) == revision, "painting does not invalidate terrain geometry");
        var left = SpatialSound.Calculate(Vector3.Zero, -Vector3.UnitX, Vector3.UnitX, 10, 1);
        var right = SpatialSound.Calculate(Vector3.Zero, Vector3.UnitX, Vector3.UnitX, 10, 1);
        Check(left.Pan < 0 && right.Pan > 0 && left.Volume == right.Volume &&
            SpatialSound.Calculate(Vector3.Zero, Vector3.UnitX * 20, Vector3.UnitX, 10, 1).Volume == 0, "spatial attenuation and Raylib 6 stereo directions");
        foreach (string id in new[] { "fire", "water", "step/grass", "step/dirt", "step/sand", "step/stone" })
        {
            var wav = WorldAudio.GenerateClip(id);
            Check(System.Text.Encoding.ASCII.GetString(wav, 0, 4) == "RIFF" && BitConverter.ToInt32(wav, 24) == 22050 && wav.Length > 1000, "generated audio is valid PCM WAV");
        }
        Console.WriteLine("World feature checks passed: environment, shapes/sweeps, triggers, multi-map progress, prefab IDs/undo, chunk invalidation and spatial audio.");
    }
    static void ExpectInvalid(Action action, string message)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid data accepted: " + message);
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("World feature check failed: " + message); }
}
