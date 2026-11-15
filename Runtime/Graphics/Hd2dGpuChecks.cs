using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class Hd2dGpuChecks
{
    public static void Run(WorldAssets assets)
    {
        const int size = 512;
        const string checkerId = "terrain/checks/hd2d-checker.png";
        const string neutralId = "terrain/checks/hd2d-neutral.png";
        const string hiddenId = "characters/checks/hd2d-transparent.png";
        var checkerImage = GenImageColor(128, 128, new(42, 58, 66, 255));
        for (int y = 0; y < 128; y += 2)
        for (int x = 0; x < 128; x += 2)
            if ((x / 2 + y / 2) % 2 == 0)
                ImageDrawRectangle(ref checkerImage, x, y, 2, 2, new(204, 185, 142, 255));
        var checkerTexture = LoadTextureFromImage(checkerImage); UnloadImage(checkerImage);
        var neutralImage = GenImageColor(8, 8, new(110, 126, 142, 255));
        var neutralTexture = LoadTextureFromImage(neutralImage); UnloadImage(neutralImage);
        var hiddenImage = GenImageColor(8, 8, Color.Blank);
        var hiddenTexture = LoadTextureFromImage(hiddenImage); UnloadImage(hiddenImage);
        assets.Terrain.Add(checkerId, checkerTexture);
        assets.Terrain.Add(neutralId, neutralTexture);
        assets.Sprites.Add(hiddenId, new(hiddenId, hiddenTexture));
        var target = LoadRenderTexture(size, size);
        var captures = new List<Image>();
        using var effects = new WorldVisualEffects();
        var renderer = new WorldRenderer(assets);
        var map = MapData.CreateEmpty(64, 64);
        map.Environment.Shadows = false;
        map.Player.Asset = hiddenId;
        Array.Fill(map.Tiles, checkerId);
        var play = new PlaySession(map);
        var off = new GraphicsSettings
        {
            Fog = false, Wind = false, Bloom = false, SunRays = false, Outlines = false,
            HeatDistortion = false, Rain = false, Damage = false, DynamicLights = false,
            DepthOfField = false, ColorGrading = false, Vignette = false
        };
        Camera3D view = new()
        {
            Position = new(0, 16, 22), Target = Vector3.Zero, Up = Vector3.UnitY,
            FovY = 35, Projection = CameraProjection.Perspective
        };
        Image Capture(GraphicsSettings settings, Vector3? player = null)
        {
            effects.Configure(settings, play); effects.Update(play, 1);
            effects.Render(renderer, map, view, player ?? Vector3.Zero, size, size, 12);
            BeginTextureMode(target); ClearBackground(Color.Black);
            effects.Draw(new(0, 0, size, size)); EndTextureMode();
            var image = LoadImageFromTexture(target.Texture);
            ImageFlipVertical(ref image); captures.Add(image); return image;
        }
        Rectangle Patch(Vector3 point, int radius)
        {
            var center = GetWorldToScreenEx(point, view, size, size);
            Check(center.X >= radius + 1 && center.X < size - radius - 1 &&
                center.Y >= radius + 1 && center.Y < size - radius - 1, "world patch fits inside the image");
            return new((int)center.X - radius, (int)center.Y - radius, radius * 2, radius * 2);
        }
        void Preview(Image image, string name)
        {
            string directory = Path.Combine(ProjectPaths.Root, "Editor", "obj");
            Directory.CreateDirectory(directory);
            Check(ExportImage(image, Path.Combine(directory, "graphics-hd2d-" + name + ".png")), "preview is saved");
        }
        try
        {
            foreach (var projection in new[] { CameraProjection.Perspective, CameraProjection.Orthographic })
            {
                view.Projection = projection;
                view.FovY = projection == CameraProjection.Perspective ? 35 : 19;
                string frozen = map.Serialize();
                var baseline = Capture(off);
                var zeroBlur = Capture(off with { DepthOfField = true, MaxBlurRadius = 0 });
                Check(MeanDifference(baseline, zeroBlur) < .1, projection + ": zero blur preserves the original image");
                var depth = off with { DepthOfField = true, FocusRange = 1, FocusFalloff = 2, MaxBlurRadius = 14 };
                var blurred = Capture(depth);
                var broadFocus = Capture(depth with { FocusRange = 64 });
                Check(MeanDifference(baseline, broadFocus) < .2, projection + ": a broad focal region keeps the visible world sharp");
                var focus = Patch(new(-2, 0, 0), 6);
                var near = Patch(new(-2, 0, 8), 16);
                var far = Patch(new(-2, 0, -8), 16);
                double nearChange = MeanDifference(baseline, blurred, near);
                double farChange = MeanDifference(baseline, blurred, far);
                double focusChange = MeanDifference(baseline, blurred, focus);
                Check(nearChange > 1 && farChange > 1, projection + ": depth of field changes foreground and background texture detail");
                Check(focusChange < Math.Min(nearChange, farChange) * .3 + .3,
                    projection + ": the player depth stays sharper than foreground and background");
                Check(Detail(blurred, near) < Detail(baseline, near) * .95 &&
                    Detail(blurred, far) < Detail(baseline, far) * .95,
                    projection + ": defocus reduces high frequency detail on both sides of the focal plane");
                var sameFrame = Capture(depth);
                Check(MeanDifference(blurred, sameFrame) < .1, projection + ": static focus does not drift between identical frames");
                var movedFocus = Capture(depth, new(0, 0, 8));
                var newFocus = Patch(new(-2, 0, 8), 6);
                Check(MeanDifference(baseline, movedFocus, newFocus) < MeanDifference(baseline, blurred, newFocus) * .3 + .3,
                    projection + ": focus follows the player when the player moves away from the camera target");
                Check(map.Serialize() == frozen, "depth effects preserve the map and player configuration");
                Preview(blurred, projection == CameraProjection.Perspective ? "depth-perspective" : "depth-isometric");
            }

            Array.Fill(map.Tiles, neutralId);
            view.Projection = CameraProjection.Perspective; view.FovY = 35;
            var neutral = Capture(off);
            var identity = Capture(off with { ColorGrading = true, Exposure = 1, Contrast = 1, Saturation = 1, Temperature = 0 });
            Check(MeanDifference(neutral, identity) < .2, "neutral grading preserves the original colors");
            var graded = Capture(off with { ColorGrading = true, Exposure = 1.1f, Contrast = 1.1f, Saturation = .7f, Temperature = .3f });
            Check(MeanDifference(neutral, graded) > 3, "grading visibly changes exposure and the scene palette");
            var vignette = Capture(off with { Vignette = true, VignetteStrength = .7f });
            Rectangle center = new(240, 240, 32, 32), corner = new(8, 8, 40, 40);
            double centerChange = MeanDifference(neutral, vignette, center);
            double cornerChange = MeanDifference(neutral, vignette, corner);
            Check(cornerChange > centerChange + 8, "vignette affects the edges more than the center");
            Check(Luminance(vignette, corner) < Luminance(neutral, corner) - 8, "vignette darkens the corners");
            var zeroVignette = Capture(off with { Vignette = true, VignetteStrength = 0 });
            Check(MeanDifference(neutral, zeroVignette) < .1, "zero-strength vignette preserves the original image");
            var masterOff = Capture(new GraphicsSettings { Enabled = false });
            Check(MeanDifference(neutral, masterOff) < .2, "master switch bypasses all new post-processing");
            Preview(graded, "grading"); Preview(vignette, "vignette");
            Console.WriteLine("HD-2D GPU checks passed: perspective/isometric depth, near/far blur, broad focus, player autofocus, static focus, grading identity, vignette and master bypass.");
        }
        finally
        {
            foreach (var image in captures) UnloadImage(image);
            UnloadRenderTexture(target);
            assets.Terrain.Remove(checkerId); assets.Terrain.Remove(neutralId); assets.Sprites.Remove(hiddenId);
            UnloadTexture(checkerTexture); UnloadTexture(neutralTexture); UnloadTexture(hiddenTexture);
        }
    }
    static double MeanDifference(Image a, Image b, Rectangle? area = null)
    {
        var patch = area ?? new Rectangle(0, 0, a.Width, a.Height);
        double sum = 0; int count = 0;
        for (int y = (int)patch.Y; y < patch.Y + patch.Height; y++)
        for (int x = (int)patch.X; x < patch.X + patch.Width; x++)
        {
            var c = GetImageColor(a, x, y); var d = GetImageColor(b, x, y);
            sum += (Math.Abs(c.R - d.R) + Math.Abs(c.G - d.G) + Math.Abs(c.B - d.B)) / 3.0; count++;
        }
        return sum / count;
    }
    static double Brightness(Color value) => value.R * .2126 + value.G * .7152 + value.B * .0722;
    static double Luminance(Image image, Rectangle patch)
    {
        double sum = 0; int count = 0;
        for (int y = (int)patch.Y; y < patch.Y + patch.Height; y++)
        for (int x = (int)patch.X; x < patch.X + patch.Width; x++)
        { sum += Brightness(GetImageColor(image, x, y)); count++; }
        return sum / count;
    }
    static double Detail(Image image, Rectangle patch)
    {
        double sum = 0; int count = 0;
        for (int y = (int)patch.Y; y < patch.Y + patch.Height - 1; y++)
        for (int x = (int)patch.X; x < patch.X + patch.Width - 1; x++)
        {
            double c = Brightness(GetImageColor(image, x, y));
            sum += Math.Abs(c - Brightness(GetImageColor(image, x + 1, y))) +
                Math.Abs(c - Brightness(GetImageColor(image, x, y + 1))); count += 2;
        }
        return sum / count;
    }
    static void Check(bool valid, string message)
    { if (!valid) throw new InvalidOperationException("HD-2D GPU check failed: " + message); }
}
