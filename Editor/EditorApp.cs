using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

enum EditTool { Select, Place, Ground, Player }

sealed partial class EditorApp : IDisposable
{
    const int Left = 252, Right = 260, Top = 88, Bottom = 32;
    readonly WorldAssets assets = new();
    readonly WorldRenderer renderer;
    readonly MapDocument document;
    readonly string mapPath;
    readonly FollowCamera camera = new(Vector3.Zero);
    RenderTexture2D canvas;
    Vector3 focus;
    EditTool tool = EditTool.Select;
    string? selectedId;
    string chosenAsset = "", chosenTerrain = "grass";
    string? chosenCharacter;
    float placeHeight = 2;
    bool placeSolid = true, snap = true, grid = true, exit, closeRequested;
    int category = 1, scroll, brushRadius, groundScroll, characterScroll;
    bool gesture, dragging, dragArmed;
    Vector3 dragOffset, dragOrigin;
    Vector3 lastPaint;
    string status = "Ready. Choose an asset, then click the map.";
    string? pendingAction;
    PlaySession? play;
    Rectangle viewport;
    readonly string[] categories = ["all", "vegetation", "rocks", "props", "effects"];
    static readonly Color Background = new(17, 23, 31, 255), Panel = new(24, 32, 43, 255),
        Card = new(33, 44, 57, 255), Border = new(49, 64, 79, 255), Text = new(222, 233, 239, 255),
        Muted = new(140, 160, 176, 255), Accent = new(104, 217, 175, 255);
    SceneObject? Selection => document.Map.Objects.FirstOrDefault(o => o.Id == selectedId);

    public EditorApp(string mapPath)
    {
        this.mapPath = mapPath;
        renderer = new(assets);
        MapData map;
        bool loaded = false;
        try
        {
            loaded = File.Exists(mapPath);
            map = loaded ? MapData.Load(mapPath) : MapData.CreateDemo(assets.Sprites.Keys);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            map = MapData.CreateDemo(assets.Sprites.Keys); loaded = false;
            status = "Map could not be loaded: " + ex.Message;
        }
        document = new(map, loaded);
        var first = assets.Sprites.Values.FirstOrDefault(a => a.Id.Contains("/trees/")) ?? assets.Sprites.Values.FirstOrDefault();
        if (first != null) Choose(first);
        tool = EditTool.Select;
        chosenCharacter = map.Player.Asset;
        focus = map.Player.Position;
        camera.Snap(focus);
        int missing = map.Objects.Count(o => !assets.Sprites.ContainsKey(o.Asset)) + map.Tiles.Distinct().Count(t => !assets.Terrain.ContainsKey(t)) +
            (map.Player.Asset != null && !assets.Sprites.ContainsKey(map.Player.Asset) ? 1 : 0);
        if (missing > 0) status = $"Missing assets: {missing}. Placeholders are shown; original references are preserved.";
        if (assets.Warnings.Count > 0) status = $"Could not load {assets.Warnings.Count} images. Other assets are available.";
    }
    public void Run(bool smoke)
    {
        int frames = 0;
        while (!exit)
        {
            if (WindowShouldClose() && !closeRequested)
            {
                closeRequested = true;
                Request("exit");
            }
            ResizeCanvas();
            float dt = Math.Min(GetFrameTime(), .05f);
            if (pendingAction == null) Update(dt);
            Render();
            if (smoke && ++frames == 12) WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "editor-smoke.png"));
            if (smoke && frames >= 15) break;
        }
    }
    void ResizeCanvas()
    {
        viewport = new(Left, Top, GetScreenWidth() - Left - Right, GetScreenHeight() - Top - Bottom);
        if (canvas.Texture.Width == (int)viewport.Width && canvas.Texture.Height == (int)viewport.Height) return;
        if (canvas.Id != 0) UnloadRenderTexture(canvas);
        canvas = LoadRenderTexture((int)viewport.Width, (int)viewport.Height);
        SetTextureFilter(canvas.Texture, TextureFilter.Bilinear);
    }
    bool InViewport => CheckCollisionPointRec(GetMousePosition(), viewport);
    Ray MouseRay => GetScreenToWorldRayEx(GetMousePosition() - new Vector2(viewport.X, viewport.Y), camera.View, (int)viewport.Width, (int)viewport.Height);
    Vector3 Snap(Vector3 p)
    {
        if (snap) { p.X = MathF.Round(p.X * 2) / 2; p.Z = MathF.Round(p.Z * 2) / 2; }
        return document.Map.Clamp(p);
    }
    void Update(float dt)
    {
        if (IsKeyPressed(KeyboardKey.F5)) TogglePlay();
        if (play != null)
        {
            if (IsKeyPressed(KeyboardKey.Escape)) { play = null; return; }
            play.Update(dt, InViewport ? GetMouseWheelMove() : 0);
            return;
        }
        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        if (ctrl && IsKeyPressed(KeyboardKey.S)) Save();
        if (ctrl && IsKeyPressed(KeyboardKey.O)) Request("load");
        if (ctrl && IsKeyPressed(KeyboardKey.N)) Request("new");
        if (ctrl && IsKeyPressed(KeyboardKey.Z)) Undo();
        if (ctrl && IsKeyPressed(KeyboardKey.Y)) Redo();
        if (ctrl && IsKeyPressed(KeyboardKey.D)) Duplicate();
        if (IsKeyPressed(KeyboardKey.Delete)) Delete();
        if (IsKeyPressed(KeyboardKey.One)) tool = EditTool.Select;
        if (IsKeyPressed(KeyboardKey.Two)) tool = EditTool.Place;
        if (IsKeyPressed(KeyboardKey.Three)) tool = EditTool.Ground;
        if (IsKeyPressed(KeyboardKey.Four)) tool = EditTool.Player;
        if (IsKeyPressed(KeyboardKey.G)) grid = !grid;
        if (IsKeyPressed(KeyboardKey.F)) { focus = Selection?.Position ?? document.Map.Player.Position; camera.Snap(focus); }
        if (IsKeyPressed(KeyboardKey.Escape)) { tool = EditTool.Select; selectedId = null; }
        if (!ctrl)
        {
            float x = (IsKeyDown(KeyboardKey.D) ? 1 : 0) - (IsKeyDown(KeyboardKey.A) ? 1 : 0);
            float z = (IsKeyDown(KeyboardKey.S) ? 1 : 0) - (IsKeyDown(KeyboardKey.W) ? 1 : 0);
            focus += (camera.Right * x + camera.Backward * z) * dt * 12;
        }
        if (InViewport)
        {
            camera.Zoom(GetMouseWheelMove());
            if (IsMouseButtonDown(MouseButton.Middle) || IsMouseButtonDown(MouseButton.Right))
            {
                var delta = GetMouseDelta();
                focus -= (camera.Right * delta.X + camera.Backward * delta.Y * 1.4f) * camera.Distance / viewport.Height;
            }
        }
        focus = document.Map.Clamp(focus);
        camera.Update(focus, dt);
        if (!IsMouseButtonDown(MouseButton.Left)) { gesture = false; dragging = false; dragArmed = false; }
        if (!InViewport) return;
        var ground = WorldRenderer.GroundPoint(MouseRay);
        if (IsMouseButtonPressed(MouseButton.Left) && tool == EditTool.Select)
        {
            var hit = renderer.Pick(document.Map, camera.View, MouseRay);
            selectedId = hit?.Id;
            dragArmed = hit != null;
            if (hit != null && ground.HasValue)
            {
                dragOrigin = ground.Value;
                dragOffset = hit.Position - ground.Value;
            }
        }
        if (!ground.HasValue || !document.Map.Contains(ground.Value.X, ground.Value.Z)) return;
        Vector3 point = Snap(ground.Value);
        if (tool == EditTool.Select && dragArmed && IsMouseButtonDown(MouseButton.Left) && Selection is { } selected &&
            (dragging || Vector3.Distance(ground.Value, dragOrigin) > .12f))
        {
            if (!dragging) { document.Checkpoint(); dragging = true; }
            point = Snap(ground.Value + dragOffset);
            selected.X = point.X; selected.Z = point.Z;
        }
        if (tool == EditTool.Place && IsMouseButtonPressed(MouseButton.Left) && chosenAsset.Length > 0)
        {
            selectedId = document.Place(chosenAsset, point, placeHeight, placeSolid).Id;
            status = "Object placed. Switch to Select (1) to move it.";
        }
        if (tool == EditTool.Player && IsMouseButtonPressed(MouseButton.Left))
        {
            document.Checkpoint();
            document.Map.Player.X = point.X; document.Map.Player.Z = point.Z;
            document.Map.Player.Asset = chosenCharacter;
            status = "Player start updated.";
        }
        if (tool == EditTool.Ground && IsMouseButtonDown(MouseButton.Left) && (gesture || IsMouseButtonPressed(MouseButton.Left)))
        {
            if (!gesture) { document.Checkpoint(); gesture = true; lastPaint = ground.Value; }
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(lastPaint, ground.Value) * 2));
            for (int step = 1; step <= steps; step++)
                document.Paint(Vector3.Lerp(lastPaint, ground.Value, step / (float)steps), brushRadius, chosenTerrain);
            lastPaint = ground.Value;
        }
    }
    void Render()
    {
        var view = play?.Camera.View ?? camera.View;
        BeginTextureMode(canvas);
        ClearBackground(new Color(38, 47, 54, 255));
        BeginMode3D(view);
        renderer.Draw(play?.Map ?? document.Map, view, grid, play?.Player);
        if (play == null)
        {
            if (Selection is { } selected)
                renderer.DrawOutline(view, selected, Accent);
            DrawCubeWires(document.Map.Player.Position + new Vector3(0, .05f, 0), .8f, .1f, .8f, new Color(80, 183, 255, 255));
            if (InViewport && pendingAction == null && WorldRenderer.GroundPoint(MouseRay) is { } ground && document.Map.Contains(ground.X, ground.Z))
            {
                var point = Snap(ground);
                if (tool == EditTool.Place && chosenAsset.Length > 0)
                    renderer.DrawSprite(view, chosenAsset, point, placeHeight, Accent, 130);
                if (tool == EditTool.Player)
                    renderer.DrawSprite(view, chosenCharacter ?? "", point, document.Map.Player.Height, new Color(80, 183, 255, 130), 130);
                if (tool == EditTool.Ground)
                {
                    float x = MathF.Floor(ground.X + document.Map.Width / 2f) - document.Map.Width / 2f + .5f;
                    float z = MathF.Floor(ground.Z + document.Map.Depth / 2f) - document.Map.Depth / 2f + .5f;
                    DrawCubeWires(new(x, .04f, z), brushRadius * 2 + 1, .08f, brushRadius * 2 + 1, Accent);
                }
            }
        }
        EndMode3D();
        EndTextureMode();
        BeginDrawing();
        ClearBackground(Background);
        DrawTexturePro(canvas.Texture, new(0, 0, canvas.Texture.Width, -canvas.Texture.Height), viewport, Vector2.Zero, 0, Color.White);
        DrawHeader();
        DrawPalette();
        DrawInspector();
        DrawRectangle(0, GetScreenHeight() - Bottom, GetScreenWidth(), Bottom, Panel);
        Label(status, 16, GetScreenHeight() - 22, 13, Muted, GetScreenWidth() - 260);
        Label($"{document.Map.Objects.Count} objects  |  {document.Map.Width} x {document.Map.Depth}", GetScreenWidth() - 235, GetScreenHeight() - 22, 13, Accent);
        DrawRectangle(Left + 12, Top + 12, 154, 25, new Color(17, 23, 31, 220));
        Label(play != null ? "PLAYTEST  /  F5 to stop" : "PERSPECTIVE  /  2.5D", Left + 20, Top + 19, 12, play != null ? Accent : Text);
        if (pendingAction != null) DrawConfirmation();
        EndDrawing();
    }
    void DrawHeader()
    {
        DrawRectangle(0, 0, GetScreenWidth(), Top, Panel);
        Label("WILDWOOD", 18, 15, 19, Accent);
        Label("MAP EDITOR", 152, 18, 12, Muted);
        Label(Path.GetFileName(mapPath) + (document.Dirty ? "  /  unsaved" : "  /  saved"), Left + 14, 17, 14, Text, GetScreenWidth() - Left - 300);
        Label($"{document.Map.Width}m workspace  /  fixed camera", GetScreenWidth() - 264, 18, 12, Muted);
        int x = 16;
        foreach (var t in Enum.GetValues<EditTool>())
        {
            if (Button($"{(int)t + 1}  {t}", x, 46, 96, tool == t, play == null)) tool = t;
            x += 102;
        }
        if (Button("Undo", x + 8, 46, 65, false, play == null && document.CanUndo)) Undo();
        if (Button("Redo", x + 79, 46, 65, false, play == null && document.CanRedo)) Redo();
        if (Button("Save", x + 164, 46, 70, false, play == null)) Save();
        if (Button("Load", x + 240, 46, 70, false, play == null)) Request("load");
        if (Button("New", x + 316, 46, 65, false, play == null)) Request("new");
        if (Button(play == null ? "Play  F5" : "Stop  F5", GetScreenWidth() - 128, 46, 110, true)) TogglePlay();
        DrawLine(0, Top - 1, GetScreenWidth(), Top - 1, Border);
    }
    void DrawPalette()
    {
        DrawRectangle(0, Top, Left, GetScreenHeight() - Top - Bottom, Panel);
        Label(tool == EditTool.Ground ? "GROUND MATERIALS" : tool == EditTool.Player ? "PLAYER" : "ASSET LIBRARY", 16, 108, 15, Text);
        if (tool == EditTool.Player)
        {
            if (Button("Blue placeholder", 16, 142, 218, chosenCharacter == null, play == null))
                ChangeCharacter(null);
            Label("Click the map to set the spawn.", 16, 187, 13, Muted);
            Label("Add PNG files to", 16, 212, 13, Muted);
            Label("Editor/characters", 16, 231, 13, Accent);
            var characters = assets.Sprites.Values.Where(s => s.Id.StartsWith("characters/")).ToArray();
            ScrollPalette(ref characterScroll, 264, characters.Length * 36);
            int cy = 268 - characterScroll;
            BeginScissorMode(0, 264, Left, GetScreenHeight() - Bottom - 264);
            foreach (var sprite in characters)
            {
                if (Button(sprite.Name, 16, cy, 218, chosenCharacter == sprite.Id,
                    play == null && GetMouseY() >= 264 && GetMouseY() < GetScreenHeight() - Bottom)) ChangeCharacter(sprite.Id);
                cy += 36;
            }
            EndScissorMode();
            return;
        }
        if (tool == EditTool.Ground)
        {
            Label("Hold LMB to paint tiles", 16, 138, 13, Muted);
            ScrollPalette(ref groundScroll, 164, (assets.Terrain.Count + 1) / 2 * 120);
            BeginScissorMode(0, 164, Left, GetScreenHeight() - Bottom - 164);
            int index = 0;
            foreach (var (id, texture) in assets.Terrain)
            {
                int x = 16 + index % 2 * 116, y = 168 + index / 2 * 120 - groundScroll;
                if (AssetCard(texture, Path.GetFileNameWithoutExtension(id), x, y, chosenTerrain == id,
                    play == null && GetMouseY() >= 164 && GetMouseY() < GetScreenHeight() - Bottom)) chosenTerrain = id;
                index++;
            }
            EndScissorMode();
            return;
        }
        if (Button("<", 16, 136, 28, false, play == null)) { category = (category + categories.Length - 1) % categories.Length; scroll = 0; }
        Label(categories[category].ToUpperInvariant(), 60, 145, 14, Accent);
        if (Button(">", 206, 136, 28, false, play == null)) { category = (category + 1) % categories.Length; scroll = 0; }
        var items = assets.Sprites.Values.Where(s => s.Id.StartsWith("objects/") && (category == 0 || s.Category == categories[category]))
            .OrderBy(s => s.Id.Contains("/trees/") ? 0 : 1).ThenBy(s => s.Id).ToArray();
        int available = GetScreenHeight() - Bottom - 188;
        if (GetMouseX() < Left && GetMouseY() > 174 && pendingAction == null)
            scroll = Math.Clamp(scroll - (int)(GetMouseWheelMove() * 45), 0, Math.Max(0, (items.Length + 1) / 2 * 120 - available));
        scroll = Math.Clamp(scroll, 0, Math.Max(0, (items.Length + 1) / 2 * 120 - available));
        BeginScissorMode(0, 176, Left, available);
        for (int i = 0; i < items.Length; i++)
        {
            int x = 16 + i % 2 * 116, y = 184 + i / 2 * 120 - scroll;
            if (y + 112 < 176 || y > GetScreenHeight() - Bottom) continue;
            if (AssetCard(items[i].Texture, items[i].Name, x, y, chosenAsset == items[i].Id,
                play == null && GetMouseY() >= 176 && GetMouseY() < GetScreenHeight() - Bottom)) Choose(items[i]);
        }
        EndScissorMode();
        DrawLine(Left - 1, Top, Left - 1, GetScreenHeight() - Bottom, Border);
    }
    void DrawInspector()
    {
        int x = GetScreenWidth() - Right + 18;
        DrawRectangle(GetScreenWidth() - Right, Top, Right, GetScreenHeight() - Top - Bottom, Panel);
        Label(play != null ? "PLAYTEST" : "PROPERTIES", x, 108, 15, Text);
        if (play != null)
        {
            Label("WASD / arrows: move", x, 150, 14, Text);
            Label("Shift: run", x, 178, 14, Text);
            Label("Wheel: zoom", x, 206, 14, Text);
            Label("F5 / Escape: back to editor", x, 250, 13, Accent);
            Label("Testing a copy of this map.", x, 291, 13, Muted);
            return;
        }
        if (tool == EditTool.Ground)
        {
            Label("Material: " + Path.GetFileNameWithoutExtension(chosenTerrain), x, 147, 14, Accent, 225);
            Label("Brush size", x, 185, 13, Muted);
            if (Button("-", x, 210, 34)) brushRadius = Math.Max(0, brushRadius - 1);
            Label($"{brushRadius * 2 + 1} x {brushRadius * 2 + 1} tiles", x + 47, 220, 14, Text);
            if (Button("+", x + 184, 210, 34)) brushRadius = Math.Min(3, brushRadius + 1);
        }
        else if (tool == EditTool.Player)
        {
            Label("Player start", x, 147, 15, Accent);
            Label($"X  {document.Map.Player.X:0.0}     Z  {document.Map.Player.Z:0.0}", x, 180, 14, Text);
            Stepper("Height", document.Map.Player.Height, x, 216, v => { document.Checkpoint(); document.Map.Player.Height = Math.Clamp(v, .25f, 12); }, .25f);
            if (!WorldRenderer.CanWalk(document.Map, document.Map.Player.Position)) Label("Spawn overlaps a solid object", x, 272, 12, new Color(255, 177, 100, 255));
        }
        else if (tool == EditTool.Place)
        {
            Label(Path.GetFileNameWithoutExtension(chosenAsset), x, 148, 16, Accent, 220);
            Stepper("Height", placeHeight, x, 190, v => placeHeight = Math.Clamp(v, .25f, 12), .25f);
            if (Button(placeSolid ? "Collision: ON" : "Collision: OFF", x, 248, 218, placeSolid)) placeSolid = !placeSolid;
            Label("Click to place. Repeat freely.", x, 294, 13, Muted);
        }
        else if (Selection is { } selected)
        {
            Label(Path.GetFileNameWithoutExtension(selected.Asset), x, 148, 16, Accent, 220);
            Stepper("X", selected.X, x, 184, v => ChangePosition(selected, v, selected.Z), .5f);
            Stepper("Z", selected.Z, x, 234, v => ChangePosition(selected, selected.X, v), .5f);
            Stepper("Height", selected.Height, x, 284, v => { document.Checkpoint(); selected.Height = Math.Clamp(v, .25f, 12); }, .25f);
            if (Button(selected.Solid ? "Collision: ON" : "Collision: OFF", x, 340, 218, selected.Solid)) { document.Checkpoint(); selected.Solid = !selected.Solid; }
            if (Button("Duplicate", x, 385, 105)) Duplicate();
            if (Button("Delete", x + 113, 385, 105)) Delete();
        }
        else
        {
            Label("Nothing selected", x, 150, 16, Muted);
            Label("Click an object to select it.", x, 183, 13, Muted);
            Label("Drag to change its position.", x, 205, 13, Muted);
        }
        int y = Math.Max(454, GetScreenHeight() - 252);
        DrawLine(x, y, x + 220, y, Border);
        if (Button(grid ? "Grid: ON" : "Grid: OFF", x, y + 16, 104, grid)) grid = !grid;
        if (Button(snap ? "Snap: 0.5" : "Snap: OFF", x + 112, y + 16, 106, snap)) snap = !snap;
        Label("NAVIGATION", x, y + 68, 12, Accent);
        Label("WASD / RMB drag: pan", x, y + 92, 13, Muted);
        Label("Wheel: zoom   F: focus", x, y + 114, 13, Muted);
        Label("Ctrl+S: save   Ctrl+O: load", x, y + 136, 13, Muted);
        Label("Ctrl+Z / Ctrl+Y: undo / redo", x, y + 158, 13, Muted);
        Label("Delete: remove   G: grid", x, y + 180, 13, Muted);
    }
    void ChangePosition(SceneObject item, float x, float z)
    {
        document.Checkpoint(); var p = document.Map.Clamp(new(x, 0, z)); item.X = p.X; item.Z = p.Z;
    }
    void ScrollPalette(ref int offset, int top, int contentHeight)
    {
        int maximum = Math.Max(0, contentHeight - (GetScreenHeight() - Bottom - top));
        if (GetMouseX() < Left && GetMouseY() >= top && GetMouseY() < GetScreenHeight() - Bottom && pendingAction == null && play == null)
            offset -= (int)(GetMouseWheelMove() * 45);
        offset = Math.Clamp(offset, 0, maximum);
    }
    void ChangeCharacter(string? id)
    {
        document.Checkpoint(); chosenCharacter = id; document.Map.Player.Asset = id;
    }
    void Stepper(string label, float value, int x, int y, Action<float> change, float step)
    {
        Label(label, x, y, 12, Muted);
        if (Button("-", x, y + 17, 34)) change(value - step);
        Label(value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), x + 70, y + 26, 14, Text);
        if (Button("+", x + 184, y + 17, 34)) change(value + step);
    }
    void Choose(SpriteAsset asset)
    {
        chosenAsset = asset.Id; placeHeight = asset.DefaultHeight; placeSolid = asset.DefaultSolid; tool = EditTool.Place;
    }
    void Delete()
    {
        if (Selection is not { } selected) return;
        document.Checkpoint(); document.Map.Objects.Remove(selected); selectedId = null;
    }
    void Duplicate()
    {
        if (Selection is not { } selected) return;
        selectedId = document.Place(selected.Asset, selected.Position + new Vector3(1, 0, 1), selected.Height, selected.Solid).Id;
    }
    bool Save()
    {
        try { document.Save(mapPath); status = "Saved: " + Path.GetRelativePath(ProjectPaths.Root, mapPath); return true; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { status = "Save failed: " + ex.Message; return false; }
    }
    void Request(string action)
    {
        if (document.Dirty) pendingAction = action;
        else Complete(action);
    }
    void Complete(string action)
    {
        if (action == "exit") { exit = true; return; }
        if (action == "new")
        {
            document.New(); selectedId = null; chosenCharacter = null; focus = Vector3.Zero;
            camera.Snap(focus); status = "New map. Choose objects and paint the ground.";
            return;
        }
        try
        {
            document.Load(mapPath); selectedId = null; chosenCharacter = document.Map.Player.Asset;
            focus = document.Map.Player.Position; camera.Snap(focus); status = "Map loaded.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException) { status = "Load failed: " + ex.Message; }
    }
    void Undo() { document.Undo(); chosenCharacter = document.Map.Player.Asset; gesture = dragging = dragArmed = false; }
    void Redo() { document.Redo(); chosenCharacter = document.Map.Player.Asset; gesture = dragging = dragArmed = false; }
    void TogglePlay()
    {
        gesture = dragging = false;
        if (play != null) { play = null; status = "Returned to editing."; }
        else { play = new(document.Map); status = "Playtest: WASD to move, F5 or Escape to return."; }
    }
    void DrawConfirmation()
    {
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new Color(0, 0, 0, 170));
        int x = GetScreenWidth() / 2 - 230, y = GetScreenHeight() / 2 - 90;
        DrawRectangle(x, y, 460, 180, Panel);
        DrawRectangleLines(x, y, 460, 180, Border);
        Label("Unsaved changes", x + 24, y + 24, 23, Text);
        Label("Save this map before continuing?", x + 24, y + 66, 16, Muted);
        if (Button("Save", x + 24, y + 118, 122, true, true, true) && Save())
        { var action = pendingAction!; pendingAction = null; Complete(action); }
        if (pendingAction != null && Button("Discard", x + 168, y + 118, 122, false, true, true))
        { var action = pendingAction; pendingAction = null; Complete(action); }
        if (pendingAction != null && Button("Cancel", x + 312, y + 118, 122, false, true, true))
        { pendingAction = null; closeRequested = false; }
    }
    bool Button(string text, int x, int y, int width, bool active = false, bool enabled = true, bool modal = false)
    {
        var rect = new Rectangle(x, y, width, 30);
        bool hover = enabled && CheckCollisionPointRec(GetMousePosition(), rect) && (pendingAction == null || modal);
        DrawRectangleRec(rect, active ? new Color(41, 78, 70, 255) : hover ? new Color(48, 63, 77, 255) : Card);
        DrawRectangleLinesEx(rect, 1, active ? Accent : Border);
        Label(text, x + (width - MeasureText(text, 13)) / 2, y + 9, 13, enabled ? active ? Accent : Text : Muted);
        return hover && IsMouseButtonPressed(MouseButton.Left);
    }
    bool AssetCard(Texture2D texture, string name, int x, int y, bool active, bool enabled)
    {
        var rect = new Rectangle(x, y, 102, 110);
        bool hover = enabled && pendingAction == null && CheckCollisionPointRec(GetMousePosition(), rect);
        DrawRectangleRec(rect, hover ? new Color(47, 60, 72, 255) : Card);
        DrawRectangleLinesEx(rect, 1, active ? Accent : Border);
        float scale = Math.Min(84f / texture.Width, 78f / texture.Height);
        DrawTexturePro(texture, new(0, 0, texture.Width, texture.Height),
            new(x + 51 - texture.Width * scale / 2, y + 6 + (78 - texture.Height * scale) / 2, texture.Width * scale, texture.Height * scale), Vector2.Zero, 0, Color.White);
        Label(name, x + 7, y + 91, 12, active ? Accent : Text, 90);
        return hover && IsMouseButtonPressed(MouseButton.Left);
    }
    static void Label(string text, int x, int y, int size, Color color, int maxWidth = 0)
    {
        if (maxWidth > 0 && MeasureText(text, size) > maxWidth)
        {
            while (text.Length > 0 && MeasureText(text + "...", size) > maxWidth) text = text[..^1];
            text += "...";
        }
        DrawText(text, x, y, size, color);
    }
    public void Dispose()
    {
        if (canvas.Id != 0) UnloadRenderTexture(canvas);
        assets.Dispose();
    }
}
