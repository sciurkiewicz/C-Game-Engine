using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class WallGpuChecks
{
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Wall GPU check failed: " + message); }
    static int Difference(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
    public static void Run(WorldAssets assets)
    {
        const int size = 512;
        var renderer = new WorldRenderer(assets);
        var target = LoadRenderTexture(size, size);
        var map = MapData.CreateEmpty(24, 24); map.Environment.Shadows = false; map.Environment.Lighting = false;
        var camera = new Camera3D { Position = new(13, 10, 0), Target = new(0, 1, 0), Up = Vector3.UnitY, FovY = 35, Projection = CameraProjection.Perspective };
        Vector3? player = null;
        Image Capture()
        {
            BeginTextureMode(target); ClearBackground(new(38, 47, 54, 255)); BeginMode3D(camera);
            renderer.Draw(map, camera, false, player, size, size); EndMode3D(); EndTextureMode();
            var image = LoadImageFromTexture(target.Texture); ImageFlipVertical(ref image); return image;
        }
        Color At(Image image, Vector3 p)
        {
            var screen = GetWorldToScreenEx(p, camera, size, size);
            Check(screen.X >= 0 && screen.X < size && screen.Y >= 0 && screen.Y < size, "sample is inside viewport");
            return GetImageColor(image, (int)screen.X, (int)screen.Y);
        }
        void Preview(Image image, string suffix) => Check(ExportImage(image, Path.Combine(ProjectPaths.Root, "Editor", "obj", "walls-" + suffix + ".png")), "screenshot saves");
        try
        {
            foreach (var style in Enum.GetValues<WallStyle>())
            {
                map.Walls.Clear(); var empty = Capture();
                var wall = new WallData { Style = style, Height = 3, Thickness = .8f, Points = [new(0, -4), new(0, 4)] };
                map.Walls.Add(wall); var frame = Capture();
                try
                {
                    Check(Difference(At(empty, new(.4f, 1.4f, .3f)), At(frame, new(.4f, 1.4f, .3f))) > 30, "vertical face renders with material " + style);
                    Check(Difference(At(empty, new(0, 3, .3f)), At(frame, new(0, 3, .3f))) > 20, "thickness creates a visible top face " + style);
                    Preview(frame, style.ToString().ToLowerInvariant());
                }
                finally { UnloadImage(empty); UnloadImage(frame); }
                camera.Position = new(-13, 10, 0);
                frame = Capture();
                try { var back = At(frame, new(-.4f, 1.4f, .3f)); Check(back.R > 20 && back.G > 20, "wall is visible from the other side " + style); }
                finally { UnloadImage(frame); }
                camera.Position = new(13, 10, 0);
            }
            foreach (var type in new[] { MapType.Cave, MapType.Building })
            {
                map.Type = type; map.Environment.TimeOfDay = 12; map.Lights.Clear();
                var dark = Capture();
                map.Lights.Add(new() { X = 2, Height = 2, Radius = 10, Intensity = 2, R = 255, G = 70, B = 30 });
                var light = Capture();
                try
                {
                    var a = At(dark, new(.4f, 1.4f, .3f)); var b = At(light, new(.4f, 1.4f, .3f));
                    Check(b.R > a.R + 40 && b.R > b.G, "local lights illuminate walls in " + type);
                    Preview(light, "lit-" + type.ToString().ToLowerInvariant());
                }
                finally { UnloadImage(dark); UnloadImage(light); }
            }
            map.Type = MapType.OpenWorld; map.Lights.Clear(); map.Walls.Clear();
            camera = new FollowCamera(Vector3.Zero, new() { InitialDistance = 22 }).View;
            player = Vector3.Zero;
            map.Walls.Add(new() { Height = 4, Points = [new(1.4f, -3), new(1.4f, 3)] });
            map.Environment.FadeOccluders = false;
            var opaque = Capture();
            map.Environment.FadeOccluders = true;
            for (int i = 0; i < 45; i++) { var frame = Capture(); UnloadImage(frame); }
            var faded = Capture();
            try
            {
                var a = At(opaque, new(0, .9f, 0)); var b = At(faded, new(0, .9f, 0));
                Check(b.B > a.B + 70, "a foreground wall fades to reveal the player behind it");
                Preview(faded, "player-reveal");
            }
            finally { UnloadImage(opaque); UnloadImage(faded); }
            var play = new PlaySession(map);
            using var effects = new WorldVisualEffects(); effects.Configure(new GraphicsSettings(), play); effects.Update(play, .1f);
            effects.Render(renderer, play.Map, camera, play.Player, size, size, 12);
            BeginTextureMode(target); ClearBackground(Color.Black); effects.Draw(new(0, 0, size, size)); EndTextureMode();
            var withEffects = LoadImageFromTexture(target.Texture); ImageFlipVertical(ref withEffects);
            try { Preview(withEffects, "runtime-effects"); Check(At(withEffects, new(0, .9f, 0)).A == 255, "walls render through the actual runtime effects pipeline"); }
            finally { UnloadImage(withEffects); }
            Console.WriteLine("Wall GPU checks passed: materials, top/back faces, Cave/Building lights, player reveal and runtime effects.");
        }
        finally { UnloadRenderTexture(target); }
    }
}
