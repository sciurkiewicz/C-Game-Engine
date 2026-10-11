using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    string? monsterSettingsId;
    MonsterData? EditedMonster => document.Map.Monsters.FirstOrDefault(m => m.Id == monsterSettingsId);
    Rectangle MonsterSettingsBounds => new(GetScreenWidth() / 2 - 290, GetScreenHeight() / 2 - 200, 580, 400);
    void UpdateMonsterSettings()
    {
        if (IsKeyPressed(KeyboardKey.Escape) || IsKeyPressed(KeyboardKey.Enter)) { monsterSettingsId = null; return; }
        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        if (ctrl && IsKeyPressed(KeyboardKey.Z)) Undo();
        if (ctrl && IsKeyPressed(KeyboardKey.Y)) Redo();
        if (ctrl && IsKeyPressed(KeyboardKey.S)) Save();
        if (EditedMonster == null) monsterSettingsId = null;
    }
    void MonsterStepper(string label, float value, int x, int y, float step, float min, float max, Action<float> change)
    {
        Label(label, x, y, 13, Muted);
        void Apply(float proposed)
        {
            float next = Math.Clamp(MathF.Round(proposed, 2), min, max);
            if (next == value) return;
            document.Checkpoint(); change(next);
        }
        if (Button("-", x, y + 24, 34, false, value > min, true)) Apply(value - step);
        Label(value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), x + 72, y + 33, 17, Text);
        if (Button("+", x + 184, y + 24, 34, false, value < max, true)) Apply(value + step);
    }
    void DrawMonsterSettings()
    {
        if (EditedMonster is not { } monster) { monsterSettingsId = null; return; }
        var box = MonsterSettingsBounds; int x = (int)box.X, y = (int)box.Y;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new(0, 0, 0, 170));
        DrawRectangleRec(box, Panel); DrawRectangleLinesEx(box, 1, Accent);
        Label("MONSTER / " + monster.Name, x + 24, y + 20, 22, Accent, 532);
        Label("Ustawienia walki zapisują się razem z mapą.", x + 24, y + 56, 14, Muted);
        MonsterStepper("HP", monster.MaxHealth, x + 24, y + 94, 5, 1, 10000, v => monster.MaxHealth = (int)v);
        MonsterStepper("Obrażenia", monster.Damage, x + 314, y + 94, 1, 1, 1000, v => monster.Damage = (int)v);
        MonsterStepper("Prędkość (m/s)", monster.MoveSpeed, x + 24, y + 168, .2f, .25f, 10, v => monster.MoveSpeed = v);
        MonsterStepper("Zasięg wykrywania (m)", monster.DetectionRange, x + 314, y + 168, 1, 1, 64, v => monster.DetectionRange = v);
        MonsterStepper("Zasięg ataku (m)", monster.AttackRange, x + 24, y + 242, .25f, .5f, 5, v => monster.AttackRange = v);
        MonsterStepper("Przerwa między atakami (s)", monster.AttackCooldown, x + 314, y + 242, .1f, .2f, 10, v => monster.AttackCooldown = v);
        Label("W grze: Spacja = atak. Po śmierci: Enter = restart.", x + 24, y + 317, 14, Text);
        if (Button("Cofnij", x + 24, y + 352, 100, false, document.CanUndo, true)) Undo();
        if (Button("Zamknij", x + 432, y + 352, 124, true, true, true)) monsterSettingsId = null;
    }
}
