using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class WorldRenderer
{
    public unsafe void RunWallCacheGpuChecks()
    {
        const int size = 512;
        var map = MapData.CreateEmpty(128, 128);
        map.Environment.Lighting = false; map.Environment.Shadows = false;
        map.WallCells = (from z in Enumerable.Range(0, 128) from x in Enumerable.Range(0, 128)
            where x % 8 == 0 || z % 8 == 0 select new GridWallCell { X = x, Z = z, Style = (WallStyle)((x / 8 + z / 8) % 3) }).ToList();
        var document = new MapDocument(map, true); var target = LoadRenderTexture(size, size);
        var camera = new FollowCamera(Vector3.Zero, new() { InitialDistance = 22 }).View;
        GridWallCell? skip = null;
        void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Wall cache GPU check failed: " + message); }
        Image Capture(bool reference, bool silhouette = false)
        {
            BeginTextureMode(target); ClearBackground(new(38, 47, 54, 255)); BeginMode3D(camera);
            WorldLighting.Apply(assets.WallShader, map, 12, camera.Target);
            WorldVisualFrame.Apply(assets.WallShader, map, camera, null, VisualSurface.Wall);
            SetShaderValue(assets.WallShader, GetShaderLocation(assets.WallShader, "wallSilhouette"), silhouette ? 1f : 0f, ShaderUniformDataType.Float);
            if (reference)
            {
                BeginShaderMode(assets.WallShader);
                foreach (var cell in map.WallCells) if (!ReferenceEquals(cell, skip)) DrawWallGeometry(map, cell.AsWall(map), Color.White);
                EndShaderMode();
            }
            else { wallFrustum.Configure(camera, size, size); assets.WallMeshes.Draw(map, wallFrustum, assets.WallShader); }
            EndMode3D(); EndTextureMode();
            var frame = LoadImageFromTexture(target.Texture); ImageFlipVertical(ref frame); return frame;
        }
        void Compare(string reason, bool silhouette = false)
        {
            var expected = Capture(true, silhouette); var actual = Capture(false, silhouette);
            try
            {
                var a = new ReadOnlySpan<Color>(expected.Data, size * size); var b = new ReadOnlySpan<Color>(actual.Data, size * size);
                int changed = 0, visible = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    if (Math.Abs(a[i].R - b[i].R) + Math.Abs(a[i].G - b[i].G) + Math.Abs(a[i].B - b[i].B) > 6) changed++;
                    if (a[i].R != 38 || a[i].G != 47 || a[i].B != 54) visible++;
                }
                Check(visible > 1000 && changed < 100, $"{reason}: {changed} differing pixels, {visible} wall pixels");
            }
            finally { UnloadImage(expected); UnloadImage(actual); }
        }
        try
        {
            Compare("perspective matches immediate geometry");
            int builds = assets.WallMeshes.BuildCount;
            Compare("unchanged frame reuses uploaded geometry");
            Check(assets.WallMeshes.BuildCount == builds, "an idle frame does not rebuild meshes");
            Check(assets.WallMeshes.VisibleCellCount < map.WallCells.Count / 2 && assets.WallMeshes.DrawCount < 100, "large offscreen sections are skipped and visible cells are batched");
            camera = new FollowCamera(Vector3.Zero, new() { InitialDistance = 22, Projection = ViewProjection.Isometric }).View;
            Compare("isometric edges match the reference");
            camera = new Camera3D { Position = new(9, 6, -12), Target = new(0, 2, 0), Up = Vector3.UnitY, FovY = 50, Projection = CameraProjection.Perspective };
            Compare("free camera matches the reference");
            var savedView = camera;
            camera = new Camera3D { Position = new(0, 8, 0), Target = new(0, 13, 5), Up = Vector3.UnitY, FovY = 35, Projection = CameraProjection.Perspective };
            builds = assets.WallMeshes.BuildCount;
            var sky = Capture(false); UnloadImage(sky);
            Check(assets.WallMeshes.BuildCount == builds && assets.WallMeshes.DrawCount == 0, "looking above a large dungeon does not upload invisible walls");
            camera = savedView;
            builds = assets.WallMeshes.BuildCount;
            document.PaintWallCells([(64, 64)], new() { Style = WallStyle.Wood, Height = 4 }, false);
            Compare("corner edits update adjacent chunks");
            Check(assets.WallMeshes.BuildCount > builds && assets.WallMeshes.BuildCount - builds <= 3, "a corner edit rebuilds at most three chunks");
            builds = assets.WallMeshes.BuildCount;
            document.PaintWallCells([(1, 1)], new(), false);
            Compare("offscreen edits preserve visible geometry");
            Check(assets.WallMeshes.BuildCount == builds, "offscreen editing does not upload invisible chunks");
            map.SetVertexHeight(64, 64, 1);
            Compare("sculpted ground refreshes wall vertices");
            var cell = map.WallCellAt(64, 64)!;
            assets.WallMeshes.Suppress(cell, true); skip = cell;
            builds = assets.WallMeshes.BuildCount; Compare("fading removes the opaque copy");
            Check(assets.WallMeshes.BuildCount == builds, "fading updates colors without rebuilding geometry");
            skip = null; Compare("light mask keeps the full silhouette", true);
            assets.WallMeshes.Suppress(cell, false); Compare("restoring opacity restores the GPU copy");
            document.Undo(); map = document.Map; Compare("undo replaces GPU cache contents");
            foreach (var projection in Enum.GetValues<ViewProjection>())
            {
                var fadeMap = MapData.CreateEmpty(16, 16); fadeMap.Environment.Lighting = false; fadeMap.Environment.Shadows = false;
                fadeMap.Player.X = fadeMap.Player.Z = -.5f;
                fadeMap.WallCells.Add(new() { X = 8, Z = projection == ViewProjection.Isometric ? 8 : 7, Height = 4 });
                var player = fadeMap.GroundPosition(-.5f, -.5f);
                camera = new FollowCamera(player, new() { InitialDistance = 22, Projection = projection }).View;
                var editorRenderer = new WorldRenderer(assets);
                Color PlayerPixel(bool editorView = false)
                {
                    BeginTextureMode(target); ClearBackground(Color.Black); BeginMode3D(camera);
                    (editorView ? editorRenderer : this).Draw(fadeMap, camera, false, editorView ? null : player, size, size); EndMode3D(); EndTextureMode();
                    var frame = LoadImageFromTexture(target.Texture); ImageFlipVertical(ref frame);
                    try { var p = GetWorldToScreenEx(player + Vector3.UnitY * .9f, camera, size, size); return GetImageColor(frame, (int)p.X, (int)p.Y); }
                    finally { UnloadImage(frame); }
                }
                fadeMap.Environment.FadeOccluders = false; var opaque = PlayerPixel();
                fadeMap.Environment.FadeOccluders = true;
                for (int i = 0; i < 45; i++) PlayerPixel();
                var revealed = PlayerPixel();
                Check(revealed.B > opaque.B + 70, "cached foreground grid walls reveal the player in " + projection);
                Check(Math.Abs(PlayerPixel(true).B - opaque.B) < 5, "another view of the same map receives opaque wall buffers");
                Check(PlayerPixel().B > opaque.B + 70, "returning to gameplay restores that renderer's wall fades");
                fadeMap.Environment.FadeOccluders = false;
                for (int i = 0; i < 45; i++) PlayerPixel();
                var restored = PlayerPixel();
                Check(Math.Abs(restored.B - opaque.B) < 5, "disabling fade restores the cached opaque wall in " + projection);
            }
            map.WallCells = []; assets.WallMeshes.Synchronize(map);
            Check(assets.WallMeshes.CachedChunkCount == 0, "clearing a map releases its mesh buffers");
            Console.WriteLine("Wall cache GPU checks passed: reference pixels in three views, culling/batching, idle reuse, local/offscreen edits, sculpting, fade masks, light masks, undo and GPU resource release.");
        }
        finally { UnloadRenderTexture(target); }
    }
}
