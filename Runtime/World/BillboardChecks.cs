using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class BillboardChecks
{
    public static void Run()
    {
        var feet = new Vector3(2, 0, -3);
        foreach (float pitchDegrees in new[] { 30f, 42.857f, 60f })
        foreach (float yaw in new[] { 0f, MathF.PI / 4, MathF.PI / 2, MathF.PI })
        {
            float pitch = pitchDegrees * MathF.PI / 180;
            var heading = new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw));
            var view = new Camera3D
            {
                Position = feet + 30 * (heading * MathF.Cos(pitch) + Vector3.UnitY * MathF.Sin(pitch)),
                Target = feet, Up = Vector3.UnitY, FovY = 35, Projection = CameraProjection.Perspective
            };
            var quad = BillboardGeometry.Create(view, feet, 2, 5);
            var up = Vector3.Normalize(quad.TopLeft - quad.BottomLeft);
            var right = Vector3.Normalize(quad.BottomRight - quad.BottomLeft);
            var toCamera = Vector3.Normalize(view.Position - view.Target);
            Check(Vector3.Distance((quad.BottomLeft + quad.BottomRight) / 2, feet) < .0001f, "feet stay anchored");
            Check(Math.Abs(quad.BottomLeft.Y) < .0001f && Math.Abs(quad.BottomRight.Y) < .0001f, "base stays on ground");
            Check(Math.Abs(Vector3.Distance(quad.TopLeft, quad.BottomLeft) - 5) < .0001f &&
                Math.Abs(Vector3.Distance(quad.BottomLeft, quad.BottomRight) - 2) < .0001f, "sprite dimensions preserved");
            Check(Math.Abs(Vector3.Dot(up, right)) < .0001f, "rectangular sprite");
            Check(up.Y >= MathF.Cos(MathF.PI / 6) - .0001f, "sprite retains upright appearance");
            Check(Vector3.Dot(quad.Normal, toCamera) > Vector3.Dot(heading, toCamera), "partial tilt improves facing");
            Check(Vector3.Dot(quad.Normal, toCamera) < .999f, "not fully aligned to camera pitch");
            var ray = new Ray { Position = view.Position, Direction = Vector3.Normalize(quad.Center - view.Position) };
            var hit = GetRayCollisionQuad(ray, quad.BottomLeft, quad.BottomRight, quad.TopRight, quad.TopLeft);
            Check(hit.Hit && Vector3.Distance(hit.Point, quad.Center) < .001f, "ray hits the rendered quad");
            view.Position += new Vector3(7, 0, -9); view.Target += new Vector3(7, 0, -9);
            var afterPan = BillboardGeometry.Create(view, feet, 2, 5);
            Check(Vector3.Distance(quad.TopLeft, afterPan.TopLeft) < .001f, "panning does not swivel individual objects");
        }
        Console.WriteLine("Billboard checks passed: partial facing, anchored feet, dimensions, panning and picking at all zoom pitches.");
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Billboard check failed: " + message);
    }
}
