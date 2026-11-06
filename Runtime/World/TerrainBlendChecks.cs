using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class TerrainBlendChecks
{
    public static void Run(WorldAssets assets)
    {
        const int size = 512;
        string[] ids = ["terrain/checks/red.png", "terrain/checks/green.png", "terrain/checks/blue.png", "terrain/checks/yellow.png", "terrain/checks/orientation.png"];
        Color[] colors = [new(220, 40, 40, 255), new(40, 220, 40, 255), new(40, 40, 220, 255), new(220, 220, 40, 255), new(70, 70, 70, 255)];
        var added = new List<Texture2D>();
        var target = LoadRenderTexture(size, size);
        try
        {
            for (int i = 0; i < ids.Length; i++)
            {
                int side = i == 4 ? 64 : 16 + i * 8;
                var image = GenImageColor(side, side, colors[i]);
                try
                {
                    if (i == 4)
                    {
                        // Distinct top, bottom, left and right markers catch rotation and mirroring on the GPU.
                        ImageDrawRectangle(ref image, 24, 8, 16, 16, colors[0]);
                        ImageDrawRectangle(ref image, 24, 40, 16, 16, colors[2]);
                        ImageDrawRectangle(ref image, 8, 24, 16, 16, colors[3]);
                        ImageDrawRectangle(ref image, 40, 24, 16, 16, colors[1]);
                    }
                    var texture = LoadTextureFromImage(image);
                    added.Add(texture); assets.Terrain.Add(ids[i], texture);
                }
                finally { UnloadImage(image); }
            }
            var renderer = new WorldRenderer(assets);
            var map = MapData.CreateEmpty(8, 8);
            var view = new Camera3D { Position = new(0, 12, 0), Target = Vector3.Zero, Up = -Vector3.UnitZ,
                Projection = CameraProjection.Orthographic, FovY = 8 };
            Image Capture(Camera3D? terrainTextureView = null)
            {
                BeginTextureMode(target); ClearBackground(new(38, 47, 54, 255)); BeginMode3D(view);
                renderer.Draw(map, view, false, new Vector3(-100, 0, -100), size, size, terrainTextureView: terrainTextureView);
                EndMode3D(); EndTextureMode();
                var frame = LoadImageFromTexture(target.Texture); ImageFlipVertical(ref frame); return frame;
            }
            Color Pixel(Image frame, float x, float z)
            {
                var point = GetWorldToScreenEx(map.GroundPosition(x, z), view, size, size);
                Check(point.X >= 0 && point.Y >= 0 && point.X < size && point.Y < size, "sample lies inside the viewport");
                return GetImageColor(frame, (int)point.X, (int)point.Y);
            }
            map.Environment.Shadows = false;
            foreach (string id in new[] { "grass", "dirt", "sand", "stone" })
            {
                Array.Fill(map.Tiles, id);
                var texture = assets.Terrain[id];
                // Draw the loaded PNG directly as an independent reference for the terrain shader.
                BeginTextureMode(target); ClearBackground(Color.Black);
                DrawTexturePro(texture, new(0, 0, texture.Width, texture.Height), new(0, 0, size, size), Vector2.Zero, 0, Color.White);
                EndTextureMode();
                var reference = LoadImageFromTexture(target.Texture); ImageFlipVertical(ref reference);
                try
                {
                    foreach (bool lighting in new[] { false, true })
                    {
                        map.Environment.Lighting = lighting;
                        map.Environment.TimeOfDay = lighting ? 12 : 0;
                        var actual = Capture();
                        try
                        {
                            for (float z = -3.25f; z <= 3; z += .75f)
                            for (float x = -3.25f; x <= 3; x += .75f)
                            {
                                var expected = Pixel(reference, x, z); var pixel = Pixel(actual, x, z);
                                // Flat terrain has a small neutral directional shade; RGB grading must not be added.
                                Check(Difference(pixel, expected) <= 4,
                                    $"{id} retains PNG colors with lighting {(lighting ? "at noon" : "disabled")}: expected {expected.R},{expected.G},{expected.B}, got {pixel.R},{pixel.G},{pixel.B}");
                            }
                        }
                        finally { UnloadImage(actual); }
                    }
                }
                finally { UnloadImage(reference); }
            }
            for (int z = 0; z < map.Depth; z++)
            for (int x = 0; x < map.Width; x++) map.Tiles[z * map.Width + x] = ids[x < 4 ? 0 : 2];
            var frame = Capture();
            try
            {
                var red = Pixel(frame, -.5f, .5f); var blue = Pixel(frame, .5f, .5f);
                Check(red.R > red.B * 4 && blue.B > blue.R * 4, "material centres retain their source texture colors");
                CheckAverage(Pixel(frame, 0, .5f), [red, blue], "edge blends both materials without darkening");
                Check(Difference(Pixel(frame, -.03f, .5f), Pixel(frame, .03f, .5f)) < 25, "adjacent tiles meet without a hard color jump");
                Check(Difference(Pixel(frame, -2.01f, .5f), red) < 5 && Difference(Pixel(frame, -1.99f, .5f), red) < 5,
                    "atlas gutters prevent other materials leaking into repeated texture edges");
            }
            finally { UnloadImage(frame); }
            for (int z = 0; z < map.Depth; z++)
            for (int x = 0; x < map.Width; x++) map.Tiles[z * map.Width + x] = ids[(x < 4 ? 0 : 1) + (z < 4 ? 0 : 2)];
            frame = Capture();
            try
            {
                CheckAverage(Pixel(frame, 0, 0), [Pixel(frame, -.5f, -.5f), Pixel(frame, .5f, -.5f),
                    Pixel(frame, -.5f, .5f), Pixel(frame, .5f, .5f)], "four-way junction has normalized, continuous weights");
            }
            finally { UnloadImage(frame); }
            Array.Fill(map.Tiles, ids[0]);
            var doc = new MapDocument(map, true); doc.Checkpoint(); doc.Paint(Vector3.Zero, 1, ids[2]);
            frame = Capture();
            try { var pixel = Pixel(frame, .5f, .5f); Check(pixel.B > pixel.R * 4, "painting refreshes material weights immediately"); }
            finally { UnloadImage(frame); }
            doc.Undo(); map = doc.Map; frame = Capture();
            try { var pixel = Pixel(frame, .5f, .5f); Check(pixel.R > pixel.B * 4, "undo refreshes the lookup for the restored map"); }
            finally { UnloadImage(frame); }
            Array.Fill(map.Tiles, "sand"); frame = Capture(); Color sand;
            try { sand = Pixel(frame, .5f, .5f); }
            finally { UnloadImage(frame); }
            Array.Fill(map.Tiles, "water"); map.WaterLevel = -1; frame = Capture();
            try { Check(Difference(Pixel(frame, .5f, .5f), sand) <= 3, "legacy water tiles keep their sand bed"); }
            finally { UnloadImage(frame); }
            Array.Fill(map.Tiles, "grass"); frame = Capture(); Color grass;
            try { grass = Pixel(frame, .5f, .5f); }
            finally { UnloadImage(frame); }
            Array.Fill(map.Tiles, "terrain/missing.png"); frame = Capture();
            try { Check(Difference(Pixel(frame, .5f, .5f), grass) <= 3, "missing textures retain the grass fallback"); }
            finally { UnloadImage(frame); }

            // More than one rlgl batch, and a lookup window larger than the initial allocation.
            map = MapData.CreateEmpty(96, 96);
            for (int z = 0; z < map.Depth; z++)
            for (int x = 0; x < map.Width; x++) map.Tiles[z * map.Width + x] = ids[x < 48 ? 0 : 2];
            view.FovY = 96; view.Position = new(0, 120, 0); frame = Capture();
            try
            {
                foreach (float z in new[] { -35.5f, 0.5f, 35.5f })
                {
                    var red = Pixel(frame, -10.5f, z); var blue = Pixel(frame, 10.5f, z);
                    Check(red.R > red.B * 4 && blue.B > blue.R * 4, "lookup sampler stays bound across terrain batch flushes");
                }
            }
            finally { UnloadImage(frame); }
            map = MapData.CreateEmpty(2048, 2048);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
            {
                map.Tiles[z * map.Width + x] = ids[0];
                map.Tiles[z * map.Width + 2040 + x] = ids[2];
            }
            view.FovY = 8;
            foreach (float x in new[] { 1020f, -1020f, 1020f })
            {
                view.Position = new(x, 12, -1020); view.Target = new(x, 0, -1020); frame = Capture();
                try
                {
                    var pixel = Pixel(frame, x + .5f, -1019.5f);
                    Check(x > 0 ? pixel.B > pixel.R * 4 : pixel.R > pixel.B * 4,
                        "panning between distant map edges refreshes the bounded lookup window");
                }
                finally { UnloadImage(frame); }
            }
            map = MapData.CreateEmpty(16, 16);
            for (int z = 0; z <= map.Depth; z++)
            for (int x = 0; x <= map.Width; x++) map.SetVertexHeight(x, z, (x - 8) * .25f + (z - 8) * .25f);
            for (int z = 0; z < map.Depth; z++)
            for (int x = 0; x < map.Width; x++) map.Tiles[z * map.Width + x] = ids[x < 8 ? 0 : 2];
            foreach (var projection in Enum.GetValues<ViewProjection>())
            {
                view = new FollowCamera(Vector3.Zero, new() { Projection = projection, InitialDistance = 15 }).View;
                frame = Capture();
                try
                {
                    CheckAverage(Pixel(frame, 0, .5f), [Pixel(frame, -.5f, .5f), Pixel(frame, .5f, .5f)],
                        "blending follows sculpted triangles in " + projection);
                }
                finally { UnloadImage(frame); }
            }

            map = MapData.CreateEmpty(8, 8); Array.Fill(map.Tiles, ids[4]);
            map.Environment.Lighting = map.Environment.Shadows = false;
            var orientedViews = new List<(string Name, Camera3D View, Vector3 Right, Vector3 Away)>();
            foreach (var projection in Enum.GetValues<ViewProjection>())
            {
                var camera = new FollowCamera(Vector3.Zero, new() { Projection = projection, InitialDistance = 15 });
                orientedViews.Add((projection.ToString(), camera.View, camera.Right, -camera.Backward));
            }
            foreach (float yaw in new[] { -.65f, 2.35f })
            {
                var backward = new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw));
                var camera = new Camera3D { Position = backward * 10 + Vector3.UnitY * 9, Target = Vector3.UnitY * 1.5f,
                    Up = Vector3.UnitY, FovY = 35, Projection = CameraProjection.Perspective };
                orientedViews.Add(("free yaw " + yaw, camera, new(MathF.Cos(yaw), 0, -MathF.Sin(yaw)), -backward));
            }
            foreach (var up in new[] { -Vector3.UnitZ, -Vector3.UnitX })
                orientedViews.Add(("top-down " + up, new Camera3D { Position = Vector3.UnitY * 12, Target = Vector3.Zero, Up = up,
                    FovY = 8, Projection = CameraProjection.Orthographic }, Vector3.Cross(-Vector3.UnitY, up), up));
            int meshes = -1;
            foreach (var oriented in orientedViews)
            {
                view = oriented.View; frame = Capture();
                try
                {
                    foreach (var sample in new[] { (Point: oriented.Away * 2, Color: colors[0], Name: "top"),
                        (Point: -oriented.Away * 2, Color: colors[2], Name: "bottom"),
                        (Point: -oriented.Right * 2, Color: colors[3], Name: "left"),
                        (Point: oriented.Right * 2, Color: colors[1], Name: "right") })
                        Check(Difference(Pixel(frame, sample.Point.X, sample.Point.Z), sample.Color) <= 8,
                            $"PNG {sample.Name} keeps its direction relative to the camera in {oriented.Name}");
                    if (meshes >= 0) Check(assets.TerrainMeshes.BuildCount == meshes, "camera rotation reuses terrain geometry");
                    meshes = assets.TerrainMeshes.BuildCount;
                }
                finally { UnloadImage(frame); }
            }
            // Free navigation must leave the same PNG markers at the same world positions.
            foreach (var projection in Enum.GetValues<ViewProjection>())
            {
                var reference = new FollowCamera(Vector3.Zero, new() { Projection = projection, InitialDistance = 15 });
                foreach (var oriented in orientedViews)
                foreach (bool moveAndZoom in new[] { false, true })
                {
                    view = oriented.View;
                    if (moveAndZoom)
                    {
                        view.Position = view.Target + (view.Position - view.Target) * 1.1f;
                        view.Position += new Vector3(.3f, .2f, -.4f); view.Target += new Vector3(.3f, .2f, -.4f);
                        if (view.Projection == CameraProjection.Orthographic) view.FovY *= 1.1f;
                    }
                    frame = Capture(reference.View);
                    try
                    {
                        foreach (var sample in new[] { (Point: -reference.Backward * 2, Color: colors[0]),
                            (Point: reference.Backward * 2, Color: colors[2]),
                            (Point: -reference.Right * 2, Color: colors[3]),
                            (Point: reference.Right * 2, Color: colors[1]) })
                            Check(Difference(Pixel(frame, sample.Point.X, sample.Point.Z), sample.Color) <= 8,
                                $"ground stays anchored to {projection} during {oriented.Name}, moved/zoomed: {moveAndZoom}");
                        Check(assets.TerrainMeshes.BuildCount == meshes, "free navigation reuses anchored terrain geometry");
                    }
                    finally { UnloadImage(frame); }
                }
            }
            map = TerrainChecks.CreateDemo();
            for (int z = 0; z < map.Depth; z++)
            for (int x = 0; x < map.Width; x++)
            {
                float wx = x - map.Width / 2f + .5f, wz = z - map.Depth / 2f + .5f;
                if (Math.Abs(wx - 5 - MathF.Sin(wz * .35f) * 2) < 1.5f) map.Tiles[z * map.Width + x] = "dirt";
                if (Vector2.Distance(new(wx, wz), new(8, -5)) < 2.5f) map.Tiles[z * map.Width + x] = "stone";
            }
            foreach (var projection in Enum.GetValues<ViewProjection>())
            {
                view = new FollowCamera(Vector3.Zero, new() { Projection = projection, InitialDistance = 27 }).View;
                frame = Capture();
                try
                {
                    string path = Path.Combine(ProjectPaths.Root, "Editor", "obj", "terrain-blend-" + projection.ToString().ToLowerInvariant() + ".png");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    if (!ExportImage(frame, path)) throw new IOException("Cannot save terrain blending preview.");
                }
                finally { UnloadImage(frame); }
            }
            Console.WriteLine("Terrain blending GPU checks passed: source PNG colors, both projections, world-anchored textures during free rotation/pan/zoom, top-down views, centres, smooth edges/corners, custom materials, atlas gutters, painting/undo, batch flushes, water beds and slopes.");
            RoadGpuChecks.Run(assets);
        }
        finally
        {
            UnloadRenderTexture(target);
            for (int i = 0; i < added.Count; i++) { assets.Terrain.Remove(ids[i]); UnloadTexture(added[i]); }
        }
    }
    static int Difference(Color a, Color b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
    static void CheckAverage(Color actual, Color[] sources, string message)
    {
        var expected = new Color((int)sources.Average(c => c.R), (int)sources.Average(c => c.G), (int)sources.Average(c => c.B), 255);
        Check(Difference(actual, expected) <= 8, $"{message}: expected {expected.R},{expected.G},{expected.B}, got {actual.R},{actual.G},{actual.B}");
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Terrain blending GPU check failed: " + message); }
}
