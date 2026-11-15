using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class WaterFlowGpuChecks
{
    public static void Run(WorldAssets assets)
    {
        const int size=512;
        const string neutralId="terrain/checks/water-flow-neutral.png";
        var source=GenImageColor(8,8,new(132,142,137,255));
        var texture=LoadTextureFromImage(source);UnloadImage(source);
        assets.Terrain.Add(neutralId,texture);
        var target=LoadRenderTexture(size,size);
        var images=new List<Image>();
        var renderer=new WorldRenderer(assets);
        var map=MapData.CreateEmpty(32,32);
        Array.Fill(map.Tiles,neutralId);
        map.Heights=Enumerable.Repeat(-2f,map.VertexCount).ToArray();
        // A dry strip and distant unpainted water verify that flow remains local.
        for(int z=0;z<=map.Depth;z++)
        for(int x=22;x<=map.Width;x++)map.SetVertexHeight(x,z,1);
        map.WaterCells=Enumerable.Repeat((byte)3,map.Tiles.Length).ToArray();
        map.WaterLevel=0;map.WaveHeight=.06f;
        map.WaterAppearance=new() { Clarity=2,ReflectionStrength=.9f,RefractionStrength=.2f,FoamStrength=0 };
        map.Environment.Shadows=false;map.Environment.FadeOccluders=false;
        var view=new Camera3D { Position=new(0,20,0),Target=Vector3.Zero,Up=-Vector3.UnitZ,
            FovY=20,Projection=CameraProjection.Orthographic };
        var settings=new GraphicsSettings { Enabled=false };
        var hiddenPlayer=new Vector3(-100,0,-100);
        Rectangle Patch(float x,float z,int radius)
        {
            var point=GetWorldToScreenEx(new(x,0,z),view,size,size);
            Check(point.X>radius&&point.Y>radius&&point.X<size-radius&&point.Y<size-radius,
                "flow sample lies inside the viewport");
            return new((int)point.X-radius,(int)point.Y-radius,radius*2,radius*2);
        }
        Image Capture(float time)
        {
            var frame=new WorldVisualFrame(settings,time,view,12,true);
            renderer.PrepareWater(map,view,size,size,12,frame,hiddenPlayer);
            BeginTextureMode(target);ClearBackground(new(38,47,54,255));BeginMode3D(view);
            renderer.Draw(map,view,false,hiddenPlayer,size,size,12,visuals:frame);
            EndMode3D();EndTextureMode();
            var image=LoadImageFromTexture(target.Texture);ImageFlipVertical(ref image);images.Add(image);
            return image;
        }
        void SetLeftFlow(Vector2 velocity,bool bend=false)
        {
            map.WaterFlow.Clear();
            for(int z=0;z<map.Depth;z++)
            for(int x=0;x<map.Width/2-1;x++)
            {
                var flow=velocity;
                if(bend)
                {
                    float blend=Math.Clamp((z-map.Depth/2f+2)/4,0,1);
                    blend=blend*blend*(3-2*blend);
                    float angle=blend*MathF.PI*.5f;
                    flow=new(MathF.Cos(angle)*velocity.Length(),MathF.Sin(angle)*velocity.Length());
                }
                map.WaterFlow[z*map.Width+x]=new(flow.X,flow.Y);
            }
        }
        void Preview(Image image,string name)
        {
            string output=Path.Combine(ProjectPaths.Root,"Editor","obj");Directory.CreateDirectory(output);
            Check(ExportImage(image,Path.Combine(output,"water-flow-"+name+".png")),"flow preview is saved");
        }
        try
        {
            Check(IsShaderValid(assets.WaterShader)&&assets.WaterShader.Id!=Rlgl.GetShaderIdDefault(),
                "the water flow shader compiles instead of falling back to the default shader");
            var painted=Patch(-4,0,64);
            var remote=Patch(3,0,32);
            var dry=Patch(8,0,24);
            const float time=.8f;
            var unpainted=Capture(time);
            SetLeftFlow(new(1.2f,0));
            var forward=Capture(time);
            var repeated=Capture(time);
            double deterministic=Difference(forward,repeated,painted);
            Check(deterministic<.05,$"the same flow frame renders deterministically ({deterministic:0.000})");
            double authored=Difference(unpainted,forward,painted);
            Check(authored>.15,$"painted current visibly changes the local wave field ({authored:0.000})");
            Check(Difference(unpainted,forward,remote)<.1,"a painted current preserves distant unpainted water");
            Check(Difference(unpainted,forward,dry)<.1,"a painted current preserves dry terrain");
            // In this top-down view +world X is +screen X. Track the actual shaded pattern,
            // comparing its predicted displacement against a displacement in the wrong direction.
            const float trackingStep=.08f;
            float pixelTravel=1.2f*trackingStep*size/view.FovY;
            var trackedForward=Capture(time+trackingStep);
            double forwardTravel=ShiftedDifference(forward,trackedForward,painted,pixelTravel);
            double forwardWrong=ShiftedDifference(forward,trackedForward,painted,-pixelTravel);
            Console.WriteLine($"Water flow travel diagnostic: forward correct/wrong {forwardTravel:0.000}/{forwardWrong:0.000}, expected {pixelTravel:0.00} px.");
            Check(forwardTravel<forwardWrong*.9,"wave features travel in the painted positive X direction");
            var later=Capture(time+.45f);
            double animated=Difference(forward,later,painted);
            Check(animated>.15,$"the painted wave field continues moving over time ({animated:0.000})");
            var beforeReset=Capture(3.999f);var afterReset=Capture(4.001f);
            var beforeStep=Capture(3.499f);var afterStep=Capture(3.501f);
            double reset=Difference(beforeReset,afterReset,painted);
            double smallStep=Difference(beforeStep,afterStep,painted);
            Check(reset<smallStep*8+.2,$"the flowmap phase reset is continuous like a normal animation step ({reset:0.000} vs {smallStep:0.000})");
            float heightReset=Math.Abs(WaterGeometry.SurfaceHeight(map,-4,0,3.999f)-WaterGeometry.SurfaceHeight(map,-4,0,4.001f));
            Check(heightReset<.005f,$"surface geometry stays continuous across the flowmap phase reset ({heightReset:0.00000} m)");
            SetLeftFlow(new(-1.2f,0));
            var reverse=Capture(time);
            double reversed=Difference(forward,reverse,painted);
            Check(reversed>.15,$"opposite flow directions produce different travelling wave fields ({reversed:0.000})");
            var trackedReverse=Capture(time+trackingStep);
            double reverseTravel=ShiftedDifference(reverse,trackedReverse,painted,-pixelTravel);
            double reverseWrong=ShiftedDifference(reverse,trackedReverse,painted,pixelTravel);
            Console.WriteLine($"Water flow travel diagnostic: reverse correct/wrong {reverseTravel:0.000}/{reverseWrong:0.000}.");
            Check(reverseTravel<reverseWrong*.9,"reversing the painted vector reverses the visible wave travel");

            SetLeftFlow(new(1.2f,0),bend:true);
            var curved=Capture(time);
            var upper=Patch(-4,-5,40);
            var lower=Patch(-4,5,40);
            Check(Difference(forward,curved,upper)<.1,"a downstream turn preserves the upstream direction");
            double turned=Difference(forward,curved,lower);
            Check(turned>.15,$"a locally painted bend changes the downstream wave field ({turned:0.000})");
            Check(Difference(unpainted,curved,remote)<.1,"a curved flow remains confined to its painted water");
            Preview(unpainted,"unpainted");Preview(forward,"forward");Preview(later,"forward-later");
            Preview(reverse,"reverse");Preview(curved,"bend");
            map.WaterFlow.Clear();
            var cleared=Capture(time);
            Check(Difference(unpainted,cleared,painted)<.05,"clearing currents restores the default wave animation");
            Console.WriteLine($"Water flow GPU checks passed: authored current {authored:0.000}, motion {animated:0.000}, reverse {reversed:0.000}, bend {turned:0.000}; local water/dry isolation, deterministic frames and reset.");
        }
        finally
        {
            foreach(var image in images)UnloadImage(image);
            UnloadRenderTexture(target);assets.Terrain.Remove(neutralId);UnloadTexture(texture);
        }
    }
    static double Difference(Image a,Image b,Rectangle area)
    {
        double sum=0;int count=0;
        for(int y=(int)area.Y;y<area.Y+area.Height;y++)
        for(int x=(int)area.X;x<area.X+area.Width;x++)
        {
            var c=GetImageColor(a,x,y);var d=GetImageColor(b,x,y);
            sum+=(Math.Abs(c.R-d.R)+Math.Abs(c.G-d.G)+Math.Abs(c.B-d.B))/3.0;count++;
        }
        return sum/count;
    }
    static double ShiftedDifference(Image first,Image later,Rectangle area,float shiftX)
    {
        double sum=0;int count=0;
        for(int y=(int)area.Y;y<area.Y+area.Height;y++)
        for(int x=(int)area.X;x<area.X+area.Width;x++)
        {
            float at=x+shiftX;int left=(int)MathF.Floor(at);float blend=at-left;
            var original=GetImageColor(first,x,y);
            var a=GetImageColor(later,left,y);var b=GetImageColor(later,left+1,y);
            sum+=(Math.Abs(original.R-(a.R+(b.R-a.R)*blend))+
                Math.Abs(original.G-(a.G+(b.G-a.G)*blend))+
                Math.Abs(original.B-(a.B+(b.B-a.B)*blend)))/3.0;
            count++;
        }
        return sum/count;
    }
    static void Check(bool valid,string message)
    { if(!valid)throw new InvalidOperationException("Water flow GPU check failed: "+message); }
}
