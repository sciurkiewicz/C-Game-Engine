using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    // Raylib 6.0 automation input events exercise the actual input loop and buttons.
    // Event values: https://github.com/raysan5/raylib/blob/6.0/src/rcore.c
    static void Input(uint type, int a = 0, int b = 0)
    {
        var ev = new AutomationEvent { Type = type };
        var data = MemoryMarshal.Cast<AutomationEvent, int>(MemoryMarshal.CreateSpan(ref ev, 1));
        data[2] = a; data[3] = b;
        PlayAutomationEvent(ev);
    }
    void TestFrame()
    {
        ResizeCanvas();
        if (pendingAction == null) Update(1f / 60);
        Render();
    }
    void Click(int x, int y)
    {
        Input(7, x, y); Input(6, 0); TestFrame();
        Input(7, x, y); Input(5, 0); TestFrame();
    }
    static void Verify(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Editor interaction check failed: " + message);
    }
    public void RunInteractionChecks()
    {
        SetWindowSize(1440, 900);
        document.New();
        document.Map.Objects.AddRange(MapData.CreateDemo(assets.Sprites.Keys).Objects);
        focus = Vector3.Zero; camera.Snap(focus);
        TestFrame();
        Click(620, 60);
        Verify(File.Exists(mapPath) && !document.Dirty, "Save button");
        int count = document.Map.Objects.Count;
        Click(220, 150); // Rocks category.
        Click(65, 235); // First rock.
        Verify(tool == EditTool.Place && chosenAsset.Contains("rocks/"), "asset palette selection");
        Click(960, 625);
        Verify(document.Map.Objects.Count == count + 1, "map click places object");
        var placed = document.Map.Objects[^1];
        string id = placed.Id;
        Click(60, 60); // Select tool.
        var screen = GetWorldToScreenEx(renderer.GetSpriteQuad(camera.View, placed.Asset, placed.Position, placed.Height).Center, camera.View,
            (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        int sx = (int)screen.X, sy = (int)screen.Y;
        Click(sx, sy);
        Verify(selectedId == id, "ray picking selects the rendered sprite");
        Vector3 before = placed.Position;
        Input(7, sx, sy); Input(6, 0); TestFrame();
        Input(7, sx - 80, sy + 30); TestFrame();
        Input(5, 0); TestFrame();
        Verify(Vector3.Distance(Selection!.Position, before) > .2f, "drag moves selected object");
        float height = Selection.Height;
        Click(1392, 316);
        Verify(Selection.Height > height, "height inspector");
        Click(1250, 398);
        Verify(document.Map.Objects.Count == count + 2, "duplicate button");
        Click(1360, 398);
        Verify(document.Map.Objects.Count == count + 1, "delete button");
        Click(460, 60);
        Verify(document.Map.Objects.Count == count + 2, "undo deletion");
        Click(530, 60);
        Verify(document.Map.Objects.Count == count + 1, "redo deletion");
        Click(260, 60);
        Click(180, 220);
        string tiles = string.Join(',', document.Map.Tiles);
        Click(830, 650);
        Verify(string.Join(',', document.Map.Tiles) != tiles, "material palette and ground painting");
        Click(365, 60);
        Click(690, 620);
        Verify(document.Map.Player.Position != Vector3.Zero, "player spawn tool");
        Vector3 spawn = document.Map.Player.Position;
        Click(1360, 60);
        Verify(play != null, "play button");
        Input(2, (int)KeyboardKey.D);
        for (int i = 0; i < 10; i++) TestFrame();
        Input(1, (int)KeyboardKey.D); TestFrame();
        Verify(Vector3.Distance(play!.Player, spawn) > .1f, "playtest movement");
        Input(2, (int)KeyboardKey.F5); TestFrame(); Input(1, (int)KeyboardKey.F5); TestFrame();
        Verify(play == null && document.Map.Player.Position == spawn, "F5 returns without changing spawn");
        Click(620, 60);
        string saved = document.Map.Serialize();
        Verify(MapData.Load(mapPath).Serialize() == saved, "saved map matches edited scene");
        Click(760, 60); // New map.
        Verify(document.Map.Objects.Count == 0, "new map button");
        Click(690, 60); // Load triggers dirty prompt.
        Verify(pendingAction == "load", "unsaved work confirmation");
        Click(GetScreenWidth() / 2 - 230 + 215, GetScreenHeight() / 2 - 90 + 132);
        Verify(document.Map.Serialize() == saved && pendingAction == null, "discard and reload saved map");
        SetWindowSize(1120, 720); TestFrame(); TestFrame();
        Verify(canvas.Texture.Width == 608 && canvas.Texture.Height == 600, "resizable viewport");
        SetWindowSize(1440, 900); TestFrame(); TestFrame();
        status = "Interaction checks passed: placement, picking, drag, paint, spawn, save/load and playtest.";
        Render();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "editor-smoke.png"));
        Console.WriteLine(status);
    }
}
