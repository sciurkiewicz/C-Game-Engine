using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    readonly List<GroundPoint2> roadPoints = [];
    string roadMaterial = "dirt", roadName = "Road";
    string? editingRoadId;
    float roadWidth = 3, roadSoftness = .75f;
    bool roadClearVegetation = true, roadListOpen, roadSmooth = true;
    int roadDraggedPoint = -1, roadListScroll;
    Rectangle RoadListBounds => new(GetScreenWidth() / 2 - 360, GetScreenHeight() / 2 - 250, 720, 500);
    void ResetRoadDraft()
    { roadPoints.Clear(); editingRoadId = null; roadDraggedPoint = -1; roadSmooth = true; }
    bool RoadShortcuts()
    {
        if (tool != EditTool.Ground || groundMode != GroundMode.Roads) return false;
        if (IsKeyPressed(KeyboardKey.Escape) && roadPoints.Count > 0) { ResetRoadDraft(); status = "Road draft cancelled."; return true; }
        if (IsKeyPressed(KeyboardKey.Backspace) && roadPoints.Count > 0) { roadPoints.RemoveAt(roadPoints.Count - 1); return true; }
        if (IsKeyPressed(KeyboardKey.Enter)) { ApplyRoadDraft(); return true; }
        return false;
    }
    void UpdateRoadInput(Vector3 ground)
    {
        if (IsMouseButtonPressed(MouseButton.Left))
        {
            var mouse = GetMousePosition();
            roadDraggedPoint = -1;
            for (int i = 0; i < roadPoints.Count; i++)
            {
                var screen = GetWorldToScreenEx(document.Map.GroundPosition(roadPoints[i].X, roadPoints[i].Z), camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
                if (Vector2.DistanceSquared(mouse, screen) < 100) { roadDraggedPoint = i; break; }
            }
            if (roadDraggedPoint < 0)
            {
                var point = Snap(ground);
                if (roadPoints.Count >= 128) { status = "A road can have up to 128 control points."; return; }
                if (roadPoints.Count == 0 || Vector2.Distance(roadPoints[^1].Vector, new(point.X, point.Z)) >= .1f)
                    roadPoints.Add(new(point.X, point.Z));
            }
        }
        if (roadDraggedPoint >= 0 && roadDraggedPoint < roadPoints.Count && IsMouseButtonDown(MouseButton.Left))
        { var point = Snap(ground); roadPoints[roadDraggedPoint] = new(point.X, point.Z); }
    }
    bool ApplyRoadDraft()
    {
        if (roadPoints.Count < 2) { status = "A road needs at least two control points."; return false; }
        try
        {
            var road = new RoadData { Id = editingRoadId ?? Guid.NewGuid().ToString("N"), Name = editingRoadId == null ? "Road " + (document.Map.Roads.Count + 1) : roadName,
                Material = roadMaterial, Width = roadWidth, Softness = Math.Min(roadSoftness, roadWidth / 2), ClearVegetation = roadClearVegetation, Smooth = roadSmooth, Points = roadPoints.ToList() };
            document.ApplyRoad(road, editingRoadId);
            ResetRoadDraft(); selectedId = null; selectedIds.Clear();
            status = "Road applied. Road list opens saved curves for editing; Ctrl+Z undoes the whole change.";
            return true;
        }
        catch (InvalidDataException ex) { status = ex.Message; return false; }
    }
    void EditRoad(RoadData road)
    {
        EndEditorGesture(); roadListOpen = false; tool = EditTool.Ground; SetGroundMode(GroundMode.Roads);
        ResetRoadDraft(); editingRoadId = road.Id; roadName = road.Name;
        roadMaterial = road.Material; roadWidth = road.Width; roadSoftness = road.Softness; roadClearVegetation = road.ClearVegetation;
        roadSmooth = road.Smooth;
        roadPoints.AddRange(road.Points);
        var center = new Vector3(road.Points.Average(p => p.X), 0, road.Points.Average(p => p.Z));
        FocusCamera(center);
    }
    void DrawRoadInspector(int x)
    {
        Label((editingRoadId == null ? "New road: " : roadName + ": ") + Path.GetFileNameWithoutExtension(roadMaterial), x, 147, 14, Accent, 220);
        Stepper("Width (m)", roadWidth, x, 184, value => { roadWidth = Math.Clamp(value, .5f, 64); roadSoftness = Math.Min(roadSoftness, roadWidth / 2); }, .5f);
        Stepper("Soft edge (m)", roadSoftness, x, 244, value => roadSoftness = Math.Clamp(value, 0, roadWidth / 2), .25f);
        if (Button(roadClearVegetation ? "Clear vegetation: ON" : "Clear vegetation: OFF", x, 304, 218, roadClearVegetation)) roadClearVegetation = !roadClearVegetation;
        if (Button("Apply / Enter", x, 346, 105, true, roadPoints.Count >= 2)) ApplyRoadDraft();
        if (Button("Remove point", x + 113, 346, 105, false, roadPoints.Count > 0)) roadPoints.RemoveAt(roadPoints.Count - 1);
        if (Button("Road list...", x, 388, 105)) { EndEditorGesture(); roadListOpen = true; roadListScroll = 0; }
        if (Button("Delete road", x + 113, 388, 105, false, editingRoadId != null))
        { document.DeleteRoad(editingRoadId!); ResetRoadDraft(); }
        Label($"{roadPoints.Count}/128 points / Esc: cancel draft", x, 432, 12, Muted);
    }
    void DrawRoadList()
    {
        var box = RoadListBounds; int x = (int)box.X, y = (int)box.Y;
        bool enabled = textEdit == null && pendingAction == null;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 180));
        DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        Label("ROADS", x + 24, y + 24, 23, Accent);
        Label("Choose a road, drag its control points, then Apply / Enter.", x + 24, y + 64, 14, Muted);
        var area = new Rectangle(x + 24, y + 106, 672, 316);
        if (enabled && CheckCollisionPointRec(GetMousePosition(), area)) roadListScroll -= (int)(GetMouseWheelMove() * 42);
        roadListScroll = Math.Clamp(roadListScroll, 0, Math.Max(0, document.Map.Roads.Count * 42 - 316));
        BeginScissorMode((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
        for (int i = 0; i < document.Map.Roads.Count; i++)
        {
            var road = document.Map.Roads[i]; int row = (int)area.Y + i * 42 - roadListScroll;
            if (row + 30 < area.Y || row > area.Y + area.Height) continue;
            bool visible = GetMouseY() >= area.Y && GetMouseY() < area.Y + area.Height;
            if (Button($"{road.Name}   /   {Path.GetFileNameWithoutExtension(road.Material)}   /   {road.Width:0.#} m", x + 24, row, 548, false, enabled && visible, true)) { EditRoad(road); break; }
            if (Button("Name", x + 584, row, 112, false, enabled && visible, true))
                BeginTextEdit("Road name", road.Name, 80, false, name => { document.Checkpoint(); road.Name = name.Trim(); });
        }
        if (document.Map.Roads.Count == 0) Label("No roads yet. Click points on the map and press Enter.", x + 36, y + 124, 15, Muted);
        EndScissorMode();
        if (Button("New road", x + 24, y + 446, 200, true, enabled, true)) { ResetRoadDraft(); roadListOpen = false; }
        if (Button("Close / Esc", x + 496, y + 446, 200, false, enabled, true)) roadListOpen = false;
    }
    void DrawRoadPreview()
    {
        if (tool != EditTool.Ground || groundMode != GroundMode.Roads || roadPoints.Count == 0) return;
        var points = roadPoints.ToList();
        if (InViewport && !ModalOpen && roadDraggedPoint < 0 && MouseTerrainPoint is { } cursor &&
            Vector2.Distance(points[^1].Vector, new(cursor.X, cursor.Z)) > .1f) points.Add(new(cursor.X, cursor.Z));
        var curve = RoadGeometry.Sample(points, roadSmooth);
        Vector3 Ground(Vector2 point) => document.Map.Clamp(new(point.X, 0, point.Y)) + Vector3.UnitY * .08f;
        for (int i = 1; i < curve.Length; i++)
        {
            var direction = curve[i] - curve[i - 1]; if (direction.LengthSquared() < .000001f) continue;
            var side = Vector2.Normalize(new Vector2(-direction.Y, direction.X)) * roadWidth / 2;
            DrawLine3D(Ground(curve[i - 1]), Ground(curve[i]), Accent);
            DrawLine3D(Ground(curve[i - 1] + side), Ground(curve[i] + side), new(104, 217, 175, 130));
            DrawLine3D(Ground(curve[i - 1] - side), Ground(curve[i] - side), new(104, 217, 175, 130));
        }
        for (int i = 0; i < roadPoints.Count; i++)
            DrawSphere(Ground(roadPoints[i].Vector), .13f, i == roadDraggedPoint ? new(255, 196, 104, 255) : Accent);
    }
}
