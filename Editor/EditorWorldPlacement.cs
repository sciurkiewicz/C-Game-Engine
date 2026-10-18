using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    bool WorldPlacementActive => drawingTrigger || drawingColliderId != null;
    void UpdateWorldPlacement(float dt)
    {
        if (IsKeyPressed(KeyboardKey.Escape))
        { drawingTrigger = false; drawingColliderId = null; triggerStart = triggerEnd = null; colliderPoints.Clear(); status = "Drawing cancelled."; return; }
        camera.Zoom(InViewport ? GetMouseWheelMove() : 0); camera.Update(focus, dt);
        if (drawingColliderId != null)
        {
            var entity = document.Map.SceneEntities.FirstOrDefault(e => e.Id == drawingColliderId);
            if (entity == null) { drawingColliderId = null; return; }
            if (IsKeyPressed(KeyboardKey.Backspace) && colliderPoints.Count > 0) colliderPoints.RemoveAt(colliderPoints.Count - 1);
            if (InViewport && MouseTerrainPoint is { } point && IsMouseButtonPressed(MouseButton.Left) && colliderPoints.Count < 32)
            {
                point = Snap(point); var local = new GroundPoint2(point.X - entity.X, point.Z - entity.Z);
                if (Math.Abs(local.X) <= 64 && Math.Abs(local.Z) <= 64) colliderPoints.Add(local);
            }
            if (IsKeyPressed(KeyboardKey.Enter))
            {
                try
                {
                    var shape = new ColliderData { Kind = ColliderKind.Polygon, Points = colliderPoints.ToList() }; shape.Validate();
                    document.Checkpoint(); entity.Collider = shape; drawingColliderId = null; colliderPoints.Clear();
                    OpenWorldTools("Collision"); selectedId = entity.Id; status = "Polygon collider applied.";
                }
                catch (InvalidDataException ex) { status = ex.Message; }
            }
            return;
        }
        if (InViewport && MouseTerrainPoint is { } ground)
        {
            ground = Snap(ground);
            if (IsMouseButtonPressed(MouseButton.Left)) triggerStart = triggerEnd = ground;
            if (triggerStart.HasValue && IsMouseButtonDown(MouseButton.Left)) triggerEnd = ground;
        }
        if (triggerStart is { } start && triggerEnd is { } end && !IsMouseButtonDown(MouseButton.Left))
        {
            var center = document.Map.Clamp((start + end) / 2);
            var trigger = new TriggerData { X = center.X, Z = center.Z, Width = Math.Clamp(Math.Abs(start.X - end.X), .5f, 256),
                Depth = Math.Clamp(Math.Abs(start.Z - end.Z), .5f, 256), DestinationX = center.X, DestinationZ = center.Z };
            document.Checkpoint(); document.Map.Triggers.Add(trigger); drawingTrigger = false; triggerStart = triggerEnd = null;
            OpenWorldTools("Triggers"); worldSelectedId = trigger.Id; status = "Trigger area created.";
        }
    }
    void SelectForDrag(SceneObject? hit)
    {
        bool additive = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);
        if (additive && hit != null)
        {
            if (selectedId != null) selectedIds.Add(selectedId);
            if (!selectedIds.Add(hit.Id)) selectedIds.Remove(hit.Id);
            selectedId = selectedIds.FirstOrDefault(); dragArmed = false; return;
        }
        if (hit == null || !selectedIds.Contains(hit.Id)) selectedIds.Clear();
        selectedId = hit?.Id; if (hit != null) selectedIds.Add(hit.Id);
        dragArmed = hit != null;
        groupDragOrigins = SelectionGroup.ToDictionary(e => e.Id, e => new Vector2(e.X, e.Z));
    }
    void MoveSelectionGroup(SceneObject primary, Vector3 point)
    {
        if (groupDragOrigins.Count <= 1 || !groupDragOrigins.TryGetValue(primary.Id, out var origin))
        { primary.X = point.X; primary.Z = point.Z; return; }
        var delta = new Vector2(point.X, point.Z) - origin;
        delta.X = Math.Clamp(delta.X, -document.Map.Width / 2f + .01f - groupDragOrigins.Values.Min(p => p.X),
            document.Map.Width / 2f - .01f - groupDragOrigins.Values.Max(p => p.X));
        delta.Y = Math.Clamp(delta.Y, -document.Map.Depth / 2f + .01f - groupDragOrigins.Values.Min(p => p.Y),
            document.Map.Depth / 2f - .01f - groupDragOrigins.Values.Max(p => p.Y));
        foreach (var entity in SelectionGroup)
            if (groupDragOrigins.TryGetValue(entity.Id, out var initial)) { entity.X = initial.X + delta.X; entity.Z = initial.Y + delta.Y; }
    }
    bool PlaceActivePrefab(Vector3 point)
    {
        if (tool != EditTool.Place || activePrefab == null) return false;
        if (IsMouseButtonPressed(MouseButton.Left))
            try
            {
                var placed = document.PlacePrefab(activePrefab, point); selectedIds.Clear();
                foreach (var entity in placed) selectedIds.Add(entity.Id);
                selectedId = placed.FirstOrDefault()?.Id; status = "Prefab placed: " + activePrefab.Name;
            }
            catch (InvalidDataException ex) { status = ex.Message; }
        return true;
    }
    static void DrawGroundLoop(MapData map, IEnumerable<Vector2> vertices, Color color, bool close = true)
    {
        var points = vertices.ToArray(); if (points.Length < 2) return;
        for (int i = 0; i < points.Length - (close ? 0 : 1); i++)
        {
            var a = points[i]; var b = points[(i + 1) % points.Length];
            DrawLine3D(map.GroundPosition(a.X, a.Y) + Vector3.UnitY * .07f, map.GroundPosition(b.X, b.Y) + Vector3.UnitY * .07f, color);
        }
    }
    static void DrawColliderOutline(MapData map, SceneObject entity, Color color)
    {
        var polygon = CollisionGeometry.Vertices(entity);
        if (polygon.Length == 0)
        {
            float radius = CollisionGeometry.Radius(entity);
            polygon = Enumerable.Range(0, 32).Select(i => new Vector2(entity.X + MathF.Cos(i * MathF.Tau / 32) * radius,
                entity.Z + MathF.Sin(i * MathF.Tau / 32) * radius)).ToArray();
        }
        DrawGroundLoop(map, polygon, color);
    }
    void DrawWorldGizmos(Camera3D view)
    {
        if (worldGizmos)
        {
            foreach (var trigger in document.Map.Triggers)
                DrawGroundLoop(document.Map, new Vector2[] { new(trigger.X - trigger.Width / 2, trigger.Z - trigger.Depth / 2),
                    new(trigger.X + trigger.Width / 2, trigger.Z - trigger.Depth / 2), new(trigger.X + trigger.Width / 2, trigger.Z + trigger.Depth / 2),
                    new(trigger.X - trigger.Width / 2, trigger.Z + trigger.Depth / 2) }, trigger.Enabled ? new(190, 140, 255, 255) : Muted);
            foreach (var light in document.Map.Lights)
            {
                var ground = document.Map.GroundPosition(light.X, light.Z); var point = ground + Vector3.UnitY * light.Height;
                DrawSphereWires(point, .16f, 4, 8, new(light.R, light.G, light.B, (byte)255)); DrawLine3D(ground, point, new(light.R, light.G, light.B, (byte)150));
            }
            foreach (var sound in document.Map.Sounds)
                DrawSphereWires(document.Map.GroundPosition(sound.X, sound.Z) + Vector3.UnitY * .3f, .22f, 4, 8, new(100, 190, 255, 255));
        }
        foreach (var selected in SelectionGroup) DrawColliderOutline(document.Map, selected, CollisionGeometry.Solid(selected) ? Accent : Muted);
        if (triggerStart is { } start && triggerEnd is { } end)
            DrawGroundLoop(document.Map, new Vector2[] { new(start.X, start.Z), new(end.X, start.Z), new(end.X, end.Z), new(start.X, end.Z) }, Accent);
        if (drawingColliderId != null && document.Map.SceneEntities.FirstOrDefault(e => e.Id == drawingColliderId) is { } entity)
        {
            var points = colliderPoints.Select(p => new Vector2(entity.X + p.X, entity.Z + p.Z)).ToList();
            if (InViewport && MouseTerrainPoint is { } cursor) points.Add(new(cursor.X, cursor.Z));
            DrawGroundLoop(document.Map, points, Accent, false);
            foreach (var point in colliderPoints) DrawCube(document.Map.GroundPosition(entity.X + point.X, entity.Z + point.Z) + Vector3.UnitY * .1f, .12f, .12f, .12f, Accent);
        }
        if (tool == EditTool.Place && activePrefab != null && InViewport && !ModalOpen && MouseTerrainPoint is { } hit)
        {
            var point = Snap(hit);
            foreach (var item in activePrefab.Entities)
                renderer.DrawSprite(view, item.Asset, document.Map.GroundPosition(point.X + item.X, point.Z + item.Z), item.Height, Accent, 120);
        }
    }
}
