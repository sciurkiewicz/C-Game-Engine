using System.Numerics;
using Raylib_cs;

static class CameraChecks
{
    public static void Run()
    {
        var camera = new FollowCamera(Vector3.Zero);
        Check(camera.View.Projection == CameraProjection.Perspective, "perspective projection");
        Check(Math.Abs(FollowCamera.DefaultYaw - MathF.PI / 2) < .001f, "original heading plus 45 degrees");
        Check(Math.Abs(camera.PitchDegrees - (30 + 30 * 15f / 35)) < .001f, "original default pitch");
        Check(Math.Abs(camera.View.FovY - 35) < .001f, "original field of view");
        Check(Math.Abs(camera.Distance - 30) < .001f, "default distance");
        Check(Vector3.Distance(camera.View.Target, new(0, 1.5f, 0)) < .001f, "initial target offset");
        foreach (int fps in new[] { 30, 60, 144 })
        {
            camera.Reset(); camera.Snap(Vector3.Zero);
            camera.Zoom(100);
            for (int i = 0; i < fps * 10; i++) camera.Update(new(3, 0, 4), 1f / fps);
            Check(Math.Abs(camera.Distance - 15) < .02f, "near zoom converges");
            Check(Math.Abs(camera.PitchDegrees - 30) < .02f, "close view is lower");
            Check(Vector3.Distance(camera.View.Target, new(3, 1.5f, 4)) < .01f, "follows player");
            camera.Zoom(-100);
            float before = camera.Distance;
            camera.Update(new(3, 0, 4), 1f / fps);
            Check(camera.Distance > before && camera.Distance < 50, "zoom interpolates without snapping");
            for (int i = 0; i < fps * 10; i++) camera.Update(Vector3.Zero, 1f / fps);
            Check(Math.Abs(camera.Distance - 50) < .02f && Math.Abs(camera.PitchDegrees - 60) < .02f, "far zoom raises pitch and clamps");
            camera.Reset(); camera.Snap(Vector3.Zero);
            camera.Zoom(1);
            Check(Math.Abs(camera.TargetDistance - 26) < .001f, "zoom step is four");
            for (int i = 0; i < fps * 5; i++)
            {
                camera.Update(new(i * .01f, 0, i * .02f), 1f / fps);
                var offset = camera.View.Position - camera.View.Target;
                var heading = Vector3.Normalize(new Vector3(offset.X, 0, offset.Z));
                Check(Vector3.Distance(heading, Vector3.UnitX) < .001f, "heading stays fixed while following and zooming");
                Check(camera.View.Up == Vector3.UnitY, "upright camera");
            }
            camera.Reset();
            Check(Math.Abs(camera.TargetDistance - 30) < .001f, "reset restores default zoom");
            var screenRight = Vector3.Normalize(Vector3.Cross(camera.View.Target - camera.View.Position, Vector3.UnitY));
            Check(Vector3.Distance(screenRight, camera.Right) < .001f, "movement matches camera heading");

            Check(Math.Abs(Vector3.Dot(camera.Right, camera.Backward)) < .001f, "movement axes stay orthogonal");
        }
        foreach (int fps in new[] { 30, 60, 144 })
        {
            var iso = new FollowCamera(Vector3.Zero, new() { Projection = ViewProjection.Isometric });
            Check(iso.View.Projection == CameraProjection.Orthographic, "isometric view uses parallel projection");
            var forward = Vector3.Normalize(iso.View.Target - iso.View.Position);
            var screenUp = Vector3.Cross(iso.Right, forward);
            float ProjectedLength(Vector3 axis) => new Vector2(Vector3.Dot(axis, iso.Right), Vector3.Dot(axis, screenUp)).Length();
            Check(Math.Abs(ProjectedLength(Vector3.UnitX) - ProjectedLength(Vector3.UnitZ)) < .001f &&
                Math.Abs(ProjectedLength(Vector3.UnitX) - ProjectedLength(Vector3.UnitY)) < .001f,
                "true isometry foreshortens all three world axes equally");
            iso.Zoom(100);
            for (int i = 0; i < fps * 10; i++) iso.Update(new(3, 2, 4), 1f / fps);
            float closeHeight = iso.View.FovY;
            Check(Math.Abs(iso.Distance - 15) < .02f && Vector3.Distance(iso.View.Target, new(3, 3.5f, 4)) < .01f,
                "isometric camera follows elevated ground and clamps close zoom");
            iso.Zoom(-100);
            for (int i = 0; i < fps * 10; i++)
            {
                iso.Update(Vector3.Zero, 1f / fps);
                Check(Math.Abs(iso.PitchDegrees - FollowCamera.IsometricPitch) < .001f, "isometric zoom keeps a fixed pitch");
                var offset = iso.View.Position - iso.View.Target;
                Check(Math.Abs(offset.X - offset.Z) < .001f, "isometric heading stays diagonal");
            }
            Check(Math.Abs(iso.Distance - 50) < .02f && iso.View.FovY > closeHeight * 3,
                "orthographic zoom changes the visible world height");
            var screenRight = Vector3.Normalize(Vector3.Cross(iso.View.Target - iso.View.Position, Vector3.UnitY));
            Check(Vector3.Distance(screenRight, iso.Right) < .001f && Math.Abs(Vector3.Dot(iso.Right, iso.Backward)) < .001f,
                "isometric movement axes follow screen directions");
            iso.Projection = ViewProjection.Perspective;
            Check(iso.View.Projection == CameraProjection.Perspective && Math.Abs(iso.View.FovY - 35) < .001f,
                "switching back restores the original perspective");
        }
        RuntimeSettingsChecks.Run();
        Console.WriteLine("Camera checks passed: perspective/isometric, follow, zoom, smoothing and movement axes at 30/60/144 FPS.");
    }
    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException($"Camera check failed: {description}");
    }
}
