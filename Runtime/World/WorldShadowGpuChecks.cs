using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class WorldShadowGpuChecks
{
    public static void Run(WorldAssets assets)
    {
        const int size = 512;
        const string narrowId = "objects/checks/narrow-shadow.png", wideId = "objects/checks/wide-shadow.png";
        // Invisible art isolates shadow pixels; source dimensions also exercise GPU-downscaled sprites.
        var image = GenImageColor(32, 32, new(0, 0, 0, 0));
        var texture = LoadTextureFromImage(image); UnloadImage(image);
        assets.Sprites.Add(narrowId, new(narrowId, texture, 16, 64));
        assets.Sprites.Add(wideId, new(wideId, texture, 128, 64));
        var target = LoadRenderTexture(size, size);
        try
        {
            var renderer = new WorldRenderer(assets);
            var map = MapData.CreateEmpty(16, 16);
            map.Environment.Lighting = false;
            var view = new Camera3D { Position = new(0, 40, 0), Target = Vector3.Zero, Up = -Vector3.UnitZ, FovY = 16, Projection = CameraProjection.Orthographic };
            var player = new Vector3(-100, 0, -100);
            var item = new SceneObject { Asset = narrowId, Height = 2 };
            map.Objects.Add(item);
            Image Capture(bool shadows)
            {
                map.Environment.Shadows = shadows;
                BeginTextureMode(target); ClearBackground(new(38, 47, 54, 255)); BeginMode3D(view);
                renderer.Draw(map, view, false, player, size, size);
                EndMode3D(); EndTextureMode();
                var frame = LoadImageFromTexture(target.Texture); ImageFlipVertical(ref frame); return frame;
            }
            static int Difference(Color before, Color after) => before.R + before.G + before.B - after.R - after.G - after.B;
            int Area()
            {
                var before = Capture(false); var after = Capture(true);
                try
                {
                    int count = 0;
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        if (Difference(GetImageColor(before, x, y), GetImageColor(after, x, y)) > 12) count++;
                    return count;
                }
                finally { UnloadImage(before); UnloadImage(after); }
            }
            int Darkening(Image before, Image after, Vector3 point)
            {
                var pixel = GetWorldToScreenEx(point, view, size, size);
                Check(pixel.X >= 0 && pixel.X < size && pixel.Y >= 0 && pixel.Y < size, "sample inside the viewport");
                return Difference(GetImageColor(before, (int)pixel.X, (int)pixel.Y), GetImageColor(after, (int)pixel.X, (int)pixel.Y));
            }
            void CheckGround(params Vector2[] positions)
            {
                var before = Capture(false); var after = Capture(true);
                try
                {
                    foreach (var p in positions)
                        Check(Darkening(before, after, map.GroundPosition(p.X, p.Y)) > 12, "shadow follows visible ground at " + p);
                }
                finally { UnloadImage(before); UnloadImage(after); }
            }
            int narrow = Area();
            item.Asset = wideId; int wide = Area();
            Check(narrow > 20 && wide > narrow * 15, "wide art casts a larger shadow than narrow art of the same height");
            item.Height = 4; int scaled = Area();
            Check(scaled > wide * 3.5f && scaled < wide * 4.5f, "doubling sprite size quadruples shadow area");
            item.Collider = new() { Radius = .05f }; item.Solid = false;
            Check(Area() == scaled, "collision settings do not change a visual shadow");
            map.Environment.Lighting = true; map.Environment.TimeOfDay = 0;
            var nightBefore = Capture(false); var nightAfter = Capture(true);
            try
            {
                var before = GetImageColor(nightBefore, size / 2, size / 2);
                var after = GetImageColor(nightAfter, size / 2, size / 2);
                Check(after.R <= before.R && after.G < before.G && after.B <= before.B, "night shadows darken the ground without adding light");
            }
            finally { UnloadImage(nightBefore); UnloadImage(nightAfter); }
            map.Environment.Lighting = false; map.Environment.TimeOfDay = 12;
            map.Objects.Clear(); map.Player.Asset = wideId; map.Player.Height = 2; player = Vector3.Zero;
            Check(Area() == wide, "player shadows use their sprite dimensions too");
            player = new(-100, 0, -100); map.Objects.Add(item); item.Height = 2;
            // A ridge and a hollow cross the footprint; a flat decal or a radial fan would clip through them.
            map.SetVertexHeight(9, 8, 2); map.SetVertexHeight(8, 9, -1);
            CheckGround(new(-.75f, -.25f), new(.75f, .25f), new(.5f, .75f), new(-.5f, .5f));
            map = MapData.CreateEmpty(32, 32); map.Environment.Lighting = false;
            item = new() { Asset = wideId, Height = 3 }; map.Objects.Add(item);
            foreach (var projection in Enum.GetValues<ViewProjection>())
            {
                view = new FollowCamera(Vector3.Zero, new() { Projection = projection }).View;
                CheckGround(Vector2.Zero);
                item.Height = 1; int small = Area(); item.Height = 3;
                Check(Area() > small * 5, "shadow scales with the sprite in " + projection);
            }
            map = MapData.CreateEmpty(8, 8); map.Environment.Lighting = false;
            view = new() { Position = new(0, 40, 0), Target = Vector3.Zero, Up = -Vector3.UnitZ, FovY = 12, Projection = CameraProjection.Orthographic };
            item = new() { Asset = wideId, X = -3.75f, Height = 2 }; map.Objects.Add(item);
            CheckGround(new Vector2(-3.5f, 0));
            var outsideBefore = Capture(false); var outsideAfter = Capture(true);
            try { Check(Darkening(outsideBefore, outsideAfter, new(-4.25f, 0, 0)) == 0, "shadow stops at the map boundary"); }
            finally { UnloadImage(outsideBefore); UnloadImage(outsideAfter); }
            // Large objects can cast a visible shadow while their feet are outside the visible terrain bounds.
            map = MapData.CreateEmpty(64, 64); map.Environment.Lighting = false; view.FovY = 16;
            map.Objects.Add(new() { Asset = wideId, X = 17, Height = 16 });
            CheckGround(new Vector2(7, 0));
            // More vertices than one render batch must retain the shader and blend/depth state.
            map = MapData.CreateEmpty(128, 128); map.Environment.Lighting = false; view.FovY = 128;
            map.Objects.Add(new() { Asset = wideId, Height = 64 });
            CheckGround(new(-32, 0), new(32, 0), new(0, -20), new(0, 20));
            Console.WriteLine("Shadow GPU checks passed: sprite proportions/source pixels, scaling, independent colliders, night, player, slopes, both projections, map/viewport edges and render batches.");
        }
        finally
        {
            UnloadRenderTexture(target);
            assets.Sprites.Remove(narrowId); assets.Sprites.Remove(wideId); UnloadTexture(texture);
        }
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Shadow GPU check failed: " + message); }
}
