// The world stays paused through the black frame and the reveal of the game screen.
sealed class NarrativeGameTransition
{
    readonly float blackHold, fadeIn;
    double elapsed;
    public float Duration => blackHold + fadeIn;
    public bool Active => elapsed < Duration;
    public float BlackOpacity
    {
        get
        {
            float progress = Math.Clamp(((float)elapsed - blackHold) / fadeIn, 0, 1);
            return 1 - progress * progress * (3 - 2 * progress);
        }
    }
    public NarrativeGameTransition(float blackHold, float fadeIn)
    {
        if (!MapData.FiniteRange(blackHold, 0, 10) || !MapData.FiniteRange(fadeIn, .1f, 30))
            throw new ArgumentOutOfRangeException(nameof(fadeIn), "Invalid narrative transition duration.");
        this.blackHold = blackHold; this.fadeIn = fadeIn;
    }
    public void Update(float dt)
    {
        if (float.IsFinite(dt) && dt > 0) elapsed = Math.Min(Duration, elapsed + dt);
    }
}
