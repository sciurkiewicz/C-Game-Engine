using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunRiverBrushInteractionChecks()
    {
        string output=Path.Combine(ProjectPaths.Root,"Editor","obj");
        Verify(Path.GetFullPath(mapPath).StartsWith(output+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),
            "river brush checks use an isolated map in Editor/obj");
        Directory.CreateDirectory(output);SetWindowSize(1440,900);
        play=null;document.New(32,32);selectedId=null;selectedIds.Clear();EndEditorGesture();
        document.Map.Player.X=5;document.Map.Player.Z=2;
        document.Map.WaterLevel=0;focus=Vector3.Zero;ApplyRuntimeSettings();
        riverWidth=4;riverDepth=1.5f;riverBrush=false;
        preferences.ShowMinimap=false;TestFrame();
        ClickHeader("Save");string dry=document.Map.Serialize();
        Verify(!document.Dirty,"the river starts from a saved dry map");

        ClickHeader("Water");Click(110,245);
        Verify(tool==EditTool.Water&&riverBrush,"the Water palette selects the river brush");
        int inspectorX=GetScreenWidth()-Right+18;
        float width=riverWidth,depth=riverDepth;
        Click(inspectorX+201,216);Click(inspectorX+201,276);
        Verify(riverWidth>width&&riverDepth>depth,"the river inspector changes brush width and channel depth");
        Verify(document.Map.Serialize()==dry&&!document.Dirty,"brush settings do not edit the map before painting");

        Vector2 OnScreen(Vector3 point) => GetWorldToScreenEx(point,camera.View,(int)viewport.Width,(int)viewport.Height)+
            new Vector2(viewport.X,viewport.Y);
        void MoveTo(Vector3 point)
        {
            var at=OnScreen(point);
            Verify(CheckCollisionPointRec(at,viewport),"the river stroke stays inside the viewport");
            Input(7,(int)at.X,(int)at.Y);
        }
        Vector3[] course=[new(0,0,-7),new(-1,0,-2),new(2,0,4)];
        MoveTo(course[0]);Input(6,0);TestFrame();
        Verify(document.Map.WaterCells.Any(mask=>mask!=0)&&document.Map.HeightAt(course[0].X,course[0].Z)<0,
            "pressing the brush digs a channel and places water on flat ground");
        string stationary=document.Map.Serialize();
        for(int i=0;i<5;i++)TestFrame();
        Verify(document.Map.Serialize()==stationary,"holding a stationary river brush does not deepen or drift the channel");
        for(int i=1;i<course.Length;i++){MoveTo(course[i]);TestFrame();}
        Input(5,0);TestFrame();
        Verify(course.All(point=>document.Map.WaterDepthAt(point.X,point.Z)>.1f),
            "the stroke follows the painted bends and keeps water along the course");
        for(int segment=1;segment<course.Length;segment++)
        for(int sample=1;sample<4;sample++)
        {
            var point=Vector3.Lerp(course[segment-1],course[segment],sample/4f);
            Verify(document.Map.WaterDepthAt(point.X,point.Z)>.1f,"fast cursor moves leave no dry gaps between brush samples");
        }
        Verify(document.Map.WaterDepthAt(9,-9)==0&&document.Map.HeightAt(9,-9)==0,
            "painting a river preserves distant dry terrain");
        string river=document.Map.Serialize();
        Verify(document.Dirty,"a painted river marks the map as changed");
        ClickHeader("Undo");Verify(document.Map.Serialize()==dry&&!document.Dirty,"one Undo removes the entire bent river stroke");
        ClickHeader("Redo");Verify(document.Map.Serialize()==river,"Redo restores the full channel and its water");

        string beforeModal=document.Map.Serialize();
        Click(inspectorX+110,422);Verify(waterSettingsOpen,"river mode opens the shared water appearance controls");
        Click(280,520);Press(KeyboardKey.F5);
        Verify(waterSettingsOpen&&play==null&&document.Map.Serialize()==beforeModal,
            "the appearance modal blocks river painting and Play behind it");
        Press(KeyboardKey.Escape);

        string beforePan=document.Map.Serialize();
        MoveTo(new(7,0,-5));Input(6,2);Input(6,0);TestFrame();
        var panStart=GetMousePosition();Input(7,(int)panStart.X+45,(int)panStart.Y+18);TestFrame();
        Input(5,0);Input(5,2);TestFrame();
        Verify(document.Map.Serialize()==beforePan,"camera panning never paints an accidental river stroke");

        Press(KeyboardKey.M);Verify(preferences.ShowMinimap,"the minimap opens while the river brush is active");
        string beforeMinimap=document.Map.Serialize();
        var destination=new Vector3(7,0,6);
        var minimapClick=TerrainOverview.Project(document.Map,destination,MinimapMapBounds,MinimapYaw);
        Click((int)minimapClick.X,(int)minimapClick.Y);
        Verify(Math.Abs(focus.X-destination.X)<.4f&&Math.Abs(focus.Z-destination.Z)<.4f&&
            document.Map.Serialize()==beforeMinimap,"a minimap click navigates without painting a river");
        Press(KeyboardKey.M);focus=Vector3.Zero;camera.Snap(focus);TestFrame();

        ClickHeader("Save");string saved=document.Map.Serialize();
        Verify(!document.Dirty&&MapData.Load(mapPath).Serialize()==saved,"the river channel and water save together");
        ClickHeader("Load");Verify(document.Map.Serialize()==saved,"loading restores the painted river");
        Input(7,100,500);TestFrame();WorldRenderer.Screenshot(Path.Combine(output,"river-brush-editor.png"));
        Press(KeyboardKey.F5);
        Verify(play!=null&&play.Map.Heights.SequenceEqual(document.Map.Heights)&&play.Map.WaterCells.SequenceEqual(document.Map.WaterCells),
            "Play starts with the painted channel and water");
        Verify(!ReferenceEquals(play!.Map.Heights,document.Map.Heights)&&!ReferenceEquals(play.Map.WaterCells,document.Map.WaterCells),
            "Play owns independent river geometry and water masks");
        for(int i=0;i<5;i++)TestFrame();WorldRenderer.Screenshot(Path.Combine(output,"river-brush-play.png"));
        play.Map.SetVertexHeight(0,0,-1);play.Map.WaterCells[0]=3;
        Verify(document.Map.Serialize()==saved,"changes to a Play copy preserve the authored river");
        Press(KeyboardKey.F5);
        Verify(play==null&&document.Map.Serialize()==saved&&!document.Dirty,"returning from Play preserves the saved river");

        SetWindowSize(1120,720);TestFrame();TestFrame();
        ClickHeader("Water");Click(110,245);
        Verify(tool==EditTool.Water&&riverBrush,"the river brush remains accessible in the minimum editor window");
        string minimumMap=document.Map.Serialize();
        float minimumWidth=riverWidth,minimumDepth=riverDepth;
        int minimumInspectorX=GetScreenWidth()-Right+18;
        Click(minimumInspectorX+201,216);Click(minimumInspectorX+201,276);
        Verify(riverWidth>minimumWidth&&riverDepth>minimumDepth&&document.Map.Serialize()==minimumMap&&!document.Dirty,
            "the minimum-window width and depth controls work without changing the map");
        Click(minimumInspectorX+110,422);
        Verify(waterSettingsOpen,"the minimum-window river inspector opens water appearance");
        var waterBox=WaterSettingsBounds;
        Verify(waterBox.X>=0&&waterBox.Y>=0&&waterBox.X+waterBox.Width<=GetScreenWidth()&&
            waterBox.Y+waterBox.Height<=GetScreenHeight(),"water appearance fits the minimum editor window");
        Press(KeyboardKey.Escape);
        Verify(!waterSettingsOpen&&document.Map.Serialize()==minimumMap&&!document.Dirty,
            "Escape closes the minimum-window appearance dialog without editing the river");
        Input(7,100,500);TestFrame();WorldRenderer.Screenshot(Path.Combine(output,"river-brush-minimum.png"));
        SetWindowSize(1440,900);TestFrame();TestFrame();
        Console.WriteLine("River brush UI checks passed: palette, width/depth, bent continuous stroke, stationary input, one-step undo/redo, modal/pan/minimap isolation, save/load, independent Play and minimum-window controls.");
    }
}
