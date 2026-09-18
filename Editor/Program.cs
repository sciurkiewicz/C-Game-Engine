using Raylib_cs;
using static Raylib_cs.Raylib;

if (args.Contains("--editor-test"))
{
    EditorChecks.Run();
    return;
}
SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
InitWindow(1440, 900, "Wildwood | Map Editor");
SetWindowMinSize(1120, 720);
SetExitKey(KeyboardKey.Null);
SetTargetFPS(60);
try
{
    int mapArgument = Array.IndexOf(args, "--map");
    if (mapArgument >= 0 && mapArgument + 1 >= args.Length) throw new ArgumentException("--map requires a JSON file path.");
    bool interactionTest = args.Contains("--interaction-test");
    string mapPath = interactionTest ? Path.Combine(ProjectPaths.Root, "Editor", "obj", "interaction-test.json") :
        mapArgument >= 0 ? Path.GetFullPath(args[mapArgument + 1]) : ProjectPaths.MapFile;
    using var editor = new EditorApp(mapPath);
    if (interactionTest) editor.RunInteractionChecks();
    else editor.Run(args.Contains("--smoke-test"));
}
catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
{
    Console.Error.WriteLine("Cannot open editor: " + ex.Message);
    Environment.ExitCode = 1;
}
finally { CloseWindow(); }
