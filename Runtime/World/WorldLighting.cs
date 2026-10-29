using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

static class WorldLighting
{
    public const string VertexShader = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec4 vertexColor;
        uniform mat4 mvp;
        out vec2 fragTexCoord;
        out vec4 fragColor;
        out vec3 fragPosition;
        void main()
        {
            fragTexCoord = vertexTexCoord; fragColor = vertexColor; fragPosition = vertexPosition;
            gl_Position = mvp * vec4(vertexPosition, 1.0);
        }
        """;
    public const string FragmentPrelude = """
        #version 330
        in vec3 fragPosition;
        uniform vec3 ambientLight;
        uniform int lightCount;
        uniform vec4 lightPositionRadius[16];
        uniform vec4 lightColorIntensity[16];
        vec3 sceneLight()
        {
            vec3 light = ambientLight;
            for (int i = 0; i < lightCount; i++)
            {
                float falloff = max(0.0, 1.0 - distance(fragPosition, lightPositionRadius[i].xyz) / lightPositionRadius[i].w);
                light += lightColorIntensity[i].rgb * lightColorIntensity[i].w * falloff * falloff;
            }
            return light;
        }

        """;
    public static Vector3 Ambient(float hour)
    {
        float daylight = Math.Clamp(MathF.Sin((hour - 6) * MathF.PI / 12) * 3, 0, 1);
        return Vector3.Lerp(new(.16f, .20f, .32f), Vector3.One, daylight);
    }
    public static IEnumerable<PointLightData> AllLights(MapData map)
    {
        foreach (var light in map.Lights.Where(l => l.Enabled)) yield return light;
        foreach (var fire in map.Objects.Where(o => o.Asset.Contains("campfire", StringComparison.OrdinalIgnoreCase) || o.Asset.Contains("torch", StringComparison.OrdinalIgnoreCase)))
            yield return new() { Id = fire.Id, X = fire.X, Z = fire.Z, Height = Math.Min(fire.Height, 2), Radius = 6, Intensity = .85f, R = 255, G = 155, B = 65 };
    }
    public static PointLightData[] Nearby(MapData map, Vector3 focus) => map.Environment.Lighting
        ? AllLights(map).OrderBy(l => Vector2.DistanceSquared(new(l.X, l.Z), new(focus.X, focus.Z))).Take(16).ToArray() : [];
    public static Vector3 Evaluate(MapData map, Vector3 point, Vector3 ambient, PointLightData[] lights)
    {
        var result = ambient;
        foreach (var light in lights)
        {
            float distance = Vector3.Distance(point, map.GroundPosition(light.X, light.Z) + Vector3.UnitY * light.Height);
            float falloff = Math.Max(0, 1 - distance / light.Radius);
            result += new Vector3(light.R, light.G, light.B) / 255 * light.Intensity * falloff * falloff;
        }
        return result;
    }
    public static void Apply(Shader shader, MapData map, float hour, Vector3 focus)
    {
        var lights = Nearby(map, focus);
        SetShaderValue(shader, GetShaderLocation(shader, "ambientLight"), map.Environment.Lighting ? Ambient(hour) : Vector3.One, ShaderUniformDataType.Vec3);
        SetShaderValue(shader, GetShaderLocation(shader, "lightCount"), lights.Length, ShaderUniformDataType.Int);
        if (lights.Length == 0) return;
        var positions = lights.Select(l => new Vector4(map.GroundPosition(l.X, l.Z) + Vector3.UnitY * l.Height, l.Radius)).ToArray();
        var colors = lights.Select(l => new Vector4(l.R / 255f, l.G / 255f, l.B / 255f, l.Intensity)).ToArray();
        SetShaderValueV(shader, GetShaderLocation(shader, "lightPositionRadius"), positions, ShaderUniformDataType.Vec4, positions.Length);
        SetShaderValueV(shader, GetShaderLocation(shader, "lightColorIntensity"), colors, ShaderUniformDataType.Vec4, colors.Length);
    }
}

sealed partial class WorldRenderer
{
    readonly Dictionary<string, float> obstacleOpacity = [];
    static bool BlocksPlayer(Camera3D camera, SpriteQuad quad, Vector3 player, float height)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        foreach (float fraction in new[] { .3f, .75f })
        {
            var body = player + Vector3.UnitY * (height * fraction);
            var origin = camera.Projection == CameraProjection.Orthographic
                ? body - forward * Vector3.Dot(body - camera.Position, forward) : camera.Position;
            var delta = body - origin; float distance = delta.Length();
            if (distance < .001f) continue;
            var hit = GetRayCollisionQuad(new Ray { Position = origin, Direction = delta / distance }, quad.BottomLeft, quad.BottomRight, quad.TopRight, quad.TopLeft);
            if (hit.Hit && hit.Distance < distance - .05f) return true;
        }
        return false;
    }
    byte ObstacleAlpha(MapData map, Camera3D camera, SceneObject item, Vector3? player)
    {
        bool blocked = player.HasValue && map.Environment.FadeOccluders && item.FadeWhenBlocking && item is not NpcData && item is not MonsterData &&
            BlocksPlayer(camera, GetSpriteQuad(camera, item.Asset, map.GroundPosition(item), item.Height), player.Value, map.Player.Height);
        float target = item.IsDoor && item.DoorOpen ? .2f : blocked ? map.Environment.OccluderOpacity : 1;
        float value = obstacleOpacity.GetValueOrDefault(item.Id, 1);
        float step = Math.Clamp(GetFrameTime(), 1f / 144, .05f) * 4;
        value += Math.Clamp(target - value, -step, step); obstacleOpacity[item.Id] = value;
        return (byte)Math.Clamp((int)(value * 255), 0, 255);
    }
}
