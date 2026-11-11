using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunWallInteractionChecks()
    {
        SetWindowSize(1440, 900); EndEditorGesture(); StopFragmentTool(true); ResetRoadDraft(); ResetWallDraft();
        document.New(28, 24); tool = EditTool.Select; selectedId = null; selectedIds.Clear(); activePrefab = null;
        preferences.ShowMinimap = false; preferences.AutosaveSeconds = 0; snap = grid = true; wallGridMode = false;
        assetSearch.Focused = false; sceneSearch.Value = ""; sceneFilter = "All";
        document.Map.Player.X = -6; document.Map.Environment.Audio = false;
        focus = Vector3.Zero; ApplyRuntimeSettings(); TestFrame(); Save();
        int ix = GetScreenWidth() - Right + 18;
        Vector2 Screen(Vector3 p) => GetWorldToScreenEx(p, camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        void GroundClick(float x, float z)
        { var p = Screen(document.Map.GroundPosition(x, z)); Click((int)p.X, (int)p.Y); }

        ClickHeader("Walls"); Verify(tool == EditTool.Walls, "the separate Walls tab opens");
        Click(100, 319); Verify(wallDraft.Style == WallStyle.Masonry, "masonry material selector");
        Click(ix + 201, 214); Click(ix + 201, 274);
        Verify(wallDraft.Height == 3.25f && Math.Abs(wallDraft.Thickness - .7f) < .001f, "height and thickness controls");
        string empty = document.Map.Serialize();
        GroundClick(-4, -3); GroundClick(4, -3); GroundClick(4, 3);
        Verify(wallDraft.Points.Count == 3 && document.Map.Serialize() == empty, "draft previews leave the map unchanged");
        Press(KeyboardKey.Backspace); Verify(wallDraft.Points.Count == 2, "Backspace removes the last draft point");
        GroundClick(4, 3); Press(KeyboardKey.Enter);
        Verify(document.Map.Walls.Count == 1 && wallDraft.Points.Count == 0 && tool == EditTool.Walls, "Enter commits a wall and leaves the tab active");
        string applied = document.Map.Serialize(); string id = document.Map.Walls[0].Id;
        ClickHeader("Undo"); Verify(document.Map.Serialize() == empty, "wall creation is one undo step");
        ClickHeader("Redo"); Verify(document.Map.Serialize() == applied, "redo restores the same wall");
        Click(100, 445); Verify(editingWallId == id && wallDraft.Points.Count == 3, "saved-wall list opens editable points");
        var handle = Screen(document.Map.GroundPosition(4, -3)); var moved = Screen(document.Map.GroundPosition(5, -3));
        Input(7, (int)handle.X, (int)handle.Y); Input(6, 0); TestFrame();
        Input(7, (int)moved.X, (int)moved.Y); TestFrame(); Input(5, 0); TestFrame();
        Verify(wallDraft.Points[1].X == 5 && document.Map.Walls[0].Points[1].X == 4, "dragging edits a draft until it is committed");
        Click(ix + 50, 495); Verify(textEdit != null, "wall name editor opens");
        AppendEdit("Mur północny"); Press(KeyboardKey.Enter); Press(KeyboardKey.Enter);
        Verify(document.Map.Walls.Count == 1 && document.Map.Walls[0].Id == id && document.Map.Walls[0].Name == "Mur północny" && document.Map.Walls[0].Points[1].X == 5,
            "editing preserves the wall ID and saves new points and name");
        Click(100, 203); Verify(wallPicking, "picking mode activates");
        var body = Screen(new(0, 1.3f, -3)); Click((int)body.X, (int)body.Y);
        Verify(editingWallId == id, "clicking a rendered wall selects its geometry");
        Click(ix + 201, 214); Press(KeyboardKey.Escape);
        Verify(document.Map.Walls[0].Height == 3.25f && wallDraft.Points.Count == 0, "Escape discards edits to an existing wall");
        Click(100, 357); Verify(wallDraft.Style == WallStyle.Wood, "wood material selector");
        GroundClick(-3, 0); GroundClick(0, 0); GroundClick(0, 4); GroundClick(-3, 4);
        Click(ix + 100, 403); Verify(wallDraft.Closed, "room outline closes without adding a duplicate point");
        Press(KeyboardKey.Enter); Verify(document.Map.Walls.Count == 2 && document.Map.Walls[1].Closed, "closed room saves");
        Click(100, 281); GroundClick(-7, 3); GroundClick(-5, 5); GroundClick(1, 6); Press(KeyboardKey.Enter);
        Verify(document.Map.Walls.Count == 3 && document.Map.Walls[2].Style == WallStyle.Cave, "cave wall material saves");
        OpenSceneSearch(); sceneSearch.Append("północny"); Press(KeyboardKey.Enter);
        Verify(tool == EditTool.Walls && editingWallId == id, "scene search opens the wall for editing by name");
        Press(KeyboardKey.Delete); Verify(document.Map.Walls.Count == 2, "Delete removes the selected wall");
        ClickHeader("Undo"); Verify(document.Map.Walls.Count == 3, "undo restores a deleted wall");
        preferences.ShowMinimap = true; TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "walls-editor-perspective.png"));
        Press(KeyboardKey.V); TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "walls-editor-isometric.png"));
        Press(KeyboardKey.V); Verify(camera.IsFree && document.Map.Walls.Count == 3, "walls remain in the map in free camera");
        Press(KeyboardKey.V); Save();
        Verify(MapData.Load(mapPath).Walls.Count == 3, "map file includes authored walls");
        string beforePlay = document.Map.Serialize(); Press(KeyboardKey.F5);
        Verify(play != null && play.Map.Walls.Count == 3 && !play.TryMovePlayer(new(6, 0, 0)), "Play includes working wall collision");
        play!.Map.Walls[0].Height = 9; Press(KeyboardKey.F5);
        Verify(document.Map.Serialize() == beforePlay, "Play cannot mutate the authored walls");
        Complete("load"); Verify(document.Map.Walls.Count == 3 && editingWallId == null, "loading a map restores walls and clears stale drafts");
        SetWindowSize(1120, 720); TestFrame(); TestFrame(); ClickHeader("Walls");
        Verify(tool == EditTool.Walls && ToolbarToolWidth * 10 < GetScreenWidth() - 450, "Walls tab and save controls fit the minimum window");
        ix = GetScreenWidth() - Right + 18;
        Click(100, 445); Verify(editingWallId == id, "wall selection works in the minimum window");
        Click(ix + 165, 495); Verify(document.Map.Walls.Count == 2 && snap, "delete stays separate from the lower common controls in the minimum window");
        ClickHeader("Undo");
        Click(ix + 165, 623); Verify(!snap, "snap toggle fits below wall controls in the minimum window");
        Click(ix + 165, 623); Verify(snap, "snap can be restored");
        ClickHeader("Select"); Press(KeyboardKey.Zero); Verify(tool == EditTool.Walls, "zero shortcut opens Walls");
        SetWindowSize(1440, 900); TestFrame(); TestFrame();
        WallGpuChecks.Run(assets);
        RunGridWallInteractionChecks();
        Console.WriteLine("Editor wall interaction checks passed: tab, materials, points, drag, room, picking, rename/delete, cancellation, search, save/load, undo/redo, minimap, cameras and Play.");
    }
}
