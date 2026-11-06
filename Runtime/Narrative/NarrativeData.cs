using System.Text.Json;
using System.Text.Json.Serialization;

enum NarrativeEffect { Typewriter, Fade, Scroll }
enum NarrativeScrollDirection { Down, Up }

sealed record NarrativeCard
{
    public IReadOnlyList<string> TextBlocks { get; init; } = [];
    public string? Image { get; init; }
    public NarrativeEffect Effect { get; init; } = NarrativeEffect.Typewriter;
    public float Duration { get; init; } = 8;
    public float FadeIn { get; init; } = .6f;
    public float FadeOut { get; init; } = .6f;
    public float CharactersPerSecond { get; init; } = 35;
    public int FontSize { get; init; } = 26;
    public NarrativeScrollDirection ScrollDirection { get; init; } = NarrativeScrollDirection.Down;
    [JsonIgnore] public string Text => string.Join("\n\n", TextBlocks).Replace("\r\n", "\n").Replace('\r', '\n');
    [JsonIgnore] public float FadeInDuration => Math.Min(FadeIn, Duration / 2);
    [JsonIgnore] public float FadeOutDuration => Math.Min(FadeOut, Duration / 2);
}

sealed record NarrativeSequence
{
    public bool Enabled { get; init; } = true;
    public float BlackHold { get; init; } = .15f;
    public float GameFadeIn { get; init; } = 1.2f;
    public IReadOnlyList<NarrativeCard> Cards { get; init; } = [];
    public static string IntroFile => Path.Combine(ProjectPaths.Root, "Runtime", "Narrative", "intro.json");
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<NarrativeEffect>(allowIntegerValues: false),
            new JsonStringEnumConverter<NarrativeScrollDirection>(allowIntegerValues: false) }
    };
    public static NarrativeSequence Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var sequence = JsonSerializer.Deserialize<NarrativeSequence>(stream, JsonOptions)
                ?? throw new InvalidDataException("Plik kart jest pusty.");
            sequence.Validate();
            if (sequence.Enabled)
                foreach (var card in sequence.Cards)
                    if (!string.IsNullOrEmpty(card.Image) && !File.Exists(ImagePath(path, card.Image)))
                        throw new InvalidDataException("Nie znaleziono obrazu: " + card.Image);
            return sequence;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        { throw new InvalidDataException($"Nieprawidłowe karty fabularne w {path}: {ex.Message}", ex); }
    }
    public void Save(string path)
    {
        Validate(); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this, JsonOptions)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static string ImagePath(string sequencePath, string image)
    {
        if (!ProjectPaths.ValidAssetId(image)) throw new InvalidDataException("Obraz musi mieć ścieżkę względną wobec pliku kart.");
        return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sequencePath))!, image.Replace('/', Path.DirectorySeparatorChar));
    }
    public void Validate()
    {
        if (!MapData.FiniteRange(BlackHold, 0, 10) || !MapData.FiniteRange(GameFadeIn, .1f, 30))
            throw new InvalidDataException("BlackHold musi wynosić 0–10 s, a GameFadeIn 0.1–30 s.");
        if (Cards == null || Cards.Count > 128) throw new InvalidDataException("Sekwencja może zawierać najwyżej 128 kart.");
        for (int i = 0; i < Cards.Count; i++)
        {
            var card = Cards[i]; string prefix = $"Karta {i + 1}: ";
            if (card == null || card.TextBlocks == null || card.TextBlocks.Count > 64 ||
                card.TextBlocks.Any(t => t == null || t.Length > 20000) || card.TextBlocks.Sum(t => t.Length) > 40000 ||
                (!card.TextBlocks.Any(t => !string.IsNullOrWhiteSpace(t)) && string.IsNullOrEmpty(card.Image)))
                throw new InvalidDataException(prefix + "dodaj bloki tekstu lub obraz; limit tekstu to 40000 znaków.");
            if (!Enum.IsDefined(card.Effect) || !Enum.IsDefined(card.ScrollDirection) ||
                !MapData.FiniteRange(card.Duration, .1f, 3600) || !MapData.FiniteRange(card.FadeIn, 0, 30) ||
                !MapData.FiniteRange(card.FadeOut, 0, 30) || !MapData.FiniteRange(card.CharactersPerSecond, 1, 1000) || card.FontSize is < 14 or > 64)
                throw new InvalidDataException(prefix + "sprawdź efekt, czas, tempo pisania i wielkość tekstu.");
            if (!string.IsNullOrEmpty(card.Image) && (!ProjectPaths.ValidAssetId(card.Image) ||
                Path.GetExtension(card.Image).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga")))
                throw new InvalidDataException(prefix + "wybierz względną ścieżkę do obrazu PNG, JPG, BMP lub TGA.");
        }
    }
}
