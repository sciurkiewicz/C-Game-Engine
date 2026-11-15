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
        in vec3 vertexNormal;
        uniform mat4 mvp;
        out vec2 fragTexCoord;
        out vec4 fragColor;
        out vec3 fragPosition;
        out vec3 fragNormal;
        void main()
        {
            fragTexCoord = vertexTexCoord; fragColor = vertexColor; fragPosition = vertexPosition;
            fragNormal = vertexNormal;
            gl_Position = mvp * vec4(vertexPosition, 1.0);
        }
        """;
    public const string FragmentPrelude = """
        #version 330
        in vec3 fragPosition;
        in vec3 fragNormal;
        uniform vec3 viewPosition;
        uniform vec3 viewForward;
        uniform vec3 fogColor;
        uniform vec4 fogParameters;
        uniform float visualTime;
        uniform float wetness;
        uniform float rainIntensity;
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
        float visualNoise(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        vec3 atmosphere(vec3 color)
        {
            if (fogParameters.z <= 0.0) return color;
            float depth = dot(fragPosition - viewPosition, viewForward);
            float distant = smoothstep(fogParameters.x, fogParameters.y, depth);
            vec2 p = fragPosition.xz * 0.25 + vec2(visualTime * 0.025, visualTime * 0.012);
            vec2 cell = floor(p), f = smoothstep(vec2(0.0), vec2(1.0), fract(p));
            float n = mix(mix(visualNoise(cell), visualNoise(cell + vec2(1,0)), f.x),
                mix(visualNoise(cell + vec2(0,1)), visualNoise(cell + vec2(1,1)), f.x), f.y);
            float ground = exp(-max(0.0, fragPosition.y) / 1.8) * fogParameters.w * (0.4 + n);
            float amount = 1.0 - exp(-(distant * 2.0 + ground) * fogParameters.z);
            return mix(color, fogColor, clamp(amount, 0.0, 0.85));
        }
        vec3 wetGround(vec3 color)
        {
            if (wetness <= 0.0) return color;
            vec3 n = normalize(fragNormal), v = normalize(viewPosition - fragPosition);
            vec3 sun = normalize(vec3(-0.35, 1.0, -0.45));
            float spec = pow(max(0.0, dot(reflect(-sun, n), v)), 28.0) * dot(ambientLight, vec3(0.3333));
            vec3 shine = vec3(0.36, 0.42, 0.48) * spec;
            for (int i = 0; i < lightCount; i++)
            {
                vec3 l = lightPositionRadius[i].xyz - fragPosition;
                float falloff = max(0.0, 1.0 - length(l) / lightPositionRadius[i].w);
                shine += lightColorIntensity[i].rgb * lightColorIntensity[i].w * falloff * falloff *
                    pow(max(0.0, dot(reflect(-normalize(l), n), v)), 18.0) * 0.45;
            }
            return color * (1.0 - wetness * 0.20) + shine * wetness;
        }
        float rainRipple()
        {
            if (rainIntensity <= 0.0) return 0.0;
            vec2 p = fragPosition.xz * 1.7, base = floor(p);
            float ripples = 0.0;
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
            {
                vec2 cell = base + vec2(x,y);
                float seed = visualNoise(cell), age = fract(visualTime * 0.9 + seed);
                vec2 center = cell + vec2(0.2 + seed * 0.6, 0.2 + visualNoise(cell + 17.0) * 0.6);
                float ring = 1.0 - smoothstep(0.018, 0.045, abs(length(p - center) - age * 0.7));
                ripples = max(ripples, ring * (1.0 - age));
            }
            return ripples * rainIntensity;
        }

        """;
    public static Vector3 Ambient(float hour)
    {
        float daylight = Math.Clamp(MathF.Sin((hour - 6) * MathF.PI / 12) * 3, 0, 1);
        return Vector3.Lerp(new(.16f, .20f, .32f), Vector3.One, daylight);
    }
    public static bool Enabled(MapData map) => map.AlwaysDark || map.Environment.Lighting;
    public static Vector3 Ambient(MapData map, float hour) => map.AlwaysDark ? Ambient(0) :
        map.Environment.Lighting ? Ambient(hour) : Vector3.One;
    public static IEnumerable<PointLightData> AllLights(MapData map, WorldVisualFrame? frame = null)
    {
        foreach (var light in map.Lights.Where(l => l.Enabled)) yield return light;
        foreach (var fire in map.Objects.Where(o => IsFire(o.Asset)))
            yield return new() { Id = fire.Id, X = fire.X, Z = fire.Z, Height = Math.Min(fire.Height, 2), Radius = 6,
                Intensity = FireIntensity(fire.Id, frame), R = 255, G = 155, B = 65 };
    }
    static float FireIntensity(string id, WorldVisualFrame? frame)
    {
        const float baseIntensity = .85f;
        if (frame?.Enabled != true || !frame.Settings.DynamicLights || frame.Settings.LightFlickerStrength <= 0 ||
            frame.Settings.LightFlickerSpeed <= 0 || !float.IsFinite(frame.Time)) return baseIntensity;
        // Stable per-object phases keep different flames independent without changing authored light data.
        uint seed = 2166136261;
        foreach (char character in id) seed = unchecked((seed ^ character) * 16777619);
        float phase = (seed & 65535) * (MathF.Tau / 65536);
        float time = frame.Time * frame.Settings.LightFlickerSpeed;
        float flicker = .55f * MathF.Sin(time * 7.3f + phase) +
            .30f * MathF.Sin(time * 13.1f + phase * 1.7f) + .15f * MathF.Sin(time * 23.7f + phase * 2.3f);
        return baseIntensity * (1 + flicker * Math.Clamp(frame.Settings.LightFlickerStrength, 0, 1));
    }
    public static bool IsFire(string id) => id.Contains("campfire", StringComparison.OrdinalIgnoreCase) ||
        id.Contains("torch", StringComparison.OrdinalIgnoreCase) || id.Contains("ognisko", StringComparison.OrdinalIgnoreCase) ||
        id.Contains("pochodnia", StringComparison.OrdinalIgnoreCase);
    public static PointLightData[] Nearby(MapData map, Vector3 focus, WorldVisualFrame? frame = null) => Enabled(map)
        ? AllLights(map, frame).OrderBy(l => Vector2.DistanceSquared(new(l.X, l.Z), new(focus.X, focus.Z))).Take(16).ToArray() : [];
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
    public static void Apply(Shader shader, MapData map, float hour, Vector3 focus, WorldVisualFrame? frame = null)
    {
        var lights = Nearby(map, focus, frame);
        SetShaderValue(shader, GetShaderLocation(shader, "ambientLight"), Ambient(map, hour), ShaderUniformDataType.Vec3);
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
