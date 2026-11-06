using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunWorkflowInteractionChecks()
    {
        SetWindowSize(1440, 900);
        EndEditorGesture(); document.New(48, 32); selectedId = null; selectedIds.Clear();
        tool = EditTool.Select; activePrefab = null; objectBrush = terrainStamp = false;
        assetSearch.Value = ""; assetSearch.Focused = false; preferences.ShowMinimap = false;
        preferences.AutosaveSeconds = 0; focus = Vector3.Zero; ApplyRuntimeSettings(); TestFrame();
        string tree = assets.Sprites.Values.First(a => a.Id.StartsWith("objects/vegetation/")).Id;
        var first = document.Place(tree, new(-2, 0, 2), 2, true);
        var second = document.Place(tree, new(2, 0, 2), 3, false);
        var npc = document.PlaceNpc(null, new(-12, 0, -10)); npc.Name = "Strażnik Żółw";
        Save();

        // The three views are reachable from the actual header; entering free mode preserves the map.
        Click(GetScreenWidth() - 150, 23);
        string mapBeforeFlight = document.Map.Serialize();
        Click(GetScreenWidth() - 150, 23);
        Verify(camera.IsFree && document.Map.Serialize() == mapBeforeFlight, "free view never changes game launch settings");
        var initialPosition = camera.View.Position;
        Press(KeyboardKey.W);
        Verify(camera.View.Position != initialPosition, "W moves the free editor camera");
        float initialY = camera.View.Position.Y; Press(KeyboardKey.E);
        Verify(camera.View.Position.Y > initialY, "E raises the free camera");
        Press(KeyboardKey.F5);
        Verify(play != null && play.Camera.View.Projection == CameraProjection.Orthographic, "Play retains the selected game projection while editing in free mode");
        Press(KeyboardKey.F5); Verify(camera.IsFree, "stopping Play restores free navigation");

        // Search owns keyboard input, finds Polish names, and selects the result.
        Input(2, (int)KeyboardKey.LeftControl); Press(KeyboardKey.F); Input(1, (int)KeyboardKey.LeftControl); TestFrame();
        Verify(sceneSearchOpen && sceneSearch.Focused, "Ctrl+F opens scene search");
        sceneSearch.Append("ŻÓŁW NPC");
        var beforeSearch = camera.View.Position;
        Press(KeyboardKey.Six); Press(KeyboardKey.W);
        Verify(sceneSearchOpen && tool == EditTool.Select && camera.View.Position == beforeSearch, "scene search blocks editor shortcuts and camera motion");
        Verify(SceneResults().Length == 1, "scene search filters names and types");
        Press(KeyboardKey.Enter);
        Verify(!sceneSearchOpen && selectedId == npc.Id && Vector3.Distance(focus, document.Map.GroundPosition(npc)) < .01f, "Enter selects and focuses the search result");
        sceneSearch.Value = "";
        Click((int)AssetSearchBounds.X + 20, (int)AssetSearchBounds.Y + 15);
        Verify(assetSearch.Focused, "library search field takes focus");
        assetSearch.Append("rock");
        var textCamera = camera.View.Position;
        Press(KeyboardKey.W); Press(KeyboardKey.Three); Press(KeyboardKey.F5);
        Verify(camera.View.Position == textCamera && tool == EditTool.Select && play == null, "library text entry blocks movement, tool shortcuts and Play");
        Press(KeyboardKey.Escape);
        Verify(!assetSearch.Focused && assetSearch.Value == "rock", "Escape leaves the library filter applied");
        Click((int)AssetSearchBounds.X + 204, (int)AssetSearchBounds.Y + 15);
        Verify(assetSearch.Value == "", "library clear button");
        Press(KeyboardKey.Escape);

        // A Ctrl+drag can begin over any sprite; group properties act on every selected object.
        ChangeProjection(ViewProjection.Perspective); focus = Vector3.Zero; camera.Snap(focus); TestFrame();
        Vector2 ScreenCenter(SceneObject entity) => GetWorldToScreenEx(renderer.GetSpriteQuad(camera.View, entity.Asset, document.Map.GroundPosition(entity), entity.Height).Center,
            camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        var a = ScreenCenter(first); var b = ScreenCenter(second);
        int minX = (int)Math.Min(a.X, b.X) - 20, minY = (int)Math.Min(a.Y, b.Y) - 20;
        int maxX = (int)Math.Max(a.X, b.X) + 20, maxY = (int)Math.Max(a.Y, b.Y) + 20;
        Input(2, (int)KeyboardKey.LeftControl);
        Input(7, minX, minY); Input(6, 0); TestFrame();
        Input(7, maxX, maxY); TestFrame(); Input(5, 0); TestFrame();
        Input(1, (int)KeyboardKey.LeftControl); TestFrame();
        Verify(SelectionGroup.Length == 2 && selectedIds.Contains(first.Id) && selectedIds.Contains(second.Id), "marquee selects both rendered sprites");
        int ix = GetScreenWidth() - Right + 18;
        float h1 = first.Height, h2 = second.Height;
        Click(ix + 165, 316);
        Verify(Math.Abs(first.Height / h1 - 1.1f) < .001f && Math.Abs(second.Height / h2 - 1.1f) < .001f, "group size button changes all relative sizes");
        Click(ix + 90, 354); Verify(first.Solid && second.Solid, "mixed group collision becomes enabled for all");
        ClickHeader("Undo");
        Verify(SelectionGroup.Count(e => e.Solid) == 1, "group collision is one undo step");
        ClickHeader("Undo");
        Verify(SelectionGroup.Any(e => e.Height == h1) && SelectionGroup.Any(e => e.Height == h2), "group scale is one undo step");

        // Scatter stroke: a stationary held mouse does not pile up more objects each frame.
        Choose(assets.Sprites[tree]); category = 0;
        Click(ix + 100, 309);
        Verify(objectBrush, "object brush toggle");
        Click(ix + 100, 349); Verify(brushSettingsOpen, "object brush settings modal");
        Press(KeyboardKey.Escape);
        int objectCount = document.Map.Objects.Count;
        var spot = GetWorldToScreenEx(new(7, 0, 6), camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        Input(7, (int)spot.X, (int)spot.Y); Input(6, 0); TestFrame();
        int afterDab = document.Map.Objects.Count;
        Verify(afterDab > objectCount, "object brush adds a dab");
        for (int i = 0; i < 10; i++) { Input(7, (int)spot.X, (int)spot.Y); TestFrame(); }
        Verify(document.Map.Objects.Count == afterDab, "object brush density is independent of frame rate");
        Input(7, (int)spot.X + 90, (int)spot.Y); TestFrame(); Input(5, 0); TestFrame();
        ClickHeader("Undo"); Verify(document.Map.Objects.Count == objectCount, "one undo removes the full object brush stroke");
        objectBrush = false;

        ClickHeader("Sculpt"); Click(100, 431);
        Verify(terrainStamp, "terrain stamp palette");
        focus = Vector3.Zero; camera.Snap(focus); TestFrame();
        spot = GetWorldToScreenEx(new(-4, 0, -2), camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        Input(7, (int)spot.X, (int)spot.Y); Input(6, 0); TestFrame();
        var heights = (float[])document.Map.Heights.Clone();
        Verify(heights.Any(h => h > 1), "click stamps a mountain");
        for (int i = 0; i < 10; i++) { Input(7, (int)spot.X, (int)spot.Y); TestFrame(); }
        Verify(document.Map.Heights.SequenceEqual(heights), "holding a stamp never repeats it each frame");
        Input(5, 0); TestFrame();
        ClickHeader("Undo"); Verify(document.Map.Heights.Length == 0, "stamp is one undo step");
        ClickHeader("Redo"); Verify(document.Map.Heights.SequenceEqual(heights), "stamp redo preserves the exact natural shape");

        // Clicking an overview must be navigation even while a destructive tool is selected.
        Press(KeyboardKey.M); Verify(preferences.ShowMinimap && overview.Texture.Id != 0, "M enables the minimap");
        string beforeMinimap = document.Map.Serialize();
        var destination = new Vector3(12, 0, 8);
        var overviewClick = TerrainOverview.Project(document.Map, destination, MinimapMapBounds);
        Click((int)overviewClick.X, (int)overviewClick.Y);
        Verify(Math.Abs(focus.X - destination.X) < .4f && Math.Abs(focus.Z - destination.Z) < .4f && document.Map.Serialize() == beforeMinimap,
            "minimap click moves the camera without stamping or placing objects");

        // Exercise the actual autosave scheduler, then restore through the confirmation dialog.
        preferences.AutosaveSeconds = 30; nextAutosave = -1;
        worldPage = "Environment";
        UpdateAutosave(); Verify(autosaveTask != null, "autosave also protects work while editing in a modal panel");
        string recoveryFile = autosaveTask!.GetAwaiter().GetResult(); UpdateAutosave();
        worldPage = null;
        Verify(MapData.Load(recoveryFile).Serialize() == document.Map.Serialize() && document.Dirty, "autosave preserves dirty state and the complete map");
        string diskMap = File.ReadAllText(mapPath);
        document.Checkpoint(); document.Map.Player.X = 9;
        string altered = document.Map.Serialize();
        OpenRecoveryList(); TestFrame();
        Verify(recoveryListOpen && recoveryCopies.Length > 0, "recovery history is available in the editor");
        RestoreRecovery(recoveryFile); TestFrame();
        Verify(pendingAction == "recover", "recovery asks what to do with current unsaved work");
        Click(GetScreenWidth() / 2 - 230 + 215, GetScreenHeight() / 2 - 90 + 132);
        Verify(pendingAction == null && document.Map.Serialize() == beforeMinimap && File.ReadAllText(mapPath) == diskMap && document.Dirty,
            "restoring a copy preserves the saved map and restores all edited data");
        ClickHeader("Undo"); Verify(document.Map.Serialize() == altered, "undo returns to the scene before recovery");
        ClickHeader("Redo");
        preferences.AutosaveSeconds = 0;
        tool = EditTool.Sculpt; terrainStamp = true; focus = Vector3.Zero; camera.Snap(focus); EnterFreeCamera();
        TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "workflow-tools.png"));
        SetWindowSize(1120, 720); TestFrame(); TestFrame();
        Verify(MinimapBounds.X >= viewport.X && MinimapBounds.X + MinimapBounds.Width <= viewport.X + viewport.Width,
            "minimap fits the smallest editor window");
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "workflow-tools-small.png"));
        SetWindowSize(1440, 900);
        preferences.ShowMinimap = false; terrainStamp = objectBrush = false; selectedIds.Clear(); selectedId = null;
        assetSearch.Value = ""; assetSearch.Focused = sceneSearch.Focused = false; sceneSearch.Value = "";
        Complete("load"); tool = EditTool.Select; TestFrame();
        Console.WriteLine("Workflow interaction checks passed: three camera views, flight/Play isolation, searches, marquee/group properties, scatter, terrain stamps, minimap navigation, autosave and recovery.");
    }
}
