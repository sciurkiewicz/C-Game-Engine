using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class WorldRenderer
{
    public void DrawSunOccluders(MapData map, Camera3D camera, WorldVisualFrame frame)
    {
        var mask = frame with { Settings = frame.Settings with { Fog = false, Outlines = false } };
        WorldLighting.Apply(assets.SpriteShader,map,frame.Hour,camera.Target);
        WorldVisualFrame.Apply(assets.SpriteShader,map,camera,mask,VisualSurface.Sprite);
        var forward=Vector3.Normalize(camera.Target-camera.Position);
        BeginShaderMode(assets.SpriteShader);
        foreach(var item in map.SceneEntities)
        {
            var position=map.GroundPosition(item);
            if(Vector3.DistanceSquared(position,camera.Target)>MathF.Pow(180+item.Height,2) || Vector3.Dot(position-camera.Position,forward)<-item.Height)continue;
            DrawSpriteGeometry(camera,item.Asset,position,item.Height,Color.Black,255,frame.Wind>0&&WorldVisualFrame.Vegetation(item),true);
        }
        EndShaderMode();
    }
}
