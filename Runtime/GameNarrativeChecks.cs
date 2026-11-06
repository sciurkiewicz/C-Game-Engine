using System.Runtime.InteropServices;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class GameApp
{
    public void RunNarrativeInteractionChecks()
    {
        string path = narrativeFile ?? throw new InvalidOperationException("Use a separate narrative fixture.");
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(Path.Combine(directory, "images"));
        var authored = MapData.CreateEmpty(16, 12);
        authored.Environment.DayNightCycle = true; authored.Environment.TimeOfDay = 9; authored.Runtime.TargetFps = 30;
        authored.Monsters.Add(new() { X = .8f }); authored.Save(mapPath);
        string savedMap = File.ReadAllText(mapPath);
        var image = GenImageColor(320, 160, new(35, 150, 190, 255));
        try
        {
            ImageDrawRectangle(ref image, 0, 0, 320, 80, new(210, 130, 55, 255));
            if (!ExportImage(image, Path.Combine(directory, "images", "check.png"))) throw new IOException("Cannot create narrative image fixture.");
        }
        finally { UnloadImage(image); }
        var deck = new NarrativeSequence { BlackHold = .25f, GameFadeIn = 1, Cards = [
            new() { TextBlocks = ["Pierwszy blok: Żółw przemierza las i szuka drogi do domu.", "Drugi blok: opowieść jest dopiero na początku."],
                Image = "images/check.png", Duration = 6, FadeIn = .5f, FadeOut = .5f, CharactersPerSecond = 12 },
            new() { TextBlocks = ["Tekst pojawia się i znika łagodnie.", "Ta karta nie ma obrazu."], Effect = NarrativeEffect.Fade,
                Duration = 2, FadeIn = .5f, FadeOut = .5f },
            new() { TextBlocks = ["Pierwszy krok", "Drugi krok", "Trzeci krok", "Twoja historia trwa."], Effect = NarrativeEffect.Scroll,
                Duration = 4, FadeIn = .5f, FadeOut = .5f, ScrollDirection = NarrativeScrollDirection.Down }
        ] };
        deck.Save(path);
        void Check(bool valid, string message)
        { if (!valid) throw new InvalidOperationException("Narrative interaction check failed: " + message); }
        void Input(uint type, int a = 0, int b = 0)
        {
            var ev = new AutomationEvent { Type = type };
            var data = MemoryMarshal.Cast<AutomationEvent, int>(MemoryMarshal.CreateSpan(ref ev, 1));
            data[2] = a; data[3] = b; PlayAutomationEvent(ev);
        }
        void Press(KeyboardKey key)
        { Input(2, (int)key); Frame(1f / 60); Input(1, (int)key); Frame(1f / 60); }
        void Click(Rectangle box)
        {
            Input(7, (int)(box.X + box.Width / 2), (int)(box.Y + box.Height / 2));
            Input(6, 0); Frame(1f / 60); Input(5, 0); Frame(1f / 60);
        }
        void Capture(string name, bool hasImage = false)
        {
            Frame(1f / 60); Frame(1f / 60);
            var frame = LoadImageFromScreen();
            try
            {
                var background = GetImageColor(frame, 10, 10);
                Check(background.R == 12 && background.G == 16 && background.B == 21, "narrative replaces the game screen and HUD");
                if (hasImage)
                {
                    int colored = 0;
                    for (int y = 0; y < frame.Height; y += 4)
                    for (int x = 0; x < frame.Width; x += 4)
                    {
                        var pixel = GetImageColor(frame, x, y);
                        if (Math.Abs(pixel.R - 210) <= 2 && Math.Abs(pixel.G - 130) <= 2 && Math.Abs(pixel.B - 55) <= 2) colored++;
                    }
                    Check(colored > 200, "the configured image is rendered in its original colors");
                }
                Check(ExportImage(frame, Path.Combine(ProjectPaths.Root, "Editor", "obj", name + ".png")), "preview is saved");
            }
            finally { UnloadImage(frame); }
        }
        Color CaptureTransition(string name, bool black = false)
        {
            Frame(0); Frame(0);
            var frame = LoadImageFromScreen();
            try
            {
                if (black)
                    for (int y = 0; y < frame.Height; y += 16)
                    for (int x = 0; x < frame.Width; x += 16)
                    {
                        var pixel = GetImageColor(frame, x, y);
                        Check(pixel.R == 0 && pixel.G == 0 && pixel.B == 0, "the handoff is fully black, including controls and HUD");
                    }
                Check(ExportImage(frame, Path.Combine(ProjectPaths.Root, "Editor", "obj", name + ".png")), "transition preview is saved");
                return GetImageColor(frame, 10, 10);
            }
            finally { UnloadImage(frame); }
        }
        void FinishGameFade()
        {
            if (gameTransition != null) Frame(gameTransition.Duration);
        }
        try
        {
            Frame(1f / 60); Click(menu.ButtonBounds(0));
            Check(narrative?.Active == true && session != null && activeFps == 60, "New game starts narrative before gameplay");
            Input(2, (int)KeyboardKey.W); Input(2, (int)KeyboardKey.I); Frame(.3f);
            Input(1, (int)KeyboardKey.W); Input(1, (int)KeyboardKey.I); Frame(.3f);
            Check(session!.Player == authored.GroundPosition(0, 0) && !session.InventoryOpen && session.TimeOfDay == 9 &&
                session.Map.Monsters.Single().X == .8f && session.PlayerHealth == session.Character.MaxHealth,
                "movement, inventory, monsters, combat and world time are paused during cards");
            Frame(1);
            Check(!narrative!.Playback.TextComplete, "typewriter reveals text gradually");
            Capture("narrative-typewriter", true);
            SetWindowSize(800, 540); Capture("narrative-small", true);
            SetWindowSize(1280, 800); Frame(1f / 60);
            Press(KeyboardKey.Space);
            Check(narrative.Playback.CardIndex == 0 && narrative.Playback.TextComplete, "Space first reveals the rest of the text");
            Press(KeyboardKey.Space); Frame(.6f);
            Check(narrative.Playback.CardIndex == 1, "a second Space fades into the next card");
            Frame(.1f); Capture("narrative-fade");
            Check(narrative.Playback.Opacity > 0 && narrative.Playback.Opacity < 1, "fade rendering uses intermediate opacity");
            Frame(2); Frame(1.4f);
            Check(narrative.Playback.CardIndex == 2 && narrative.Playback.Card!.ScrollDirection == NarrativeScrollDirection.Down,
                "duration automatically starts the downward scroll card");
            Capture("narrative-scroll-down");
            var changed = deck with { Cards = deck.Cards.Select((c, i) => i == 2 ? c with
            { TextBlocks = ["Zmieniony blok", "Kolejny fragment historii"], Duration = 6, Image = "images/check.png", ScrollDirection = NarrativeScrollDirection.Up } : c).ToArray() };
            changed.Save(path);
            Check(narrative.Playback.Card!.Duration == 4 && narrative.Playback.Card.ScrollDirection == NarrativeScrollDirection.Down,
                "editing the file preserves active playback");
            Input(2, (int)KeyboardKey.Space); Frame(.25f);
            var outro = CaptureTransition("narrative-outro-fade");
            Check(narrative != null && outro.R > 0 && outro.R < 12 && outro.G < 16 && outro.B < 21,
                "the last scroll card fades its background and controls towards black");
            Frame(2.5f);
            Check(narrative == null && gameTransition != null && session != null && activeFps == 60 && session.TimeOfDay == 9,
                "ending the final card starts a paused handoff through black");
            CaptureTransition("narrative-black", true);
            Frame(deck.BlackHold + deck.GameFadeIn / 2);
            var halfway = CaptureTransition("narrative-game-fade");
            Check(halfway.R > 0 && halfway.R < 38 && halfway.G > 0 && halfway.G < 47 && halfway.B > 0 && halfway.B < 54,
                "the world is partly visible halfway through the fade-in");
            Check(session!.TimeOfDay == 9 && session.PlayerHealth == session.Character.MaxHealth,
                "world time and combat stay paused while the game is being revealed");
            FinishGameFade();
            var revealed = CaptureTransition("narrative-game-revealed");
            Check(gameTransition == null && activeFps == 30 && revealed.R == 38 && revealed.G == 47 && revealed.B == 54,
                "the fade ends on the fully visible game and restores its FPS limit");
            Frame(.05f); Check(session!.TimeOfDay == 9, "held narrative input cannot leak into combat on transition");
            Input(1, (int)KeyboardKey.Space); Frame(.05f);
            Check(session.TimeOfDay > 9, "gameplay resumes after the introduction");
            Press(KeyboardKey.Escape); Click(menu.ButtonBounds(0)); Frame(8); Frame(3);
            Check(narrative?.Playback.CardIndex == 2 && narrative.Playback.Card!.Duration == 6 &&
                narrative.Playback.Card.ScrollDirection == NarrativeScrollDirection.Up && narrative.Playback.Text.Contains("Zmieniony blok"),
                "a new game reloads edited blocks, image, duration and scroll direction");
            Capture("narrative-scroll-up", true);
            Frame(4);
            Check(narrative == null && gameTransition != null && gameTransition.BlackOpacity == 1,
                "automatic completion of the final scroll also passes through full black");
            FinishGameFade(); Press(KeyboardKey.Escape); Click(menu.ButtonBounds(0));
            Press(KeyboardKey.Escape);
            Check(narrative == null && gameTransition != null && session != null && !quit, "Escape skips the introduction into the game fade");
            FinishGameFade();
            Press(KeyboardKey.Escape); Click(menu.ButtonBounds(0)); Click(narrative!.SkipBounds);
            Check(narrative == null && session != null, "the skip button enters the game");
            FinishGameFade();
            Press(KeyboardKey.Escape); (deck with { Enabled = false }).Save(path); Click(menu.ButtonBounds(0));
            Check(narrative == null && session != null && activeFps == 30, "disabled narrative starts the game immediately");
            Press(KeyboardKey.Escape); (deck with { Cards = [deck.Cards[0] with { Image = "images/missing.png" }] }).Save(path);
            Click(menu.ButtonBounds(0));
            Check(narrative == null && session == null && menu.Message.Contains("missing.png"), "missing images leave a usable menu with a clear error");
            Check(File.ReadAllText(mapPath) == savedMap, "playing and editing narrative preserves the authored map");
            Console.WriteLine("Narrative interaction checks passed: New game, image/text layout, typewriter, fades, both scroll directions, full black/game fade-in, pause/input isolation, duration, edits/reload, resize, skip, disabled intro and invalid image recovery.");
        }
        finally { ReturnToMenu(); Directory.Delete(directory, true); }
    }
}
