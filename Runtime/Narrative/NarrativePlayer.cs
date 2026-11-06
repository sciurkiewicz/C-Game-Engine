using System.Globalization;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed class NarrativePlayer : IDisposable
{
    readonly string sequencePath;
    Texture2D texture;
    int loadedCard = -1, layoutCard = -1, layoutWidth, layoutHeight, fontSize;
    float scrollOffset, textHeight, imageWidth, imageHeight, gap, lineHeight;
    List<(string Text, int Start)> lines = [];
    Rectangle content;
    public NarrativePlayback Playback { get; }
    public NarrativeGameTransition GameTransition { get; }
    public bool Active => Playback.Active;
    public Rectangle SkipBounds => new(GetScreenWidth() - 136, GetScreenHeight() - 45, 112, 30);
    public NarrativePlayer(NarrativeSequence sequence, string path)
    {
        sequencePath = path; Playback = new(sequence); GameTransition = new(sequence.BlackHold, sequence.GameFadeIn);
        try { PrepareCard(); }
        catch { Dispose(); throw; }
    }
    void PrepareCard()
    {
        if (!Active || loadedCard == Playback.CardIndex) return;
        if (texture.Id != 0) UnloadTexture(texture);
        texture = default; loadedCard = Playback.CardIndex; layoutCard = -1; scrollOffset = 0;
        if (string.IsNullOrEmpty(Playback.Card!.Image)) return;
        string path = NarrativeSequence.ImagePath(sequencePath, Playback.Card.Image);
        var image = LoadImage(path);
        try
        {
            if (image.Width <= 0 || image.Height <= 0) throw new InvalidDataException("Nie można odczytać obrazu karty: " + path);
            if (Math.Max(image.Width, image.Height) > 2048)
            {
                float scale = 2048f / Math.Max(image.Width, image.Height);
                ImageResize(ref image, Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
            }
            texture = LoadTextureFromImage(image);
            if (!IsTextureValid(texture)) throw new InvalidDataException("Nie można wyświetlić obrazu karty: " + path);
            SetTextureFilter(texture, TextureFilter.Bilinear);
        }
        finally { UnloadImage(image); }
    }
    public void Update(float dt)
    {
        if (IsKeyPressed(KeyboardKey.Escape) || IsMouseButtonPressed(MouseButton.Left) && CheckCollisionPointRec(GetMousePosition(), SkipBounds))
        { Playback.Skip(); return; }
        if (IsKeyPressed(KeyboardKey.Space) || IsKeyPressed(KeyboardKey.Enter) || IsMouseButtonPressed(MouseButton.Left)) Playback.Advance();
        Playback.Update(dt); PrepareCard();
        if (Playback.Card?.Effect != NarrativeEffect.Scroll) scrollOffset -= GetMouseWheelMove() * 48;
    }
    void Layout(Rectangle bounds)
    {
        if (layoutCard == Playback.CardIndex && layoutWidth == (int)bounds.Width && layoutHeight == (int)bounds.Height) return;
        layoutCard = Playback.CardIndex; layoutWidth = (int)bounds.Width; layoutHeight = (int)bounds.Height;
        float margin = Math.Clamp(bounds.Width * .08f, 24, 100), width = Math.Min(940, bounds.Width - margin * 2);
        content = new(bounds.X + (bounds.Width - width) / 2, bounds.Y + 36, width, bounds.Height - 100);
        float scale = Math.Clamp(Math.Min(bounds.Width / 1280, bounds.Height / 800), .75f, 1.25f);
        fontSize = Math.Clamp((int)MathF.Round(Playback.Card!.FontSize * scale), 14, 72);
        lineHeight = fontSize * 1.45f;
        lines = Wrap(Playback.Text, fontSize, (int)content.Width);
        textHeight = Playback.Text.Length == 0 ? 0 : lines.Count * lineHeight;
        imageWidth = imageHeight = gap = 0;
        if (texture.Id == 0) return;
        float height = Math.Min(340, content.Height * .4f);
        if (Playback.Card.Effect != NarrativeEffect.Scroll) height = Math.Min(height, Math.Max(60, content.Height - textHeight - 28));
        float imageScale = Math.Min(content.Width / texture.Width, height / texture.Height);
        imageWidth = texture.Width * imageScale; imageHeight = texture.Height * imageScale;
        gap = textHeight > 0 ? 28 * scale : 0;
    }
    // Keep full-line positions stable during typing, and never wrap inside a Unicode text element.
    static List<(string Text, int Start)> Wrap(string text, int size, int width)
    {
        var result = new List<(string, int)>(); int start = 0;
        int[] elements = StringInfo.ParseCombiningCharacters(text);
        for (int i = 0; i < elements.Length; i++)
        {
            int at = elements[i], end = i + 1 < elements.Length ? elements[i + 1] : text.Length;
            if (text[at] == '\n')
            { result.Add((text[start..at], start)); start = end; continue; }
            if (at == start || UiText.Measure(text[start..end], size) <= width) continue;
            int space = text.LastIndexOf(' ', at - 1, at - start);
            int stop = space >= start ? space : at;
            result.Add((text[start..stop], start)); start = space >= start ? space + 1 : at;
        }
        result.Add((text[start..], start)); return result;
    }
    public void Draw(Rectangle bounds)
    {
        float exit = Playback.IsLastCard ? Playback.FadeOutOpacity : 1;
        ClearBackground(new((int)MathF.Round(12 * exit), (int)MathF.Round(16 * exit), (int)MathF.Round(21 * exit), 255));
        if (!Active) return;
        Layout(bounds);
        float totalHeight = imageHeight + gap + textHeight;
        float y;
        if (Playback.Card!.Effect == NarrativeEffect.Scroll)
        {
            float above = content.Y - totalHeight, below = content.Y + content.Height;
            y = Playback.Card.ScrollDirection == NarrativeScrollDirection.Down
                ? above + (below - above) * Playback.ScrollProgress : below + (above - below) * Playback.ScrollProgress;
        }
        else
        {
            scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, totalHeight - content.Height));
            y = content.Y + Math.Max(0, (content.Height - totalHeight) / 2) - scrollOffset;
        }
        byte alpha = (byte)MathF.Round(Playback.Opacity * 255);
        BeginScissorMode((int)content.X, (int)content.Y, (int)content.Width, (int)content.Height);
        if (texture.Id != 0)
        {
            DrawTexturePro(texture, new(0, 0, texture.Width, texture.Height),
                new(content.X + (content.Width - imageWidth) / 2, y, imageWidth, imageHeight), Vector2.Zero, 0, new(255, 255, 255, (int)alpha));
            y += imageHeight + gap;
        }
        int visible = Playback.VisibleCharacterIndex;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i]; int length = Math.Clamp(visible - line.Start, 0, line.Text.Length);
            if (length == 0) continue;
            int x = (int)(content.X + (content.Width - UiText.Measure(line.Text, fontSize)) / 2);
            UiText.Draw(line.Text[..length], x, (int)(y + i * lineHeight), fontSize, new(233, 235, 238, (int)alpha));
        }
        EndScissorMode();
        UiText.Draw(Playback.TextComplete ? "Spacja / Enter: dalej" : "Spacja / Enter: pokaż cały tekst", (int)bounds.X + 24,
            (int)(bounds.Y + bounds.Height) - 34, 14, new(130, 139, 151, (int)MathF.Round(255 * exit)));
        var skip = SkipBounds; bool hover = CheckCollisionPointRec(GetMousePosition(), skip);
        int controlsAlpha = (int)MathF.Round(255 * exit);
        DrawRectangleRec(skip, hover ? new(43, 52, 64, controlsAlpha) : new(25, 32, 42, controlsAlpha));
        UiText.Draw("Pomiń / Esc", (int)skip.X + 13, (int)skip.Y + 7, 14, new(180, 188, 199, controlsAlpha));
    }
    public void Dispose()
    {
        if (texture.Id != 0) UnloadTexture(texture);
        texture = default;
    }
}
