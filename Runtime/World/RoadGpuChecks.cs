using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class RoadGpuChecks
{
    public static void Run(WorldAssets assets)
    {
        const string groundId = "terrain/checks/road-ground.png", roadId = "terrain/checks/road-surface.png";
        var groundImage = GenImageColor(16, 16, new(40, 200, 40, 255));
        var roadImage = GenImageColor(16, 16, new(220, 50, 40, 255));
        var ground = LoadTextureFromImage(groundImage); var surface = LoadTextureFromImage(roadImage);
        UnloadImage(groundImage); UnloadImage(roadImage);
        assets.Terrain.Add(groundId, ground); assets.Terrain.Add(roadId, surface);
        var target = LoadRenderTexture(512, 512);
        try
        {
            var map = MapData.CreateEmpty(32, 32); Array.Fill(map.Tiles, groundId);
            map.Environment.Lighting = map.Environment.Shadows = false;
            var road = new RoadData { Material = roadId, Width = 4, Softness = 1.5f, Points = [new(-12, 0), new(12, 0)] };
            map.Roads.Add(road);
            var view = new Camera3D { Position = new(0, 24, 0), Target = Vector3.Zero, Up = -Vector3.UnitZ, FovY = 24, Projection = CameraProjection.Orthographic };
            var renderer = new WorldRenderer(assets);
            Image Capture()
            {
                BeginTextureMode(target); ClearBackground(Color.Black); BeginMode3D(view);
                renderer.Draw(map, view, false, new(-100, 0, -100), 512, 512);
                EndMode3D(); EndTextureMode();
                var result = LoadImageFromTexture(target.Texture); ImageFlipVertical(ref result); return result;
            }
            Color Pixel(Image frame, float x, float z)
            {
                var point = GetWorldToScreenEx(map.GroundPosition(x, z), view, 512, 512);
                Check(point.X >= 0 && point.Y >= 0 && point.X < 512 && point.Y < 512, "sample in viewport");
                return GetImageColor(frame, (int)point.X, (int)point.Y);
            }
            var image = Capture();
            try
            {
                var core = Pixel(image, 0, 0); var shoulder = Pixel(image, 0, 1.25f); var outside = Pixel(image, 0, 3);
                Check(core.R > 200 && core.G < 70, "road centre uses the selected material");
                Check(shoulder.R is > 80 and < 190 && shoulder.G is > 70 and < 180, "road shoulders blend into the underlying terrain");
                Check(outside.G > 180 && outside.R < 70, "road leaves distant terrain intact");
            }
            finally { UnloadImage(image); }
            int meshes = assets.TerrainMeshes.BuildCount;
            road.Width = 1; road.Softness = .25f; image = Capture();
            try { Check(Pixel(image, 0, 1.25f).G > 180, "editing width refreshes the road mask"); }
            finally { UnloadImage(image); }
            Check(assets.TerrainMeshes.BuildCount == meshes, "road editing does not rebuild terrain meshes");
            var document = new MapDocument(map, true); document.Checkpoint();
            road.Width = 4; road.Points = [new(-12, 0), new(0, 4), new(12, 0)]; image = Capture();
            try { Check(Pixel(image, 0, 4).R > 200 && Pixel(image, 0, -3).G > 180, "control points move the curved road without rotating its footprint"); }
            finally { UnloadImage(image); }
            document.Undo(); map = document.Map;
            foreach (var projection in Enum.GetValues<ViewProjection>())
            {
                view = new FollowCamera(Vector3.Zero, new() { Projection = projection, InitialDistance = 15 }).View;
                image = Capture();
                try { Check(Pixel(image, 0, 0).R > 200 && Pixel(image, 0, 3).G > 180, "undo restores the narrow road in " + projection); }
                finally { UnloadImage(image); }
            }
            for (int z = 0; z <= map.Depth; z++)
            for (int x = 0; x <= map.Width; x++) map.SetVertexHeight(x, z, x * .25f - 4);
            image = Capture();
            try { Check(Pixel(image, 0, 0).R > 180, "roads follow the sculpted terrain surface"); }
            finally { UnloadImage(image); }
            document.DeleteRoad(map.Roads[0].Id); image = Capture();
            try { Check(Pixel(image, 0, 0).G > 150, "deleting the road restores its original ground material"); }
            finally { UnloadImage(image); }
            map = MapData.CreateEmpty(32, 32); Array.Fill(map.Tiles, groundId); map.Environment.Lighting = map.Environment.Shadows = false;
            map.Roads.Add(new() { Material = roadId, Width = 4, Points = [new(-15.8f, 0), new(-9, 0)] });
            view = new() { Position = new(0, 24, 0), Target = Vector3.Zero, Up = -Vector3.UnitZ, FovY = 32, Projection = CameraProjection.Orthographic };
            image = Capture();
            try { Check(Pixel(image, -15.95f, 0).R > 200 && Pixel(image, 15.95f, 0).G > 180, "coverage at a map edge never wraps to the opposite edge"); }
            finally { UnloadImage(image); }
            using var cache = new RoadMaterialMask();
            var huge = MapData.CreateEmpty(2048, 2048); huge.Roads.Add(new() { Points = [new(-1000, 0), new(1000, 0)] });
            var materials = new Dictionary<string, int> { ["dirt"] = 1 };
            cache.Update(huge, new(0, 0, 2048, 2048), materials, 1);
            Check(cache.Texture.Width <= 1024 && cache.Texture.Height <= 1024, "whole-world road lookup has bounded GPU memory");
            cache.Update(huge, new(0, 0, 2048, 2048), materials, 1);
            Check(cache.BuildCount == 1, "unchanged road masks are cached");
            Console.WriteLine("Road GPU checks passed: material, soft edges, curves, width edits, undo, both views, sculpted terrain, map edges, deletion and bounded/cached coverage.");
        }
        finally
        {
            UnloadRenderTexture(target); assets.Terrain.Remove(groundId); assets.Terrain.Remove(roadId); UnloadTexture(ground); UnloadTexture(surface);
        }
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Road GPU check failed: " + message); }
}
