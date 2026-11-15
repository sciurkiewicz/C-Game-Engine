using System.Globalization;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    bool waterSettingsOpen;
    Rectangle WaterSettingsBounds => new(GetScreenWidth() / 2 - 360, GetScreenHeight() / 2 - 300, 720, 600);
    void OpenWaterSettings()
    {
        EndEditorGesture(); assetSearch.Focused = false; waterSettingsOpen = true;
    }
    void UpdateWaterSettings()
    {
        if (IsKeyPressed(KeyboardKey.Escape)) { waterSettingsOpen = false; return; }
        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        if (ctrl && IsKeyPressed(KeyboardKey.Z)) Undo();
        if (ctrl && IsKeyPressed(KeyboardKey.Y)) Redo();
        if (ctrl && IsKeyPressed(KeyboardKey.S)) Save();
    }
    void ChangeWaterAppearance(WaterAppearance appearance)
    {
        appearance.Validate();
        if (document.Map.WaterAppearance == appearance) return;
        document.Checkpoint(); document.Map.WaterAppearance = appearance;
        status = "Wygląd wody zmieniony. Zapisz mapę, aby użyć go w grze.";
    }
    void WaterNumber(string label, float value, int x, int y, float step, float min, float max, Action<float> apply)
    {
        bool enabled = pendingAction == null;
        Label(label, x, y + 9, 13, Muted, 160);
        void Change(float delta)
        {
            float next = Math.Clamp(MathF.Round(value + delta, 3), min, max);
            if (next != value) apply(next);
        }
        if (Button("-", x + 168, y, 30, false, enabled && value > min, true)) Change(-step);
        Label(value.ToString("0.##", CultureInfo.InvariantCulture), x + 208, y + 9, 13, Text, 57);
        if (Button("+", x + 278, y, 30, false, enabled && value < max, true)) Change(step);
    }
    void DrawWaterSettings()
    {
        var box = WaterSettingsBounds; int x = (int)box.X, y = (int)box.Y;
        bool enabled = pendingAction == null;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 180));
        DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        Label("WYGLĄD WODY", x + 24, y + 22, 23, Accent);
        Label("Kolory i przejrzystość zapisują się z mapą. Ctrl+Z / Ctrl+Y: cofnij / ponów.", x + 24, y + 61, 13, Muted, 672);
        string[] names = ["Jezioro", "Rzeka", "Tropikalna", "Bagno"];
        WaterAppearance[] presets =
        [
            new(),
            new() { R = 80, G = 143, B = 142, DeepR = 24, DeepG = 72, DeepB = 77, Clarity = 2, ReflectionStrength = .55f, RefractionStrength = .4f },
            new() { R = 70, G = 202, B = 186, DeepR = 12, DeepG = 82, DeepB = 117, Clarity = 6, ReflectionStrength = .7f, RefractionStrength = .25f, FoamStrength = .35f },
            new() { R = 105, G = 129, B = 67, DeepR = 35, DeepG = 55, DeepB = 29, Clarity = .65f, ReflectionStrength = .45f, RefractionStrength = .15f, FoamStrength = .08f }
        ];
        for (int i = 0; i < names.Length; i++)
            if (Button(names[i], x + 24 + i * 172, y + 99, 156, document.Map.WaterAppearance == presets[i], enabled, true))
                ChangeWaterAppearance(presets[i]);

        var water = document.Map.WaterAppearance;
        int a = x + 24, b = x + 380;
        Label("KOLOR PŁYCIZNY", a, y + 157, 14, Text);
        Label("KOLOR GŁĘBI", b, y + 157, 14, Text);
        DrawRectangle(a + 248, y + 146, 60, 26, new(water.R,water.G,water.B,255));
        DrawRectangle(b + 248, y + 146, 60, 26, new(water.DeepR,water.DeepG,water.DeepB,255));
        bool precise = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);
        float colorStep = precise ? 1 : 8;
        WaterNumber("Czerwony / R", water.R, a, y + 188, colorStep, 0, 255, v => ChangeWaterAppearance(water with { R = (int)v }));
        WaterNumber("Zielony / G", water.G, a, y + 230, colorStep, 0, 255, v => ChangeWaterAppearance(water with { G = (int)v }));
        WaterNumber("Niebieski / B", water.B, a, y + 272, colorStep, 0, 255, v => ChangeWaterAppearance(water with { B = (int)v }));
        WaterNumber("Czerwony / R", water.DeepR, b, y + 188, colorStep, 0, 255, v => ChangeWaterAppearance(water with { DeepR = (int)v }));
        WaterNumber("Zielony / G", water.DeepG, b, y + 230, colorStep, 0, 255, v => ChangeWaterAppearance(water with { DeepG = (int)v }));
        WaterNumber("Niebieski / B", water.DeepB, b, y + 272, colorStep, 0, 255, v => ChangeWaterAppearance(water with { DeepB = (int)v }));
        DrawLine(x + 24, y + 325, x + 688, y + 325, Border);
        WaterNumber("Przejrzystość (m)", water.Clarity, a, y + 344, .25f, .1f, 64, v => ChangeWaterAppearance(water with { Clarity = v }));
        WaterNumber("Siła odbić", water.ReflectionStrength, b, y + 344, .05f, 0, 1, v => ChangeWaterAppearance(water with { ReflectionStrength = v }));
        WaterNumber("Załamanie obrazu", water.RefractionStrength, a, y + 390, .05f, 0, 1, v => ChangeWaterAppearance(water with { RefractionStrength = v }));
        WaterNumber("Piana przy brzegu", water.FoamStrength, b, y + 390, .05f, 0, 1, v => ChangeWaterAppearance(water with { FoamStrength = v }));
        WaterNumber("Wysokość fal (m)", document.Map.WaveHeight, a, y + 436, .01f, 0, .25f, v =>
        { document.Checkpoint(); document.Map.WaveHeight = v; });
        Label("Większa przejrzystość odsłania więcej dna.", b, y + 445, 12, Muted, 308);
        Label("Shift: regulacja RGB co 1. Zmiany widać od razu na mapie.", x + 24, y + 491, 13, Muted, 664);
        DrawLine(x + 24, y + 527, x + 688, y + 527, Border);
        if (Button("Domyślne", x + 24, y + 548, 124, false, enabled, true)) ChangeWaterAppearance(new());
        if (Button("Zapisz mapę", x + 402, y + 548, 132, false, enabled, true)) Save();
        if (Button("Zamknij / Esc", x + 548, y + 548, 140, true, enabled, true)) waterSettingsOpen = false;
    }
}
