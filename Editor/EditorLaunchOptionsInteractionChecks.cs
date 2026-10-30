using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    void ClickLaunchOptionsMenu()
    {
        Click(GetScreenWidth() - 36, 23);
        Verify(launchMenuOpen, "hamburger opens its menu");
        var menu = LaunchMenuBounds; Click((int)menu.X + 130, (int)menu.Y + 67);
    }
    public void RunLaunchOptionsInteractionChecks()
    {
        SetWindowSize(1440, 900); document.New(32, 32); tool = EditTool.Select; selectedId = null;
        focus = Vector3.Zero; ApplyRuntimeSettings(); TestFrame(); Save();
        string saved = document.Map.Serialize();
        Click(GetScreenWidth() - 150, 23);
        Verify(camera.View.Projection == CameraProjection.Orthographic && document.Dirty, "header switches to isometric view");
        ClickHeader("Undo"); Verify(camera.View.Projection == CameraProjection.Perspective && !document.Dirty, "undo synchronizes projection");
        ClickHeader("Redo"); Verify(camera.View.Projection == CameraProjection.Orthographic, "redo synchronizes projection");
        ClickHeader("Undo");
        ClickLaunchOptionsMenu();
        Verify(launchOptions != null && ModalOpen && !launchMenuOpen, "menu opens launch options");
        var box = LaunchOptionsBounds; int x = (int)box.X, y = (int)box.Y;
        ClickHeader("Monster"); Press(KeyboardKey.F5); Press(KeyboardKey.Nine);
        Input(2, (int)KeyboardKey.W); TestFrame(); Input(1, (int)KeyboardKey.W); TestFrame();
        Verify(tool == EditTool.Select && play == null && focus == Vector3.Zero, "modal blocks background buttons, shortcuts and camera movement");
        Click(x + 450, y + 142);
        Press(KeyboardKey.Escape);
        Verify(launchOptions == null && document.Map.Serialize() == saved, "cancel discards the settings draft");
        ClickLaunchOptionsMenu();
        Click(x + 450, y + 142); // Isometric.
        Click(x + 335, y + 220); // Distance 29.
        Click(x + 510, y + 296); // Grid off.
        Click(x + 440, y + 340); // 120 FPS.
        TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "launch-options.png"));
        Click(x + 330, y + 464);
        var options = document.Map.Runtime with { };
        Verify(launchOptions == null && camera.View.Projection == CameraProjection.Orthographic && camera.Distance == 29 &&
            !options.ShowGrid && options.TargetFps == 120 && document.Dirty, "Apply commits every launch option");
        ClickHeader("Undo"); Verify(document.Map.Runtime == new RuntimeSettings() && !document.Dirty, "one undo restores the complete settings change");
        ClickHeader("Redo"); Verify(document.Map.Runtime == options && camera.Distance == 29, "redo restores settings and editor camera");
        Choose(assets.Sprites.Values.First(a => a.Id.Contains("rocks/")));
        var point = GetWorldToScreenEx(new(3, 0, 0), camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        Click((int)point.X, (int)point.Y);
        Verify(document.Map.Objects.Count == 1 && Vector3.Distance(document.Map.Objects[0].Position, new(3, 0, 0)) < .01f,
            "orthographic terrain picking places objects at the cursor");
        var item = document.Map.Objects[0];
        ClickHeader("Select");
        point = GetWorldToScreenEx(renderer.GetSpriteQuad(camera.View, item.Asset, item.Position, item.Height).Center,
            camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        Click((int)point.X, (int)point.Y); Verify(selectedId == item.Id, "orthographic ray picking selects the sprite");
        var original = item.Position;
        Input(7, (int)point.X, (int)point.Y); Input(6, 0); TestFrame();
        Input(7, (int)point.X + 40, (int)point.Y + 20); TestFrame();
        Input(5, 0); TestFrame(); Verify(item.Position != original, "isometric object dragging");
        TestFrame(); TestFrame(); WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "isometric-editor.png"));
        ClickHeader("Save");
        Verify(MapData.Load(mapPath).Runtime == options && !document.Dirty, "saved map includes runtime options");
        Complete("load"); Verify(camera.Projection == ViewProjection.Isometric && camera.Distance == 29, "loading applies camera settings");
        Press(KeyboardKey.F5);
        Verify(play != null && play.Camera.Projection == ViewProjection.Isometric && play.Camera.Distance == 29 && play.Map.Runtime == options,
            "F5 uses the selected launch settings");
        var spawn = play!.Player;
        Input(2, (int)KeyboardKey.D); for (int i = 0; i < 10; i++) TestFrame(); Input(1, (int)KeyboardKey.D); TestFrame();
        var movement = play.Player - spawn;
        Verify(movement.X > .1f && movement.Z < -.1f, "isometric movement follows the camera heading");
        Press(KeyboardKey.Equal); Verify(play.Camera.TargetDistance == 25, "isometric play zoom");
        Press(KeyboardKey.R); Verify(play.Camera.TargetDistance == 29, "R restores the configured starting zoom");
        Press(KeyboardKey.Escape);
        Verify(play == null && document.Map.Runtime == options && document.Map.Player.Position == spawn,
            "Escape returns to editing and preserves map settings and spawn");
        ClickLaunchOptionsMenu();
        SetWindowSize(1120, 720); TestFrame(); TestFrame(); box = LaunchOptionsBounds;
        Verify(box.X >= 0 && box.Y >= 0 && box.X + box.Width <= GetScreenWidth() && box.Y + box.Height <= GetScreenHeight(),
            "launch options fit the minimum window");
        x = (int)box.X; y = (int)box.Y;
        Click(x + 70, y + 414); Verify(launchOptions == new RuntimeSettings(), "Defaults resets the draft");
        Click(x + 500, y + 464);
        Verify(launchOptions == null && play?.Camera.Projection == ViewProjection.Perspective && play.Camera.Distance == 30,
            "Apply & Play starts with the draft settings");
        Press(KeyboardKey.F5);
        SetWindowSize(1440, 900); TestFrame(); TestFrame();
        Console.WriteLine("Launch options interaction checks passed: modal, projections, picking/dragging, undo/redo, save/load, play/zoom and small window.");
    }
}
