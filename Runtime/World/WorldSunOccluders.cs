using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class WorldRenderer
{
    public void DrawSunOccluders(MapData map, Camera3D camera, WorldVisualFrame frame)
    {
        var mask = frame with { Settings = frame.Settings with { Fog = false, Outlines = false } };
        WorldLighting.Apply(assets.SpriteShader,map,frame.Hour,camera.Target,frame);
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
        WorldLighting.Apply(assets.WallShader,map,frame.Hour,camera.Target,frame);
        WorldVisualFrame.Apply(assets.WallShader,map,camera,mask,VisualSurface.Wall);
        SetShaderValue(assets.WallShader,GetShaderLocation(assets.WallShader,"wallSilhouette"),1f,ShaderUniformDataType.Float);
        if (map.WallCells.Count > 0) assets.WallMeshes.Draw(map,wallFrustum,assets.WallShader);
        BeginShaderMode(assets.WallShader);
        foreach (var wall in map.Walls)
        {
            var mesh = wall.Geometry(map);
            if (wallFrustum.Intersects(mesh.Min,mesh.Max)) DrawWallGeometry(map,wall,Color.Black);
        }
        EndShaderMode();
    }
}
