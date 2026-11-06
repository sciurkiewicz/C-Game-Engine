using Raylib_cs;
using static Raylib_cs.Raylib;

enum MenuAction { None, NewGame, Quit }

sealed class MainMenu
{
    static readonly Color Background = new(18, 23, 29, 255), Panel = new(31, 39, 48, 255),
        Hover = new(43, 56, 66, 255), Accent = new(234, 200, 128, 255), Text = new(231, 235, 239, 255),
        Muted = new(143, 152, 162, 255), Disabled = new(85, 94, 104, 255);
    static readonly string[] MainLabels = ["Nowa gra", "Wczytaj grę", "Opcje", "Wyjdź z gry"];
    int selected;
    public bool OptionsOpen { get; private set; }
    public float Volume { get; private set; } = 1;
    public string Message { get; set; } = "";
    public Rectangle ButtonBounds(int index) => new(GetScreenWidth() / 2f - 170, GetScreenHeight() / 2f - 70 + index * 60, 340, 48);

    public MenuAction Update()
    {
        if (OptionsOpen && IsKeyPressed(KeyboardKey.Escape)) { CloseOptions(); return MenuAction.None; }
        int count = OptionsOpen ? 3 : MainLabels.Length;
        int direction = IsKeyPressed(KeyboardKey.Down) ? 1 : IsKeyPressed(KeyboardKey.Up) ? -1 : 0;
        if (direction != 0)
        {
            do { selected = (selected + count + direction) % count; }
            while (!OptionsOpen && selected == 1);
        }
        bool click = IsMouseButtonPressed(MouseButton.Left);
        var mouse = GetMousePosition();
        int clicked = -1;
        for (int i = 0; i < count; i++)
        {
            if (!OptionsOpen && i == 1 || !CheckCollisionPointRec(mouse, ButtonBounds(i))) continue;
            if (click || GetMouseDelta().LengthSquared() > 0) selected = i;
            if (click) clicked = i;
        }
        if (OptionsOpen && selected == 0)
        {
            var row = ButtonBounds(0);
            if (IsKeyPressed(KeyboardKey.Left) || clicked == 0 && mouse.X < row.X + 54) ChangeVolume(-.1f);
            if (IsKeyPressed(KeyboardKey.Right) || clicked == 0 && mouse.X > row.X + row.Width - 54) ChangeVolume(.1f);
        }
        int activated = clicked >= 0 ? clicked : IsKeyPressed(KeyboardKey.Enter) ? selected : -1;
        if (OptionsOpen)
        {
            if (activated == 1) ToggleBorderlessWindowed();
            else if (activated == 2) CloseOptions();
        }
        else
        {
            if (activated == 0) return MenuAction.NewGame;
            if (activated == 2) { OptionsOpen = true; selected = 0; }
            if (activated == 3) return MenuAction.Quit;
        }
        return MenuAction.None;
    }
    void ChangeVolume(float amount) => Volume = Math.Clamp(MathF.Round((Volume + amount) * 10) / 10, 0, 1);
    void CloseOptions() { OptionsOpen = false; selected = 2; }
    public void Draw()
    {
        ClearBackground(Background);
        Center(OptionsOpen ? "OPCJE" : "WILDWOOD", GetScreenHeight() / 2 - 185, 42, Accent);
        int count = OptionsOpen ? 3 : MainLabels.Length;
        for (int i = 0; i < count; i++)
        {
            var box = ButtonBounds(i);
            bool disabled = !OptionsOpen && i == 1;
            bool active = !disabled && (i == selected || CheckCollisionPointRec(GetMousePosition(), box));
            DrawRectangleRec(box, disabled ? new(24, 30, 36, 255) : active ? Hover : Panel);
            DrawRectangleLinesEx(box, 1, disabled ? new(35, 42, 49, 255) : active ? Accent : new(51, 61, 72, 255));
            string label = OptionsOpen ? i switch
            {
                0 => $"Głośność: {Volume * 100:0}%",
                1 => "Pełny ekran: " + (IsWindowState(ConfigFlags.BorderlessWindowMode) ? "WŁ." : "WYŁ."),
                _ => "Powrót"
            } : MainLabels[i];
            Center(label, (int)box.Y + 13, 22, disabled ? Disabled : Text);
            if (OptionsOpen && i == 0)
            {
                UiText.Draw("-", (int)box.X + 20, (int)box.Y + 11, 26, Volume > 0 ? Accent : Disabled);
                UiText.Draw("+", (int)(box.X + box.Width) - 36, (int)box.Y + 11, 26, Volume < 1 ? Accent : Disabled);
            }
        }
        if (!OptionsOpen && Message.Length > 0)
        {
            var lines = UiText.Wrap(Message, 14, Math.Min(650, GetScreenWidth() - 48));
            for (int i = 0; i < Math.Min(3, lines.Count); i++) Center(lines[i], (int)ButtonBounds(3).Y + 60 + i * 18, 14, new(229, 139, 128, 255));
        }
        Center(OptionsOpen ? "Lewo / prawo: głośność     Esc: powrót" : "Mysz lub strzałki + Enter", GetScreenHeight() - 32, 14, Muted);
    }
    static void Center(string text, int y, int size, Color color) =>
        UiText.Draw(text, (GetScreenWidth() - UiText.Measure(text, size)) / 2, y, size, color);
}
