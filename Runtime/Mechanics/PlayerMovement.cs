using System.Numerics;

sealed partial class PlaySession
{
    // Input is relative to the game camera. Keeping this separate also allows checks without a window.
    public void MovePlayer(Vector2 input, bool running, float dt)
    {
        if (!PlayerAlive || Dialogue.Active || InventoryOpen || !float.IsFinite(dt) || dt <= 0 ||
            !float.IsFinite(input.X) || !float.IsFinite(input.Y)) return;
        var move = Camera.Right * input.X + Camera.Backward * input.Y;
        if (move.LengthSquared() == 0) return;
        move = Vector3.Normalize(move) * (running ? Character.RunSpeed : Character.WalkSpeed) * dt;
        TryMovePlayer(Player + new Vector3(move.X, 0, 0));
        TryMovePlayer(Player + new Vector3(0, 0, move.Z));
    }
    public bool TryMovePlayer(Vector3 destination)
    {
        destination = Map.Clamp(destination);
        if (!WorldRenderer.CanWalk(Map, destination, Player)) return false;
        Player = destination; return true;
    }
}
