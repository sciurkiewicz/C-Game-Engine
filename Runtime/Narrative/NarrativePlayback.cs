using System.Globalization;

// Timing and text reveal are independent of Raylib, frame rate and the game simulation.
sealed class NarrativePlayback
{
    readonly NarrativeCard[] cards;
    int[] textElements = [];
    double elapsed, finishElapsed;
    float finishAlpha;
    bool finishing, textRevealed;
    public int CardIndex { get; private set; }
    public bool Active => CardIndex < cards.Length;
    public bool IsLastCard => Active && CardIndex == cards.Length - 1;
    public NarrativeCard? Card => Active ? cards[CardIndex] : null;
    public string Text { get; private set; } = "";
    public float Elapsed => (float)elapsed;
    public float Progress => Card is { } card ? Math.Clamp(Elapsed / card.Duration, 0, 1) : 1;
    public float ScrollProgress => Card is { } card ? Math.Clamp((Elapsed - card.FadeInDuration) /
        Math.Max(.001f, card.Duration - card.FadeInDuration - card.FadeOutDuration), 0, 1) : 1;
    public int VisibleCharacterIndex
    {
        get
        {
            if (Card is not { Effect: NarrativeEffect.Typewriter } card || textRevealed) return Text.Length;
            int count = (int)Math.Min(textElements.Length, Math.Max(0, elapsed - card.FadeInDuration) * card.CharactersPerSecond);
            return count == textElements.Length ? Text.Length : textElements[count];
        }
    }
    public bool TextComplete => VisibleCharacterIndex == Text.Length;
    public float FadeOutOpacity
    {
        get
        {
            if (Card is not { } card) return 0;
            float alpha = card.FadeOutDuration == 0 ? 1 : (card.Duration - Elapsed) / card.FadeOutDuration;
            if (finishing) alpha = Math.Min(alpha, 1 - (float)finishElapsed / Math.Max(.001f, card.FadeOutDuration));
            return Math.Clamp(alpha, 0, 1);
        }
    }
    public float Opacity
    {
        get
        {
            if (Card is not { } card) return 0;
            float alpha = Math.Min(card.FadeInDuration == 0 ? 1 : Elapsed / card.FadeInDuration, FadeOutOpacity);
            if (finishing) alpha = Math.Min(alpha, finishAlpha * (1 - (float)finishElapsed / Math.Max(.001f, card.FadeOutDuration)));
            return Math.Clamp(alpha, 0, 1);
        }
    }
    public NarrativePlayback(NarrativeSequence sequence)
    {
        sequence.Validate();
        cards = sequence.Enabled ? sequence.Cards.Select(c => c with { TextBlocks = c.TextBlocks.ToArray() }).ToArray() : [];
        PrepareCard();
    }
    void PrepareCard()
    {
        elapsed = finishElapsed = 0; finishing = textRevealed = false;
        Text = Card?.Text ?? ""; textElements = StringInfo.ParseCombiningCharacters(Text);
    }
    void NextCard() { CardIndex++; PrepareCard(); }
    public void Update(float dt)
    {
        if (!float.IsFinite(dt) || dt <= 0) return;
        double remainingTime = dt;
        while (Active && remainingTime > 0)
        {
            var card = Card!;
            double remainingCard = Math.Max(0, card.Duration - elapsed);
            if (finishing) remainingCard = Math.Min(remainingCard, Math.Max(0, card.FadeOutDuration - finishElapsed));
            double step = Math.Min(remainingTime, remainingCard);
            elapsed += step; if (finishing) finishElapsed += step;
            remainingTime -= step;
            if (step >= remainingCard) NextCard();
        }
    }
    public void Advance()
    {
        if (!Active || finishing) return;
        if (!TextComplete) { textRevealed = true; return; }
        if (Card!.FadeOutDuration == 0) { NextCard(); return; }
        finishAlpha = Opacity; finishing = true; finishElapsed = 0;
    }
    public void Skip() { CardIndex = cards.Length; PrepareCard(); }
}
