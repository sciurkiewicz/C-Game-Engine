using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunWorldFeatureInteractionChecks()
    {
        string library = prefabDirectory;
        try
        {
            prefabDirectory = Path.Combine(ProjectPaths.Root, "Editor", "obj", "world-prefabs-" + Guid.NewGuid().ToString("N"));
            SetWindowSize(1440, 900); document.New(32, 32); tool = EditTool.Select; selectedId = null; selectedIds.Clear();
            worldPage = null; activePrefab = null; focus = Vector3.Zero; ApplyRuntimeSettings(); document.Map.Environment.Audio = false;
            var asset = assets.Sprites.Values.First(a => a.Id.Contains("rocks/"));
            var first = document.Place(asset.Id, new(-3, 0, 2), 2, true);
            var second = document.Place(asset.Id, new(3, 0, 2), 2, true); TestFrame();
            void GroundClick(Vector3 point)
            {
                var screen = GetWorldToScreenEx(point, camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
                Click((int)screen.X, (int)screen.Y);
            }
            void ObjectClick(SceneObject item)
            {
                GroundClick(renderer.GetSpriteQuad(camera.View, item.Asset, document.Map.GroundPosition(item), item.Height).Center);
            }
            Click(GetScreenWidth() - 288, 23); Verify(worldPage == "Environment" && ModalOpen, "World button opens tools");
            var box = WorldToolsBounds; int x = (int)box.X, y = (int)box.Y;
            Press(KeyboardKey.F5); Press(KeyboardKey.Nine);
            Verify(play == null && tool == EditTool.Select, "world modal blocks background shortcuts");
            Click(x + 154, y + 365); Verify(document.Map.Environment.TimeOfDay == 11.5f, "environment starting hour control");
            Click(x + 180, y + 323); Verify(document.Map.Environment.DayNightCycle, "day/night toggle");
            Click(x + 220, y + 101); Click(x + 136, y + 489);
            Verify(document.Map.Lights.Count == 1, "add point light");
            Click(x + 782, y + 199); Verify(document.Map.Lights[0].Radius == 6.5f, "light radius control");
            Click(x + 352, y + 101); Click(x + 136, y + 489);
            Verify(document.Map.Sounds.Count == 1, "add sound emitter");
            Click(x + 500, y + 259); Verify(document.Map.Sounds[0].Clip == "water", "sound selector");
            Click(x + 484, y + 101); Click(x + 136, y + 489);
            Verify(drawingTrigger && worldPage == null, "trigger drawing mode");
            var a = GetWorldToScreenEx(new(-2, 0, -2), camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
            var b = GetWorldToScreenEx(new(2, 0, 2), camera.View, (int)viewport.Width, (int)viewport.Height) + new Vector2(viewport.X, viewport.Y);
            Input(7, (int)a.X, (int)a.Y); Input(6, 0); TestFrame();
            Input(7, (int)b.X, (int)b.Y); TestFrame(); Input(5, 0); TestFrame();
            Verify(worldPage == "Triggers" && document.Map.Triggers.Count == 1 && document.Map.Triggers[0].Width == 4 && document.Map.Triggers[0].Depth == 4,
                "dragging creates a trigger rectangle");
            Click(x + 500, y + 381); Verify(textEdit != null, "trigger message editor");
            AppendEdit("Witaj w świecie!"); Press(KeyboardKey.Enter);
            Verify(document.Map.Triggers[0].Text == "Witaj w świecie!", "trigger message applies");
            Press(KeyboardKey.Escape); selectedId = first.Id; selectedIds.Clear();
            Click(GetScreenWidth() - 288, 23); Click(x + 748, y + 101); Click(x + 720, y + 197);
            Verify(Selection!.Collider?.Kind == ColliderKind.Polygon, "polygon collider selection");
            Click(x + 180, y + 397); Verify(drawingColliderId == first.Id && worldPage == null, "draw polygon mode");
            foreach (var point in new Vector3[] { new(-4, 0, 1), new(-2, 0, 1), new(-2, 0, 3), new(-4, 0, 3) }) GroundClick(point);
            Press(KeyboardKey.Enter);
            Verify(worldPage == "Collision" && Selection!.Collider?.Points.Count == 4 && !WorldRenderer.CanWalk(document.Map, first.Position), "drawn collider blocks movement");
            Click(x + 600, y + 247); Click(x + 600, y + 291);
            Verify(Selection!.IsDoor && Selection.DoorOpen && WorldRenderer.CanWalk(document.Map, first.Position), "door flags open the collider");
            Press(KeyboardKey.Escape); ObjectClick(first);
            Input(2, (int)KeyboardKey.LeftShift); ObjectClick(second); Input(1, (int)KeyboardKey.LeftShift); TestFrame();
            Verify(SelectionGroup.Length == 2, "Shift+click selects a group");
            Input(2, (int)KeyboardKey.LeftControl); Press(KeyboardKey.D); Input(1, (int)KeyboardKey.LeftControl); TestFrame();
            Verify(document.Map.Objects.Count == 4 && SelectionGroup.Length == 2, "group duplication");
            ClickHeader("Undo"); Verify(document.Map.Objects.Count == 2, "group duplicate is one undo step");
            first = document.Map.Objects[0]; second = document.Map.Objects[1]; ObjectClick(first);
            Input(2, (int)KeyboardKey.LeftShift); ObjectClick(second); Input(1, (int)KeyboardKey.LeftShift); TestFrame();
            Click(GetScreenWidth() - 288, 23); Click(x + 616, y + 101); Click(x + 136, y + 489);
            Verify(textEdit != null, "prefab name editor"); AppendEdit("Test camp"); Press(KeyboardKey.Enter);
            Verify(prefabs.Count == 1 && prefabs[0].Prefab.Entities.Count() == 2 && File.Exists(prefabs[0].Path), "prefab is saved in the library");
            Click(x + 500, y + 401); Verify(activePrefab != null && worldPage == null, "prefab placement mode");
            GroundClick(new(0, 0, -4)); Verify(document.Map.Objects.Count == 4 && document.Map.Objects.Select(o => o.Id).Distinct().Count() == 4, "map click instantiates the prefab");
            Press(KeyboardKey.Escape); ClickHeader("Save"); string saved = document.Map.Serialize();
            Verify(MapData.Load(mapPath).Serialize() == saved, "save/load preserves all world settings");
            Press(KeyboardKey.F5); Verify(play != null && play.FiredTriggers.Count == 1 && play.Notice == "Witaj w świecie!", "Play executes trigger areas");
            var spawn = play!.Player; Press(KeyboardKey.F6); Verify(File.Exists(play.ProgressPath), "F6 saves progress");
            Input(2, (int)KeyboardKey.D); for (int i = 0; i < 10; i++) TestFrame(); Input(1, (int)KeyboardKey.D); TestFrame();
            Verify(Vector3.Distance(play.Player, spawn) > .1f, "move after save");
            Press(KeyboardKey.F9); Verify(Vector3.Distance(play.Player, spawn) < .01f && play.FiredTriggers.Count == 1, "F9 restores progress and trigger state");
            Press(KeyboardKey.F5); Verify(document.Map.Serialize() == saved, "Play and progress preserve the authoring map");
            Click(GetScreenWidth() - 288, 23); SetWindowSize(1120, 720); TestFrame(); TestFrame(); box = WorldToolsBounds;
            Verify(box.X >= 0 && box.Y >= 0 && box.X + box.Width <= GetScreenWidth() && box.Y + box.Height <= GetScreenHeight(), "world tools fit minimum window");
            WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "world-tools.png"));
            Press(KeyboardKey.Escape); SetWindowSize(1440, 900); TestFrame(); TestFrame();
            Console.WriteLine("World UI checks passed: environment, light/sound controls, drawn triggers/polygons, doors, group selection/duplicate, prefab library/placement, save/load and F6/F9 progress.");
        }
        finally { prefabDirectory = library; }
    }
}
