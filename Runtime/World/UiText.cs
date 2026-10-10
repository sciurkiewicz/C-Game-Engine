using System.Numerics;
using System.Text;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class UiText
{
    static Font font;
    static bool loaded;
    public static void Load()
    {
        // Use a system font with Polish glyphs without adding a platform-specific asset to maps.
        string? path = new[] {
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf")
        }.FirstOrDefault(File.Exists);
        if (path == null) return;
        font = LoadFontEx(path, 24, Enumerable.Range(32, 352).ToArray(), 352);
        loaded = font.Texture.Id != 0;
    }
    public static void Unload() { if (loaded) UnloadFont(font); loaded = false; }
    public static int Measure(string text, int size) => loaded ? (int)MathF.Ceiling(MeasureTextEx(font, text, size, 1).X) : MeasureText(text, size);
    public static void Draw(string text, int x, int y, int size, Color color)
    {
        if (loaded) DrawTextEx(font, text, new Vector2(x, y), size, 1, color);
        else DrawText(text, x, y, size, color);
    }
    public static List<string> Wrap(string text, int size, int width)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            string line = "";
            foreach (Rune rune in paragraph.EnumerateRunes())
            {
                string next = line + rune;
                if (line.Length > 0 && Measure(next, size) > width)
                {
                    int space = line.LastIndexOf(' ');
                    if (space > 0) { lines.Add(line[..space]); line = line[(space + 1)..] + rune; }
                    else { lines.Add(line); line = rune.ToString(); }
                }
                else line = next;
            }
            lines.Add(line);
        }
        return lines;
    }
}
