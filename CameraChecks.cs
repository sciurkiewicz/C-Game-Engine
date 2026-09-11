using System.Numerics;

static class CameraChecks
{
    public static void Run()
    {
        var camera = new FollowCamera(Vector3.Zero);
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
            var originalOffset = camera.View.Position - camera.View.Target;
            for (int turn = 0; turn < 8; turn++)
            {
                camera.Rotate(1);
                Vector3 previous = Vector3.Normalize(camera.View.Position - camera.View.Target);
                for (int i = 0; i < fps; i++)
                {
                    camera.Update(Vector3.Zero, 1f / fps);
                    Vector3 next = Vector3.Normalize(camera.View.Position - camera.View.Target);
                    Check(Vector3.Dot(previous, next) > .8f, "rotation has no wrap jump");
                    previous = next;
                }
            }
            Check(Vector3.Distance(originalOffset, camera.View.Position - camera.View.Target) < .02f, "eight turns make a full circle");
            Check(Math.Abs(Vector3.Dot(camera.Right, camera.Backward)) < .001f, "movement axes stay orthogonal");
        }
        Console.WriteLine("Camera checks passed: follow, zoom limits/pitch, smoothing and full rotation at 30/60/144 FPS.");
    }
    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException($"Camera check failed: {description}");
    }
}
