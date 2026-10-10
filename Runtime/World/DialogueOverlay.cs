using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed class DialogueOverlay
{
    string? nodeId;
    int choiceScroll, textScroll, selectedChoice;
    static readonly Color Panel = new(20, 28, 38, 248), Text = new(230, 238, 242, 255),
        Accent = new(244, 192, 96, 255), Muted = new(150, 170, 186, 255);
    public void Draw(PlaySession play, Rectangle bounds)
    {
        var dialogue = play.Dialogue;
        if (!dialogue.Active)
        {
            nodeId = null;
            if (play.NearbyNpc is { } nearby)
            {
                string hint = $"E  Porozmawiaj: {nearby.Name}";
                int width = Math.Min((int)bounds.Width - 24, UiText.Measure(hint, 18) + 32);
                int hx = (int)(bounds.X + (bounds.Width - width) / 2), hy = (int)(bounds.Y + bounds.Height - 58);
                DrawRectangle(hx, hy, width, 38, Panel);
                UiText.Draw(hint, hx + 16, hy + 10, 18, Accent);
            }
            return;
        }
        var current = dialogue.Current!;
        if (nodeId != current.Id) { nodeId = current.Id; choiceScroll = textScroll = selectedChoice = 0; }
        int w = Math.Min(900, (int)bounds.Width - 32), h = Math.Min(420, (int)bounds.Height - 48);
        int x = (int)(bounds.X + (bounds.Width - w) / 2), y = (int)(bounds.Y + bounds.Height - h - 20);
        DrawRectangle(x, y, w, h, Panel);
        DrawRectangleLines(x, y, w, h, Accent);
        UiText.Draw(dialogue.Speaker!.Name, x + 20, y + 16, 22, Accent);
        UiText.Draw("Esc: zamknij", x + w - 130, y + 22, 14, Muted);
        var textBox = new Rectangle(x + 20, y + 52, w - 40, 110);
        var lines = UiText.Wrap(current.Text, 18, w - 48);
        if (CheckCollisionPointRec(GetMousePosition(), textBox)) textScroll -= (int)(GetMouseWheelMove() * 30);
        textScroll = Math.Clamp(textScroll, 0, Math.Max(0, lines.Count * 23 - (int)textBox.Height));
        BeginScissorMode((int)textBox.X, (int)textBox.Y, (int)textBox.Width, (int)textBox.Height);
        for (int i = 0; i < lines.Count; i++) UiText.Draw(lines[i], x + 20, y + 52 + i * 23 - textScroll, 18, Text);
        EndScissorMode();
        DrawLine(x + 20, y + 172, x + w - 20, y + 172, Muted);
        var choices = dialogue.Choices;
        if (choices.Count == 0)
        {
            var close = new Rectangle(x + 20, y + h - 65, w - 40, 40);
            DrawRectangleRec(close, new Color(48, 64, 78, 255));
            UiText.Draw("Enter / kliknij: zakończ rozmowę", x + 34, (int)close.Y + 11, 17, Text);
            if (IsKeyPressed(KeyboardKey.Enter) || (IsMouseButtonPressed(MouseButton.Left) && CheckCollisionPointRec(GetMousePosition(), close))) dialogue.Close();
            return;
        }
        var choiceBox = new Rectangle(x + 20, y + 184, w - 40, h - 220);
        var rows = choices.Select((c, i) => UiText.Wrap($"{i + 1}. {c.Text}", 17, w - 70)).ToArray();
        int[] heights = rows.Select(r => Math.Max(38, r.Count * 22 + 14)).ToArray();
        bool moved = false;
        if (IsKeyPressed(KeyboardKey.Down) || IsKeyPressedRepeat(KeyboardKey.Down)) { selectedChoice = (selectedChoice + 1) % choices.Count; moved = true; }
        if (IsKeyPressed(KeyboardKey.Up) || IsKeyPressedRepeat(KeyboardKey.Up)) { selectedChoice = (selectedChoice + choices.Count - 1) % choices.Count; moved = true; }
        int selectedTop = heights.Take(selectedChoice).Sum() + selectedChoice * 6;
        if (moved)
        {
            if (selectedTop < choiceScroll) choiceScroll = selectedTop;
            if (selectedTop + heights[selectedChoice] > choiceScroll + choiceBox.Height)
                choiceScroll = selectedTop + heights[selectedChoice] - (int)choiceBox.Height;
        }
        if (CheckCollisionPointRec(GetMousePosition(), choiceBox)) choiceScroll -= (int)(GetMouseWheelMove() * 35);
        choiceScroll = Math.Clamp(choiceScroll, 0, Math.Max(0, heights.Sum() + (choices.Count - 1) * 6 - (int)choiceBox.Height));
        int choose = IsKeyPressed(KeyboardKey.Enter) ? selectedChoice : -1;
        for (int i = 0; i < Math.Min(9, choices.Count); i++)
            if (IsKeyPressed((KeyboardKey)((int)KeyboardKey.One + i))) choose = i;
        BeginScissorMode((int)choiceBox.X, (int)choiceBox.Y, (int)choiceBox.Width, (int)choiceBox.Height);
        int rowY = (int)choiceBox.Y - choiceScroll;
        for (int i = 0; i < choices.Count; i++)
        {
            var rect = new Rectangle(choiceBox.X, rowY, choiceBox.Width, heights[i]);
            bool hover = CheckCollisionPointRec(GetMousePosition(), choiceBox) && CheckCollisionPointRec(GetMousePosition(), rect);
            DrawRectangleRec(rect, hover || i == selectedChoice ? new Color(57, 75, 89, 255) : new Color(33, 45, 58, 255));
            for (int line = 0; line < rows[i].Count; line++) UiText.Draw(rows[i][line], x + 32, rowY + 8 + line * 22, 17, Text);
            if (hover && IsMouseButtonPressed(MouseButton.Left)) choose = i;
            rowY += heights[i] + 6;
        }
        EndScissorMode();
        UiText.Draw("1-9: odpowiedź   /   strzałki + Enter   /   kółko: przewijanie", x + 20, y + h - 24, 13, Muted);
        if (choose >= 0) dialogue.Choose(choose);
    }
}
