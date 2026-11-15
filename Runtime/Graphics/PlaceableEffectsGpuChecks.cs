using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class PlaceableEffectsGpuChecks
{
    public static void Run(WorldAssets assets)
    {
        const int size = 512;
        const string groundId = "terrain/checks/placeable-effects.png";
        const string blockerId = "objects/checks/placeable-effects-blocker.png";
        string[] effectIds = ["smoke", "steam", "heat_haze", "embers", "fireflies"];
        foreach (string name in effectIds)
        {
            string id = "objects/effects/" + name + ".png";
            Check(WorldEffectCatalog.TryGet(id, out var effect) && effect.DefaultHeight > 0,
                "the built-in effect is available in the catalog: " + name);
            Check(assets.Sprites.TryGetValue(id, out var asset) && !asset.DefaultSolid && IsTextureValid(asset.Texture),
                "the effect has a palette thumbnail and defaults to no collision: " + name);
        }
        var groundImage = GenImageColor(64, 64, new(57, 83, 109, 255));
        for (int y = 0; y < 64; y += 4)
        for (int x = 0; x < 64; x += 4)
            if ((x / 4 + y / 4) % 2 == 0)
                ImageDrawRectangle(ref groundImage, x, y, 4, 4, new(182, 155, 105, 255));
        var groundTexture = LoadTextureFromImage(groundImage); UnloadImage(groundImage);
        var blockerImage = GenImageColor(64, 64, new(85, 107, 132, 255));
        for (int x = 0; x < 64; x += 8)
            ImageDrawRectangle(ref blockerImage, x, 0, 3, 64, new(145, 160, 180, 255));
        var blockerTexture = LoadTextureFromImage(blockerImage); UnloadImage(blockerImage);
        assets.Terrain.Add(groundId, groundTexture);
        assets.Sprites.Add(blockerId, new(blockerId, blockerTexture));
        var target = LoadRenderTexture(size, size);
        var captures = new List<Image>();
        using var effects = new WorldVisualEffects();
        var renderer = new WorldRenderer(assets);
        var map = MapData.CreateEmpty(32, 32);
        Array.Fill(map.Tiles, groundId);
        map.Environment.Shadows = false;
        map.Environment.FadeOccluders = false;
        var view = new Camera3D
        {
            Position = new(0, 10, 16), Target = new(0, 1.3f, 0), Up = Vector3.UnitY,
            FovY = 35, Projection = CameraProjection.Perspective
        };
        var hiddenPlayer = new Vector3(-100, 0, -100);
        var play = new PlaySession(map);
        var off = new GraphicsSettings
        {
            Enabled = false, Fog = false, Wind = false, Bloom = false, SunRays = false,
            Outlines = false, HeatDistortion = false, Rain = false, Damage = false,
            DynamicLights = false, DepthOfField = false, ColorGrading = false, Vignette = false
        };
        Image FinishCapture()
        {
            var image = LoadImageFromTexture(target.Texture);
            ImageFlipVertical(ref image); captures.Add(image); return image;
        }
        Image Capture(float time, GraphicsSettings? settings = null)
        {
            effects.Configure(settings ?? off, play); effects.Update(play, time);
            effects.Render(renderer, map, view, hiddenPlayer, size, size, 12);
            BeginTextureMode(target); ClearBackground(Color.Black);
            effects.Draw(new(0, 0, size, size)); EndTextureMode();
            return FinishCapture();
        }
        Image DepthWriteProbe(bool drawEffects)
        {
            BeginTextureMode(target); ClearBackground(Color.Black); BeginMode3D(view);
            if (drawEffects) renderer.DrawEffectObjects(map, view, 1);
            // The panel is behind every particle. It must overwrite them when their depth writes are off.
            DrawCube(new(0, 2, -5), 100, 100, .1f, new(30, 90, 210, 255));
            EndMode3D(); EndTextureMode(); return FinishCapture();
        }
        void Preview(Image image, string name)
        {
            string directory = Path.Combine(ProjectPaths.Root, "Editor", "obj");
            Directory.CreateDirectory(directory);
            Check(ExportImage(image, Path.Combine(directory, "placeable-effects-" + name + ".png")), "preview is saved");
        }
        try
        {
            var baseline = Capture(1);
            var noEffects = Capture(1, off with { Enabled = true });
            Check(Changed(baseline, noEffects, 1) == 0, "a map without effect objects preserves its original image");
            foreach (string name in new[] { "smoke", "steam", "embers", "fireflies" })
            {
                string id = "objects/effects/" + name + ".png";
                map.Objects.Clear();
                map.Objects.Add(new() { Id = "gpu-" + name, Asset = id, Height = 4, Solid = false });
                string authored = map.Serialize();
                var restored = MapData.Parse(authored).Objects.Single();
                Check(restored.Asset == id && restored.Height == 4 && !restored.Solid,
                    "effect objects preserve type, scale and collision through map JSON");
                Check(WorldRenderer.CanWalk(map, Vector3.Zero), "the emitter does not block movement: " + name);
                var first = Capture(1); var later = Capture(3);
                Check(Changed(baseline, first, 3) > 4, "the emitter visibly renders with global post-processing disabled: " + name);
                Check(Changed(first, later, 3) > 4, "the emitter animates over time: " + name);
                Check(Changed(first, Capture(1), 1) == 0, "the same emitter frame is deterministic: " + name);
                Check(map.Serialize() == authored, "rendering preserves authored emitter data: " + name);
                var panelOnly = DepthWriteProbe(false); var panelAfterParticles = DepthWriteProbe(true);
                Check(Changed(panelOnly, panelAfterParticles, 1) == 0,
                    "transparent particles leave the depth buffer and render state usable: " + name);
                Preview(later, name);
            }

            map.Objects.Clear();
            var heat = new SceneObject { Id = "gpu-heat", Asset = "objects/effects/heat_haze.png", Height = 4, Solid = false };
            map.Objects.Add(heat);
            string heatAuthored = map.Serialize();
            var heatA = Capture(1); var heatB = Capture(3);
            Check(Changed(baseline, heatA, 2) > 15, "a placed heat emitter refracts actual background pixels even with global effects disabled");
            Check(Changed(heatA, heatB, 2) > 10, "the local heat refraction animates");
            Check(Changed(baseline, heatA, 1, new(8, 8, 72, 72)) == 0,
                "a heat emitter leaves distant parts of the image unchanged");
            Check(Changed(heatA, Capture(1, off with { Enabled = true }), 1) == 0,
                "authored heat does not depend on the global effects master switch");
            Check(map.Serialize() == heatAuthored, "heat animation does not rewrite the map");
            Preview(heatB, "heat-haze");

            var blocker = new SceneObject
            {
                Id = "gpu-blocker", Asset = blockerId, Z = 4, Height = 16,
                Solid = false, FadeWhenBlocking = false
            };
            map.Objects.Add(blocker);
            var obscuredHeat = Capture(1);
            map.Objects.Remove(heat);
            var blockerOnly = Capture(1);
            Check(Changed(blockerOnly, obscuredHeat, 2) < 10,
                "opaque foreground geometry blocks a heat emitter behind it");
            Preview(obscuredHeat, "occluded-heat");
            Console.WriteLine("Placeable effects GPU checks passed: palette registration, JSON/collision, smoke/steam/embers/fireflies animation, deterministic frames, transparent depth writes and local/occluded heat refraction.");
        }
        finally
        {
            foreach (var image in captures) UnloadImage(image);
            UnloadRenderTexture(target);
            assets.Terrain.Remove(groundId); assets.Sprites.Remove(blockerId);
            UnloadTexture(groundTexture); UnloadTexture(blockerTexture);
        }
    }
    static int Difference(Color a, Color b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
    static int Changed(Image a, Image b, int threshold, Rectangle? area = null)
    {
        var patch = area ?? new Rectangle(0, 0, a.Width, a.Height);
        int count = 0;
        for (int y = (int)patch.Y; y < patch.Y + patch.Height; y += 2)
        for (int x = (int)patch.X; x < patch.X + patch.Width; x += 2)
            if (Difference(GetImageColor(a, x, y), GetImageColor(b, x, y)) > threshold) count++;
        return count;
    }
    static void Check(bool valid, string message)
    { if (!valid) throw new InvalidOperationException("Placeable effects GPU check failed: " + message); }
}
