using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class WorldFeatureGpuChecks
{
    public static void Run(WorldAssets assets)
    {
        WorldShadowGpuChecks.Run(assets);
        const int size = 512;
        const string spriteId = "objects/checks/occluder.png";
        var image = GenImageColor(32, 64, new(40, 210, 60, 255));
        var texture = LoadTextureFromImage(image); UnloadImage(image); assets.Sprites.Add(spriteId, new(spriteId, texture));
        var target = LoadRenderTexture(size, size);
        try
        {
            var renderer = new WorldRenderer(assets);
            var map = MapData.CreateEmpty(32, 32);
            var view = new Camera3D { Position = new(0, 20, 0), Target = Vector3.Zero, Up = -Vector3.UnitZ, FovY = 16, Projection = CameraProjection.Orthographic };
            Vector3 player = new(-100, 0, -100);
            Image Capture()
            {
                BeginTextureMode(target); ClearBackground(new(38, 47, 54, 255)); BeginMode3D(view);
                renderer.Draw(map, view, false, player, size, size); EndMode3D(); EndTextureMode();
                var frame = LoadImageFromTexture(target.Texture); ImageFlipVertical(ref frame); return frame;
            }
            Color Pixel(Image frame, Vector3 position)
            {
                var point = GetWorldToScreenEx(position, view, size, size);
                Check(point.X >= 0 && point.Y >= 0 && point.X < size && point.Y < size, "sample in viewport");
                return GetImageColor(frame, (int)point.X, (int)point.Y);
            }
            Color Sample(Vector3 point)
            {
                var frame = Capture(); try { return Pixel(frame, point); } finally { UnloadImage(frame); }
            }
            map.Environment.Shadows = false;
            var day = Sample(new(1.5f, 0, 1.5f)); map.Environment.TimeOfDay = 0;
            var night = Sample(new(1.5f, 0, 1.5f));
            Check(night.G < day.G * .3f && night.R < day.R * .3f, "night lighting darkens the actual terrain pixels");
            map.Lights.Add(new() { X = 1.5f, Z = 1.5f, Height = 1, Radius = 6, Intensity = 2, R = 255, G = 40, B = 15 });
            var lit = Sample(new(1.5f, 0, 1.5f));
            Check(lit.R > night.R * 3 && lit.R > lit.G, "point lights illuminate terrain with their color");
            map.Lights.Clear(); map.Environment.TimeOfDay = 12;
            var scenery = new SceneObject { Asset = spriteId, X = 2, Z = 2, Height = 3 }; map.Objects.Add(scenery);
            var before = Sample(new(1.8f, 0, 2.25f)); map.Environment.Shadows = true;
            var shadow = Sample(new(1.8f, 0, 2.25f));
            Check(shadow.G < before.G - 3, "soft contact shadow darkens ground outside the sprite");
            map.Environment.Shadows = false;
            var quad = renderer.GetSpriteQuad(view, spriteId, map.GroundPosition(scenery), scenery.Height);
            var artDay = Sample(quad.Center); map.Environment.TimeOfDay = 0; var artNight = Sample(quad.Center);
            Check(artNight.G < artDay.G * .3f, "the same lighting affects sprites");
            foreach (var projection in Enum.GetValues<ViewProjection>())
            {
                map = MapData.CreateEmpty(32, 32); map.Environment.Shadows = false;
                var camera = new FollowCamera(Vector3.Zero, new() { Projection = projection }); view = camera.View; player = Vector3.Zero;
                scenery = new() { Asset = spriteId, X = camera.Backward.X * 1.5f, Z = camera.Backward.Z * 1.5f, Height = 5 };
                map.Objects.Add(scenery);
                map.Environment.FadeOccluders = false;
                var opaque = Sample(player + Vector3.UnitY * .9f);
                Check(opaque.G > 180 && opaque.B < 100, "occluder actually covers player in " + projection);
                map.Environment.FadeOccluders = true;
                for (int frame = 0; frame < 40; frame++) { var fadeFrame = Capture(); UnloadImage(fadeFrame); }
                var faded = Sample(player + Vector3.UnitY * .9f);
                Check(faded.B > opaque.B + 90, "faded scenery reveals the player in " + projection);
                string file = Path.Combine(ProjectPaths.Root, "Editor", "obj", "world-occlusion-" + projection.ToString().ToLowerInvariant() + ".png");
                var preview = Capture(); try { ExportImage(preview, file); } finally { UnloadImage(preview); }
                map.Environment.FadeOccluders = false;
                for (int frame = 0; frame < 40; frame++) { var fadeFrame = Capture(); UnloadImage(fadeFrame); }
                var restored = Sample(player + Vector3.UnitY * .9f);
                Check(restored.G > 180 && restored.B < 100, "disabling fade restores opacity");
            }
            map = MapData.CreateEmpty(64, 64); map.Environment.Shadows = false; player = new(-100, 0, -100);
            view = new() { Position = new(0, 20, 0), Target = Vector3.Zero, Up = -Vector3.UnitZ, FovY = 32, Projection = CameraProjection.Orthographic };
            var initial = Capture(); UnloadImage(initial); int built = assets.TerrainMeshes.BuildCount;
            var unchanged = Capture(); UnloadImage(unchanged);
            Check(assets.TerrainMeshes.BuildCount == built, "unchanged frames reuse GPU terrain meshes");
            new MapDocument(map, true).Paint(Vector3.Zero, 1, "sand"); var painted = Capture(); UnloadImage(painted);
            Check(assets.TerrainMeshes.BuildCount == built, "painting updates blending without rebuilding geometry");
            map.SetVertexHeight(24, 24, 1); var sculpted = Capture(); UnloadImage(sculpted);
            Check(assets.TerrainMeshes.BuildCount == built + 1, "sculpting rebuilds only the affected chunk, including first height allocation");
            map = MapData.CreateEmpty(512, 512); map.Environment.Shadows = false; view.FovY = 64;
            foreach (int x in new[] { -192, -64, 64, 192 })
            foreach (int z in new[] { -192, -64, 64, 192 })
            { view.Position = new(x, 40, z); view.Target = new(x, 0, z); var pan = Capture(); UnloadImage(pan); }
            Check(assets.TerrainMeshes.CachedChunkCount <= 256, "terrain mesh memory remains bounded during panning");
            foreach (string id in new[] { "fire", "water", "step/grass", "step/stone" })
            {
                var wave = LoadWaveFromMemory(".wav", WorldAudio.GenerateClip(id));
                try { Check(IsWaveValid(wave) && wave.FrameCount > 0, "generated placeholder decodes through Raylib audio"); }
                finally { UnloadWave(wave); }
            }
            Console.WriteLine("World GPU checks passed: day/night, colored lights, contact shadows, sprite lighting, occlusion in both views, partial chunk rebuilds/LRU and audio decoding.");
        }
        finally { UnloadRenderTexture(target); assets.Sprites.Remove(spriteId); UnloadTexture(texture); }
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("World GPU check failed: " + message); }
}
