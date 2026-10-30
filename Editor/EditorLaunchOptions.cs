using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    RuntimeSettings? launchOptions;
    bool launchMenuOpen, menuDismissedThisFrame;
    Rectangle LaunchMenuBounds => new(GetScreenWidth() - 278, 48, 260, 136);
    Rectangle LaunchOptionsBounds => new(GetScreenWidth() / 2 - 310, GetScreenHeight() / 2 - 250, 620, 500);

    void ApplyRuntimeSettings()
    {
        camera.ApplySettings(document.Map.Runtime);
        camera.Snap(focus);
    }
    void ChangeProjection(ViewProjection projection)
    {
        if (document.Map.Runtime.Projection == projection) return;
        document.Checkpoint();
        document.Map.Runtime.Projection = camera.Projection = projection;
        status = projection == ViewProjection.Isometric ? "Isometric view selected." : "Perspective view selected.";
    }
    void DrawLaunchMenuButton()
    {
        int x = GetScreenWidth() - 54;
        if (Button("", x, 8, 36, launchMenuOpen, play == null && pendingAction == null, launchMenuOpen))
        {
            document.EndSculptStroke(); gesture = dragging = dragArmed = false;
            launchMenuOpen = !launchMenuOpen;
        }
        for (int i = 0; i < 3; i++) DrawRectangle(x + 10, 16 + i * 6, 16, 2, play == null ? Text : Muted);
    }
    void UpdateLaunchMenu()
    {
        if (IsKeyPressed(KeyboardKey.Escape)) { launchMenuOpen = false; return; }
        if (IsMouseButtonPressed(MouseButton.Left) && !CheckCollisionPointRec(GetMousePosition(), LaunchMenuBounds) &&
            !CheckCollisionPointRec(GetMousePosition(), new(GetScreenWidth() - 54, 8, 36, 30)))
        { launchMenuOpen = false; menuDismissedThisFrame = true; }
    }
    void DrawLaunchMenu()
    {
        var box = LaunchMenuBounds; int x = (int)box.X, y = (int)box.Y;
        DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        bool enabled = play == null && pendingAction == null;
        if (Button("Nowa mapa", x + 10, y + 10, 240, false, enabled, true))
        { launchMenuOpen = false; Request("new"); return; }
        if (Button("Launch options", x + 10, y + 52, 240, false, enabled, true))
        { launchMenuOpen = false; launchOptions = document.Map.Runtime with { }; return; }
        if (Button("World tools", x + 10, y + 94, 240, false, enabled, true))
        { launchMenuOpen = false; OpenWorldTools(); }
    }
    void UpdateLaunchOptions()
    {
        if (IsKeyPressed(KeyboardKey.Escape)) { launchOptions = null; return; }
        if (IsKeyPressed(KeyboardKey.Enter)) ConfirmLaunchOptions(false);
    }
    void ConfirmLaunchOptions(bool startPlay)
    {
        if (launchOptions == null) return;
        launchOptions.Validate();
        if (document.Map.Runtime != launchOptions)
        {
            document.Checkpoint();
            document.Map.Runtime = launchOptions with { };
        }
        launchOptions = null;
        ApplyRuntimeSettings();
        status = "Launch options applied. Save the map to use them in Runtime.";
        if (startPlay) TogglePlay();
    }
    void DrawLaunchOptions()
    {
        if (launchOptions is not { } options) return;
        bool enabled = pendingAction == null;
        var box = LaunchOptionsBounds; int x = (int)box.X, y = (int)box.Y;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 180));
        DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        Label("LAUNCH OPTIONS", x + 24, y + 22, 23, Accent);
        Label("Saved with the map. Used by Play and Runtime.", x + 24, y + 58, 14, Muted);
        Label("CAMERA PROJECTION", x + 24, y + 104, 13, Muted);
        if (Button("Perspective / current", x + 24, y + 128, 278, options.Projection == ViewProjection.Perspective, enabled, true))
            options.Projection = ViewProjection.Perspective;
        if (Button("Isometric", x + 318, y + 128, 278, options.Projection == ViewProjection.Isometric, enabled, true))
            options.Projection = ViewProjection.Isometric;
        Label(options.Projection == ViewProjection.Isometric ? "Parallel projection. Zoom keeps the same viewing angle." :
            "Original perspective. Camera pitch changes with zoom.", x + 24, y + 172, 14, Text);
        Label("Starting zoom / camera distance", x + 24, y + 216, 14, Muted);
        if (Button("-", x + 318, y + 206, 36, false, enabled && options.InitialDistance > FollowCamera.MinDistance, true))
            options.InitialDistance = Math.Max(FollowCamera.MinDistance, options.InitialDistance - 1);
        Label(options.InitialDistance.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " m", x + 410, y + 215, 16, Text);
        if (Button("+", x + 560, y + 206, 36, false, enabled && options.InitialDistance < FollowCamera.MaxDistance, true))
            options.InitialDistance = Math.Min(FollowCamera.MaxDistance, options.InitialDistance + 1);
        Label("15-50 m. A smaller distance brings the view closer.", x + 24, y + 248, 13, Muted);
        Label("Grid in Play / Runtime", x + 24, y + 292, 14, Text);
        if (Button(options.ShowGrid ? "On" : "Off", x + 438, y + 282, 158, options.ShowGrid, enabled, true))
            options.ShowGrid = !options.ShowGrid;
        Label("Frame limit", x + 24, y + 336, 14, Muted);
        int fx = x + 174;
        foreach (int fps in new[] { 30, 60, 120, 0 })
        {
            if (Button(fps == 0 ? "Unlimited" : fps + " FPS", fx, y + 326, 98, options.TargetFps == fps, enabled, true))
                options.TargetFps = fps;
            fx += 108;
        }
        DrawLine(x + 24, y + 382, x + 596, y + 382, Border);
        if (Button("Defaults", x + 24, y + 400, 108, false, enabled, true)) launchOptions = new();
        if (Button("Apply & world tools", x + 156, y + 400, 172, false, enabled, true))
        { ConfirmLaunchOptions(false); OpenWorldTools(); }
        Label("Enter: apply / Esc: cancel", x + 348, y + 409, 12, Muted);
        if (Button("Cancel", x + 24, y + 450, 130, false, enabled, true)) launchOptions = null;
        if (Button("Apply", x + 270, y + 450, 130, false, enabled, true)) ConfirmLaunchOptions(false);
        if (Button("Apply & Play", x + 416, y + 450, 180, true, enabled, true)) ConfirmLaunchOptions(true);
    }
}
