using System.Numerics;
using Raylib_cs;

// The focus plane follows the hero, including terrain height and camera zoom.
// Pure camera math also lets previews with an off-screen player use the camera target.
static class WorldFocus
{
    public static float Distance(Camera3D camera, Vector3 player, float height, float aspect)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, camera.Up));
        var up = Vector3.Cross(right, forward);
        var delta = player + Vector3.UnitY * (height * .5f) - camera.Position;
        float depth = Vector3.Dot(delta, forward);
        float halfHeight = camera.Projection == CameraProjection.Orthographic ? camera.FovY * .5f :
            Math.Max(0, depth) * MathF.Tan(camera.FovY * MathF.PI / 360);
        bool visible = depth > 0 && Math.Abs(Vector3.Dot(delta, up)) <= halfHeight &&
            Math.Abs(Vector3.Dot(delta, right)) <= halfHeight * aspect;
        return visible ? depth : Vector3.Distance(camera.Position, camera.Target);
    }
}
