using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class WaterAppearanceGpuChecks
{
    public static void Run(WorldAssets assets)
    {
        const int size = 512;
        const string checkerId = "terrain/checks/water-appearance-checker.png";
        const string neutralId = "terrain/checks/water-appearance-neutral.png";
        const string redId = "objects/checks/water-reflection-red.png";
        var checkerImage = GenImageColor(64,64,new(50,65,75,255));
        for(int y=0;y<64;y+=4)
        for(int x=0;x<64;x+=4)
            if((x/4+y/4)%2==0)ImageDrawRectangle(ref checkerImage,x,y,4,4,new(205,195,165,255));
        var checkerTexture=LoadTextureFromImage(checkerImage); UnloadImage(checkerImage);
        var neutralImage=GenImageColor(8,8,new(140,140,140,255));
        var neutralTexture=LoadTextureFromImage(neutralImage); UnloadImage(neutralImage);
        var redImage=GenImageColor(64,128,new(245,35,20,255));
        ImageDrawRectangle(ref redImage,16,8,12,112,new(255,225,175,255));
        var redTexture=LoadTextureFromImage(redImage); UnloadImage(redImage);
        assets.Terrain.Add(checkerId,checkerTexture); assets.Terrain.Add(neutralId,neutralTexture);
        assets.Sprites.Add(redId,new(redId,redTexture));
        var target=LoadRenderTexture(size,size);
        var images=new List<Image>();
        var renderer=new WorldRenderer(assets);
        var map=MapData.CreateEmpty(32,32);
        var view=new Camera3D { Position=new(0,14,16),Target=Vector3.Zero,Up=Vector3.UnitY,
            FovY=20,Projection=CameraProjection.Orthographic };
        var settings=new GraphicsSettings { Enabled=false };
        var hiddenPlayer=new Vector3(-100,0,-100);
        void FlatBed(float height)
        {
            map.Heights=Enumerable.Repeat(height,map.VertexCount).ToArray();
            map.WaterLevel=0; map.WaveHeight=0;
            map.WaterCells=Enumerable.Repeat((byte)3,map.Tiles.Length).ToArray();
            map.Environment.Shadows=false; map.Environment.FadeOccluders=false;
        }
        Image Capture(float time=1,bool depthProbe=false)
        {
            var frame=new WorldVisualFrame(settings,time,view,12,true);
            renderer.PrepareWater(map,view,size,size,12,frame,hiddenPlayer);
            BeginTextureMode(target); ClearBackground(new(38,47,54,255)); BeginMode3D(view);
            renderer.Draw(map,view,false,hiddenPlayer,size,size,12,visuals:frame);
            if(depthProbe)
                DrawCube(new(0,-.1f,0),12,.04f,12,Color.Magenta);
            EndMode3D(); EndTextureMode();
            var image=LoadImageFromTexture(target.Texture); ImageFlipVertical(ref image); images.Add(image); return image;
        }
        Image DryCapture()
        {
            var water=map.WaterCells;
            try { map.WaterCells=[]; return Capture(); }
            finally { map.WaterCells=water; }
        }
        Rectangle Patch(Vector3 position,int radius=32)
        {
            var p=GetWorldToScreenEx(position,view,size,size);
            Check(p.X>radius&&p.Y>radius&&p.X<size-radius&&p.Y<size-radius,"water fixture sample lies inside the viewport");
            return new((int)p.X-radius,(int)p.Y-radius,radius*2,radius*2);
        }
        void Preview(Image image,string name)
        {
            string directory=Path.Combine(ProjectPaths.Root,"Editor","obj"); Directory.CreateDirectory(directory);
            Check(ExportImage(image,Path.Combine(directory,"water-"+name+".png")),"water preview is saved");
        }
        void PreviewTexture(Texture2D texture,string name)
        {
            if(texture.Id==0)return;
            var image=LoadImageFromTexture(texture); ImageFlipVertical(ref image);
            try { Preview(image,name); }
            finally { UnloadImage(image); }
        }
        try
        {
            Array.Fill(map.Tiles,checkerId); FlatBed(-.25f);
            // Isolate loss of image detail from absorption, waves and reflected highlights.
            var originalView=view;
            view=new() { Position=new(0,20,0),Target=Vector3.Zero,Up=-Vector3.UnitZ,
                FovY=16,Projection=CameraProjection.Orthographic };
            map.WaterAppearance=new() { ReflectionStrength=0,RefractionStrength=0,FoamStrength=0,Clarity=64 };
            var sharpDry=DryCapture(); var sharpWater=Capture();
            Check(assets.WaterSurfaces.Reflection.Texture.Width==size&&assets.WaterSurfaces.Reflection.Texture.Height==size&&
                assets.WaterSurfaces.Refraction.Texture.Width==size&&assets.WaterSurfaces.Refraction.Texture.Height==size,
                "reflection and refraction preserve the viewport's full pixel resolution");
            var sharpPatch=new Rectangle(160,160,192,192);
            double sharpDetail=Detail(sharpWater,sharpPatch)/Math.Max(1,Detail(sharpDry,sharpPatch));
            Check(sharpDetail>.8,$"clear still water preserves sharp bed edges instead of downsampling them ({sharpDetail:0.000})");
            var waterDepthProbe=Capture(depthProbe:true);
            Check(MeanDifference(sharpWater,waterDepthProbe,sharpPatch)<.2,
                "the water surface writes depth and occludes geometry drawn later between the surface and the bed");
            Preview(sharpWater,"sharp-clear-bed");
            assets.WaterSurfaces.Ensure(640,360);
            Check(assets.WaterSurfaces.Reflection.Texture.Width==640&&assets.WaterSurfaces.Reflection.Texture.Height==360&&
                assets.WaterSurfaces.Refraction.Texture.Width==640&&assets.WaterSurfaces.Refraction.Texture.Height==360,
                "both water captures resize to the exact viewport dimensions");
            view=originalView;
            map.WaterAppearance=new() { ReflectionStrength=0,RefractionStrength=0,FoamStrength=0,Clarity=2 };
            var shallowDry=DryCapture(); var shallow=Capture();
            Preview(shallowDry,"shallow-dry"); Preview(shallow,"shallow-transparency");
            PreviewTexture(assets.WaterSurfaces.Refraction.Texture,"shallow-refraction-capture");
            var bedPatch=Patch(new(0,0,2),48);
            double shallowDetail=Detail(shallow,bedPatch)/Math.Max(1,Detail(shallowDry,bedPatch));
            FlatBed(-4);
            var deepDry=DryCapture(); var deep=Capture();
            Preview(deepDry,"deep-dry"); Preview(deep,"deep-absorption");
            PreviewTexture(assets.WaterSurfaces.Refraction.Texture,"deep-refraction-capture");
            double deepDetail=Detail(deep,bedPatch)/Math.Max(1,Detail(deepDry,bedPatch));
            Console.WriteLine($"Water bed detail diagnostic: shallow wet/dry {Detail(shallow,bedPatch):0.000}/{Detail(shallowDry,bedPatch):0.000}; deep wet/dry {Detail(deep,bedPatch):0.000}/{Detail(deepDry,bedPatch):0.000}.");
            Check(shallowDetail>deepDetail+.12,
                $"shallow water preserves more bed detail than deep water ({shallowDetail:0.000} vs {deepDetail:0.000})");
            Check(MeanDifference(shallow,deep,bedPatch)>8,"depth visibly changes the water absorption and tint");

            Array.Fill(map.Tiles,neutralId); FlatBed(-3);
            var appearance=map.WaterAppearance with { Clarity=1 };
            map.WaterAppearance=appearance with { R=180,G=50,B=35,DeepR=135,DeepG=25,DeepB=20 };
            var warm=Capture();
            map.WaterAppearance=appearance with { R=25,G=90,B=190,DeepR=10,DeepG=30,DeepB=145 };
            var cool=Capture();
            var warmMean=Mean(warm,bedPatch); var coolMean=Mean(cool,bedPatch);
            Check(warmMean.X>coolMean.X+20&&coolMean.Z>warmMean.Z+20,"authored shallow and deep colors control the rendered water palette");
            Preview(warm,"custom-red"); Preview(cool,"custom-blue");

            map.WaterAppearance=appearance with { Clarity=4,ReflectionStrength=.75f,RefractionStrength=.35f };
            Array.Fill(map.Tiles,checkerId); map.WaveHeight=.08f;
            var animatedA=Capture(.3f); var animatedB=Capture(1.2f);
            Check(Changed(animatedA,animatedB,3)>100,"water highlights and refraction animate with the wave field");
            Preview(animatedB,"animated-refraction");

            // A red upright object stands on the far bank; its reflected image lands in the water in front of it.
            map=MapData.CreateEmpty(32,32); Array.Fill(map.Tiles,neutralId); FlatBed(-3);
            for(int z=0;z<=map.Depth;z++)
            for(int x=0;x<=map.Width;x++)
                if(z-map.Depth/2f<=-4)map.SetVertexHeight(x,z,.5f);
            map.WaterAppearance=new() { ReflectionStrength=1,RefractionStrength=0,FoamStrength=0,Clarity=.6f };
            view=new() { Position=new(0,10,16),Target=new(0,1,0),Up=Vector3.UnitY,FovY=40,Projection=CameraProjection.Perspective };
            var tree=new SceneObject { Id="water-reflection-check",Asset=redId,X=0,Z=-5,Height=6,Solid=false,FadeWhenBlocking=false };
            var reflectedCenter=map.GroundPosition(tree)+Vector3.UnitY*tree.Height*.5f;
            reflectedCenter.Y=2*map.WaterLevel-reflectedCenter.Y;
            var reflectionPatch=Patch(reflectedCenter,24);
            var emptyReflection=Capture(); map.Objects.Add(tree); var withReflection=Capture();
            double reflectedObject=MeanDifference(emptyReflection,withReflection,reflectionPatch);
            map.WaterAppearance=map.WaterAppearance with { ReflectionStrength=0 };
            var noReflectionObject=Capture(); map.Objects.Clear(); var noReflectionEmpty=Capture();
            double unreflectedObject=MeanDifference(noReflectionEmpty,noReflectionObject,reflectionPatch);
            Check(reflectedObject>unreflectedObject+2,
                $"water reflects actual scenery rather than only a sky tint ({reflectedObject:0.000} vs {unreflectedObject:0.000})");
            Preview(withReflection,"scenery-reflection");

            // The upper half of a fully submerged object must not reappear as a reflected object above the surface.
            map.WaterAppearance=map.WaterAppearance with { ReflectionStrength=1 };
            var underwater=new SceneObject { Id="underwater-reflection-check",Asset=redId,X=3,Z=-1,Height=.75f,Solid=false };
            map.Objects.Add(underwater);
            var submerged=Capture(); map.Objects.Clear(); var submergedEmpty=Capture();
            var underwaterMirror=map.GroundPosition(underwater)+Vector3.UnitY*underwater.Height*.5f;
            underwaterMirror.Y=2*map.WaterLevel-underwaterMirror.Y;
            var underwaterPatch=Patch(underwaterMirror,12);
            Check(MeanDifference(submerged,submergedEmpty,underwaterPatch)<3,"the reflection pass clips scenery entirely below the water plane");

            // A visual review scene uses only supplied art; it never writes a map or changes the user's world.
            map=MapData.CreateEmpty(40,40);
            map.Environment.Shadows=true; map.Environment.FadeOccluders=false;
            map.WaterLevel=0; map.WaveHeight=.06f;
            map.WaterCells=Enumerable.Repeat((byte)3,map.Tiles.Length).ToArray();
            map.WaterAppearance=new() { R=70,G=163,B=170,DeepR=12,DeepG=59,DeepB=81,
                Clarity=4.5f,ReflectionStrength=.85f,RefractionStrength=.15f,FoamStrength=.12f };
            for(int z=0;z<=map.Depth;z++)
            for(int x=0;x<=map.Width;x++)
            {
                float wx=x-map.Width/2f,wz=z-map.Depth/2f;
                float angle=MathF.Atan2(wz,wx);
                float shore=9.8f+MathF.Sin(angle*3.0f)*.6f+MathF.Cos(angle*5.0f)*.25f;
                float radius=MathF.Sqrt(wx*wx+wz*wz)/shore;
                float slope=Math.Clamp(radius,0,1); slope=slope*slope*(3-2*slope);
                map.SetVertexHeight(x,z,-3+3.75f*slope);
            }
            for(int z=0;z<map.Depth;z++)
            for(int x=0;x<map.Width;x++)
            {
                float bed=map.HeightAt(x-map.Width/2f+.5f,z-map.Depth/2f+.5f);
                map.Tiles[z*map.Width+x]=bed<-.4f ? "stone" : bed<.75f ? "sand" : "grass";
            }
            var trees=assets.Sprites.Values.Where(a=>a.Id.Contains("/trees/",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(trees.Length==0)trees=assets.Sprites.Values.Where(a=>a.Id.Contains("/vegetation/",StringComparison.OrdinalIgnoreCase)).ToArray();
            var rocks=assets.Sprites.Values.Where(a=>a.Id.Contains("/rocks/",StringComparison.OrdinalIgnoreCase)).ToArray();
            var treePositions=new Vector2[] { new(-7,-8),new(-3,-10),new(2,-10),new(8,-7),new(-10,1),new(8,8),new(-6,9) };
            for(int i=0;i<treePositions.Length&&trees.Length>0;i++)
                map.Objects.Add(new() { Id="lake-preview-tree-"+i,Asset=trees[i%trees.Length].Id,
                    X=treePositions[i].X,Z=treePositions[i].Y,Height=3.8f+i%3*.65f,FadeWhenBlocking=false });
            var rockPositions=new Vector2[] { new(-8,-3),new(-6,-7),new(5,-7),new(8,-2),new(6,6),new(-2,9) };
            for(int i=0;i<rockPositions.Length&&rocks.Length>0;i++)
                map.Objects.Add(new() { Id="lake-preview-rock-"+i,Asset=rocks[i%rocks.Length].Id,
                    X=rockPositions[i].X,Z=rockPositions[i].Y,Height=1.1f+i%3*.35f,FadeWhenBlocking=false });
            view=new() { Position=new(7,13,14),Target=new(0,-.1f,0),Up=Vector3.UnitY,FovY=40,Projection=CameraProjection.Perspective };
            var previewTarget=LoadRenderTexture(1000,700);
            try
            {
                var previewFrame=new WorldVisualFrame(settings,2.4f,view,12,true);
                renderer.PrepareWater(map,view,1000,700,12,previewFrame,hiddenPlayer);
                BeginTextureMode(previewTarget);ClearBackground(new(38,47,54,255));BeginMode3D(view);
                renderer.Draw(map,view,false,hiddenPlayer,1000,700,12,visuals:previewFrame);
                EndMode3D();EndTextureMode();
                var preview=LoadImageFromTexture(previewTarget.Texture);ImageFlipVertical(ref preview);
                try { Preview(preview,"lake-preview"); } finally { UnloadImage(preview); }
                // Review the shoreline at editing zoom as well as in the whole-lake view.
                view=new() { Position=new(7.2f,5.6f,7.5f),Target=new(7.2f,0,0),Up=Vector3.UnitY,FovY=40,
                    Projection=CameraProjection.Perspective };
                map.Objects.RemoveAll(o=>Vector3.DistanceSquared(map.GroundPosition(o),view.Position)<49);
                previewFrame=new(settings,2.4f,view,12,true);
                renderer.PrepareWater(map,view,1000,700,12,previewFrame,hiddenPlayer);
                BeginTextureMode(previewTarget);ClearBackground(new(38,47,54,255));BeginMode3D(view);
                renderer.Draw(map,view,false,hiddenPlayer,1000,700,12,visuals:previewFrame);
                EndMode3D();EndTextureMode();
                preview=LoadImageFromTexture(previewTarget.Texture);ImageFlipVertical(ref preview);
                try { Preview(preview,"shore-closeup"); } finally { UnloadImage(preview); }
            }
            finally { UnloadRenderTexture(previewTarget); }
            Console.WriteLine("Water appearance GPU checks passed: shallow/deep absorption, visible bed detail, editable colors, animated refraction, scenery reflection and underwater clipping.");
        }
        finally
        {
            foreach(var image in images)UnloadImage(image);
            UnloadRenderTexture(target);
            assets.Terrain.Remove(checkerId); assets.Terrain.Remove(neutralId); assets.Sprites.Remove(redId);
            UnloadTexture(checkerTexture); UnloadTexture(neutralTexture); UnloadTexture(redTexture);
        }
    }
    static Vector3 Mean(Image image,Rectangle area)
    {
        var sum=Vector3.Zero; int count=0;
        for(int y=(int)area.Y;y<area.Y+area.Height;y++)
        for(int x=(int)area.X;x<area.X+area.Width;x++)
        { var c=GetImageColor(image,x,y); sum+=new Vector3(c.R,c.G,c.B); count++; }
        return sum/count;
    }
    static double MeanDifference(Image a,Image b,Rectangle area)
    {
        double sum=0; int count=0;
        for(int y=(int)area.Y;y<area.Y+area.Height;y++)
        for(int x=(int)area.X;x<area.X+area.Width;x++)
        { var c=GetImageColor(a,x,y); var d=GetImageColor(b,x,y); sum+=(Math.Abs(c.R-d.R)+Math.Abs(c.G-d.G)+Math.Abs(c.B-d.B))/3.0; count++; }
        return sum/count;
    }
    static double Brightness(Color c)=>c.R*.2126+c.G*.7152+c.B*.0722;
    static double Detail(Image image,Rectangle area)
    {
        double sum=0; int count=0;
        for(int y=(int)area.Y;y<area.Y+area.Height-1;y++)
        for(int x=(int)area.X;x<area.X+area.Width-1;x++)
        {
            double c=Brightness(GetImageColor(image,x,y));
            sum+=Math.Abs(c-Brightness(GetImageColor(image,x+1,y)))+Math.Abs(c-Brightness(GetImageColor(image,x,y+1))); count+=2;
        }
        return sum/count;
    }
    static int Changed(Image a,Image b,int threshold)
    {
        int count=0;
        for(int y=0;y<a.Height;y+=2)
        for(int x=0;x<a.Width;x+=2)
        { var c=GetImageColor(a,x,y); var d=GetImageColor(b,x,y); if(Math.Max(Math.Abs(c.R-d.R),Math.Max(Math.Abs(c.G-d.G),Math.Abs(c.B-d.B)))>threshold)count++; }
        return count;
    }
    static void Check(bool valid,string message)
    { if(!valid)throw new InvalidOperationException("Water appearance GPU check failed: "+message); }
}
