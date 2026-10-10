using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    void RunTerrainInteractionChecks()
    {
        document.New(32, 32); focus = Vector3.Zero; camera.Snap(focus); TestFrame();
        ClickHeader("Sculpt");
        var screen = GetWorldToScreenEx(Vector3.Zero, camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        int mx = (int)screen.X, my = (int)screen.Y;
        Input(7, mx, my); Input(6, 0);
        for (int i = 0; i < 30; i++) { Input(7, mx, my); TestFrame(); }
        Input(5, 0); TestFrame();
        float height = document.Map.HeightAt(0, 0);
        Verify(height > .5f, "sculpt tool raises terrain under the cursor");
        Verify(document.Map.Heights.All(h => h == MapData.SnapElevation(h)), "sculpt stores elevations in 0.25 steps");
        ClickHeader("Undo"); Verify(document.Map.HeightAt(0, 0) == 0, "one undo restores a held sculpt stroke");
        ClickHeader("Redo"); Verify(document.Map.HeightAt(0, 0) > .5f, "redo restores sculpt stroke");
        ClickHeader("Sculpt"); Click(80, 204); // Lower mode.
        Verify(sculptMode == SculptMode.Lower, "sculpt mode palette");
        ClickHeader("Undo"); // Start from a flat surface, then dig with the actual sculpt tool.
        focus = Vector3.Zero; camera.Snap(focus); TestFrame();
        screen = GetWorldToScreenEx(Vector3.Zero, camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        mx = (int)screen.X; my = (int)screen.Y;
        Input(7, mx, my); Input(6, 0);
        for (int i = 0; i < 40; i++) { Input(7, mx, my); TestFrame(); }
        Input(5, 0); TestFrame();
        Verify(document.Map.HeightAt(0, 0) < -.5f, "lower brush digs a basin before adding water");
        var bed = (float[])document.Map.Heights.Clone();
        focus = Vector3.Zero; camera.Snap(focus); TestFrame();
        screen = GetWorldToScreenEx(document.Map.GroundPosition(0, 0), camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        mx = (int)screen.X; my = (int)screen.Y;
        ClickHeader("Water"); Click(mx, my);
        Verify(document.Map.WaterCells.Any(mask => mask != 0) && document.Map.Heights.SequenceEqual(bed), "one click fills the basin without changing its bed");
        Verify(!WorldRenderer.CanWalk(document.Map, document.Map.GroundPosition(.5f, .5f)), "new deep lake blocks runtime walking");
        ClickHeader("Undo"); Verify(document.Map.WaterDepthAt(0, 0) == 0 && document.Map.Heights.SequenceEqual(bed), "undo fill preserves the sculpted basin");
        ClickHeader("Redo"); Click(80, 204); Click(mx, my);
        Verify(document.Map.WaterDepthAt(0, 0) == 0 && document.Map.Heights.SequenceEqual(bed), "drain tool removes water without changing terrain");
        Click(80, 160); Click(mx, my);
        Verify(document.Map.WaterDepthAt(0, 0) > .5f, "drained basin can be filled again");
        // A picturesque fixture also exercises triangles, grounded sprites and clipped shores.
        var demo = TerrainChecks.CreateDemo();
        string fixture = Path.Combine(ProjectPaths.Root, "Editor", "obj", "terrain-water-test.json");
        demo.Save(fixture); document.Load(fixture); selectedId = null;
        focus = Vector3.Zero; camera.Zoom(2); camera.Snap(focus); tool = EditTool.Select;
        TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "terrain-water-editor.png"));
        var tree = document.Map.Objects[0];
        var quad = renderer.GetSpriteQuad(camera.View, tree.Asset, document.Map.GroundPosition(tree), tree.Height);
        var spriteScreen = GetWorldToScreenEx(quad.Center, camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
        Click((int)spriteScreen.X, (int)spriteScreen.Y);
        Verify(selectedId == tree.Id, "picking uses the elevated sprite position");
        Click(1360, 60);
        Verify(play != null && Math.Abs(play.Player.Y - document.Map.HeightAt(play.Player.X, play.Player.Z)) < .001f, "playtest player starts on sculpted terrain");
        Input(2, (int)KeyboardKey.D);
        for (int i = 0; i < 10; i++) TestFrame();
        Input(1, (int)KeyboardKey.D); TestFrame();
        Verify(Math.Abs(play!.Player.Y - play.Map.HeightAt(play.Player.X, play.Player.Z)) < .001f, "movement follows terrain elevation");
        TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "terrain-water-playtest.png"));
        Press(KeyboardKey.F5); Complete("load");
    }
}
