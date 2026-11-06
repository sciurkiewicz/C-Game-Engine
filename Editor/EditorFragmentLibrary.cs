using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    bool fragmentLibraryOpen, fragmentOptionsOpen;
    string fragmentDirectory = Path.Combine(ProjectPaths.AssetRoot, "fragments");
    readonly List<(string Path, MapFragment Fragment)> fragmentLibrary = [];
    int fragmentLibraryScroll;
    Rectangle FragmentLibraryBounds => new(GetScreenWidth() / 2 - 380, GetScreenHeight() / 2 - 270, 760, 540);
    Rectangle FragmentOptionsBounds => new(GetScreenWidth() / 2 - 330, GetScreenHeight() / 2 - 275, 660, 550);
    void ReloadFragmentLibrary()
    {
        fragmentLibrary.Clear(); fragmentLibraryScroll = 0;
        if (!Directory.Exists(fragmentDirectory)) return;
        foreach (var path in Directory.EnumerateFiles(fragmentDirectory, "*.json").Order().Take(128))
            try { fragmentLibrary.Add((path, MapFragment.Load(path))); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            { status = "Cannot load fragment " + Path.GetFileName(path) + ": " + ex.Message; }
        fragmentLibrary.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Fragment.Name, b.Fragment.Name));
    }
    void OpenFragmentLibrary()
    { EndEditorGesture(); assetSearch.Focused = false; fragmentLibraryOpen = true; ReloadFragmentLibrary(); }
    void SaveFragmentToLibrary()
    {
        if (fragmentClipboard == null) return;
        var fragment = fragmentClipboard;
        BeginTextEdit("Fragment name", fragment.Name, 80, false, name =>
        {
            fragment.Name = name.Trim();
            try { fragment.Save(fragmentDirectory); status = "Fragment saved in the library: " + fragment.Name; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { status = "Cannot save fragment: " + ex.Message; }
        });
    }
    void DrawFragmentLibrary()
    {
        var box = FragmentLibraryBounds; int x = (int)box.X, y = (int)box.Y;
        bool enabled = textEdit == null && pendingAction == null;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 180));
        DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        Label("MAP FRAGMENT LIBRARY", x + 24, y + 24, 23, Accent);
        Label("Reusable terrain, water, actors, roads, lights, sounds and triggers.", x + 24, y + 64, 14, Muted);
        var area = new Rectangle(x + 24, y + 112, 712, 330);
        if (enabled && CheckCollisionPointRec(GetMousePosition(), area)) fragmentLibraryScroll -= (int)(GetMouseWheelMove() * 50);
        fragmentLibraryScroll = Math.Clamp(fragmentLibraryScroll, 0, Math.Max(0, fragmentLibrary.Count * 50 - 330));
        BeginScissorMode((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
        for (int i = 0; i < fragmentLibrary.Count; i++)
        {
            var fragment = fragmentLibrary[i].Fragment; int row = (int)area.Y + i * 50 - fragmentLibraryScroll;
            if (row + 42 < area.Y || row > area.Y + area.Height) continue;
            DrawRectangle(x + 24, row, 712, 44, Background);
            Label(fragment.Name, x + 36, row + 6, 15, Text, 500);
            Label($"{fragment.Width} x {fragment.Depth} tiles / {fragment.Actors.Entities.Count()} actors / {fragment.Roads.Count} roads", x + 36, row + 26, 12, Muted, 510);
            bool visible = GetMouseY() >= area.Y && GetMouseY() < area.Y + area.Height;
            if (Button("Place", x + 596, row + 6, 128, false, enabled && visible, true)) { BeginFragmentPaste(fragment); break; }
        }
        if (fragmentLibrary.Count == 0) Label("No fragments yet. Select an area, then Save fragment.", x + 36, y + 138, 16, Muted);
        EndScissorMode();
        if (Button("Refresh", x + 24, y + 480, 160, false, enabled, true)) ReloadFragmentLibrary();
        if (Button("Select area", x + 200, y + 480, 160, true, enabled, true)) { fragmentLibraryOpen = false; BeginFragmentSelection(); }
        if (Button("Close / Esc", x + 576, y + 480, 160, false, enabled, true)) fragmentLibraryOpen = false;
    }
    void DrawFragmentOptions()
    {
        var box = FragmentOptionsBounds; int x = (int)box.X, y = (int)box.Y;
        bool enabled = pendingAction == null;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 180));
        DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        Label("PASTE OPTIONS", x + 24, y + 24, 23, Accent);
        Label("Choose which parts of the fragment to paste.", x + 24, y + 64, 14, Muted);
        void Toggle(string title, bool current, int row, Action<bool> change)
        { if (Button(title + (current ? ": ON" : ": OFF"), x + 24, y + row, 612, current, enabled, true)) change(!current); }
        Toggle("Terrain, heights and water", fragmentOptions.Terrain, 104, value => fragmentOptions.Terrain = value);
        Toggle("Objects, NPC, chests and monsters", fragmentOptions.Actors, 146, value => fragmentOptions.Actors = value);
        Toggle("Roads", fragmentOptions.Roads, 188, value => fragmentOptions.Roads = value);
        Toggle("Lights, sounds and triggers", fragmentOptions.WorldFeatures, 230, value => fragmentOptions.WorldFeatures = value);
        Toggle("Replace destination contents", fragmentOptions.ReplaceContents, 286, value => fragmentOptions.ReplaceContents = value);
        Label("Replacement affects selected categories. Terrain is overwritten when enabled.", x + 24, y + 328, 12, Muted);
        Toggle("Align copied water to this map's water level", fragmentOptions.AlignWater, 358, value => fragmentOptions.AlignWater = value);
        Label("Additional terrain height offset (m)", x + 24, y + 417, 14, Text);
        if (Button("-", x + 360, y + 408, 38, false, enabled && fragmentOptions.Terrain, true)) fragmentOptions.HeightOffset = Math.Max(-96, fragmentOptions.HeightOffset - .25f);
        Label(fragmentOptions.HeightOffset.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), x + 440, y + 417, 14, Accent);
        if (Button("+", x + 598, y + 408, 38, false, enabled && fragmentOptions.Terrain, true)) fragmentOptions.HeightOffset = Math.Min(96, fragmentOptions.HeightOffset + .25f);
        if (Button("Defaults", x + 24, y + 490, 180, false, enabled, true))
        { fragmentOptions.Terrain = fragmentOptions.Actors = fragmentOptions.Roads = fragmentOptions.WorldFeatures = fragmentOptions.AlignWater = true; fragmentOptions.ReplaceContents = false; fragmentOptions.HeightOffset = 0; }
        if (Button("Done / Esc", x + 456, y + 490, 180, true, enabled, true)) fragmentOptionsOpen = false;
    }
}
