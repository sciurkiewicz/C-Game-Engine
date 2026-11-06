using System.Globalization;

static class NarrativeChecks
{
    public static void Run()
    {
        var typing = new NarrativeCard { TextBlocks = ["Z\u0307o\u0301łw 👩‍🚀", "Drugi blok."], Duration = 6,
            FadeIn = .25f, FadeOut = .5f, CharactersPerSecond = 2 };
        var fade = new NarrativeCard { TextBlocks = ["Pełny tekst."], Effect = NarrativeEffect.Fade, Duration = 2, FadeIn = .5f, FadeOut = .5f };
        var scroll = new NarrativeCard { TextBlocks = ["Pierwszy wiersz", "Ostatni wiersz"], Effect = NarrativeEffect.Scroll,
            Duration = 3, FadeIn = .5f, FadeOut = .5f };
        var sequence = new NarrativeSequence { Cards = [typing, fade, scroll] };
        var play = new NarrativePlayback(sequence);
        Check(play.Active && play.Opacity == 0 && play.VisibleCharacterIndex == 0, "first card begins with a fade from black");
        play.Update(.25f); Check(play.Opacity == 1 && !play.TextComplete, "fade-in precedes typing");
        play.Update(.5f); Check(play.Text[..play.VisibleCharacterIndex] == "Z\u0307", "typing preserves a combining character");
        play.Update(.5f); Check(play.Text[..play.VisibleCharacterIndex] == "Z\u0307o\u0301", "configured typing speed reveals whole text elements");
        play.Advance(); Check(play.TextComplete && play.CardIndex == 0, "first advance reveals the complete current text");
        play.Advance(); play.Advance(); play.Update(.25f);
        Check(play.CardIndex == 0 && Math.Abs(play.Opacity - .5f) < .001f, "manual advance fades out without skipping another card");
        play.Update(.25f); Check(play.CardIndex == 1 && play.Opacity == 0 && play.TextComplete, "next fade card starts at zero opacity with complete text");
        play.Update(.25f); Check(Math.Abs(play.Opacity - .5f) < .001f, "fade-in has intermediate opacity");
        play.Update(1.5f); Check(Math.Abs(play.Opacity - .5f) < .001f, "fade-out has intermediate opacity");
        play.Update(.25f); Check(play.CardIndex == 2, "duration automatically advances to a scrolling card");
        play.Update(1.5f); Check(Math.Abs(play.ScrollProgress - .5f) < .001f, "scrolling traverses the viewport during the visible interval");
        play.Update(1.25f); Check(play.IsLastCard && Math.Abs(play.FadeOutOpacity - .5f) < .001f,
            "the final scrolling card fades its complete presentation before the game handoff");
        play.Update(.25f); Check(!play.Active, "final duration completes playback");
        var transition = new NarrativeGameTransition(.25f, 1);
        Check(transition.Active && transition.BlackOpacity == 1, "game handoff begins at full black");
        transition.Update(.25f); Check(transition.BlackOpacity == 1, "black remains visible for its configured hold time");
        transition.Update(.5f); Check(Math.Abs(transition.BlackOpacity - .5f) < .001f, "game is half revealed halfway through fade-in");
        transition.Update(.5f); Check(!transition.Active && transition.BlackOpacity == 0, "handoff finishes on the visible game");
        foreach (int fps in new[] { 30, 60, 144 })
        {
            var timedTransition = new NarrativeGameTransition(.25f, 1);
            for (int i = 0; i < fps * 2; i++) timedTransition.Update(1f / fps);
            Check(!timedTransition.Active && timedTransition.BlackOpacity == 0, "game reveal duration works at " + fps + " FPS");
        }
        foreach (int fps in new[] { 30, 60, 144 })
        {
            var timed = new NarrativePlayback(sequence);
            for (int i = 0; i < fps * 7; i++) timed.Update(1f / fps);
            Check(timed.CardIndex == 1 && Math.Abs(timed.Elapsed - 1) < .001f, "timing is independent of frame rate at " + fps);
        }
        var largeStep = new NarrativePlayback(sequence); largeStep.Update(20);
        Check(!largeStep.Active, "large time steps carry through all card boundaries");
        var unicode = new NarrativePlayback(sequence);
        int[] boundaries = StringInfo.ParseCombiningCharacters(unicode.Text);
        for (int i = 0; i < 150; i++)
        {
            unicode.Update(.02f);
            Check(boundaries.Contains(unicode.VisibleCharacterIndex) || unicode.VisibleCharacterIndex == unicode.Text.Length,
                "typing never reveals half an emoji or combining sequence");
        }
        float before = unicode.Elapsed; unicode.Update(float.NaN); unicode.Update(-1); unicode.Update(0);
        Check(unicode.Elapsed == before, "invalid time steps leave playback unchanged");
        unicode.Skip(); Check(!unicode.Active, "skip completes the entire sequence");
        Check(!new NarrativePlayback(sequence with { Enabled = false }).Active && !new NarrativePlayback(new()).Active,
            "disabled or empty sequences start gameplay immediately");
        var shortCard = new NarrativePlayback(new() { Cards = [fade with { Duration = .2f, FadeIn = 5, FadeOut = 5 }] });
        shortCard.Update(.1f); Check(shortCard.Opacity > .999f, "short card fades fit its configured duration");

        string directory = Path.Combine(Path.GetTempPath(), "wildwood-narrative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "intro.json"); sequence.Save(path);
            var loaded = NarrativeSequence.Load(path);
            Check(loaded.Cards.Count == 3 && loaded.Cards[0].Text == typing.Text && loaded.Cards[2].Effect == NarrativeEffect.Scroll,
                "editable text blocks, effects and durations survive save/load");
            string original = File.ReadAllText(path);
            ExpectInvalid(() => (sequence with { BlackHold = -1 }).Save(path));
            ExpectInvalid(() => (sequence with { GameFadeIn = float.NaN }).Save(path));
            ExpectInvalid(() => (sequence with { GameFadeIn = 0 }).Save(path));
            foreach (var invalid in new[] { typing with { Duration = 0 }, typing with { Duration = float.NaN },
                typing with { FadeIn = -1 }, typing with { FadeOut = float.PositiveInfinity }, typing with { CharactersPerSecond = 0 },
                typing with { FontSize = 3 }, typing with { Image = "../outside.png" }, typing with { Image = "/outside.png" },
                typing with { Image = "image.exe" }, typing with { TextBlocks = [null!] }, typing with { TextBlocks = [] },
                typing with { Effect = (NarrativeEffect)90 }, typing with { TextBlocks = [new string('x', 20001)] } })
                ExpectInvalid(() => (sequence with { Cards = [invalid] }).Save(path));
            Check(File.ReadAllText(path) == original, "invalid edits preserve the last valid file");
            File.WriteAllText(path, "{\"Cards\":[{\"TextBlocks\":[\"Tekst\"],\"Effect\":\"unknown\"}]}");
            ExpectInvalid(() => NarrativeSequence.Load(path));
            File.WriteAllText(path, "{\"Cards\":[{\"TextBlocks\":[\"Tekst\"],\"Duraton\":7}]}");
            ExpectInvalid(() => NarrativeSequence.Load(path));
            (sequence with { Cards = [typing with { Image = "missing.png" }] }).Save(path);
            ExpectInvalid(() => NarrativeSequence.Load(path));
            (sequence with { Enabled = false, Cards = [typing with { Image = "missing.png" }] }).Save(path);
            Check(!NarrativeSequence.Load(path).Enabled, "disabled intros do not require their images");
            var blocks = new[] { "Oryginalny tekst" };
            var snapshot = new NarrativePlayback(new() { Cards = [typing with { TextBlocks = blocks }] }); blocks[0] = "Nowy tekst";
            Check(snapshot.Text == "Oryginalny tekst", "active playback is independent of later source edits");
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine("Narrative checks passed: text blocks/JSON, validation, Unicode typing, fades, scrolling, duration, frame rates, advance/skip and frozen playback.");
    }
    static void ExpectInvalid(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid narrative configuration accepted.");
    }
    static void Check(bool valid, string message)
    { if (!valid) throw new InvalidOperationException("Narrative check failed: " + message); }
}
