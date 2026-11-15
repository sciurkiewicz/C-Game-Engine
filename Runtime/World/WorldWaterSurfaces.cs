using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed class WaterSurfaceTargets : IDisposable
{
    public RenderTexture2D Reflection { get; private set; }
    public RenderTexture2D Refraction { get; private set; }
    MapData? preparedMap;
    Camera3D preparedCamera;
    public bool Matches(MapData map,Camera3D camera) => ReferenceEquals(preparedMap,map) &&
        camera.Position==preparedCamera.Position && camera.Target==preparedCamera.Target && camera.Up==preparedCamera.Up &&
        camera.Projection==preparedCamera.Projection && camera.FovY==preparedCamera.FovY;
    public void Invalidate() => preparedMap=null;
    public void Complete(MapData map,Camera3D camera) { preparedMap=map; preparedCamera=camera; }
    public void Ensure(int width,int height)
    {
        // Preserve the scene's pixel detail through the water instead of upscaling a reduced capture.
        width=Math.Max(1,width); height=Math.Max(1,height);
        if(Reflection.Id!=0&&Refraction.Id!=0&&Reflection.Texture.Width==width&&Reflection.Texture.Height==height&&
            Refraction.Texture.Width==width&&Refraction.Texture.Height==height)return;
        Dispose();
        try { Reflection=Target(width,height); Refraction=Target(width,height); }
        catch { Dispose(); throw; }
    }
    static RenderTexture2D Target(int width,int height)
    {
        var target=LoadRenderTexture(width,height);
        if(!IsRenderTextureValid(target))throw new InvalidDataException("Cannot create water surface framebuffer.");
        SetTextureFilter(target.Texture,TextureFilter.Bilinear);SetTextureWrap(target.Texture,TextureWrap.Clamp);
        return target;
    }
    public void Dispose()
    {
        if(Reflection.Id!=0)UnloadRenderTexture(Reflection);
        if(Refraction.Id!=0)UnloadRenderTexture(Refraction);
        Reflection=Refraction=default; preparedMap=null;
    }
}

sealed partial class WorldRenderer
{
    readonly object waterCaptureOwner=new();
    public void PrepareWater(MapData map,Camera3D camera,int width,int height,float hour,WorldVisualFrame? visuals=null,
        Vector3? player=null,Camera3D? terrainTextureView=null)
    {
        var targets=assets.WaterSurfaces;targets.Invalidate();
        if(camera.Position.Y<=map.WaterLevel+.03f)return;
        var bounds=TerrainVisibility.GetBounds(map,camera,width,height,128);
        if(!bounds.Indices(map.Width).Any(i=>map.WaterMask(i)!=0))return;
        targets.Ensure(width,height);
        var frame=visuals??new WorldVisualFrame(new GraphicsSettings{Enabled=false},(float)(GetTime()%3600),camera,hour,map.Type==MapType.OpenWorld);
        var below=frame with { WaterCapture=true,ClipDirection=-1,ClipHeight=map.WaterLevel };
        BeginTextureMode(targets.Refraction);ClearBackground(Color.Blank);BeginMode3D(camera);
        Draw(map,camera,false,player,targets.Refraction.Texture.Width,targets.Refraction.Texture.Height,hour,
            maximumDistance:128,terrainTextureView:terrainTextureView??camera,visuals:below);
        EndMode3D();EndTextureMode();
        var mirrored=camera;
        mirrored.Position.Y=2*map.WaterLevel-camera.Position.Y;
        mirrored.Target.Y=2*map.WaterLevel-camera.Target.Y;
        var above=frame with { Camera=mirrored,BillboardCamera=camera,WaterCapture=true,ClipDirection=1,ClipHeight=map.WaterLevel };
        BeginTextureMode(targets.Reflection);ClearBackground(Color.Blank);BeginMode3D(mirrored);
        Rlgl.DisableBackfaceCulling();
        Draw(map,mirrored,false,player,targets.Reflection.Texture.Width,targets.Reflection.Texture.Height,hour,
            maximumDistance:96,terrainTextureView:terrainTextureView??camera,visuals:above);
        Rlgl.DrawRenderBatchActive();Rlgl.EnableBackfaceCulling();EndMode3D();EndTextureMode();
        targets.Complete(map,camera);
    }
}
