using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class GraphicsGpuChecks
{
    public static void Run(WorldAssets assets)
    {
        const int size=512;
        const string plantId="objects/vegetation/checks/effects-plant.png", groundId="terrain/checks/effects.png";
        var plantImage=GenImageColor(32,64,Color.Blank);
        ImageDrawRectangle(ref plantImage,6,0,20,48,new(90,170,65,255));
        ImageDrawRectangle(ref plantImage,14,42,4,22,new(100,85,40,255));
        var plantTexture=LoadTextureFromImage(plantImage); UnloadImage(plantImage);
        var groundImage=GenImageColor(32,32,new(95,125,70,255));
        for(int x=0;x<32;x+=8)ImageDrawRectangle(ref groundImage,x,0,4,32,new(140,145,95,255));
        var groundTexture=LoadTextureFromImage(groundImage); UnloadImage(groundImage);
        assets.Sprites.Add(plantId,new(plantId,plantTexture)); assets.Terrain.Add(groundId,groundTexture);
        var target=LoadRenderTexture(size,size);
        using var fx=new WorldVisualEffects();
        var renderer=new WorldRenderer(assets);
        var map=MapData.CreateEmpty(32,32); Array.Fill(map.Tiles,groundId); map.Environment.Shadows=false;
        var view=new FollowCamera(Vector3.Zero,new(){InitialDistance=22}).View;
        var hiddenPlayer=new Vector3(-100,0,-100);
        var play=new PlaySession(map);
        var off=new GraphicsSettings { Fog=false,Wind=false,Bloom=false,SunRays=false,Outlines=false,HeatDistortion=false,Rain=false,Damage=false,
            DynamicLights=false,DepthOfField=false,ColorGrading=false,Vignette=false };
        Image Capture(GraphicsSettings mode,float time=1,float hour=12)
        {
            fx.Configure(mode,play); fx.Update(play,time); fx.Render(renderer,map,view,hiddenPlayer,size,size,hour);
            BeginTextureMode(target); ClearBackground(Color.Black); fx.Draw(new(0,0,size,size)); EndTextureMode();
            var image=LoadImageFromTexture(target.Texture); ImageFlipVertical(ref image); return image;
        }
        Image Direct(WorldVisualFrame? frame=null)
        {
            BeginTextureMode(target); ClearBackground(new(38,47,54,255)); BeginMode3D(view);
            renderer.Draw(map,view,false,hiddenPlayer,size,size,12,visuals:frame);
            EndMode3D(); EndTextureMode();
            var image=LoadImageFromTexture(target.Texture); ImageFlipVertical(ref image); return image;
        }
        Color At(Image image,Vector3 position)
        {
            var p=GetWorldToScreenEx(position,view,size,size);
            Check(p.X>=0&&p.Y>=0&&p.X<size&&p.Y<size,"sample is inside the viewport");
            return GetImageColor(image,(int)p.X,(int)p.Y);
        }
        void Preview(Image image,string name)
        { Check(ExportImage(image,Path.Combine(ProjectPaths.Root,"Editor","obj","graphics-"+name+".png")),"preview is saved"); }
        try
        {
            var original=Direct(); var bypass=Capture(off with {Enabled=false});
            try { Check(Changed(original,bypass,3)<30,"master-off preserves the original renderer, orientation and colors"); }
            finally { UnloadImage(original); UnloadImage(bypass); }
            var baseline=Capture(off); var fog=Capture(off with{Fog=true,FogStrength=1,FogStart=0,FogEnd=8,GroundFog=0});
            try
            {
                var far=new Vector3(-4,0,2); var near=new Vector3(4,0,2);
                Check(Difference(At(baseline,far),At(fog,far))>Difference(At(baseline,near),At(fog,near))+5,"fog increases with scene depth");
                Preview(fog,"fog");
            }
            finally { UnloadImage(baseline); UnloadImage(fog); }
            var plant=new SceneObject {Asset=plantId,Height=4}; map.Objects.Add(plant);
            string frozen=map.Serialize();
            var still=Capture(off); var windA=Capture(off with{Wind=true,WindStrength=.7f},1); var windB=Capture(off with{Wind=true,WindStrength=.7f},3.5f);
            try
            {
                Check(Changed(windA,windB,5)>100,"the vegetation visibly moves as shader time changes");
                Check(Difference(At(windA,new(0,.015f,0)),At(windB,new(0,.015f,0)))<=5,"vegetation roots stay in the same place");
                Check(map.Serialize()==frozen,"wind changes neither map geometry nor object positions"); Preview(windB,"wind");
            }
            finally { UnloadImage(windA); UnloadImage(windB); }
            var outlined=Capture(off with{Outlines=true,OutlineWidth=2,OutlineStrength=1});
            try { Check(Changed(still,outlined,8)>70,"outline shader changes visible silhouette edges"); Preview(outlined,"outlines"); }
            finally { UnloadImage(outlined); }
            var rays=Capture(off with{SunRays=true,SunRaysStrength=.7f});
            try { Check(Changed(still,rays,4)>1000,"screen-space sun rays illuminate the image around sprite occluders"); Preview(rays,"rays"); }
            finally { UnloadImage(rays); UnloadImage(still); }
            var night=Capture(off,1,0); var noSun=Capture(off with{SunRays=true,SunRaysStrength=1},1,0);
            try { Check(Changed(night,noSun,2)==0,"sun shafts disappear at night"); }
            finally { UnloadImage(night); UnloadImage(noSun); }
            map.Objects.Add(new(){Id="graphics-fire",Asset="objects/props/camp/ognisko.png",X=2,Z=1,Height=1.4f});
            var animatedLight = off with { DynamicLights=true,LightFlickerStrength=.8f };
            var fireTimes = Enumerable.Range(0,80).Select(i => i * .05f)
                .OrderBy(t => WorldLighting.AllLights(map,new(animatedLight,t,view,0,true)).Single(l=>l.Id=="graphics-fire").Intensity).ToArray();
            var lightDim=Capture(animatedLight,fireTimes[0],0); var lightBright=Capture(animatedLight,fireTimes[^1],0);
            try
            {
                Check(Changed(lightDim,lightBright,5)>100,"animated fire changes actual nighttime surface illumination");
                Preview(lightBright,"dynamic-lights");
            }
            finally { UnloadImage(lightDim); UnloadImage(lightBright); }
            var fire=Capture(off); var bloom=Capture(off with{Bloom=true,BloomStrength=1,BloomThreshold=.6f});
            try { Check(Changed(fire,bloom,4)>100,"the supplied campfire creates blurred light bloom"); Preview(bloom,"bloom"); }
            finally { UnloadImage(bloom); }
            var heat=Capture(off with{HeatDistortion=true,HeatStrength=.025f},1.5f);
            try { Check(Changed(fire,heat,4)>20,"heat above a campfire refracts the actual scene image"); Preview(heat,"heat"); }
            finally { UnloadImage(fire); UnloadImage(heat); }
            var dry=Capture(off); var wet=Capture(off with{Rain=true,RainIntensity=0,Wetness=.8f});
            try { Check(Changed(dry,wet,4)>1000,"wet ground changes terrain shading even without foreground rain streaks"); Preview(wet,"wet"); }
            finally { UnloadImage(wet); }
            var rainA=Capture(off with{Rain=true,RainIntensity=1,Wetness=0},.3f); var rainB=Capture(off with{Rain=true,RainIntensity=1,Wetness=0},1.2f);
            try { Check(Changed(dry,rainA,4)>100&&Changed(rainA,rainB,4)>100,"rain falls and animates over the world image"); Preview(rainB,"rain"); }
            finally { UnloadImage(dry); UnloadImage(rainA); UnloadImage(rainB); }
            map.Type=MapType.Cave;
            var indoor=Capture(off); var sheltered=Capture(new GraphicsSettings {Bloom=false,Outlines=false,HeatDistortion=false,Damage=false,
                DynamicLights=false,DepthOfField=false,ColorGrading=false,Vignette=false});
            try { Check(Changed(indoor,sheltered,2)==0,"caves are sheltered from rain, wetness, fog, wind and sun"); }
            finally { UnloadImage(indoor); UnloadImage(sheltered); }
            map.Type=MapType.OpenWorld;
            map.WaterLevel=.2f; map.WaveHeight=0; map.WaterCells=Enumerable.Repeat((byte)3,map.Tiles.Length).ToArray();
            var rippleSettings=off with{Rain=true,RainIntensity=1,Wetness=0};
            var waterA=Direct(new(rippleSettings,.3f,view,12,true)); var waterB=Direct(new(rippleSettings,1.2f,view,12,true));
            try { Check(Changed(waterA,waterB,3)>100,"rain produces animated circles on the water surface"); Preview(waterB,"water-ripples"); }
            finally { UnloadImage(waterA); UnloadImage(waterB); }
            map.WaterCells=[]; map.WaterLevel=0;
            var fightMap=MapData.CreateEmpty(16,16); fightMap.Monsters.Add(new(){X=.8f});
            play=new PlaySession(fightMap); var damageSettings=off with{Damage=true,DamageStrength=1,CameraShake=0};
            fx.Configure(damageSettings,play); play.UpdateCombat(.1f); fx.Update(play,.01f);
            fx.Render(renderer,map,view,hiddenPlayer,size,size,12);
            BeginTextureMode(target); ClearBackground(Color.Black); fx.Draw(new(0,0,size,size)); EndTextureMode();
            var hurt=LoadImageFromTexture(target.Texture); ImageFlipVertical(ref hurt);
            var healthy=Capture(off);
            try { var a=GetImageColor(hurt,3,3); var b=GetImageColor(healthy,3,3); Check(a.R>b.R+15&&a.R>a.G*2,"real combat damage produces a red screen-edge response"); Preview(hurt,"damage"); }
            finally { UnloadImage(hurt); UnloadImage(healthy); }
            foreach(var dimensions in new[]{(320,240),(800,540),(512,512)})
            { fx.Render(renderer,map,view,hiddenPlayer,dimensions.Item1,dimensions.Item2,12); }
            var all=Capture(new GraphicsSettings(),2,9); try { Preview(all,"all"); } finally { UnloadImage(all); }
            Hd2dGpuChecks.Run(assets);
            Console.WriteLine("Graphics GPU checks passed: renderer bypass/colors, depth fog, anchored wind, outlines, animated nighttime lights, fire bloom, heat refraction, sun/night/interior gating, wet ground, animated rain/water ripples, real damage vignette and framebuffer resizing.");
        }
        finally { UnloadRenderTexture(target); assets.Sprites.Remove(plantId); assets.Terrain.Remove(groundId); UnloadTexture(plantTexture); UnloadTexture(groundTexture); }
    }
    static int Difference(Color a,Color b)=>Math.Max(Math.Abs(a.R-b.R),Math.Max(Math.Abs(a.G-b.G),Math.Abs(a.B-b.B)));
    static int Changed(Image a,Image b,int threshold)
    {
        int count=0;
        for(int y=0;y<a.Height;y+=2)for(int x=0;x<a.Width;x+=2)if(Difference(GetImageColor(a,x,y),GetImageColor(b,x,y))>threshold)count++;
        return count;
    }
    static void Check(bool valid,string message)
    { if(!valid)throw new InvalidOperationException("Graphics GPU check failed: "+message); }
}
