using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class SpriteWaterChecks
{
    // Compare actual GPU pixels: invisible PNG pixels must not hide the water drawn afterwards.
    public static void Run(WorldAssets assets)
    {
        const int size = 512;
        const string id = "objects/checks/sprite-water.png";
        var source = GenImageColor(64, 64, new Color(255, 255, 255, 0));
        ImageDrawRectangle(ref source, 16, 0, 32, 24, Color.White);
        ImageDrawRectangle(ref source, 16, 40, 32, 24, Color.White);
        ImageDrawRectangle(ref source, 16, 24, 8, 16, Color.White);
        ImageDrawRectangle(ref source, 40, 24, 8, 16, Color.White);
        var texture = LoadTextureFromImage(source); UnloadImage(source);
        SetTextureFilter(texture, TextureFilter.Bilinear);
        assets.Sprites.Add(id, new(id, texture));
        var target = LoadRenderTexture(size, size);
        try
        {
            var map = MapData.CreateEmpty(16, 16);
            map.Environment.Shadows = false; // Isolate alpha/depth behavior from an object's ground shadow.
            map.WaterLevel = .5f; map.WaveHeight = 0; map.WaterCells = Enumerable.Repeat((byte)3, map.Tiles.Length).ToArray();
            map.Player.X = map.Player.Z = -7;
            var renderer = new WorldRenderer(assets);
            var sprite = new SceneObject { Asset = id, Height = 4 };
            Image Capture(Camera3D view)
            {
                BeginTextureMode(target);
                ClearBackground(Color.Black);
                BeginMode3D(view);
                renderer.Draw(map, view, false, viewWidth: size, viewHeight: size);
                EndMode3D(); EndTextureMode();
                var result = LoadImageFromTexture(target.Texture);
                ImageFlipVertical(ref result);
                return result;
            }
            foreach (float zoom in new[] { 100f, 0f, -100f })
            {
                var camera = new FollowCamera(Vector3.Zero); camera.Zoom(zoom); camera.Snap(Vector3.Zero);
                var view = camera.View;
                map.Objects.Clear(); var background = Capture(view);
                map.Objects.Add(sprite); var composite = Capture(view);
                try
                {
                    var quad = renderer.GetSpriteQuad(view, id, map.GroundPosition(sprite), sprite.Height);
                    Color Pixel(Image frame, float u, float v)
                    {
                        var position = quad.BottomLeft + (quad.BottomRight - quad.BottomLeft) * u + (quad.TopLeft - quad.BottomLeft) * (1 - v);
                        var point = GetWorldToScreenEx(position, view, size, size);
                        return GetImageColor(frame, (int)MathF.Round(point.X), (int)MathF.Round(point.Y));
                    }
                    foreach (var uv in new[] { new Vector2(.12f, .35f), new Vector2(.5f, .5f), new Vector2(.88f, .35f) })
                    {
                        var before = Pixel(background, uv.X, uv.Y); var after = Pixel(composite, uv.X, uv.Y);
                        Check(before.B > before.R + 30, "fixture pixel is over water");
                        Check(Math.Abs(before.R - after.R) <= 3 && Math.Abs(before.G - after.G) <= 3 && Math.Abs(before.B - after.B) <= 3,
                            $"transparent sprite pixel preserves water at zoom {zoom}, UV {uv}: {before.R},{before.G},{before.B} -> {after.R},{after.G},{after.B}");
                    }
                    var opaque = Pixel(composite, .28f, .35f);
                    Check(opaque.R > 230 && opaque.G > 230 && opaque.B > 230, "opaque sprite above water remains visible");
                    var submerged = Pixel(composite, .28f, .95f);
                    Check(submerged.R < 170 && submerged.B > submerged.R + 30, "water still overlays the submerged part of the sprite");
                }
                finally { UnloadImage(background); UnloadImage(composite); }
            }
            var tree = assets.Sprites.Values.First(a => a.Id.Contains("/trees/"));
            map.Objects.Clear(); map.Objects.Add(new() { Asset = tree.Id, Height = 5 });
            var example = Capture(new FollowCamera(Vector3.Zero).View);
            try
            {
                string output = Path.Combine(ProjectPaths.Root, "Editor", "obj", "sprite-water.png");
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                if (!ExportImage(example, output)) throw new IOException("Cannot save sprite/water preview.");
            }
            finally { UnloadImage(example); }
            Console.WriteLine("Sprite/water GPU checks passed: transparent borders and holes, opaque art, submersion and all zoom pitches.");
        }
        finally
        {
            UnloadRenderTexture(target); assets.Sprites.Remove(id); UnloadTexture(texture);
        }
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Sprite/water GPU check failed: " + message); }
}
