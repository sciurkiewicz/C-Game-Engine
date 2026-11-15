using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Refraction belongs to a placed emitter. It also works in the editing view and with global effects disabled.
sealed class WorldHeatHaze : IDisposable
{
    const int MaximumSources = 32;
    Shader shader;
    RenderTexture2D target;
    readonly Dictionary<string, int> locations = [];
    public int SourceCount { get; private set; }
    int Location(string name)
    { if (!locations.TryGetValue(name, out int at)) locations[name] = at = GetShaderLocation(shader, name); return at; }

    public Texture2D Apply(Texture2D color, Texture2D depth, MapData map, Camera3D camera, float time, int width, int height,
        GraphicsSettings? settings = null)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var sources = new List<Vector4>(); var parameters = new List<Vector3>();
        bool automaticFire = settings is { Enabled: true, HeatDistortion: true, HeatStrength: > 0 };
        foreach (var item in map.Objects.Where(o => o.Asset == WorldEffectCatalog.HeatHazeId || automaticFire && WorldLighting.IsFire(o.Asset))
            .OrderBy(o => Vector3.DistanceSquared(map.GroundPosition(o), camera.Target)))
        {
            bool placed = WorldEffectCatalog.TryGet(item.Asset, out var effect) && effect.Kind == WorldEffectKind.HeatHaze;
            if (!placed && !(automaticFire && WorldLighting.IsFire(item.Asset))) continue;
            var feet = map.GroundPosition(item);
            float sourceHeight = placed ? item.Height : 2;
            if (!placed) feet += Vector3.UnitY * (item.Height + .1f);
            var quad = BillboardGeometry.Create(camera, feet, sourceHeight * .85f, sourceHeight);
            float front = Math.Min(Vector3.Dot(quad.BottomLeft-camera.Position,forward), Vector3.Dot(quad.TopLeft-camera.Position,forward));
            if (front <= .05f) continue;
            var at = GetWorldToScreenEx(quad.Center, camera, width, height);
            var left = GetWorldToScreenEx((quad.BottomLeft+quad.TopLeft)*.5f,camera,width,height);
            var top = GetWorldToScreenEx((quad.TopLeft+quad.TopRight)*.5f,camera,width,height);
            var radius = new Vector2(Math.Max(1,Vector2.Distance(at,left)),Math.Max(1,Vector2.Distance(at,top)));
            if (at.X+radius.X<0 || at.X-radius.X>width || at.Y+radius.Y<0 || at.Y-radius.Y>height) continue;
            sources.Add(new(at.X/width,1-at.Y/height,radius.X/width,radius.Y/height));
            float strength = placed ? .003f : settings!.HeatStrength;
            uint seed=2166136261;
            foreach(char character in item.Id) seed=unchecked((seed^character)*16777619);
            parameters.Add(new(front-sourceHeight*.15f,strength*Math.Min(1,radius.Y/height*12),(seed&65535)/65536f));
            if (sources.Count == MaximumSources) break;
        }
        SourceCount = sources.Count;
        if (SourceCount == 0) return color;
        EnsureTarget(width,height);
        SetShaderValue(shader,Location("sourceCount"),SourceCount,ShaderUniformDataType.Int);
        SetShaderValueV(shader,Location("sources"),sources.ToArray(),ShaderUniformDataType.Vec4,SourceCount);
        SetShaderValueV(shader,Location("sourceParameters"),parameters.ToArray(),ShaderUniformDataType.Vec3,SourceCount);
        SetShaderValue(shader,Location("time"),time,ShaderUniformDataType.Float);
        SetShaderValue(shader,Location("cameraPlanes"),new Vector3((float)Rlgl.GetCullDistanceNear(),(float)Rlgl.GetCullDistanceFar(),
            camera.Projection == CameraProjection.Orthographic ? 1 : 0),ShaderUniformDataType.Vec3);
        BeginTextureMode(target); ClearBackground(Color.Black); BeginShaderMode(shader);
        SetShaderValueTexture(shader,Location("depthMap"),depth);
        DrawTexturePro(color,new(0,0,color.Width,-color.Height),new(0,0,width,height),Vector2.Zero,0,Color.White);
        EndShaderMode(); EndTextureMode();
        return target.Texture;
    }
    void EnsureTarget(int width, int height)
    {
        if (shader.Id == 0)
        {
            shader = LoadShaderFromMemory(GraphicsShaders.PostVertex,FragmentShader);
            if (!IsShaderValid(shader) || shader.Id == Rlgl.GetShaderIdDefault())
            { shader=default; throw new InvalidDataException("Cannot compile local heat haze shader."); }
        }
        if (target.Id != 0 && target.Texture.Width == width && target.Texture.Height == height) return;
        if (target.Id != 0) UnloadRenderTexture(target);
        target=LoadRenderTexture(width,height);
        if (!IsRenderTextureValid(target)) throw new InvalidDataException("Cannot create local heat haze framebuffer.");
        SetTextureFilter(target.Texture,TextureFilter.Bilinear); SetTextureWrap(target.Texture,TextureWrap.Clamp);
    }
    public void Dispose()
    {
        if (target.Id!=0) UnloadRenderTexture(target);
        if (shader.Id!=0) UnloadShader(shader);
        target=default; shader=default; locations.Clear();
    }
    const string FragmentShader = """
        #version 330
        in vec2 fragTexCoord;
        uniform sampler2D texture0;
        uniform sampler2D depthMap;
        uniform int sourceCount;
        uniform vec4 sources[32];
        uniform vec3 sourceParameters[32]; // front depth, amplitude, stable emitter phase
        uniform vec3 cameraPlanes;
        uniform float time;
        out vec4 finalColor;
        float linearDepth(float d)
        {
            float n=cameraPlanes.x,f=cameraPlanes.y;
            return cameraPlanes.z>0.5 ? mix(n,f,d) : (2.0*n*f)/(f+n-(d*2.0-1.0)*(f-n));
        }
        float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
        float noise(vec2 p)
        {
            vec2 cell=floor(p),f=fract(p); f=f*f*(3.0-2.0*f);
            return mix(mix(hash(cell),hash(cell+vec2(1,0)),f.x),
                mix(hash(cell+vec2(0,1)),hash(cell+vec2(1,1)),f.x),f.y);
        }
        void main()
        {
            vec2 uv=fragTexCoord,offset=vec2(0);
            float visibleDepth=linearDepth(texture(depthMap,uv).r);
            for(int i=0;i<sourceCount;i++)
            {
                if(visibleDepth<sourceParameters[i].x)continue;
                vec2 p=(uv-sources[i].xy)/sources[i].zw;
                float envelope=max(0.0,1.0-dot(p,p));
                if(envelope<=0.0)continue;
                float strength=sourceParameters[i].y*envelope*envelope;
                // Fine, rising turbulence: separate scales and drift rates avoid a broad repeating wave.
                float phase=sourceParameters[i].z;
                vec2 q=p*vec2(7.0,12.0)+vec2(phase*37.0,phase*53.0);
                float coarse=noise(q+vec2(time*0.35,-time*12.0))*2.0-1.0;
                float fine=noise(q*1.83+vec2(17.3-time*0.27,-time*19.0))*2.0-1.0;
                float vertical=noise(q*1.37+vec2(43.1+time*0.19,-time*15.0))*2.0-1.0;
                offset+=vec2(coarse*0.65+fine*0.35,vertical*0.22)*strength;
            }
            vec2 refracted=clamp(uv+offset,vec2(0.001),vec2(0.999));
            // A displaced sample must not pull a solid foreground surface into the heat.
            float displacedDepth=linearDepth(texture(depthMap,refracted).r);
            if(displacedDepth<visibleDepth-1.0)refracted=uv;
            finalColor=vec4(texture(texture0,refracted).rgb,1);
        }
        """;
}
