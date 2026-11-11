using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunWallPerformanceChecks(string label)
    {
        SetTargetFPS(0); SetWindowSize(1440, 900); EndEditorGesture(); StopFragmentTool(true);
        preferences.AutosaveSeconds = 0; preferences.ShowMinimap = false;
        tool = EditTool.Select; grid = false; selectedId = null; selectedIds.Clear();
        document.New(256, 256, MapType.Cave);
        document.Map.WallCells = (from z in Enumerable.Range(0, 256) from x in Enumerable.Range(0, 256)
            where x % 8 == 0 || z % 8 == 0 select new GridWallCell { X = x, Z = z }).ToList();
        document.Map.Environment.Shadows = false; document.Map.Environment.Audio = false;
        document.Map.Player.X = document.Map.Player.Z = 3.5f;
        focus = Vector3.Zero; ApplyRuntimeSettings(); ResizeCanvas(); document.Save(mapPath);
        int sceneWalls = document.Map.WallCells.Count;
        Input(7, 12, 12);
        var results = new List<object>();
        void Measure(string name, Action action, int frames)
        {
            for (int i = 0; i < 3; i++) action();
            GC.Collect(); GC.WaitForPendingFinalizers();
            long bytes = GC.GetAllocatedBytesForCurrentThread(); var times = new double[frames];
            for (int i = 0; i < frames; i++)
            {
                long start = Stopwatch.GetTimestamp(); action();
                times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            double allocations = (GC.GetAllocatedBytesForCurrentThread() - bytes) / (1024.0 * 1024 * frames);
            Array.Sort(times);
            var entry = new { name, medianMs = times[frames / 2], meanMs = times.Average(), allocatedMiB = allocations };
            results.Add(entry); Console.WriteLine(JsonSerializer.Serialize(entry));
        }
        Measure("editor-idle", Render, 12);
        Measure("saved-state-check", () => { _ = document.Dirty; }, 40);
        preferences.ShowMinimap = true; overview.Update(document.Map, assets);
        Measure("editor-minimap", Render, 12); preferences.ShowMinimap = false;
        Measure("game-world", () => {
            BeginTextureMode(canvas); ClearBackground(Color.Black); BeginMode3D(camera.View);
            renderer.Draw(document.Map, camera.View, false, document.Map.GroundPosition(3.5f, 3.5f), (int)viewport.Width, (int)viewport.Height);
            EndMode3D(); EndTextureMode();
        }, 12);
        int column = 2;
        document.Checkpoint();
        Measure("paint-one-cell", () => {
            document.PaintWallCells([(column, 2)], new(), false, false); column += 2;
        }, 16);
        string output = Path.Combine(ProjectPaths.Root, "Editor", "obj", "wall-performance-" + label + ".json");
        File.WriteAllText(output, JsonSerializer.Serialize(new { walls = sceneWalls, results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Wall performance measurements saved: " + output);
    }
}
