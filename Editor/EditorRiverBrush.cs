using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    bool riverBrush,riverStrokeActive,riverStrokeChanged;
    float riverWidth=4,riverDepth=1.5f;
    Vector3? riverLastPoint;

    Vector3? RiverCursorPoint
    {
        get
        {
            // Pick a fixed water plane so carving the bed cannot move a stationary cursor's target.
            var ray=MouseRay;
            if(Math.Abs(ray.Direction.Y)<.0001f)return null;
            float distance=(document.Map.WaterLevel-ray.Position.Y)/ray.Direction.Y;
            if(distance<0)return null;
            var point=ray.Position+ray.Direction*distance;
            return document.Map.Contains(point.X,point.Z)?point:null;
        }
    }
    void EndRiverStroke()
    {
        if(riverStrokeActive)gesture=false;
        riverStrokeActive=riverStrokeChanged=false;riverLastPoint=null;
    }
    void SetWaterMode(bool river,bool erase=false)
    {
        EndEditorGesture();riverBrush=river;waterFlowBrush=false;eraseWater=erase;
        status=river ? "Pędzel rzeki: przytrzymaj LMB i narysuj trasę. Ctrl+Z cofa całe pociągnięcie." :
            erase ? "Click inside a filled basin to drain it." : "Click below the water level to fill a basin.";
    }
    void DrawWaterPalette()
    {
        if(Button("Fill basin",16,146,218,!riverBrush&&!waterFlowBrush&&!eraseWater,play==null))SetWaterMode(false);
        if(Button("Drain basin",16,188,218,!riverBrush&&!waterFlowBrush&&eraseWater,play==null))SetWaterMode(false,true);
        if(Button("Pędzel rzeki",16,230,218,riverBrush,play==null))SetWaterMode(true);
        if(Button("Kierunek nurtu",16,272,218,waterFlowBrush,play==null))SelectWaterFlowBrush();
        if(waterFlowBrush)
        {
            Label("Przeciągnij zgodnie z nurtem.",16,324,13,Accent);
            Label("Każdy zakręt ma własny kierunek.",16,350,12,Muted,218);
            Label("Strzałki pokazują ruch wody.",16,376,12,Muted,218);
            Label("Ponowne malowanie zmienia kierunek.",16,414,12,Muted,218);
            Label("Ctrl+Z cofa całe pociągnięcie.",16,442,12,Accent,218);
        }
        else if(riverBrush)
        {
            Label("Przeciągnij LMB po mapie.",16,324,13,Accent);
            Label("Pędzel wykopie i wypełni koryto.",16,350,12,Muted,218);
            Label("Szerokość i głębokość: po prawej.",16,376,12,Muted,218);
            Label("Jedno pociągnięcie = jedno cofnięcie.",16,414,12,Accent,218);
            Label("Poziom wody jest wspólny dla mapy.",16,442,12,Muted,218);
        }
        else
        {
            Label("Click inside a terrain depression.",16,324,13,Muted);
            Label("Level is shared by this map.",16,350,13,Muted);
            Label("Shore follows the terrain.",16,382,13,Accent);
            Label("Dig the basin with Sculpt first.",16,414,13,Muted);
        }
    }
    void DrawRiverInspector(int x)
    {
        Label("Pędzel rzeki",x,147,16,Accent);
        Stepper("Szerokość (m)",riverWidth,x,184,v=>
        { EndEditorGesture();riverWidth=Math.Clamp(v,2,32); },.5f);
        Stepper("Głębokość (m)",riverDepth,x,244,v=>
        { EndEditorGesture();riverDepth=Math.Clamp(v,.25f,8); },.25f);
        Stepper("Poziom wody",document.Map.WaterLevel,x,304,document.SetWaterLevel,.25f);
        if(Button(document.Map.WaveHeight>0?"Waves: ON":"Waves: OFF",x,364,218,document.Map.WaveHeight>0))
        { document.Checkpoint();document.Map.WaveHeight=document.Map.WaveHeight>0?0:.08f; }
        if(Button("Wygląd wody...",x,408,218))OpenWaterSettings();
    }
    void UpdateRiverBrush()
    {
        if(!InViewport||ModalOpen||IsMouseButtonDown(MouseButton.Right)||IsMouseButtonDown(MouseButton.Middle))
        { EndRiverStroke();return; }
        var point=RiverCursorPoint;
        if(point==null||!IsMouseButtonDown(MouseButton.Left)) { EndRiverStroke();return; }
        if(!riverStrokeActive)
        {
            if(!IsMouseButtonPressed(MouseButton.Left))return;
            riverStrokeActive=true;riverStrokeChanged=false;riverLastPoint=point;gesture=true;
        }
        bool changed=document.PaintRiverSegment(riverLastPoint??point.Value,point.Value,riverWidth,riverDepth,
            checkpoint:!riverStrokeChanged);
        riverStrokeChanged|=changed;riverLastPoint=point;
        status=riverStrokeChanged ? "Rysowanie rzeki. Puść LMB, aby zakończyć; Ctrl+Z cofnie całą trasę." :
            "Koryto jest już gotowe. Maluj dalej, aby przedłużyć rzekę.";
    }
    void DrawRiverBrushPreview()
    {
        if(!InViewport||ModalOpen||RiverCursorPoint is not { } point)return;
        // Draw on the actual ground before digging and at water level inside the new channel.
        renderer.DrawBrush(document.Map,point,riverWidth*.5f,Accent,false,document.Map.WaterLevel);
        var bed=document.Map.GroundPosition(point.X,point.Z);
        DrawLine3D(bed,new(point.X,document.Map.WaterLevel,point.Z),new(80,183,255,180));
    }
}
