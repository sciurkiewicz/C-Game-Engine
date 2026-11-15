using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    bool waterFlowBrush,eraseWaterFlow,waterFlowStrokeActive,waterFlowStrokeChanged;
    float waterFlowWidth=6,waterFlowSpeed=1.2f;
    Vector3? waterFlowLastPoint;
    Vector2 waterFlowLastMouse;
    Vector2 waterFlowLastDirection=Vector2.UnitX;

    void EndWaterFlowStroke()
    {
        if(waterFlowStrokeActive)gesture=false;
        waterFlowStrokeActive=waterFlowStrokeChanged=false;waterFlowLastPoint=null;
    }
    void SelectWaterFlowBrush()
    {
        EndEditorGesture();waterFlowBrush=true;riverBrush=false;
        status="Kierunek nurtu: przeciągnij LMB zgodnie z biegiem rzeki. Strzałki pokazują kierunek fal.";
    }
    void DrawWaterFlowInspector(int x)
    {
        Label("Kierunek nurtu",x,147,16,Accent);
        Stepper("Szerokość pędzla (m)",waterFlowWidth,x,184,v=>
        { EndEditorGesture();waterFlowWidth=Math.Clamp(v,2,32); },.5f);
        Stepper("Prędkość nurtu (m/s)",waterFlowSpeed,x,244,v=>
        { EndEditorGesture();waterFlowSpeed=Math.Clamp(v,.2f,4); },.2f);
        if(Button(eraseWaterFlow?"Usuń nurt: ON":"Maluj kierunek",x,304,218,eraseWaterFlow))
        { EndEditorGesture();eraseWaterFlow=!eraseWaterFlow; }
        if(Button("Wygląd wody...",x,348,218))OpenWaterSettings();
        Label("Ruch pędzla wyznacza kierunek fal.",x,402,12,Accent,218);
        Label("Malowanie działa na istniejącej wodzie.",x,428,12,Muted,218);
    }
    void UpdateWaterFlowBrush()
    {
        if(!InViewport||ModalOpen||IsMouseButtonDown(MouseButton.Right)||IsMouseButtonDown(MouseButton.Middle))
        { EndWaterFlowStroke();return; }
        var point=RiverCursorPoint;
        if(point==null||!IsMouseButtonDown(MouseButton.Left)) { EndWaterFlowStroke();return; }
        bool startedStroke=false;
        if(!waterFlowStrokeActive)
        {
            if(!IsMouseButtonPressed(MouseButton.Left))return;
            waterFlowStrokeActive=true;waterFlowStrokeChanged=false;waterFlowLastPoint=point;
            waterFlowLastMouse=GetMousePosition();gesture=true;startedStroke=true;
        }
        var from=waterFlowLastPoint??point.Value;
        var delta=new Vector2(point.Value.X-from.X,point.Value.Z-from.Z);
        // Retain the anchor until there is a meaningful drag; a click cannot invent a direction.
        bool mouseMoved=Vector2.DistanceSquared(GetMousePosition(),waterFlowLastMouse)>=1;
        if(!eraseWaterFlow&&(!mouseMoved||delta.LengthSquared()<.01f))return;
        if(eraseWaterFlow&&!startedStroke&&!mouseMoved)return;
        if(delta.LengthSquared()>=.01f)waterFlowLastDirection=Vector2.Normalize(delta);
        bool changed=document.PaintWaterFlowSegment(from,point.Value,waterFlowWidth,waterFlowSpeed,
            erase:eraseWaterFlow,checkpoint:!waterFlowStrokeChanged);
        waterFlowStrokeChanged|=changed;waterFlowLastPoint=point;waterFlowLastMouse=GetMousePosition();
        status=eraseWaterFlow ? "Usuwanie namalowanego nurtu. Ctrl+Z cofa całe pociągnięcie." :
            "Fale płyną zgodnie z ruchem pędzla. Puść LMB, aby zakończyć.";
    }
    void DrawFlowArrow(Vector3 point,Vector2 velocity,Color color)
    {
        float speed=velocity.Length();if(speed<.03f)return;
        var direction=velocity/speed;
        float length=.65f+Math.Min(speed,3)*.22f;
        var axis=new Vector3(direction.X,0,direction.Y);
        var side=new Vector3(-direction.Y,0,direction.X);
        point.Y=WaterGeometry.SurfaceHeight(document.Map,point.X,point.Z,(float)(GetTime()%3600))+.12f;
        var start=point-axis*length*.5f;var tip=point+axis*length*.5f;
        DrawLine3D(start,tip,color);
        DrawLine3D(tip,tip-axis*.3f+side*.18f,color);
        DrawLine3D(tip,tip-axis*.3f-side*.18f,color);
    }
    void DrawWaterFlowOverlay()
    {
        if(tool!=EditTool.Water||!waterFlowBrush)return;
        // Only inspect visible cells, at a legible spacing even when the whole map is in view.
        var bounds=TerrainVisibility.GetBounds(document.Map,camera.View,(int)viewport.Width,(int)viewport.Height,
            camera.IsFree?128:0);
        int spacing=Math.Max(2,(int)Math.Ceiling(Math.Sqrt(bounds.TileCount/350.0)));
        for(int z=bounds.MinZ;z<bounds.MaxZ;z+=spacing)
        for(int x=bounds.MinX;x<bounds.MaxX;x+=spacing)
        {
            var point=new Vector3(x-document.Map.Width/2f+.5f,document.Map.WaterLevel,z-document.Map.Depth/2f+.5f);
            var flow=document.Map.SampleWaterFlow(point.X,point.Z);
            DrawFlowArrow(point,flow,new(137,232,218,230));
        }
    }
    void DrawWaterFlowPreview()
    {
        if(!InViewport||ModalOpen||RiverCursorPoint is not { } point)return;
        var color=eraseWaterFlow?new Color(255,177,100,255):Accent;
        renderer.DrawBrush(document.Map,point,waterFlowWidth*.5f,color,false,document.Map.WaterLevel);
        if(!eraseWaterFlow)DrawFlowArrow(point,waterFlowLastDirection*waterFlowSpeed,color);
    }
}
