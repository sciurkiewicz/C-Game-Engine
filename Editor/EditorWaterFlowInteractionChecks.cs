using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunWaterFlowInteractionChecks()
    {
        string output=Path.Combine(ProjectPaths.Root,"Editor","obj");
        Verify(Path.GetFullPath(mapPath).StartsWith(output+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),
            "water flow checks use an isolated map in Editor/obj");
        Directory.CreateDirectory(output);SetWindowSize(1440,900);
        play=null;document.New(32,32);selectedId=null;selectedIds.Clear();EndEditorGesture();
        for(int z=0;z<=document.Map.Depth;z++)
        for(int x=0;x<=document.Map.Width;x++)document.Map.SetVertexHeight(x,z,-2);
        document.Map.WaterCells=Enumerable.Repeat((byte)3,document.Map.Tiles.Length).ToArray();
        document.Map.WaterLevel=0;document.Map.Player.X=5;document.Map.Player.Z=2;
        waterFlowBrush=false;eraseWaterFlow=false;waterFlowWidth=6;waterFlowSpeed=1.2f;
        preferences.ShowMinimap=false;focus=Vector3.Zero;ApplyRuntimeSettings();TestFrame();
        ClickHeader("Save");string still=document.Map.Serialize();
        float[] bed=[..document.Map.Heights];byte[] water=[..document.Map.WaterCells];string[] tiles=[..document.Map.Tiles];
        Verify(!document.Dirty&&document.Map.WaterFlow.Count==0,"the flow brush starts from saved still water");

        ClickHeader("Water");Click(110,287);
        Verify(tool==EditTool.Water&&waterFlowBrush&&!riverBrush,"the Water palette selects a separate flow direction brush");
        int inspectorX=GetScreenWidth()-Right+18;
        float width=waterFlowWidth,speed=waterFlowSpeed;
        Click(inspectorX+201,216);Click(inspectorX+201,276);
        Verify(waterFlowWidth>width&&waterFlowSpeed>speed,"the flow inspector changes brush width and current speed");
        Verify(document.Map.Serialize()==still&&!document.Dirty,"flow brush settings do not edit the map");
        while(waterFlowWidth>3)Click(inspectorX+17,216);
        Verify(waterFlowWidth==3,"the width control can narrow the flow brush for separate bends");

        Vector2 OnScreen(Vector3 point) => GetWorldToScreenEx(point,camera.View,(int)viewport.Width,(int)viewport.Height)+
            new Vector2(viewport.X,viewport.Y);
        void MoveTo(Vector3 point)
        {
            var at=OnScreen(point);
            Verify(CheckCollisionPointRec(at,viewport),"the flow stroke stays inside the viewport");
            Input(7,(int)at.X,(int)at.Y);
        }
        void Stroke(params Vector3[] points)
        {
            MoveTo(points[0]);Input(6,0);TestFrame();
            for(int i=1;i<points.Length;i++){MoveTo(points[i]);TestFrame();}
            Input(5,0);TestFrame();
        }
        bool SameWaterGeometry() => document.Map.Heights.SequenceEqual(bed)&&document.Map.WaterCells.SequenceEqual(water)&&
            document.Map.Tiles.SequenceEqual(tiles);

        Vector3[] course=[new(0,0,-8),new(0,0,0),new(6,0,0)];
        MoveTo(course[0]);Input(6,0);TestFrame();
        for(int i=0;i<5;i++)TestFrame();
        Verify(document.Map.Serialize()==still&&!document.Dirty,
            "clicking and holding a stationary flow brush does not invent a direction or change the map");
        for(int i=1;i<course.Length;i++){MoveTo(course[i]);TestFrame();}
        string held=document.Map.Serialize();for(int i=0;i<5;i++)TestFrame();
        Verify(document.Map.Serialize()==held,"holding the brush after a drag preserves the painted velocity");
        Input(5,0);TestFrame();
        var upstream=document.Map.SampleWaterFlow(0,-5);
        var downstream=document.Map.SampleWaterFlow(4.5f,0);
        Verify(upstream.Y>.2f&&Math.Abs(upstream.X)<upstream.Y*.25f&&
            downstream.X>.2f&&Math.Abs(downstream.Y)<downstream.X*.25f,
            "one bent stroke paints local +Z and +X directions along its separate reaches");
        Verify(SameWaterGeometry()&&document.Map.SampleWaterFlow(-7,-7)==Vector2.Zero,
            "painting local flow preserves terrain, water masks, bed material and distant still water");
        string bent=document.Map.Serialize();
        Verify(document.Dirty,"painting a current marks the map as changed");
        ClickHeader("Undo");Verify(document.Map.Serialize()==still&&!document.Dirty,"one Undo removes the entire bent flow stroke");
        ClickHeader("Redo");Verify(document.Map.Serialize()==bent,"Redo restores the full local flow field");

        Stroke(course[1],course[0]);
        var reversed=document.Map.SampleWaterFlow(0,-5);
        Verify(reversed.Y<-.2f&&Math.Abs(reversed.X)<-reversed.Y*.25f&&document.Map.SampleWaterFlow(4.5f,0).X>.2f,
            "repainting one reach in reverse changes its direction without reversing the other reach");
        string opposite=document.Map.Serialize();
        ClickHeader("Undo");Verify(document.Map.Serialize()==bent,"one Undo restores the direction before repainting");
        ClickHeader("Redo");Verify(document.Map.Serialize()==opposite,"Redo restores the reversed reach");

        Click(inspectorX+110,318);Verify(eraseWaterFlow,"the flow inspector switches to erasing local currents");
        Stroke(course[0],course[1]);
        Verify(document.Map.SampleWaterFlow(0,-5)==Vector2.Zero&&document.Map.SampleWaterFlow(4.5f,0).X>.2f&&SameWaterGeometry(),
            "erasing removes the local current while preserving other reaches and the water itself");
        ClickHeader("Undo");Verify(document.Map.Serialize()==opposite,"one Undo restores the whole erased reach");
        ClickHeader("Undo");Verify(document.Map.Serialize()==bent,"the earlier bent flow remains available in history");
        Click(inspectorX+110,318);Verify(!eraseWaterFlow,"the erase toggle returns to painting directions");

        string beforeModal=document.Map.Serialize();
        Click(inspectorX+110,362);Verify(waterSettingsOpen,"flow mode opens the shared water appearance controls");
        Click(280,520);Press(KeyboardKey.F5);
        Verify(waterSettingsOpen&&play==null&&document.Map.Serialize()==beforeModal,
            "the water appearance modal blocks flow painting and Play behind it");
        Press(KeyboardKey.Escape);
        string beforePan=document.Map.Serialize();
        MoveTo(new(-6,0,-5));Input(6,2);Input(6,0);TestFrame();
        var panStart=GetMousePosition();Input(7,(int)panStart.X+45,(int)panStart.Y+18);TestFrame();
        Input(5,0);Input(5,2);TestFrame();
        Verify(document.Map.Serialize()==beforePan,"camera panning does not paint an accidental current");

        Press(KeyboardKey.M);Verify(preferences.ShowMinimap,"the minimap opens while the flow brush is active");
        string beforeMinimap=document.Map.Serialize();
        var destination=new Vector3(7,0,6);
        var minimapClick=TerrainOverview.Project(document.Map,destination,MinimapMapBounds,MinimapYaw);
        Click((int)minimapClick.X,(int)minimapClick.Y);
        Verify(Math.Abs(focus.X-destination.X)<.4f&&Math.Abs(focus.Z-destination.Z)<.4f&&
            document.Map.Serialize()==beforeMinimap,"a minimap click navigates without editing the flow field");
        Press(KeyboardKey.M);

        // Make a dry test patch without altering its bed, away from the authored bend.
        for(int z=20;z<32;z++)
        for(int x=4;x<13;x++)document.Map.WaterCells[z*document.Map.Width+x]=0;
        ClickHeader("Save");string dryPatch=document.Map.Serialize();
        focus=new(-7,0,9);camera.Snap(focus);TestFrame();
        Stroke(new(-7,0,7),new(-7,0,11));
        Verify(document.Map.Serialize()==dryPatch&&!document.Dirty,
            "dragging the flow brush over dry terrain changes neither geometry, water masks nor local flow");
        focus=Vector3.Zero;camera.Snap(focus);TestFrame();

        ClickHeader("Save");string saved=document.Map.Serialize();
        Verify(!document.Dirty&&MapData.Load(mapPath).Serialize()==saved,"the local flow field saves with the map");
        ClickHeader("Load");Verify(document.Map.Serialize()==saved,"loading restores all painted flow directions");
        Input(7,100,500);TestFrame();WorldRenderer.Screenshot(Path.Combine(output,"water-flow-editor.png"));
        Press(KeyboardKey.F5);
        Verify(play!=null&&play.Map.WaterFlow.Count==document.Map.WaterFlow.Count&&
            play.Map.WaterFlow.All(pair=>document.Map.WaterFlow.TryGetValue(pair.Key,out var flow)&&flow==pair.Value),
            "Play starts with the authored local flow vectors");
        Verify(!ReferenceEquals(play!.Map.WaterFlow,document.Map.WaterFlow),"Play owns an independent flow dictionary");
        for(int i=0;i<5;i++)TestFrame();WorldRenderer.Screenshot(Path.Combine(output,"water-flow-play.png"));
        play.Map.WaterFlow.Clear();
        Verify(document.Map.Serialize()==saved,"changing the Play flow field preserves the authored map");
        Press(KeyboardKey.F5);
        Verify(play==null&&document.Map.Serialize()==saved&&!document.Dirty,"returning from Play preserves saved flow directions");

        SetWindowSize(1120,720);TestFrame();TestFrame();
        ClickHeader("Water");Click(110,287);
        Verify(tool==EditTool.Water&&waterFlowBrush&&!riverBrush,"the flow brush remains accessible in the minimum editor window");
        string minimumMap=document.Map.Serialize();
        float minimumWidth=waterFlowWidth,minimumSpeed=waterFlowSpeed;
        int minimumInspectorX=GetScreenWidth()-Right+18;
        Click(minimumInspectorX+201,216);Click(minimumInspectorX+201,276);
        Verify(waterFlowWidth>minimumWidth&&waterFlowSpeed>minimumSpeed&&document.Map.Serialize()==minimumMap&&!document.Dirty,
            "minimum-window width and speed controls work without changing the map");
        Click(minimumInspectorX+110,318);Verify(eraseWaterFlow,"the minimum-window erase control remains accessible");
        Click(minimumInspectorX+110,318);Verify(!eraseWaterFlow,"the minimum-window erase control returns to painting");
        Click(minimumInspectorX+110,362);Verify(waterSettingsOpen,"the minimum-window flow inspector opens water appearance");
        var waterBox=WaterSettingsBounds;
        Verify(waterBox.X>=0&&waterBox.Y>=0&&waterBox.X+waterBox.Width<=GetScreenWidth()&&
            waterBox.Y+waterBox.Height<=GetScreenHeight(),"flow appearance controls fit the minimum editor window");
        Press(KeyboardKey.Escape);
        Verify(!waterSettingsOpen&&document.Map.Serialize()==minimumMap&&!document.Dirty,
            "Escape closes minimum-window water appearance without editing the flow field");
        Input(7,100,500);TestFrame();WorldRenderer.Screenshot(Path.Combine(output,"water-flow-minimum.png"));
        SetWindowSize(1440,900);TestFrame();TestFrame();
        Console.WriteLine("Water flow UI checks passed: palette, width/speed, local bent directions, stationary input, one-step undo/redo, reversal, erase, dry terrain, modal/pan/minimap isolation, save/load, independent Play and minimum-window controls.");
    }
}
