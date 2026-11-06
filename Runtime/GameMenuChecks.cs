using System.Runtime.InteropServices;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class GameApp
{
    public void RunMenuInteractionChecks()
    {
        var authored = MapData.CreateEmpty(16, 12); authored.Player.Inventory.Add("potion"); authored.Runtime.TargetFps = 30;
        authored.Save(mapPath); string saved = File.ReadAllText(mapPath);
        void Check(bool valid, string message)
        { if (!valid) throw new InvalidOperationException("Main menu interaction check failed: " + message); }
        void Input(uint type, int a = 0, int b = 0)
        {
            var ev = new AutomationEvent { Type = type };
            var data = MemoryMarshal.Cast<AutomationEvent, int>(MemoryMarshal.CreateSpan(ref ev, 1));
            data[2] = a; data[3] = b; PlayAutomationEvent(ev);
        }
        void Press(KeyboardKey key)
        { Input(2, (int)key); Frame(1f / 60); Input(1, (int)key); Frame(1f / 60); }
        void Click(int index, float horizontal = .5f)
        {
            var box = menu.ButtonBounds(index);
            Input(7, (int)(box.X + box.Width * horizontal), (int)(box.Y + box.Height / 2));
            Input(6, 0); Frame(1f / 60); Input(5, 0); Frame(1f / 60);
        }
        void NewGame()
        {
            Click(0);
            if (narrative != null) Press(KeyboardKey.Escape);
            if (gameTransition != null) { Frame(gameTransition.Duration); Frame(1f / 60); }
        }
        void Preview(string name)
        {
            // Both window buffers need a rendered frame after startup or a native resize.
            Frame(1f / 60); Frame(1f / 60);
            var image = LoadImageFromScreen();
            try
            {
                var box = menu.ButtonBounds(0);
                var background = GetImageColor(image, 10, 10);
                var button = GetImageColor(image, (int)box.X + 10, (int)box.Y + 5);
                Check(button.R > background.R + 8, "the captured menu contains visible buttons");
                if (!menu.OptionsOpen)
                {
                    var disabled = menu.ButtonBounds(1);
                    Check(GetImageColor(image, (int)disabled.X + 10, (int)disabled.Y + 5).R < button.R,
                        "Load game is visibly dimmed");
                }
                Check(ExportImage(image, Path.Combine(ProjectPaths.Root, "Editor", "obj", name + ".png")), "menu preview is saved");
            }
            finally { UnloadImage(image); }
        }
        try
        {
            Frame(1f / 60);
            Check(session == null && !quit && !menu.OptionsOpen, "startup shows the menu before creating a game session");
            Preview("main-menu");
            Click(1);
            Check(session == null && !quit && !menu.OptionsOpen, "disabled Load game cannot start or exit the game");
            Press(KeyboardKey.Down); Press(KeyboardKey.Enter);
            Check(menu.OptionsOpen, "keyboard navigation skips disabled Load game");
            Press(KeyboardKey.Left); Check(menu.Volume == .9f, "left arrow reduces volume");
            Click(0, .94f); Check(menu.Volume == 1, "plus button increases volume");
            Click(0, .06f); Check(menu.Volume == .9f, "minus button reduces volume");
            Preview("main-menu-options");
            for (int i = 0; i < 12; i++) Press(KeyboardKey.Left);
            Check(menu.Volume == 0, "volume stops at mute");
            for (int i = 0; i < 12; i++) Press(KeyboardKey.Right);
            Check(menu.Volume == 1, "volume cannot exceed 100 percent");
            Press(KeyboardKey.Left);
            bool originalFullscreen = IsWindowState(ConfigFlags.BorderlessWindowMode);
            Press(KeyboardKey.Down); Press(KeyboardKey.Enter);
            Check(IsWindowState(ConfigFlags.BorderlessWindowMode) != originalFullscreen, "fullscreen option changes the real window mode");
            Press(KeyboardKey.Enter);
            Check(IsWindowState(ConfigFlags.BorderlessWindowMode) == originalFullscreen, "fullscreen can be switched back to a window");
            Press(KeyboardKey.Escape); Check(!menu.OptionsOpen && !quit, "Escape closes options");
            Click(2); Click(2); Check(!menu.OptionsOpen, "Back button closes options");
            SetWindowSize(800, 540); Frame(1f / 60);
            for (int i = 0; i < 4; i++)
            {
                var box = menu.ButtonBounds(i);
                Check(box.X >= 0 && box.Y >= 0 && box.X + box.Width <= GetScreenWidth() && box.Y + box.Height <= GetScreenHeight(),
                    "buttons fit the minimum window");
            }
            Preview("main-menu-small");
            SetWindowSize(1280, 800); Frame(1f / 60);
            NewGame(); var first = session;
            Check(first != null && first.Inventory.Items.Count == 1 && first.PlayerHealth == first.Character.MaxHealth && activeFps == 30,
                "New game starts the authored map with its character, inventory and FPS limit");
            Check(!IsAudioDeviceReady() || Math.Abs(GetMasterVolume() - menu.Volume) < .001f, "menu volume is applied to game audio");
            Check(File.ReadAllText(mapPath) == saved, "starting a game preserves the source map");
            Press(KeyboardKey.I); Check(session!.InventoryOpen, "inventory remains available during gameplay");
            Press(KeyboardKey.Escape); Check(session != null && !session.InventoryOpen, "Escape closes inventory before returning to the menu");
            session!.Inventory.Items.Clear(); Press(KeyboardKey.Escape);
            Check(session == null && activeFps == 60 && !quit, "Escape returns to the main menu");
            NewGame();
            Check(session != null && !ReferenceEquals(first, session) && session.Inventory.Items.Count == 1,
                "New game starts fresh rather than continuing an earlier session");
            Press(KeyboardKey.Escape); File.WriteAllText(mapPath, "{ broken"); NewGame();
            Check(session == null && menu.Message.Length > 0 && !quit, "a bad map leaves a usable menu with an error message");
            File.WriteAllText(mapPath, saved); NewGame();
            Check(session != null, "New game can retry after fixing the map");
            Press(KeyboardKey.Escape); Click(3);
            Check(quit, "Exit game requests application shutdown");
            Console.WriteLine("Main menu interaction checks passed: startup, disabled load, mouse/keyboard, volume limits, fullscreen, minimum window, new game/reset, inventory/Escape, map error recovery and exit.");
        }
        finally { if (File.Exists(mapPath)) File.Delete(mapPath); }
    }
}
