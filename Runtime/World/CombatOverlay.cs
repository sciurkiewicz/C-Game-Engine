using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed class CombatOverlay
{
    static readonly Color Panel = new(20, 28, 38, 235), Text = new(230, 238, 242, 255), Red = new(222, 75, 86, 255);
    public void Draw(PlaySession play, Rectangle bounds)
    {
        BeginScissorMode((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
        if (!play.Dialogue.Active && !play.InventoryOpen)
        {
            var camera = play.Camera.View;
            foreach (var monster in play.Monsters)
            {
                var quad = BillboardGeometry.Create(camera, play.Map.GroundPosition(monster.Actor), monster.Actor.Height * .6f, monster.Actor.Height);
                var head = (quad.TopLeft + quad.TopRight) / 2 + Vector3.UnitY * .25f;
                if (Vector3.Dot(head - camera.Position, camera.Target - camera.Position) <= 0) continue;
                var p = GetWorldToScreenEx(head, camera, (int)bounds.Width, (int)bounds.Height) + new Vector2(bounds.X, bounds.Y);
                int mx = (int)p.X - 42, my = (int)p.Y;
                if (mx + 84 < bounds.X || mx > bounds.X + bounds.Width || my < bounds.Y || my > bounds.Y + bounds.Height) continue;
                DrawRectangle(mx, my, 84, 9, Panel);
                DrawRectangle(mx + 1, my + 1, (int)(82f * monster.Health / monster.Actor.MaxHealth), 7,
                    monster.HitFlash > 0 ? Color.White : Red);
                UiText.Draw(monster.Actor.Name, mx, my - 17, 12, Text);
            }
        }
        int x = (int)bounds.X + 14, y = (int)bounds.Y + 46;
        DrawRectangle(x, y, 194, 60, Panel);
        UiText.Draw($"HP  {play.PlayerHealth} / {PlaySession.PlayerMaxHealth}", x + 10, y + 7, 16,
            play.PlayerHitFlash > 0 ? Red : Text);
        DrawRectangle(x + 10, y + 29, 174, 7, new(55, 63, 74, 255));
        DrawRectangle(x + 10, y + 29, (int)(174f * play.PlayerHealth / PlaySession.PlayerMaxHealth), 7, Red);
        UiText.Draw("Spacja: atak", x + 10, y + 42, 12, play.PlayerAttackFlash > 0 ? Color.White : new(150, 170, 186, 255));
        UiText.Draw("F6: zapisz postęp / F9: wczytaj", (int)bounds.X + 14, (int)(bounds.Y + bounds.Height) - 24, 13, Text);
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
