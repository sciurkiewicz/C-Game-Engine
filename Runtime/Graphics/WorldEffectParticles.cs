using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Stateless particles: emitter age is derived from frame time and a stable ID.
// Editing, cloning and loading maps never need to store live particle buffers.
sealed class WorldEffectParticles : IDisposable
{
    readonly record struct Particle(Vector3 Position, float Width, float Height, Color Color, float Style, float Seed);
    readonly Shader shader;
    readonly int timeLocation;
    readonly List<Particle> particles = [];
    const string VertexShader = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec4 vertexColor;
        in vec3 vertexNormal;
        uniform mat4 mvp;
        out vec2 uv;
        out vec4 tint;
        out vec2 parameters;
        void main()
        {
            uv = vertexTexCoord; tint = vertexColor; parameters = vertexNormal.xy;
            gl_Position = mvp * vec4(vertexPosition, 1.0);
        }
        """;
    const string FragmentShader = """
        #version 330
        in vec2 uv;
        in vec4 tint;
        in vec2 parameters;
        uniform float time;
        out vec4 finalColor;
        float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7))) * 43758.5453); }
        float noise(vec2 p)
        {
            vec2 cell=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
            return mix(mix(hash(cell),hash(cell+vec2(1,0)),f.x),mix(hash(cell+vec2(0,1)),hash(cell+vec2(1)),f.x),f.y);
        }
        void main()
        {
            vec2 p=uv*2.0-1.0;
            float radius=length(p), alpha;
            vec3 color=tint.rgb;
            if (parameters.x < 1.5)
            {
                float n=noise(p*3.8+vec2(parameters.y*29.0,time*0.23));
                float wisps=noise(p*7.0-vec2(time*0.15,parameters.y*13.0));
                alpha=(1.0-smoothstep(0.40+n*0.12,0.94,radius)) * (0.56+n*0.30+wisps*0.14);
                color*=0.78+n*0.28;
            }
            else if (parameters.x < 2.5)
            {
                alpha=exp(-radius*radius*5.5) * (1.0-smoothstep(0.8,1.0,radius));
                color=mix(color,vec3(1.0,0.98,0.78),exp(-radius*radius*45.0)*0.75);
            }
            else
            {
                float line=abs(sin(p.x*14.0+sin(p.y*5.0-time*2.2+parameters.y)*1.5));
                alpha=(1.0-smoothstep(0.05,0.3,line))*(1.0-smoothstep(0.3,1.0,radius))*0.55;
            }
            alpha*=tint.a;
            if(alpha<0.002)discard;
            finalColor=vec4(color,alpha);
        }
        """;
    public WorldEffectParticles()
    {
        shader = LoadShaderFromMemory(VertexShader,FragmentShader);
        if (!IsShaderValid(shader) || shader.Id == Rlgl.GetShaderIdDefault())
            throw new InvalidDataException("Cannot compile map effect particle shader.");
        timeLocation = GetShaderLocation(shader,"time");
    }
    public void Draw(MapData map, Camera3D camera, float time, byte opacity = 255)
    {
        particles.Clear();
        var forward = Vector3.Normalize(camera.Target-camera.Position);
        foreach (var emitter in map.Objects)
        {
            if (!WorldEffectCatalog.TryGet(emitter.Asset,out var definition) || definition.Kind == WorldEffectKind.HeatHaze) continue;
            var position = map.GroundPosition(emitter);
            // Keep cost bounded by visible scenery rather than every emitter on a large map.
            if (Vector3.DistanceSquared(position,camera.Target) > MathF.Pow(180+emitter.Height,2) ||
                Vector3.Dot(position-camera.Position,forward) < -emitter.Height) continue;
            Emit(definition.Kind,position,emitter.Height,time,Seed(emitter.Id),opacity);
        }
        Render(camera,time);
    }
    public void Preview(Camera3D camera, string id, Vector3 position, float height, float time, byte alpha = 130)
    {
        if (!WorldEffectCatalog.TryGet(id,out var definition)) return;
        particles.Clear();
        Emit(definition.Kind,position,height,time,Seed(id),alpha);
        Render(camera,time);
    }
    static float Seed(string id)
    {
        uint hash = 2166136261;
        foreach(char c in id) hash = unchecked((hash^c)*16777619);
        return (hash & 65535)/65536f;
    }
    static float Fract(float value) => value-MathF.Floor(value);
    static int Alpha(float value, byte opacity) => (int)Math.Clamp(value*opacity,0,255);
    void Emit(WorldEffectKind kind, Vector3 origin, float height, float time, float phase, byte opacity)
    {
        height = Math.Clamp(height,SceneObject.MinHeight,SceneObject.MaxHeight);
        if (!float.IsFinite(time)) time=0;
        if (kind == WorldEffectKind.HeatHaze)
        {
            particles.Add(new(origin+Vector3.UnitY*height*.5f,height*.7f,height,new(255,192,118,Alpha(.7f,opacity)),3,phase));
            return;
        }
        int count = kind switch { WorldEffectKind.Smoke => 16, WorldEffectKind.Steam => 18, WorldEffectKind.Embers => 22, _ => 14 };
        for (int i=0;i<count;i++)
        {
            float seed=Fract(phase+i*.61803399f), second=Fract(seed*7.371f+.123f);
            if (kind == WorldEffectKind.Fireflies)
            {
                float t=time*(.3f+second*.3f)+seed*MathF.Tau;
                var position=origin+height*new Vector3(MathF.Sin(t*1.1f+seed*9)*.4f,
                    .38f+MathF.Sin(t*.8f+second*7)*.25f,MathF.Cos(t*.9f+second*8)*.4f);
                float flicker=.35f+.65f*MathF.Pow(.5f+.5f*MathF.Sin(time*2.4f+seed*19),2);
                particles.Add(new(position,height*.07f,height*.07f,new(192,249,93,Alpha(flicker,opacity)),2,seed));
                continue;
            }
            bool smoke = kind == WorldEffectKind.Smoke, steam = kind == WorldEffectKind.Steam;
            float lifetime=smoke ? 5.2f : steam ? 3.1f : 1.5f+second*.8f;
            float age=Fract(time/lifetime+seed);
            float fade=MathF.Pow(Math.Max(0,MathF.Sin(age*MathF.PI)),.8f);
            float spread=smoke ? .17f : steam ? .09f : .18f;
            var offset=new Vector3(MathF.Sin(seed*19+age*2.8f)*spread*age,
                age*.9f,MathF.Cos(second*17+age*2.2f)*spread*age);
            if (smoke || steam)
            {
                float size=height*(smoke ? .20f+age*.35f : .13f+age*.28f);
                var color=smoke ? new Color(126,133,139,Alpha(fade*.30f,opacity)) : new Color(226,238,243,Alpha(fade*.22f,opacity));
                particles.Add(new(origin+height*offset+Vector3.UnitY*size*.25f,size,size,color,smoke ? 0 : 1,seed));
            }
            else
            {
                offset.X+=(seed-.5f)*.12f; offset.Z+=(second-.5f)*.12f;
                float size=height*(.02f+second*.025f);
                particles.Add(new(origin+height*offset,size,size*(1.3f+age),new(255,(byte)(125+second*90),32,Alpha(fade,opacity)),2,seed));
            }
        }
    }
    void Render(Camera3D camera, float time)
    {
        if (particles.Count == 0) return;
        var forward=Vector3.Normalize(camera.Target-camera.Position);
        var right=Vector3.Normalize(Vector3.Cross(forward,camera.Up));
        var up=Vector3.Cross(right,forward);
        particles.Sort((a,b)=>Vector3.Dot(b.Position-camera.Position,forward).CompareTo(Vector3.Dot(a.Position-camera.Position,forward)));
        SetShaderValue(shader,timeLocation,float.IsFinite(time)?time:0,ShaderUniformDataType.Float);
        BeginShaderMode(shader);
        Rlgl.DrawRenderBatchActive(); Rlgl.DisableDepthMask(); Rlgl.DisableBackfaceCulling();
        try
        {
            Rlgl.SetTexture(0);
            foreach (var particle in particles)
            {
                var x=right*particle.Width*.5f; var y=up*particle.Height*.5f;
                Rlgl.CheckRenderBatchLimit(4);
                Rlgl.Begin((int)DrawMode.Quads);
                Rlgl.Color4ub(particle.Color.R,particle.Color.G,particle.Color.B,particle.Color.A);
                Rlgl.Normal3f(particle.Style,particle.Seed,0);
                Vertex(particle.Position-x-y,0,1); Vertex(particle.Position+x-y,1,1);
                Vertex(particle.Position+x+y,1,0); Vertex(particle.Position-x+y,0,0);
                Rlgl.End();
            }
            Rlgl.DrawRenderBatchActive();
        }
        finally { Rlgl.EnableBackfaceCulling(); Rlgl.EnableDepthMask(); EndShaderMode(); }
    }
    static void Vertex(Vector3 position,float u,float v)
    { Rlgl.TexCoord2f(u,v); Rlgl.Vertex3f(position.X,position.Y,position.Z); }
    public void Dispose() => UnloadShader(shader);
}

sealed partial class WorldRenderer
{
    public void DrawEffectObjects(MapData map, Camera3D camera, float time, Vector3? player = null) =>
        assets.EffectParticles.Draw(map,camera,time);
    public void DrawEffectPreview(Camera3D camera,string id,Vector3 position,float height,float time,byte alpha=130) =>
        assets.EffectParticles.Preview(camera,id,position,height,time,alpha);
}
