using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunFragmentInteractionChecks()
    {
        string originalDirectory = fragmentDirectory;
        fragmentDirectory = Path.Combine(ProjectPaths.AssetRoot, "obj", "fragment-library-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            SetWindowSize(1440, 900); EndEditorGesture(); StopFragmentTool(true); fragmentClipboard = null;
            tool = EditTool.Select; selectedId = null; selectedIds.Clear(); activePrefab = null;
            groundMode = GroundMode.Paint; terrainErosion = terrainStamp = objectBrush = false; ResetRoadDraft();
            preferences.ShowMinimap = false; preferences.AutosaveSeconds = 0;
            assetSearch.Value = sceneSearch.Value = ""; assetSearch.Focused = sceneSearch.Focused = false; sceneFilter = "All";
            fragmentOptions.Terrain = fragmentOptions.Actors = fragmentOptions.Roads = fragmentOptions.WorldFeatures = fragmentOptions.AlignWater = true;
            fragmentOptions.ReplaceContents = false; fragmentOptions.HeightOffset = 0;
            var map = MapData.CreateEmpty(32, 32); map.Player.X = map.Player.Z = 12; map.WaterLevel = 2;
            map.Environment.Audio = false; map.Environment.Shadows = false;
            for (int z = 10; z <= 16; z++)
            for (int x = 10; x <= 16; x++) map.SetVertexHeight(x, z, (x - 10 + z - 10) * .25f);
            for (int z = 10; z < 16; z++)
            for (int x = 10; x < 16; x++) map.Tiles[z * 32 + x] = (x + z) % 3 == 0 ? "stone" : "dirt";
            map.WaterCells = new byte[1024]; map.WaterCells[11 * 32 + 11] = 3; map.WaterCells[11 * 32 + 12] = 1;
            var vegetation = assets.Sprites.Values.First(s => s.Id.StartsWith("objects/vegetation/"));
            var rock = assets.Sprites.Values.First(s => s.Id.Contains("/rocks/"));
            map.Objects.Add(new() { Id = "source-tree", Asset = vegetation.Id, X = -5, Z = -5, Height = 2 });
            map.Objects.Add(new() { Id = "source-rock", Asset = rock.Id, X = -1, Z = -1, Height = 1 });
            map.Objects.Add(new() { Id = "source-door", Asset = rock.Id, X = -3, Z = -2, Height = 1, IsDoor = true });
            map.Objects.Add(new() { Id = "destination-prop", Asset = vegetation.Id, X = 4, Z = 4, Height = 2 });
            map.Npcs.Add(new() { Id = "source-npc", Name = "Obozowy strażnik", X = -2, Z = -4 });
            var chest = new ChestData { Id = "source-chest", X = -4, Z = -3 }; chest.Contents.Add("sword"); map.Chests.Add(chest);
            map.Monsters.Add(new() { Id = "source-monster", X = -1, Z = -4 });
            map.Lights.Add(new() { Id = "source-light", X = -5, Z = -3 });
            map.Sounds.Add(new() { Id = "source-sound", X = -3, Z = -5 });
            map.Triggers.Add(new() { Id = "source-trigger", X = -5, Z = -2, Action = TriggerAction.ToggleDoor, TargetId = "source-door" });
            map.Roads.Add(new() { Id = "source-road", Name = "Camp trail", Width = 1, Softness = .25f, Points = [new(-8, -1), new(3, -1)] });
            map.Save(mapPath); document.Load(mapPath); focus = document.Map.GroundPosition(0, 0); ApplyRuntimeSettings(); TestFrame();
            void Ctrl(KeyboardKey key, bool shift = false)
            {
                Input(2, (int)KeyboardKey.LeftControl); if (shift) Input(2, (int)KeyboardKey.LeftShift);
                Press(key); if (shift) Input(1, (int)KeyboardKey.LeftShift); Input(1, (int)KeyboardKey.LeftControl); TestFrame();
            }
            Vector2 Screen(float x, float z) => GetWorldToScreenEx(document.Map.GroundPosition(x, z), camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
            void GroundClick(float x, float z) { var p = Screen(x, z); Click((int)p.X, (int)p.Y); }
            Ctrl(KeyboardKey.V);
            Verify(fragmentMode == FragmentToolMode.Off && camera.Projection == ViewProjection.Perspective, "Ctrl+V without a clipboard does not change the camera");
            string original = document.Map.Serialize(); Ctrl(KeyboardKey.C, true);
            Verify(fragmentMode == FragmentToolMode.Selecting && tool == EditTool.Select, "Ctrl+Shift+C enters area selection");
            var start = Screen(-5.9f, -5.9f); var end = Screen(-.1f, -.1f);
            Input(7, (int)start.X, (int)start.Y); Input(6, 0); TestFrame();
            Input(7, (int)end.X, (int)end.Y); TestFrame(); Input(5, 0); TestFrame();
            Verify(fragmentMode == FragmentToolMode.Ready && fragmentSource == new MapRegion(10, 10, 6, 6) && fragmentClipboard is { Width: 6, Depth: 6 }, $"drag captures the selected full tiles (mode {fragmentMode}, area {fragmentSource})");
            Verify(document.Map.Serialize() == original && !document.Dirty && !document.CanUndo, "copying is read-only");
            Verify(fragmentClipboard!.Actors.Entities.Count() == 6 && fragmentClipboard.Roads.Count == 1 && fragmentClipboard.HasWater, "area includes actors, roads and water");
            TestFrame(); WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "map-fragment-selection.png"));
            Ctrl(KeyboardKey.C); Ctrl(KeyboardKey.V);
            Verify(fragmentMode == FragmentToolMode.Pasting, "Ctrl+V starts the paste preview");
            var point = Screen(5.1f, 5.1f); Input(7, (int)point.X, (int)point.Y); TestFrame();
            Verify(fragmentDestination == new MapRegion(18, 18, 6, 6) && document.Map.Serialize() == original, "moving the preview snaps to tiles without editing");
            WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "map-fragment-preview.png"));
            Click((int)point.X, (int)point.Y); string firstPaste = document.Map.Serialize();
            Verify(document.Map.SceneEntities.Count() == 13 && fragmentMode == FragmentToolMode.Pasting && document.Dirty, "click pastes a complete independent area");
            var pastedDoor = SelectionGroup.Single(e => e.IsDoor);
            Verify(document.Map.Triggers.Single(t => t.Id != "source-trigger").TargetId == pastedDoor.Id, "pasted trigger controls the pasted door");
            Input(7, (int)point.X, (int)point.Y); Input(6, 0); TestFrame();
            string secondPaste = document.Map.Serialize();
            for (int i = 0; i < 8; i++) { Input(7, (int)point.X, (int)point.Y); TestFrame(); }
            Verify(document.Map.Serialize() == secondPaste && document.Map.SceneEntities.Count() == 19, "held mouse does not repeat paste each frame");
            Input(5, 0); TestFrame();
            Ctrl(KeyboardKey.Z); Verify(document.Map.Serialize() == firstPaste, "one undo removes one full paste");
            Ctrl(KeyboardKey.Z); Verify(document.Map.Serialize() == original && !document.Dirty, "undo restores materials, heights, water and all content");
            Ctrl(KeyboardKey.Y); Ctrl(KeyboardKey.Y); Verify(document.Map.Serialize() == secondPaste, "redo restores the same IDs and links");
            Click(100, 371); Verify(fragmentOptionsOpen, "paste options panel");
            var options = FragmentOptionsBounds;
            Press(KeyboardKey.Nine); Press(KeyboardKey.F5);
            Verify(play == null && tool == EditTool.Select && fragmentOptionsOpen, "paste options block editor shortcuts");
            Click((int)options.X + 200, (int)options.Y + 119); // Terrain off.
            Click((int)options.X + 200, (int)options.Y + 203); // Roads off.
            Click((int)options.X + 200, (int)options.Y + 245); // World features off.
            Press(KeyboardKey.Escape);
            Verify(!fragmentOptions.Terrain && fragmentOptions.Actors && !fragmentOptions.Roads && !fragmentOptions.WorldFeatures, "category controls");
            var terrain = (string[])document.Map.Tiles.Clone(); var heights = (float[])document.Map.Heights.Clone(); var water = document.Map.CopyWaterCells();
            GroundClick(5.1f, -10.1f);
            Verify(document.Map.Tiles.SequenceEqual(terrain) && document.Map.Heights.SequenceEqual(heights) && document.Map.CopyWaterCells().SequenceEqual(water), "actors-only paste leaves terrain and water intact");
            Click(100, 371); options = FragmentOptionsBounds;
            Click((int)options.X + 100, (int)options.Y + 505); // Defaults.
            Click((int)options.X + 200, (int)options.Y + 301); // Replace.
            Press(KeyboardKey.Escape);
            string beforeReplace = document.Map.Serialize(); GroundClick(5.1f, 5.1f);
            var destination = new FragmentBounds(2, 2, 8, 8);
            Verify(document.Map.SceneEntities.Count(e => destination.Contains(e.X, e.Z)) == 6 && document.Map.Objects.All(e => e.Id != "destination-prop"), "replacement replaces destination actors");
            Ctrl(KeyboardKey.Z); Verify(document.Map.Serialize() == beforeReplace, "replacement is one undoable action");
            fragmentOptions.ReplaceContents = false;
            focus = document.Map.GroundPosition(13.5f, 0); camera.Snap(focus); TestFrame();
            point = Screen(15.5f, 0); Input(7, (int)point.X, (int)point.Y); TestFrame();
            string invalid = document.Map.Serialize(); Click((int)point.X, (int)point.Y);
            Verify(document.Map.Serialize() == invalid && !fragmentDestination.Fits(document.Map), "invalid edge preview cannot paste a partial fragment");
            string beforeLibrary = document.Map.Serialize(); Click(100, 287);
            Verify(textEdit != null, "fragment name dialog"); AppendEdit("Obóz testowy"); Press(KeyboardKey.Enter);
            Verify(Directory.EnumerateFiles(fragmentDirectory, "*.json").Count() == 1 && document.Map.Serialize() == beforeLibrary, "saving a fragment does not save or change the map");
            File.WriteAllText(Path.Combine(fragmentDirectory, "broken.json"), "{broken");
            Click(100, 329); Verify(fragmentLibraryOpen && fragmentLibrary.Count == 1, "library skips a corrupt fragment while retaining valid files");
            TestFrame(); WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "map-fragment-library.png"));
            Press(KeyboardKey.Escape); Save();
            newWidth = newDepth = 24; newMapType = MapType.OpenWorld; Complete("new");
            Verify(fragmentClipboard != null && fragmentSource == null && fragmentMode == FragmentToolMode.Off, "new maps retain the copied clipboard");
            OpenFragmentLibrary(); TestFrame(); var library = FragmentLibraryBounds;
            Click((int)library.X + 650, (int)library.Y + 133);
            Verify(fragmentMode == FragmentToolMode.Pasting && !fragmentLibraryOpen && fragmentClipboard!.Name == "Obóz testowy", "load a reusable fragment for a different map");
            Click(100, 371); options = FragmentOptionsBounds; Click((int)options.X + 100, (int)options.Y + 505); Press(KeyboardKey.Escape);
            Verify(fragmentOptions.ElevationShift(fragmentClipboard!, document.Map) == -2, "wet fragments align to the destination map's water level");
            Press(KeyboardKey.M); var minimap = MinimapMapBounds;
            string beforeMinimap = document.Map.Serialize(); Click((int)(minimap.X + minimap.Width * .6f), (int)(minimap.Y + minimap.Height * .6f));
            Verify(document.Map.Serialize() == beforeMinimap && fragmentMode == FragmentToolMode.Pasting, "minimap navigation cannot paste a fragment");
            focus = Vector3.Zero; camera.Snap(focus); TestFrame(); GroundClick(.1f, .1f);
            Verify(document.Map.SceneEntities.Count() == 6 && document.Map.VertexHeight(15, 15) == 1 && document.Map.WaterLevel == 0, "library paste restores terrain, water and actors on another map");
            string complete = document.Map.Serialize(); Save(); Verify(MapData.Load(mapPath).Serialize() == complete, "pasted content saves as an ordinary map");
            Press(KeyboardKey.F5); Verify(play != null && play.Map.Roads.Count == 1 && play.Map.Chests.Count == 1 && fragmentMode == FragmentToolMode.Off, "Play loads pasted roads, chests and linked actors");
            Press(KeyboardKey.F5); Verify(document.Map.Serialize() == complete, "Play leaves pasted editor content unchanged");
            BeginFragmentPaste(); SetWindowSize(1120, 720); TestFrame(); TestFrame();
            WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "map-fragment-small.png"));
            Click(100, 371); TestFrame(); Verify(fragmentOptionsOpen && FragmentOptionsBounds.Height < GetScreenHeight(), "options fit the minimum window");
            Press(KeyboardKey.Escape); Press(KeyboardKey.Escape); Press(KeyboardKey.Escape);
            Verify(fragmentMode == FragmentToolMode.Off && fragmentClipboard != null, "Escape closes the tool without losing the clipboard");
            SetWindowSize(1440, 900); preferences.ShowMinimap = false; Complete("load"); tool = EditTool.Select; TestFrame();
            Console.WriteLine("Fragment interaction checks passed: grid selection, copy/preview/repeat paste, category options, replacement, invalid edges, names/library, cross-map water alignment, minimap, independent links, undo/redo, save/load, Play and minimum window.");
        }
        finally
        {
            StopFragmentTool(true); fragmentLibraryOpen = fragmentOptionsOpen = false;
            if (Directory.Exists(fragmentDirectory)) Directory.Delete(fragmentDirectory, true);
            fragmentDirectory = originalDirectory;
        }
    }
}
