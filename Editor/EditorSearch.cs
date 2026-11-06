using System.Numerics;
using System.Text;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed class SearchInput
{
    public string Value { get; set; } = "";
    public bool Focused { get; set; }
    bool replace;
    public void Update()
    {
        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl) || IsKeyDown(KeyboardKey.LeftSuper) || IsKeyDown(KeyboardKey.RightSuper);
        if (ctrl && IsKeyPressed(KeyboardKey.A)) replace = true;
        if (ctrl && IsKeyPressed(KeyboardKey.V)) Append(GetClipboardText_());
        if (IsKeyPressed(KeyboardKey.Backspace) || IsKeyPressedRepeat(KeyboardKey.Backspace))
        { Value = replace ? "" : Value.Length == 0 ? "" : Value[..(Value.Length - (Value.Length > 1 && char.IsLowSurrogate(Value[^1]) ? 2 : 1))]; replace = false; }
        for (int code = GetCharPressed(); code != 0; code = GetCharPressed())
            if (code >= 32 && !ctrl) Append(char.ConvertFromUtf32(code));
        if (IsKeyPressed(KeyboardKey.Escape) || IsKeyPressed(KeyboardKey.Enter)) { Focused = false; replace = false; }
    }
    public void Append(string text)
    {
        text = string.Concat(text.EnumerateRunes().Where(r => !Rune.IsControl(r)).Select(r => r.ToString()));
        if (text.Length == 0) return;
        if (replace) { Value = ""; replace = false; }
        foreach (var rune in text.EnumerateRunes())
            if (Value.Length + rune.Utf16SequenceLength <= 120) Value += rune.ToString();
    }
    public static bool Matches(string query, params string[] fields) => query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .All(word => fields.Any(field => field.Contains(word, StringComparison.OrdinalIgnoreCase)));
}

readonly record struct SceneSearchEntry(string Id, string Kind, string Name, string Asset, Vector3 Position);

sealed partial class EditorApp
{
    readonly SearchInput assetSearch = new(), sceneSearch = new();
    bool sceneSearchOpen;
    int sceneSearchScroll;
    string sceneFilter = "All";
    readonly string[] sceneFilters = ["All", "Object", "NPC", "Chest", "Monster", "Trigger", "Light", "Sound", "Road"];
    Rectangle SceneSearchBounds => new(GetScreenWidth() / 2 - 380, GetScreenHeight() / 2 - 280, 760, 560);
    void OpenSceneSearch()
    {
        EndEditorGesture(); sceneSearchOpen = true; sceneSearch.Focused = true; assetSearch.Focused = false;
        sceneSearchScroll = 0;
        while (GetCharPressed() != 0) { }
    }
    IEnumerable<SceneSearchEntry> SceneEntries()
    {
        var map = document.Map;
        yield return new("player", "Player", "Player start", map.Player.Asset ?? "", map.GroundPosition(map.Player.X, map.Player.Z));
        foreach (var entity in map.SceneEntities)
            yield return new(entity.Id, entity is NpcData ? "NPC" : entity is ChestData ? "Chest" : entity is MonsterData ? "Monster" : "Object",
                EntityName(entity), entity.Asset, map.GroundPosition(entity));
        foreach (var trigger in map.Triggers) yield return new(trigger.Id, "Trigger", trigger.Name, trigger.Action.ToString(), map.GroundPosition(trigger.X, trigger.Z));
        foreach (var light in map.Lights) yield return new(light.Id, "Light", light.Name, "", map.GroundPosition(light.X, light.Z));
        foreach (var sound in map.Sounds) yield return new(sound.Id, "Sound", sound.Name, sound.Clip, map.GroundPosition(sound.X, sound.Z));
        foreach (var road in map.Roads) yield return new(road.Id, "Road", road.Name, road.Material,
            map.GroundPosition(road.Points.Average(p => p.X), road.Points.Average(p => p.Z)));
    }
    SceneSearchEntry[] SceneResults() => SceneEntries().Where(e => (sceneFilter == "All" || sceneFilter == e.Kind) &&
        SearchInput.Matches(sceneSearch.Value, e.Name, e.Kind, e.Asset, e.Id)).ToArray();
    void FocusSearchResult(SceneSearchEntry entry)
    {
        sceneSearchOpen = false; sceneSearch.Focused = false;
        StopFragmentTool();
        selectedIds.Clear(); selectedId = null;
        FocusCamera(entry.Position);
        if (entry.Kind is "Object" or "NPC" or "Chest" or "Monster")
        { selectedId = entry.Id; selectedIds.Add(entry.Id); tool = EditTool.Select; }
        else if (entry.Kind == "Player") tool = EditTool.Player;
        else if (entry.Kind == "Road") EditRoad(document.Map.Roads.First(r => r.Id == entry.Id));
        else
        {
            OpenWorldTools(entry.Kind == "Trigger" ? "Triggers" : entry.Kind == "Light" ? "Lights" : "Sounds");
            worldSelectedId = entry.Id;
        }
        status = $"Found {entry.Kind}: {entry.Name}.";
    }
    void UpdateSceneSearch()
    {
        if (IsKeyPressed(KeyboardKey.Escape)) { sceneSearchOpen = false; sceneSearch.Focused = false; return; }
        bool enter = IsKeyPressed(KeyboardKey.Enter);
        string before = sceneSearch.Value;
        if (sceneSearch.Focused) sceneSearch.Update();
        if (before != sceneSearch.Value) sceneSearchScroll = 0;
        if (enter && SceneResults().FirstOrDefault() is { Id: not null } first) FocusSearchResult(first);
    }
    bool UpdateAssetSearch()
    {
        if (!assetSearch.Focused) return false;
        if (IsMouseButtonPressed(MouseButton.Left) && !CheckCollisionPointRec(GetMousePosition(), AssetSearchBounds))
        { assetSearch.Focused = false; return false; }
        string before = assetSearch.Value;
        assetSearch.Update();
        if (before != assetSearch.Value) scroll = groundScroll = characterScroll = 0;
        return true;
    }
    Rectangle AssetSearchBounds => new(16, tool == EditTool.Ground ? 164 : tool is EditTool.Player or EditTool.NPC or EditTool.Monster ? 264 : 176, 218, 30);
    void DrawSearchField(SearchInput input, Rectangle rect, string placeholder, bool modal = false)
    {
        bool enabled = play == null && (!ModalOpen || modal);
        bool hover = enabled && CheckCollisionPointRec(GetMousePosition(), rect);
        if (hover && IsMouseButtonPressed(MouseButton.Left))
        { input.Focused = true; EndCameraLook(); while (GetCharPressed() != 0) { } }
        DrawRectangleRec(rect, Background); DrawRectangleLinesEx(rect, 1, input.Focused ? Accent : Border);
        string value = input.Value;
        // Keep the end of long queries visible while typing.
        while (value.Length > 0 && UiText.Measure(value, 13) > rect.Width - 48) value = value[NextCharacter(value, 0)..];
        Label(value.Length == 0 ? placeholder : value, (int)rect.X + 8, (int)rect.Y + 9, 13, value.Length == 0 ? Muted : Text, (int)rect.Width - 38);
        var clear = new Rectangle(rect.X + rect.Width - 27, rect.Y, 27, 30);
        Label("x", (int)clear.X + 9, (int)clear.Y + 9, 13, Muted);
        if (enabled && IsMouseButtonPressed(MouseButton.Left) && CheckCollisionPointRec(GetMousePosition(), clear))
        { input.Value = ""; scroll = groundScroll = characterScroll = sceneSearchScroll = 0; }
    }
    void DrawSceneSearch()
    {
        var box = SceneSearchBounds; int x = (int)box.X, y = (int)box.Y;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 180));
        DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        Label("SCENE OBJECTS", x + 24, y + 22, 23, Accent);
        Label("Search names, types, graphics or IDs. Click a result to find it on the map.", x + 24, y + 58, 13, Muted);
        DrawSearchField(sceneSearch, new(x + 24, y + 86, 712, 30), "Search the map...", true);
        for (int i = 0; i < sceneFilters.Length; i++)
            if (Button(sceneFilters[i], x + 24 + i * (712 / sceneFilters.Length), y + 128, 712 / sceneFilters.Length - 4, sceneFilter == sceneFilters[i], true, true))
            { sceneFilter = sceneFilters[i]; sceneSearchScroll = 0; }
        var entries = SceneResults();
        var area = new Rectangle(x + 24, y + 174, 712, 306);
        int maximum = Math.Max(0, entries.Length * 44 - (int)area.Height);
        if (CheckCollisionPointRec(GetMousePosition(), area)) sceneSearchScroll -= (int)(GetMouseWheelMove() * 44);
        sceneSearchScroll = Math.Clamp(sceneSearchScroll, 0, maximum);
        BeginScissorMode((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
        for (int i = 0; i < entries.Length; i++)
        {
            int row = (int)area.Y + i * 44 - sceneSearchScroll;
            if (row + 40 < area.Y || row > area.Y + area.Height) continue;
            var entry = entries[i];
            bool hover = CheckCollisionPointRec(GetMousePosition(), new(area.X, row, area.Width, 40)) && CheckCollisionPointRec(GetMousePosition(), area);
            DrawRectangle((int)area.X, row, (int)area.Width, 40, hover ? Card : Background);
            Label(entry.Kind, x + 34, row + 13, 12, Accent, 72);
            Label(entry.Name, x + 116, row + 7, 14, Text, 420);
            Label(entry.Asset.Length == 0 ? entry.Id : entry.Asset, x + 116, row + 24, 10, Muted, 420);
            Label($"{entry.Position.X:0.#}, {entry.Position.Z:0.#}", x + 570, row + 13, 12, Muted, 155);
            if (hover && IsMouseButtonPressed(MouseButton.Left)) { FocusSearchResult(entry); break; }
        }
        if (entries.Length == 0) Label("No matching objects.", x + 36, y + 196, 16, Muted);
        EndScissorMode();
        Label($"{entries.Length} results  /  Enter: first result", x + 24, y + 510, 13, Muted);
        if (Button("Close / Esc", x + 566, y + 500, 170, false, true, true)) { sceneSearchOpen = false; sceneSearch.Focused = false; }
    }
}
