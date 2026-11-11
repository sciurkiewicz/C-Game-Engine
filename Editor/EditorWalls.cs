using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    WallData wallDraft = new();
    readonly WallData wallPreview = new();
    string? editingWallId;
    bool wallPicking;
    int wallDraggedPoint = -1, wallListScroll;
    static string WallStyleName(WallStyle style) => style switch { WallStyle.Cave => "Skała jaskiniowa", WallStyle.Masonry => "Mur", _ => "Drewno" };

    void ResetWallDraft()
    {
        wallDraft = new() { Style = wallDraft.Style, Height = wallDraft.Height, Thickness = wallDraft.Thickness,
            Solid = wallDraft.Solid, FadeWhenBlocking = wallDraft.FadeWhenBlocking };
        editingWallId = null; wallDraggedPoint = -1;
    }
    void OpenWallTool()
    {
        EndEditorGesture(); StopFragmentTool(); activePrefab = null; assetSearch.Focused = false;
        if (tool != EditTool.Walls) { ResetWallDraft(); wallPicking = false; }
        tool = EditTool.Walls; selectedId = null; selectedIds.Clear();
        status = wallGridMode ? "Lochy: maluj ściany LMB po siatce. Shift+LMB wykuwa korytarze." : "Ściany: klikaj kolejne punkty na mapie. Enter zapisuje, Esc anuluje szkic.";
    }
    void EditWall(WallData wall)
    {
        OpenWallTool(); wallGridMode = false; wallDraft = ScenePrefab.Clone(wall); editingWallId = wall.Id;
        wallPicking = false; wallDraggedPoint = -1;
        status = "Przeciągaj punkty ściany na podłożu. Enter zapisuje zmiany, Esc je odrzuca.";
    }
    bool ApplyWallDraft()
    {
        if (wallDraft.Points.Count < (wallDraft.Closed ? 3 : 2)) { status = "Dodaj co najmniej dwa punkty (trzy dla zamkniętego obrysu)."; return false; }
        try
        {
            if (editingWallId == null) wallDraft.Name = "Ściana " + (document.Map.Walls.Count + 1);
            document.ApplyWall(wallDraft, editingWallId);
            ResetWallDraft(); wallPicking = false;
            status = "Ściana zapisana. Klikaj, aby narysować kolejną. Ctrl+Z cofa całą operację.";
            return true;
        }
        catch (InvalidDataException ex) { status = ex.Message; return false; }
    }
    void DeleteEditedWall()
    {
        if (editingWallId != null) document.DeleteWall(editingWallId);
        ResetWallDraft(); status = "Ściana usunięta. Ctrl+Z przywraca ją na mapie.";
    }
    bool WallShortcuts(bool ctrl)
    {
        if (tool != EditTool.Walls) return false;
        if (wallGridMode)
        {
            if (IsKeyPressed(KeyboardKey.Escape) && gridWallStart != null) { EndEditorGesture(); return true; }
            return false;
        }
        if (IsKeyPressed(KeyboardKey.Escape) && (wallDraft.Points.Count > 0 || wallPicking))
        { ResetWallDraft(); wallPicking = false; status = "Szkic ściany anulowany."; return true; }
        if (IsKeyPressed(KeyboardKey.Backspace) && wallDraft.Points.Count > 0)
        { wallDraft.Points.RemoveAt(wallDraft.Points.Count - 1); if (wallDraft.Points.Count < 3) wallDraft.Closed = false; return true; }
        if (IsKeyPressed(KeyboardKey.Enter)) { ApplyWallDraft(); return true; }
        if (IsKeyPressed(KeyboardKey.Delete)) { DeleteEditedWall(); return true; }
        if (ctrl && IsKeyPressed(KeyboardKey.D) && editingWallId != null)
        {
            wallDraft.Id = Guid.NewGuid().ToString("N"); editingWallId = null;
            wallDraft.Name += " (kopia)";
            status = "Kopia ściany: przesuń punkty i zapisz Enterem.";
            return true;
        }
        return false;
    }
    bool UpdateWallTool()
    {
        if (tool != EditTool.Walls) return false;
        if (wallGridMode) return UpdateGridWallTool();
        if (!IsMouseButtonDown(MouseButton.Left)) wallDraggedPoint = -1;
        if (!InViewport) return true;
        var ground = MouseTerrainPoint;
        if (IsMouseButtonPressed(MouseButton.Left))
        {
            if (wallPicking)
            {
                if (renderer.PickWall(document.Map, MouseRay) is { } wall) EditWall(wall);
                else status = "Kliknij ścianę lub wybierz ją z listy po lewej.";
                return true;
            }
            for (int i = 0; i < wallDraft.Points.Count; i++)
            {
                var p = wallDraft.Points[i];
                var screen = GetWorldToScreenEx(document.Map.GroundPosition(p.X, p.Z), camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
                if (Vector2.DistanceSquared(screen, GetMousePosition()) < 100) { wallDraggedPoint = i; break; }
            }
            if (wallDraggedPoint < 0 && ground is { } cursor && document.Map.Contains(cursor.X, cursor.Z))
            {
                var p = Snap(cursor);
                if (wallDraft.Points.Count >= WallData.PointLimit) status = "Limit: 128 punktów na ścianę.";
                else if (!wallDraft.Closed && (wallDraft.Points.Count == 0 || Vector2.DistanceSquared(wallDraft.Points[^1].Vector, new(p.X, p.Z)) >= .01f))
                    wallDraft.Points.Add(new(p.X, p.Z));
            }
        }
        if (wallDraggedPoint >= 0 && ground is { } dragged && IsMouseButtonDown(MouseButton.Left))
        { var p = Snap(dragged); wallDraft.Points[wallDraggedPoint] = new(p.X, p.Z); }
        return true;
    }
    void DrawWallPalette()
    {
        Label("ŚCIANY", 16, 108, 15, Accent);
        if (Button("Punkty", 84, 99, 70, !wallGridMode, play == null)) SetWallGridMode(false);
        if (Button("Siatka", 162, 99, 72, wallGridMode, play == null)) SetWallGridMode(true);
        if (wallGridMode) { DrawGridWallPalette(); return; }
        if (Button("Rysuj nową ścianę", 16, 146, 218, !wallPicking && editingWallId == null, play == null))
        { ResetWallDraft(); wallPicking = false; }
        if (Button("Wybierz ścianę na mapie", 16, 188, 218, wallPicking, play == null))
        { ResetWallDraft(); wallPicking = true; }
        Label("MATERIAŁ", 16, 242, 12, Muted);
        foreach (var style in Enum.GetValues<WallStyle>())
            if (Button(WallStyleName(style), 16, 266 + (int)style * 38, 218, wallDraft.Style == style, play == null)) wallDraft.Style = style;
        Label("ZAPISANE ŚCIANY", 16, 402, 12, Muted);
        var area = new Rectangle(0, 426, Left, Math.Max(1, GetScreenHeight() - Bottom - 426));
        if (play == null && !ModalOpen && CheckCollisionPointRec(GetMousePosition(), area)) wallListScroll -= (int)(GetMouseWheelMove() * 38);
        wallListScroll = Math.Clamp(wallListScroll, 0, Math.Max(0, document.Map.Walls.Count * 38 - (int)area.Height));
        BeginScissorMode((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
        foreach (var pair in document.Map.Walls.Select((wall, index) => (wall, index)))
        {
            int y = 430 + pair.index * 38 - wallListScroll;
            if (y + 30 < area.Y || y > area.Y + area.Height) continue;
            if (Button(pair.wall.Name, 16, y, 218, editingWallId == pair.wall.Id,
                play == null && GetMouseY() >= area.Y && GetMouseY() < area.Y + area.Height))
            {
                EditWall(pair.wall);
                FocusCamera(new(pair.wall.Points.Average(p => p.X), 0, pair.wall.Points.Average(p => p.Z)));
            }
        }
        if (document.Map.Walls.Count == 0) Label("Brak ścian. Narysuj pierwszą.", 16, 438, 12, Muted);
        EndScissorMode();
    }
    void DrawWallInspector(int x)
    {
        if (wallGridMode) { DrawGridWallInspector(x); return; }
        Label(editingWallId == null ? "Nowa ściana" : wallDraft.Name, x, 147, 16, Accent, 220);
        Stepper("Wysokość (m)", wallDraft.Height, x, 184, v => wallDraft.Height = Math.Clamp(v, .25f, 24), .25f);
        Stepper("Grubość (m)", wallDraft.Thickness, x, 244, v => wallDraft.Thickness = Math.Clamp(v, .1f, 4), .1f);
        if (Button("Kolizja: " + (wallDraft.Solid ? "WŁ." : "WYŁ."), x, 304, 218, wallDraft.Solid)) wallDraft.Solid = !wallDraft.Solid;
        if (Button("Odsłanianie gracza: " + (wallDraft.FadeWhenBlocking ? "WŁ." : "WYŁ."), x, 346, 218, wallDraft.FadeWhenBlocking)) wallDraft.FadeWhenBlocking = !wallDraft.FadeWhenBlocking;
        if (Button(wallDraft.Closed ? "Obrys zamknięty" : "Zamknij obrys / pokój", x, 388, 218, wallDraft.Closed, wallDraft.Points.Count >= 3)) wallDraft.Closed = !wallDraft.Closed;
        if (Button("Zapisz / Enter", x, 438, 105, true, wallDraft.Points.Count >= (wallDraft.Closed ? 3 : 2))) ApplyWallDraft();
        if (Button("Cofnij punkt", x + 113, 438, 105, false, wallDraft.Points.Count > 0))
        { wallDraft.Points.RemoveAt(wallDraft.Points.Count - 1); if (wallDraft.Points.Count < 3) wallDraft.Closed = false; }
        if (Button("Nazwa", x, 480, 105, false, editingWallId != null))
            BeginTextEdit("Nazwa ściany", wallDraft.Name, 80, false, name => wallDraft.Name = name.Trim());
        if (Button("Usuń ścianę", x + 113, 480, 105, false, editingWallId != null)) DeleteEditedWall();
        Label($"Punkty: {wallDraft.Points.Count}/128", x, 536, 13, Text);
        bool compact = GetScreenHeight() < 800;
        Label(compact ? "Enter: zapisz / Esc: odrzuć szkic" : "Klikaj punkty na podłożu.", x, 566, 12, Muted);
        Label("Przeciągaj uchwyty, aby je przesunąć.", x, 588, 12, Muted, 224);
        if (!compact)
        {
            Label("Enter: zapisz / Esc: odrzuć szkic", x, 614, 12, Muted);
            Label("Pozostaw przerwę na drzwi.", x, 640, 12, Accent);
        }
        // Keep the common controls below the wall tools even in the 720 px window.
        int row = compact ? 608 : 684;
        if (Button(grid ? "Grid: ON" : "Grid: OFF", x, row, 104, grid)) grid = !grid;
        if (Button(snap ? "Snap: 0.5" : "Snap: OFF", x + 112, row, 106, snap)) snap = !snap;
        if (Button("Find objects", x, row + 42, 104)) OpenSceneSearch();
        if (Button("Minimap / M", x + 112, row + 42, 106, preferences.ShowMinimap)) ToggleMinimap();
    }
    void DrawWallDraft(Camera3D view)
    {
        if (tool != EditTool.Walls) return;
        if (wallGridMode) { DrawGridWallPreview(view); return; }
        wallPreview.Style = wallDraft.Style; wallPreview.Height = wallDraft.Height; wallPreview.Thickness = wallDraft.Thickness;
        wallPreview.Closed = wallDraft.Closed; wallPreview.Points = wallDraft.Points.ToList();
        if (editingWallId == null && !wallPicking && !wallDraft.Closed && wallDraft.Points.Count > 0 && wallDraggedPoint < 0 &&
            InViewport && !ModalOpen && MouseTerrainPoint is { } cursor && document.Map.Contains(cursor.X, cursor.Z))
        {
            var point = Snap(cursor);
            if (Vector2.DistanceSquared(wallDraft.Points[^1].Vector, new(point.X, point.Z)) >= .01f)
                wallPreview.Points.Add(new(point.X, point.Z));
        }
        if (wallPreview.Points.Count >= 2)
        {
            renderer.DrawWallPreview(document.Map, view, wallPreview, Color.White);
            renderer.DrawWallOutline(document.Map, wallPreview, Accent);
        }
        Rlgl.DrawRenderBatchActive(); Rlgl.DisableDepthTest();
        foreach (var p in wallDraft.Points)
            DrawSphere(document.Map.GroundPosition(p.X, p.Z) + Vector3.UnitY * .09f, .14f, Accent);
        Rlgl.DrawRenderBatchActive(); Rlgl.EnableDepthTest();
    }
}
