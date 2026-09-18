using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed class PlaySession
{
    public MapData Map { get; }
    public Vector3 Player { get; private set; }
    public FollowCamera Camera { get; }
    public PlaySession(MapData map)
    {
        Map = MapData.Parse(map.Serialize());
        Player = Map.Player.Position;
        Camera = new FollowCamera(Player);
    }
    public void Update(float dt, float wheel)
    {
        if (IsKeyPressed(KeyboardKey.Equal) || IsKeyPressedRepeat(KeyboardKey.Equal) || IsKeyPressed(KeyboardKey.KpAdd) || IsKeyPressedRepeat(KeyboardKey.KpAdd)) wheel++;
        if (IsKeyPressed(KeyboardKey.Minus) || IsKeyPressedRepeat(KeyboardKey.Minus) || IsKeyPressed(KeyboardKey.KpSubtract) || IsKeyPressedRepeat(KeyboardKey.KpSubtract)) wheel--;
        Camera.Zoom(wheel);
        if (IsKeyPressed(KeyboardKey.R)) Camera.Reset();
        float x = (IsKeyDown(KeyboardKey.D) || IsKeyDown(KeyboardKey.Right) ? 1 : 0) - (IsKeyDown(KeyboardKey.A) || IsKeyDown(KeyboardKey.Left) ? 1 : 0);
        float z = (IsKeyDown(KeyboardKey.S) || IsKeyDown(KeyboardKey.Down) ? 1 : 0) - (IsKeyDown(KeyboardKey.W) || IsKeyDown(KeyboardKey.Up) ? 1 : 0);
        var move = Camera.Right * x + Camera.Backward * z;
        if (move.LengthSquared() > 0)
        {
            move = Vector3.Normalize(move) * (IsKeyDown(KeyboardKey.LeftShift) ? 6 : 3.6f) * dt;
            var next = Map.Clamp(Player + new Vector3(move.X, 0, 0));
            if (WorldRenderer.CanWalk(Map, next)) Player = next;
            next = Map.Clamp(Player + new Vector3(0, 0, move.Z));
            if (WorldRenderer.CanWalk(Map, next)) Player = next;
        }
        Camera.Update(Player, dt);
    }
}
