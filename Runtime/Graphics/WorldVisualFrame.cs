using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

enum VisualSurface { Terrain, Sprite, Water, Wall }

sealed record WorldVisualFrame(GraphicsSettings Settings, float Time, Camera3D Camera, float Hour, bool Outdoors)
{
    public bool Enabled => Settings.Enabled;
    public float Rain => Enabled && Outdoors && Settings.Rain ? Settings.RainIntensity : 0;
    public float Wetness => Enabled && Outdoors && Settings.Rain ? Settings.Wetness : 0;
    public float Wind => Enabled && Outdoors && Settings.Wind ? Settings.WindStrength : 0;
    public float Sunlight => Outdoors ? Math.Clamp(MathF.Sin((Hour - 6) * MathF.PI / 12), 0, 1) : 0;
    public static bool Vegetation(string id) => id.Contains("/vegetation/", StringComparison.OrdinalIgnoreCase) || id.Contains("/grass/", StringComparison.OrdinalIgnoreCase);
    public static bool Vegetation(SceneObject? item) => item?.GetType() == typeof(SceneObject) && Vegetation(item.Asset);
    public static void Apply(Shader shader, MapData map, Camera3D camera, WorldVisualFrame? frame, VisualSurface surface)
    {
        var settings = frame?.Settings;
        bool enabled = frame?.Enabled == true;
        var fog = enabled && frame!.Outdoors && settings!.Fog
            ? new Vector4(Vector3.Distance(camera.Position, camera.Target) + settings.FogStart,
                Vector3.Distance(camera.Position, camera.Target) + settings.FogEnd, settings.FogStrength, settings.GroundFog) : Vector4.Zero;
        SetShaderValue(shader, GetShaderLocation(shader, "fogParameters"), fog, ShaderUniformDataType.Vec4);
        SetShaderValue(shader, GetShaderLocation(shader, "viewPosition"), camera.Position, ShaderUniformDataType.Vec3);
        SetShaderValue(shader, GetShaderLocation(shader, "viewForward"), Vector3.Normalize(camera.Target - camera.Position), ShaderUniformDataType.Vec3);
        SetShaderValue(shader, GetShaderLocation(shader, "fogColor"), WorldLighting.Ambient(map, frame?.Hour ?? map.Environment.TimeOfDay) * new Vector3(.57f, .65f, .69f), ShaderUniformDataType.Vec3);
        SetShaderValue(shader, GetShaderLocation(shader, "visualTime"), frame?.Time ?? 0, ShaderUniformDataType.Float);
        if (surface is VisualSurface.Terrain or VisualSurface.Wall) SetShaderValue(shader, GetShaderLocation(shader, "wetness"), frame?.Wetness ?? 0, ShaderUniformDataType.Float);
        if (surface == VisualSurface.Water) SetShaderValue(shader, GetShaderLocation(shader, "rainIntensity"), frame?.Rain ?? 0, ShaderUniformDataType.Float);
        if (surface == VisualSurface.Sprite)
        {
            SetShaderValue(shader, GetShaderLocation(shader, "windParameters"), new Vector4(frame?.Wind ?? 0, settings?.WindSpeed ?? 0, frame?.Time ?? 0, 0), ShaderUniformDataType.Vec4);
            SetShaderValue(shader, GetShaderLocation(shader, "outlineParameters"), enabled && settings!.Outlines ? new Vector2(settings.OutlineWidth, settings.OutlineStrength) : Vector2.Zero, ShaderUniformDataType.Vec2);
        }
    }
}
