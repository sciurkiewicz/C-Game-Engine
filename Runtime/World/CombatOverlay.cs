using Raylib_cs;
using static Raylib_cs.Raylib;

sealed class CombatOverlay
{
    static readonly Color Panel = new(20, 28, 38, 235), Text = new(230, 238, 242, 255), Red = new(222, 75, 86, 255);
    public void Draw(PlaySession play, Rectangle bounds)
    {
        BeginScissorMode((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
        int x = (int)bounds.X + 14, y = (int)bounds.Y + 46;
        DrawRectangle(x, y, 180, 12, Panel);
        DrawRectangle(x + 2, y + 2, (int)(176f * play.PlayerHealth / play.Character.MaxHealth), 8, Red);
        if (play.Notice.Length > 0)
        {
            int w = Math.Min((int)bounds.Width - 28, 620), nx = (int)(bounds.X + (bounds.Width - w) / 2), ny = (int)bounds.Y + 12;
            DrawRectangle(nx, ny, w, 42, Panel);
            UiText.Draw(play.Notice.Length > 90 ? play.Notice[..87] + "..." : play.Notice, nx + 12, ny + 13, 14, Text);
        }
        if (!play.PlayerAlive)
        {
            DrawRectangleRec(bounds, new Color(0, 0, 0, 125));
            int w = Math.Min(360, (int)bounds.Width - 32), dx = (int)(bounds.X + (bounds.Width - w) / 2), dy = (int)(bounds.Y + bounds.Height / 2 - 65);
            DrawRectangle(dx, dy, w, 130, Panel); DrawRectangleLines(dx, dy, w, 130, Red);
            UiText.Draw("Porażka", dx + 24, dy + 22, 28, Red);
            UiText.Draw("Enter: rozpocznij od nowa", dx + 24, dy + 75, 18, Text);
        }
        EndScissorMode();
    }
}
