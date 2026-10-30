using System.Globalization;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    string? worldPage, worldSelectedId, drawingColliderId;
    bool drawingTrigger, worldGizmos = true;
    Vector3? triggerStart, triggerEnd;
    readonly List<GroundPoint2> colliderPoints = [];
    readonly HashSet<string> selectedIds = [];
    Dictionary<string, Vector2> groupDragOrigins = [];
    readonly List<(string Path, ScenePrefab Prefab)> prefabs = [];
    string prefabDirectory = Path.Combine(ProjectPaths.AssetRoot, "prefabs");
    ScenePrefab? activePrefab;
    int worldListScroll;
    readonly string[] worldPages = ["Environment", "Lights", "Sounds", "Triggers", "Prefabs", "Collision"];
    Rectangle WorldToolsBounds => new(GetScreenWidth() / 2 - 420, GetScreenHeight() / 2 - 300, 840, 600);
    bool WorldControlsEnabled => textEdit == null && pendingAction == null;
    SceneObject[] SelectionGroup
    {
        get
        {
            if (selectedId == null || !selectedIds.Contains(selectedId)) selectedIds.Clear();
            return document.Map.SceneEntities.Where(o => selectedIds.Contains(o.Id) || o.Id == selectedId).ToArray();
        }
    }
    void OpenWorldTools(string page = "Environment")
    {
        worldPage = page; worldSelectedId = null; worldListScroll = 0;
        document.EndSculptStroke(); gesture = dragging = dragArmed = false;
        if (page == "Prefabs") ReloadPrefabs();
    }
    void ReloadPrefabs()
    {
        prefabs.Clear(); if (!Directory.Exists(prefabDirectory)) return;
        foreach (var file in Directory.EnumerateFiles(prefabDirectory, "*.json").Order().Take(512))
            try { prefabs.Add((file, ScenePrefab.Load(file))); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
            { status = "Cannot load prefab: " + Path.GetFileName(file) + ": " + ex.Message; }
    }
    void UpdateWorldTools()
    {
        if (IsKeyPressed(KeyboardKey.Escape)) { worldPage = null; return; }
        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        if (ctrl && IsKeyPressed(KeyboardKey.Z)) Undo();
        if (ctrl && IsKeyPressed(KeyboardKey.Y)) Redo();
        if (ctrl && IsKeyPressed(KeyboardKey.S)) Save();
    }
    bool WorldButton(string text, int x, int y, int width, bool active = false, bool enabled = true) =>
        Button(text, x, y, width, active, enabled && WorldControlsEnabled, true);
    void WorldToggle(string name, bool value, int x, int y, int width, Action<bool> apply, bool enabled = true)
    {
        if (WorldButton(name + (value ? ": ON" : ": OFF"), x, y, width, value, enabled)) { document.Checkpoint(); apply(!value); }
    }
    void WorldNumber(string label, float value, int x, int y, float step, float min, float max, Action<float> apply, bool enabled = true)
    {
        Label(label, x, y + 9, 13, Muted, 112);
        void Change(float delta)
        {
            float next = Math.Clamp(MathF.Round(value + delta, 3), min, max);
            if (next == value) return; document.Checkpoint(); apply(next);
        }
        if (WorldButton("-", x + 116, y, 28, false, enabled && value > min)) Change(-step);
        Label(value.ToString("0.##", CultureInfo.InvariantCulture), x + 151, y + 9, 13, Text, 64);
        if (WorldButton("+", x + 220, y, 28, false, enabled && value < max)) Change(step);
    }
    void WorldName(string title, string value, int x, int y, int width, Action<string> apply, int limit = 80)
    {
        if (WorldButton(title + ": " + (value.Length > 45 ? value[..42] + "..." : value), x, y, width))
            BeginTextEdit(title, value, limit, false, next =>
            { if (string.IsNullOrWhiteSpace(next)) return; document.Checkpoint(); apply(next.Trim()); });
    }
    static string EntityName(SceneObject entity) => entity switch
    { NpcData npc => npc.Name, ChestData chest => chest.Name, MonsterData monster => monster.Name, _ => Path.GetFileNameWithoutExtension(entity.Asset) };
    void WorldList((string Id, string Name)[] entries, int x, int y)
    {
        if (worldSelectedId == null || !entries.Any(e => e.Id == worldSelectedId)) worldSelectedId = entries.FirstOrDefault().Id;
        var area = new Rectangle(x, y, 224, 318);
        int maximum = Math.Max(0, entries.Length * 36 - 318);
        if (CheckCollisionPointRec(GetMousePosition(), area) && WorldControlsEnabled)
            worldListScroll = Math.Clamp(worldListScroll - (int)(GetMouseWheelMove() * 36), 0, maximum);
        worldListScroll = Math.Clamp(worldListScroll, 0, maximum);
        BeginScissorMode(x, y, 224, 318);
        for (int i = 0; i < entries.Length; i++)
        {
            int row = y + i * 36 - worldListScroll;
            if (row + 30 < y || row >= y + 318) continue;
            if (WorldButton(entries[i].Name.Length > 27 ? entries[i].Name[..24] + "..." : entries[i].Name, x, row, 224, entries[i].Id == worldSelectedId,
                GetMouseY() >= y && GetMouseY() < y + 318)) worldSelectedId = entries[i].Id;
        }
        EndScissorMode();
    }
    void DrawWorldTools()
    {
        var box = WorldToolsBounds; int x = (int)box.X, y = (int)box.Y;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 180)); DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        Label("WORLD TOOLS", x + 24, y + 20, 23, Accent);
        Label("Changes belong to the map. Ctrl+Z / Ctrl+Y undo and redo.", x + 24, y + 56, 14, Muted);
        for (int i = 0; i < worldPages.Length; i++)
            if (WorldButton(worldPages[i], x + 24 + i * 132, y + 86, 128, worldPage == worldPages[i]))
            { worldPage = worldPages[i]; worldSelectedId = null; worldListScroll = 0; if (worldPage == "Prefabs") ReloadPrefabs(); }
        switch (worldPage)
        {
            case "Environment": DrawEnvironmentTools(x, y); break;
            case "Lights": DrawLightTools(x, y); break;
            case "Sounds": DrawSoundTools(x, y); break;
            case "Triggers": DrawTriggerTools(x, y); break;
            case "Prefabs": DrawPrefabTools(x, y); break;
            case "Collision": DrawColliderTools(x, y); break;
        }
        DrawLine(x + 24, y + 546, x + 816, y + 546, Border);
        if (WorldButton("Undo", x + 24, y + 558, 90, false, document.CanUndo)) Undo();
        if (WorldButton("Redo", x + 122, y + 558, 90, false, document.CanRedo)) Redo();
        if (WorldButton("Save map", x + 556, y + 558, 124)) Save();
        if (WorldButton("Close / Esc", x + 692, y + 558, 124, true)) worldPage = null;
    }
    void DrawEnvironmentTools(int x, int y)
    {
        var environment = document.Map.Environment;
        bool interior = document.Map.AlwaysDark;
        int a = x + 24, b = x + 444;
        WorldToggle("Lighting", WorldLighting.Enabled(document.Map), a, y + 140, 330, v => environment.Lighting = v, !interior);
        WorldToggle("Contact shadows", environment.Shadows, a, y + 182, 330, v => environment.Shadows = v);
        WorldToggle("Fade blocking scenery", environment.FadeOccluders, a, y + 224, 330, v => environment.FadeOccluders = v);
        WorldNumber("Fade opacity", environment.OccluderOpacity, a, y + 266, .05f, .05f, .8f, v => environment.OccluderOpacity = v);
        WorldToggle("Day / night cycle", !interior && environment.DayNightCycle, a, y + 308, 330, v => environment.DayNightCycle = v, !interior);
        WorldNumber("Starting hour", interior ? 0 : environment.TimeOfDay, a, y + 350, .5f, 0, 24, v => environment.TimeOfDay = v, !interior);
        WorldNumber("Day length (s)", environment.DayLengthSeconds, a, y + 392, 10, 10, 86400, v => environment.DayLengthSeconds = v, !interior);
        WorldToggle("Spatial audio", environment.Audio, b, y + 140, 330, v => environment.Audio = v);
        WorldNumber("Master volume", environment.MasterVolume, b, y + 182, .05f, 0, 1, v => environment.MasterVolume = v);
        if (WorldButton("World gizmos" + (worldGizmos ? ": ON" : ": OFF"), b, y + 224, 330, worldGizmos)) worldGizmos = !worldGizmos;
        Label("Fire and water ambience is automatic.", b, y + 284, 14, Text);
        Label("Add custom WAV / OGG / MP3 in Editor/audio.", b, y + 310, 13, Muted);
        Label("F6 saves progress / F9 loads during Play.", b, y + 352, 14, Text);
        Label("Editor preview has its own progress slot.", b, y + 378, 13, Muted);
        Label("Lighting affects ground, sprites and water.", b, y + 422, 14, Text);
        Label("Blocking scenery fades only during play.", b, y + 448, 13, Muted);
        Label("Typ mapy: " + MapTypeName(document.Map.Type), b, y + 478, 14, Accent);
        Label(interior ? "Stały mrok we wnętrzach. Dodaj światła w zakładce Lights." :
            "Save the map to use these settings in Runtime.", a, y + 502, 13, interior ? Accent : Muted);
    }
    void DrawLightTools(int x, int y)
    {
        WorldList(document.Map.Lights.Select(l => (l.Id, l.Name)).ToArray(), x + 24, y + 136);
        if (WorldButton("Add light at focus", x + 24, y + 474, 224, false, document.Map.Lights.Count < 256))
        { document.Checkpoint(); var light = new PointLightData { X = focus.X, Z = focus.Z }; document.Map.Lights.Add(light); worldSelectedId = light.Id; }
        var lightData = document.Map.Lights.FirstOrDefault(l => l.Id == worldSelectedId);
        if (lightData == null) { Label("Add a light to begin.", x + 278, y + 160, 16, Muted); return; }
        if (WorldButton("Delete light", x + 24, y + 512, 224)) { document.Checkpoint(); document.Map.Lights.Remove(lightData); return; }
        int a = x + 278, b = x + 548;
        WorldNumber("X", lightData.X, a, y + 140, .5f, -document.Map.Width / 2f, document.Map.Width / 2f - .01f, v => lightData.X = v);
        WorldNumber("Z", lightData.Z, b, y + 140, .5f, -document.Map.Depth / 2f, document.Map.Depth / 2f - .01f, v => lightData.Z = v);
        WorldNumber("Height", lightData.Height, a, y + 184, .25f, 0, 64, v => lightData.Height = v);
        WorldNumber("Radius", lightData.Radius, b, y + 184, .5f, .25f, 128, v => lightData.Radius = v);
        WorldNumber("Intensity", lightData.Intensity, a, y + 228, .1f, 0, 8, v => lightData.Intensity = v);
        WorldToggle("Enabled", lightData.Enabled, b, y + 228, 248, v => lightData.Enabled = v);
        WorldNumber("Red", lightData.R, a, y + 282, 16, 0, 255, v => lightData.R = (byte)v);
        WorldNumber("Green", lightData.G, a, y + 326, 16, 0, 255, v => lightData.G = (byte)v);
        WorldNumber("Blue", lightData.B, a, y + 370, 16, 0, 255, v => lightData.B = (byte)v);
        DrawRectangle(b + 80, y + 282, 96, 96, new(lightData.R, lightData.G, lightData.B, (byte)255));
        WorldName("Name", lightData.Name, a, y + 424, 518, v => lightData.Name = v);
        Label("Preview at night in the Environment tab.", a, y + 480, 14, Muted);
    }
    void DrawSoundTools(int x, int y)
    {
        WorldList(document.Map.Sounds.Select(s => (s.Id, s.Name)).ToArray(), x + 24, y + 136);
        if (WorldButton("Add sound at focus", x + 24, y + 474, 224, false, document.Map.Sounds.Count < 256))
        { document.Checkpoint(); var sound = new SoundEmitterData { X = focus.X, Z = focus.Z }; document.Map.Sounds.Add(sound); worldSelectedId = sound.Id; }
        var emitter = document.Map.Sounds.FirstOrDefault(s => s.Id == worldSelectedId);
        if (emitter == null) { Label("Add an ambient sound to begin.", x + 278, y + 160, 16, Muted); return; }
        if (WorldButton("Delete sound", x + 24, y + 512, 224)) { document.Checkpoint(); document.Map.Sounds.Remove(emitter); return; }
        int a = x + 278, b = x + 548;
        WorldNumber("X", emitter.X, a, y + 140, .5f, -document.Map.Width / 2f, document.Map.Width / 2f - .01f, v => emitter.X = v);
        WorldNumber("Z", emitter.Z, b, y + 140, .5f, -document.Map.Depth / 2f, document.Map.Depth / 2f - .01f, v => emitter.Z = v);
        WorldNumber("Radius", emitter.Radius, a, y + 184, .5f, .5f, 128, v => emitter.Radius = v);
        WorldNumber("Volume", emitter.Volume, b, y + 184, .05f, 0, 1, v => emitter.Volume = v);
        if (WorldButton("Clip: " + emitter.Clip, a, y + 244, 518))
        { var ids = WorldAudio.ClipIds(); document.Checkpoint(); emitter.Clip = ids[(Array.IndexOf(ids, emitter.Clip) + 1) % ids.Length]; }
        WorldToggle("Enabled", emitter.Enabled, a, y + 290, 248, v => emitter.Enabled = v);
        if (WorldButton("Listen", b, y + 290, 248)) status = audio.Preview(emitter.Clip, emitter.Volume * document.Map.Environment.MasterVolume) ? "Playing sound preview." : "Cannot play sound. Check file and audio device.";
        WorldName("Name", emitter.Name, a, y + 342, 518, v => emitter.Name = v);
        Label("Volume fades with distance. Stereo follows the camera.", a, y + 408, 14, Text);
        Label("Click Clip to cycle through built-in and custom sounds.", a, y + 438, 13, Muted);
    }
    void DrawTriggerTools(int x, int y)
    {
        WorldList(document.Map.Triggers.Select(t => (t.Id, t.Name)).ToArray(), x + 24, y + 136);
        if (WorldButton("Draw area on map", x + 24, y + 474, 224, false, document.Map.Triggers.Count < 1000))
        { worldPage = null; drawingTrigger = true; triggerStart = triggerEnd = null; activePrefab = null; tool = EditTool.Select; status = "Drag LMB to draw a trigger area. Escape cancels."; }
        var trigger = document.Map.Triggers.FirstOrDefault(t => t.Id == worldSelectedId);
        if (trigger == null) { Label("Draw an area to add a trigger.", x + 278, y + 160, 16, Muted); return; }
        if (WorldButton("Delete trigger", x + 24, y + 512, 224)) { document.Checkpoint(); document.Map.Triggers.Remove(trigger); return; }
        int a = x + 278, b = x + 548;
        WorldName("Name", trigger.Name, a, y + 136, 518, v => trigger.Name = v);
        WorldNumber("X", trigger.X, a, y + 180, .5f, -document.Map.Width / 2f, document.Map.Width / 2f - .01f, v => trigger.X = v);
        WorldNumber("Z", trigger.Z, b, y + 180, .5f, -document.Map.Depth / 2f, document.Map.Depth / 2f - .01f, v => trigger.Z = v);
        WorldNumber("Width", trigger.Width, a, y + 224, .5f, .5f, 256, v => trigger.Width = v);
        WorldNumber("Depth", trigger.Depth, b, y + 224, .5f, .5f, 256, v => trigger.Depth = v);
        WorldToggle("Once", trigger.Once, a, y + 268, 248, v => trigger.Once = v);
        WorldToggle("Enabled", trigger.Enabled, b, y + 268, 248, v => trigger.Enabled = v);
        if (WorldButton("Action: " + trigger.Action, a, y + 312, 518))
        {
            document.Checkpoint(); trigger.Action = (TriggerAction)(((int)trigger.Action + 1) % Enum.GetValues<TriggerAction>().Length);
            trigger.DestinationX = trigger.X; trigger.DestinationZ = trigger.Z;
            trigger.TargetId = trigger.Action == TriggerAction.Dialogue ? document.Map.Npcs.FirstOrDefault()?.Id ?? "" :
                trigger.Action == TriggerAction.ToggleDoor ? document.Map.SceneEntities.FirstOrDefault(e => e.IsDoor)?.Id ?? "" : "";
        }
        if (trigger.Action == TriggerAction.Message)
            WorldName("Message", trigger.Text, a, y + 366, 518, v => trigger.Text = v, 2000);
        if (trigger.Action is TriggerAction.Dialogue or TriggerAction.ToggleDoor)
        {
            var targets = (trigger.Action == TriggerAction.Dialogue ? document.Map.Npcs.Cast<SceneObject>() : document.Map.SceneEntities.Where(e => e.IsDoor)).ToArray();
            string name = targets.FirstOrDefault(t => t.Id == trigger.TargetId) is { } target ? EntityName(target) : "Choose a target";
            if (WorldButton("Target: " + name, a, y + 366, 518, false, targets.Length > 0))
            { document.Checkpoint(); trigger.TargetId = targets[(Array.FindIndex(targets, t => t.Id == trigger.TargetId) + 1) % targets.Length].Id; }
            if (targets.Length == 0) Label("Create an NPC / mark an object as a door first.", a, y + 416, 14, Muted);
        }
        if (trigger.Action == TriggerAction.SpawnMonster)
        {
            if (WorldButton("Template: " + trigger.MonsterTemplate.Name, a, y + 366, 518, false, document.Map.Monsters.Count > 0))
            {
                document.Checkpoint(); int index = document.Map.Monsters.FindIndex(m => m.Id == trigger.MonsterTemplate.Id);
                trigger.MonsterTemplate = ScenePrefab.Clone(document.Map.Monsters[(index + 1) % document.Map.Monsters.Count]);
            }
            Label("Without a template: default monster stats.", a, y + 504, 13, Muted);
        }
        if (trigger.Action == TriggerAction.ChangeMap) WorldName("Map in Runtime/Maps", trigger.MapFile, a, y + 366, 518, v =>
        {
            if (ProjectPaths.ValidAssetId(v) && v.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) trigger.MapFile = v;
            else status = "Choose a relative JSON file inside Runtime/Maps.";
        }, 256);
        if (trigger.Action is TriggerAction.Teleport or TriggerAction.SpawnMonster or TriggerAction.ChangeMap)
        {
            float minX = trigger.Action == TriggerAction.ChangeMap ? -1024 : -document.Map.Width / 2f;
            float maxX = trigger.Action == TriggerAction.ChangeMap ? 1024 : document.Map.Width / 2f - .01f;
            float minZ = trigger.Action == TriggerAction.ChangeMap ? -1024 : -document.Map.Depth / 2f;
            float maxZ = trigger.Action == TriggerAction.ChangeMap ? 1024 : document.Map.Depth / 2f - .01f;
            WorldNumber("Target X", trigger.DestinationX, a, y + 420, .5f, minX, maxX, v => trigger.DestinationX = v);
            WorldNumber("Target Z", trigger.DestinationZ, b, y + 420, .5f, minZ, maxZ, v => trigger.DestinationZ = v);
        }
    }
    void DrawPrefabTools(int x, int y)
    {
        WorldList(prefabs.Select(p => (p.Path, p.Prefab.Name)).ToArray(), x + 24, y + 136);
        if (WorldButton("Save selected group", x + 24, y + 474, 224, false, SelectionGroup.Length > 0))
            BeginTextEdit("Prefab name", "New prefab", 80, false, name =>
            {
                try { var prefab = ScenePrefab.Capture(name, SelectionGroup); worldSelectedId = prefab.Save(prefabDirectory); ReloadPrefabs(); status = "Prefab saved: " + prefab.Name; }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { status = "Cannot save prefab: " + ex.Message; }
            });
        if (WorldButton("Refresh library", x + 24, y + 512, 224)) ReloadPrefabs();
        var selected = prefabs.FirstOrDefault(p => p.Path == worldSelectedId).Prefab;
        int a = x + 278;
        Label($"Selected actors: {SelectionGroup.Length}", a, y + 146, 16, Accent);
        Label("Shift+click objects to build a group.", a, y + 182, 14, Text);
        Label("Prefabs preserve sprites, colliders, dialogue and contents.", a, y + 214, 13, Muted);
        if (selected == null) { Label("Save a selection or add JSON files to Editor/prefabs.", a, y + 280, 14, Muted); return; }
        Label(selected.Name, a, y + 280, 22, Text, 510);
        Label($"{selected.Objects.Count} objects / {selected.Npcs.Count} NPC / {selected.Chests.Count} chests / {selected.Monsters.Count} monsters", a, y + 324, 14, Muted);
        if (WorldButton("Place prefab on map", a, y + 386, 518, true))
        { activePrefab = ScenePrefab.Clone(selected); tool = EditTool.Place; worldPage = null; status = "Click the map to place " + selected.Name + ". Escape ends placement."; }
        Label("Each placement has its own IDs and inventories.", a, y + 450, 14, Muted);
    }
    void DrawColliderTools(int x, int y)
    {
        int a = x + 24, b = x + 444;
        var entities = document.Map.SceneEntities.ToArray();
        var selected = Selection;
        if (WorldButton("Object: " + (selected == null ? "select one" : EntityName(selected)), a, y + 136, 792, false, entities.Length > 0))
        { selectedId = entities[(Array.FindIndex(entities, e => e.Id == selectedId) + 1) % entities.Length].Id; selectedIds.Clear(); }
        if (selected == null) { Label("Select an object on the map or cycle the Object button.", a, y + 192, 16, Muted); return; }
        string[] kinds = ["Automatic", "Circle", "Box", "Polygon"];
        for (int i = 0; i < kinds.Length; i++)
            if (WorldButton(kinds[i], a + i * 200, y + 182, 192, selected.Collider == null ? i == 0 : i == (int)selected.Collider.Kind + 1))
            {
                document.Checkpoint(); selected.Collider = i == 0 ? null : new ColliderData { Kind = (ColliderKind)(i - 1),
                    Points = i == 3 ? [new(-.5f, -.5f), new(.5f, -.5f), new(.5f, .5f), new(-.5f, .5f)] : [] };
            }
        WorldToggle("Collision", selected.Solid, a, y + 232, 330, v => selected.Solid = v);
        WorldToggle("Door", selected.IsDoor, b, y + 232, 330, v => { selected.IsDoor = v; if (!v) selected.DoorOpen = false; });
        WorldToggle("Fade when blocking player", selected.FadeWhenBlocking, a, y + 276, 330, v => selected.FadeWhenBlocking = v);
        WorldToggle("Door open", selected.DoorOpen, b, y + 276, 330, v => { selected.IsDoor = true; selected.DoorOpen = v; });
        if (selected.Collider is not { } collider)
        { Label("Automatic radius follows the object's height.", a, y + 342, 15, Muted); return; }
        WorldNumber("Rotation", collider.Rotation, b, y + 330, 15, -360, 360, v => collider.Rotation = v);
        if (collider.Kind == ColliderKind.Circle) WorldNumber("Radius", collider.Radius, a, y + 330, .1f, .05f, 64, v => collider.Radius = v);
        if (collider.Kind == ColliderKind.Box)
        {
            WorldNumber("Width", collider.Width, a, y + 330, .1f, .1f, 128, v => collider.Width = v);
            WorldNumber("Depth", collider.Depth, a, y + 374, .1f, .1f, 128, v => collider.Depth = v);
        }
        if (collider.Kind == ColliderKind.Polygon)
        {
            Label($"Polygon vertices: {collider.Points.Count}", a, y + 342, 15, Accent);
            if (WorldButton("Draw polygon on map", a, y + 382, 330, true))
            { drawingColliderId = selected.Id; colliderPoints.Clear(); worldPage = null; status = "Click polygon corners. Enter finishes, Backspace removes a point, Escape cancels."; }
            Label("Points use metres relative to the object.", a, y + 438, 14, Muted);
        }
        Label("Colliders block walking, monster paths and melee.", a, y + 500, 14, Text);
    }
}
