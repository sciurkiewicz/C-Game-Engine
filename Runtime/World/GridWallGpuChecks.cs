using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class GridWallGpuChecks
{
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Dungeon GPU check failed: " + message); }
    public static void Run(WorldAssets assets)
    {
        const int size = 640;
        var document = new MapDocument(MapData.CreateEmpty(24, 24, MapType.Cave), true);
        var settings = new GridWallCell { Style = WallStyle.Cave, Roughness = 0, Height = 2.7f };
        document.PaintWallCells(GridWallGeometry.Rectangle((4, 4), (19, 19)), settings, false);
        document.PaintWallCells(Enumerable.Range(4, 16).Where(z => z != 8 && z != 15).Select(z => (11, z)), settings, false);
        document.PaintWallCells(Enumerable.Range(4, 16).Where(x => x != 6 && x != 16).Select(x => (x, 11)), settings, false);
        var map = document.Map; map.Environment.Shadows = false;
        map.Lights.Add(new() { X = -3, Z = -3, Height = 4, Radius = 12, Intensity = 2.2f, R = 255, G = 210, B = 160 });
        map.Lights.Add(new() { X = 5, Z = 4, Height = 3, Radius = 10, Intensity = 2, R = 140, G = 185, B = 255 });
        var renderer = new WorldRenderer(assets); var target = LoadRenderTexture(size, size);
        var view = new Camera3D { Position = new(16, 22, 18), Target = new(0, 1, 0), Up = Vector3.UnitY, FovY = 40, Projection = CameraProjection.Perspective };
        Image Capture()
        {
            BeginTextureMode(target); ClearBackground(new(17, 23, 31, 255)); BeginMode3D(view);
            renderer.Draw(map, view, false, null, size, size); EndMode3D(); EndTextureMode();
            var image = LoadImageFromTexture(target.Texture); ImageFlipVertical(ref image); return image;
        }
        void Preview(Image image, string suffix) => Check(ExportImage(image, Path.Combine(ProjectPaths.Root, "Editor", "obj", "dungeon-" + suffix + ".png")), "preview saves");
        try
        {
            var straight = Capture();
            map.WallCells = map.WallCells.Select(c => new GridWallCell { X = c.X, Z = c.Z, Height = c.Height, Roughness = .9f, Style = c.Style }).ToList();
            var rough = Capture();
            try
            {
                int changed = 0;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var a = GetImageColor(straight, x, y); var b = GetImageColor(rough, x, y);
                    if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) > 25) changed++;
                }
                Check(changed > 4000, "uneven cave geometry changes the rendered edges, crest and shading");
                Preview(rough, "cave-maze");
            }
            finally { UnloadImage(straight); UnloadImage(rough); }
            view.Projection = CameraProjection.Orthographic; view.FovY = 25;
            var isometric = Capture(); try { Preview(isometric, "cave-isometric"); } finally { UnloadImage(isometric); }
            map.Type = MapType.Building;
            map.WallCells = map.WallCells.Select(c => new GridWallCell { X = c.X, Z = c.Z, Height = c.Height, Style = WallStyle.Masonry }).ToList();
            var masonry = Capture(); try { Preview(masonry, "masonry-maze"); } finally { UnloadImage(masonry); }
            var play = new PlaySession(map); using var effects = new WorldVisualEffects();
            effects.Configure(new GraphicsSettings(), play); effects.Update(play, .1f);
            effects.Render(renderer, play.Map, view, play.Player, size, size, 0);
            BeginTextureMode(target); ClearBackground(Color.Black); effects.Draw(new(0, 0, size, size)); EndTextureMode();
            var runtime = LoadImageFromTexture(target.Texture); ImageFlipVertical(ref runtime);
            try { Preview(runtime, "runtime"); } finally { UnloadImage(runtime); }
            Console.WriteLine("Dungeon GPU checks passed: contiguous maze blocks, rough cave silhouette, flat masonry, both projections and Runtime effects.");
        }
        finally { UnloadRenderTexture(target); }
    }
}
