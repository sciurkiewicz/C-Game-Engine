using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed class EditorPreferences
{
    public int AutosaveSeconds { get; set; } = 60;
    public bool ShowMinimap { get; set; }
    public static EditorPreferences Load(string path)
    {
        try
        {
            var preferences = JsonSerializer.Deserialize<EditorPreferences>(File.ReadAllText(path)) ?? new();
            if (preferences.AutosaveSeconds is not (0 or 30 or 60 or 120 or 300)) preferences.AutosaveSeconds = 60;
            return preferences;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this)); File.Move(path + ".tmp", path, true);
    }
}

readonly record struct RecoveryCopy(string Path, DateTime Time);

sealed class EditorRecoveryStore
{
    public const int MaximumCopies = 5;
    public string DirectoryPath { get; }
    readonly string mapPath;
    public EditorRecoveryStore(string mapPath, string root)
    {
        this.mapPath = Path.GetFullPath(mapPath);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(this.mapPath)))[..24];
        DirectoryPath = Path.Combine(root, key);
    }
    public RecoveryCopy[] Copies() => !Directory.Exists(DirectoryPath) ? [] : Directory.EnumerateFiles(DirectoryPath, "*.json")
        .Select(path => new RecoveryCopy(path, File.GetLastWriteTimeUtc(path))).OrderByDescending(c => c.Time).ThenByDescending(c => c.Path).ToArray();
    public RecoveryCopy? LatestUnsaved()
    {
        DateTime savedAt = File.Exists(mapPath) ? File.GetLastWriteTimeUtc(mapPath) : DateTime.MinValue;
        var latest = Copies().FirstOrDefault();
        return latest.Path != null && latest.Time > savedAt ? latest : null;
    }
    public string Save(MapData map, DateTime? capturedAt = null)
    {
        DateTime time = capturedAt ?? DateTime.UtcNow;
        string path = Path.Combine(DirectoryPath, $"{time.Ticks:D19}-{Guid.NewGuid():N}.json");
        map.Save(path);
        // A slow background write must not make an older snapshot newer than a manual save.
        File.SetLastWriteTimeUtc(path, time);
        foreach (var copy in Copies().Skip(MaximumCopies)) File.Delete(copy.Path);
        return path;
    }
}

sealed partial class MapDocument
{
    public void Recover(string path)
    {
        var recovered = MapData.Load(path);
        Checkpoint(); Map = recovered;
        UpdateChangedTiles(); UpdateChangedHeights(); UpdateChangedWater();
    }
}

sealed partial class EditorApp
{
    EditorPreferences preferences = new();
    EditorRecoveryStore recovery = null!;
    readonly Stopwatch autosaveClock = Stopwatch.StartNew();
    double nextAutosave;
    Task<string>? autosaveTask;
    MapSnapshot? autosaveSnapshot;
    string? recoveryPath;
    bool recoveryPrompt, recoveryListOpen, editorSettingsOpen, automatedChecks;
    RecoveryCopy[] recoveryCopies = [];
    string PreferencesPath => Path.Combine(ProjectPaths.Root, ".wildwood", "editor-settings.json");
    Rectangle EditorSettingsBounds => new(GetScreenWidth() / 2 - 320, GetScreenHeight() / 2 - 220, 640, 440);
    void InitializeEditorPreferences(bool testing)
    {
        automatedChecks = testing;
        preferences = testing ? new() { AutosaveSeconds = 0 } : EditorPreferences.Load(PreferencesPath);
        recovery = new(mapPath, testing ? Path.Combine(ProjectPaths.AssetRoot, "obj", "test-autosaves") : Path.Combine(ProjectPaths.Root, ".wildwood", "autosaves"));
        nextAutosave = preferences.AutosaveSeconds;
        if (testing) return;
        try
        {
            if (recovery.LatestUnsaved() is { } latest)
            { recoveryPath = latest.Path; recoveryPrompt = true; }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status = "Cannot read recovery copies: " + ex.Message; }
    }
    void SaveEditorPreferences()
    {
        if (automatedChecks) return;
        try { preferences.Save(PreferencesPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status = "Cannot save editor settings: " + ex.Message; }
    }
    void UpdateAutosave()
    {
        if (autosaveTask?.IsCompleted == true)
        {
            try { autosaveTask.GetAwaiter().GetResult(); status = "Recovery copy saved. Ctrl+S saves the map file."; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            { status = "Automatic save failed: " + ex.Message; autosaveSnapshot = null; }
            autosaveTask = null;
        }
        if (preferences.AutosaveSeconds == 0 || autosaveTask != null || play != null || pendingAction != null || recoveryPrompt || recoveryListOpen || gesture || dragging || marqueeStart != null ||
            cameraLooking || autosaveClock.Elapsed.TotalSeconds < nextAutosave) return;
        nextAutosave = autosaveClock.Elapsed.TotalSeconds + preferences.AutosaveSeconds;
        if (!document.Dirty) return;
        var snapshot = MapSnapshot.Capture(document.Map);
        if (autosaveSnapshot?.Matches(snapshot) == true) return;
        autosaveSnapshot = snapshot;
        var store = recovery;
        DateTime capturedAt = DateTime.UtcNow;
        autosaveTask = Task.Run(() => store.Save(snapshot.Restore(), capturedAt));
    }
    void OpenRecoveryList()
    {
        EndEditorGesture(); editorSettingsOpen = false; recoveryListOpen = true;
        try { recoveryCopies = recovery.Copies(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { recoveryCopies = []; status = ex.Message; }
    }
    void RestoreRecovery(string path)
    {
        recoveryPath = path; recoveryPrompt = recoveryListOpen = false;
        Request("recover");
    }
    void ApplyRecovery()
    {
        if (recoveryPath == null) return;
        document.Recover(recoveryPath);
        selectedId = null; selectedIds.Clear(); activePrefab = null;
        ResetRoadDraft();
        ResetWallDraft();
        StopFragmentTool(true);
        drawingTrigger = false; drawingColliderId = null; triggerStart = triggerEnd = null; colliderPoints.Clear();
        chosenCharacter = document.Map.Player.Asset;
        focus = document.Map.GroundPosition(document.Map.Player.X, document.Map.Player.Z);
        ApplyRuntimeSettings(); EndEditorGesture();
        autosaveSnapshot = MapSnapshot.Capture(document.Map);
        status = "Recovered work. Ctrl+S saves it to the map; Ctrl+Z returns to the previous scene.";
    }
    void DrawEditorSettings()
    {
        var box = EditorSettingsBounds; int x = (int)box.X, y = (int)box.Y;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 180));
        DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        Label("EDITOR SETTINGS", x + 24, y + 22, 23, Accent);
        Label("Automatic recovery copies", x + 24, y + 78, 15, Text);
        int[] intervals = [0, 30, 60, 120, 300];
        for (int i = 0; i < intervals.Length; i++)
        {
            int seconds = intervals[i];
            if (Button(seconds == 0 ? "Off" : seconds + " s", x + 24 + i * 120, y + 110, 112, preferences.AutosaveSeconds == seconds, true, true))
            { preferences.AutosaveSeconds = seconds; nextAutosave = autosaveClock.Elapsed.TotalSeconds + seconds; SaveEditorPreferences(); }
        }
        Label("Keeps the latest five copies separately from your map file.", x + 24, y + 158, 14, Muted);
        if (Button("Browse recovery copies", x + 24, y + 192, 280, false, true, true)) OpenRecoveryList();
        if (Button(preferences.ShowMinimap ? "Minimap: ON" : "Minimap: OFF", x + 24, y + 252, 280, preferences.ShowMinimap, true, true)) ToggleMinimap();
        Label("M toggles the overview. Click it to move the editor camera.", x + 24, y + 302, 13, Muted);
        if (Button("Close / Esc", x + 416, y + 386, 200, true, true, true)) editorSettingsOpen = false;
    }
    void DrawRecovery()
    {
        int w = 680, h = recoveryPrompt ? 260 : 460;
        int x = (GetScreenWidth() - w) / 2, y = (GetScreenHeight() - h) / 2;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 180));
        DrawRectangle(x, y, w, h, Panel); DrawRectangleLines(x, y, w, h, Accent);
        Label(recoveryPrompt ? "RECOVER UNSAVED WORK?" : "RECOVERY COPIES", x + 24, y + 24, 23, Accent);
        Label("Map: " + Path.GetFileName(mapPath), x + 24, y + 64, 14, Text, 630);
        if (recoveryPrompt)
        {
            Label("An automatic copy is newer than the saved map.", x + 24, y + 112, 16, Text);
            Label("Restoring keeps the map file intact until you save.", x + 24, y + 144, 14, Muted);
            if (Button("Restore", x + 24, y + 202, 300, true, true, true)) RestoreRecovery(recoveryPath!);
            if (Button("Later / Esc", x + 356, y + 202, 300, false, true, true)) recoveryPrompt = false;
            return;
        }
        Label("Select a copy to restore. Your current work can be saved first.", x + 24, y + 102, 14, Muted);
        for (int i = 0; i < recoveryCopies.Length && i < EditorRecoveryStore.MaximumCopies; i++)
        {
            var copy = recoveryCopies[i];
            if (Button($"{copy.Time.ToLocalTime():yyyy-MM-dd HH:mm:ss}    Restore", x + 24, y + 144 + i * 44, 632, false, true, true))
            { RestoreRecovery(copy.Path); break; }
        }
        if (recoveryCopies.Length == 0) Label("No recovery copies for this map yet.", x + 24, y + 156, 16, Muted);
        if (Button("Close / Esc", x + 456, y + h - 54, 200, false, true, true)) recoveryListOpen = false;
    }
}
