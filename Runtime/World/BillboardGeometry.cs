using System.Numerics;
using Raylib_cs;

readonly record struct SpriteQuad(Vector3 BottomLeft, Vector3 BottomRight, Vector3 TopRight, Vector3 TopLeft)
{
    public Vector3 Center => (BottomLeft + TopRight) / 2;
    public Vector3 Normal => Vector3.Normalize(Vector3.Cross(BottomRight - BottomLeft, TopLeft - BottomLeft));
}

static class BillboardGeometry
{
    // Our visual tuning, not a documented Don't Starve engine constant.
    // Partial pitch keeps the art readable while retaining an upright appearance.
    public const float PitchFollow = .5f;

    public static SpriteQuad Create(Camera3D camera, Vector3 feet, float width, float height)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var horizontal = new Vector3(forward.X, 0, forward.Z);
        float horizontalLength = horizontal.Length();
        // The game camera never looks straight down, but keep editor geometry valid there too.
        horizontal = horizontalLength > .0001f ? horizontal / horizontalLength : -Vector3.UnitZ;
        var right = Vector3.Cross(horizontal, Vector3.UnitY);
        float cameraPitch = MathF.Atan2(-forward.Y, horizontalLength);
        float lean = Math.Clamp(cameraPitch * PitchFollow, 0, MathF.PI / 6);
        var up = Vector3.UnitY * MathF.Cos(lean) + horizontal * MathF.Sin(lean);
        var leftFoot = feet - right * width / 2;
        var rightFoot = feet + right * width / 2;
        return new(leftFoot, rightFoot, rightFoot + up * height, leftFoot + up * height);
    }
}
