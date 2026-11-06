using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    Vector2? marqueeStart;
    Vector2 marqueeEnd;
    bool marqueeAdditive;
    Rectangle MarqueeBounds => new(Math.Min(marqueeStart!.Value.X, marqueeEnd.X), Math.Min(marqueeStart.Value.Y, marqueeEnd.Y),
        Math.Abs(marqueeEnd.X - marqueeStart.Value.X), Math.Abs(marqueeEnd.Y - marqueeStart.Value.Y));
    void EndEditorGesture()
    {
        document.EndSculptStroke(); gesture = dragging = dragArmed = false;
        marqueeStart = null; EndCameraLook();
        fragmentDragStart = null; roadDraggedPoint = -1;
    }
    void BeginMarquee()
    {
        marqueeStart = marqueeEnd = GetMousePosition();
        marqueeAdditive = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);
        dragArmed = false;
        if (!marqueeAdditive) { selectedIds.Clear(); selectedId = null; }
        else if (selectedId != null) selectedIds.Add(selectedId);
    }
    bool UpdateMarquee()
    {
        if (marqueeStart == null) return false;
        var mouse = GetMousePosition();
        marqueeEnd = new(Math.Clamp(mouse.X, viewport.X, viewport.X + viewport.Width), Math.Clamp(mouse.Y, viewport.Y, viewport.Y + viewport.Height));
        if (IsMouseButtonDown(MouseButton.Left)) return true;
        if (Vector2.DistanceSquared(marqueeStart.Value, marqueeEnd) > 25)
        {
            var rect = MarqueeBounds;
            var view = camera.View;
            foreach (var entity in document.Map.SceneEntities)
            {
                var center = renderer.GetSpriteQuad(view, entity.Asset, document.Map.GroundPosition(entity), entity.Height).Center;
                if (Vector3.Dot(center - view.Position, view.Target - view.Position) <= 0) continue;
                var point = GetWorldToScreenEx(center, view, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
                if (CheckCollisionPointRec(point, rect)) selectedIds.Add(entity.Id);
            }
            selectedId = selectedIds.FirstOrDefault();
            status = $"Selected {SelectionGroup.Length} objects. Shift+drag on empty space adds to the selection.";
        }
        marqueeStart = null;
        return true;
    }
    void DrawMarquee()
    {
        if (marqueeStart == null) return;
        DrawRectangleRec(MarqueeBounds, new(104, 217, 175, 30));
        DrawRectangleLinesEx(MarqueeBounds, 1, Accent);
    }
    void TranslateGroup(Vector2 delta)
    {
        var group = SelectionGroup;
        if (group.Length == 0) return;
        delta.X = Math.Clamp(delta.X, -document.Map.Width / 2f + .01f - group.Min(e => e.X), document.Map.Width / 2f - .01f - group.Max(e => e.X));
        delta.Y = Math.Clamp(delta.Y, -document.Map.Depth / 2f + .01f - group.Min(e => e.Z), document.Map.Depth / 2f - .01f - group.Max(e => e.Z));
        if (delta == Vector2.Zero) return;
        document.Checkpoint();
        foreach (var entity in group) { entity.X += delta.X; entity.Z += delta.Y; }
    }
    void ScaleGroup(float factor)
    {
        var group = SelectionGroup; if (group.Length == 0) return;
        // Clamp the common factor so relative sizes remain intact.
        factor = Math.Clamp(factor, group.Max(e => SceneObject.MinHeight / e.Height), group.Min(e => SceneObject.MaxHeight / e.Height));
        if (factor == 1) return;
        document.Checkpoint();
        foreach (var entity in group) entity.Height *= factor;
    }
    void SetGroupCollision(bool solid)
    {
        var group = SelectionGroup; if (group.Length == 0 || group.All(e => e.Solid == solid)) return;
        document.Checkpoint(); foreach (var entity in group) entity.Solid = solid;
    }
    void DrawGroupInspector(int x, SceneObject[] group)
    {
        Label($"{group.Length} selected objects", x, 148, 16, Accent);
        float cx = group.Average(e => e.X), cz = group.Average(e => e.Z);
        Stepper("Group centre X", cx, x, 184, value => TranslateGroup(new(value - cx, 0)), .5f);
        Stepper("Group centre Z", cz, x, 234, value => TranslateGroup(new(0, value - cz)), .5f);
        Label("Relative size", x, 284, 12, Muted);
        if (Button("-10%", x, 301, 104)) ScaleGroup(.9f);
        if (Button("+10%", x + 114, 301, 104)) ScaleGroup(1.1f);
        bool allSolid = group.All(e => e.Solid), anySolid = group.Any(e => e.Solid);
        if (Button("Collision: " + (allSolid ? "ON" : anySolid ? "MIXED" : "OFF"), x, 340, 218, allSolid)) SetGroupCollision(!allSolid);
        if (Button("Duplicate", x, 385, 105)) Duplicate();
        if (Button("Delete", x + 113, 385, 105)) Delete();
        if (Button("Save as prefab", x, 423, 218)) OpenWorldTools("Prefabs");
    }
}
