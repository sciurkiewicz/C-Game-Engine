using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Owns the world image only. HUD, dialogue, inventory and narrative are drawn afterwards.
sealed class WorldVisualEffects : IDisposable
{
    sealed class ProgramShader(string fragment) : IDisposable
    {
        public Shader Shader { get; } = Load(fragment);
        readonly Dictionary<string, int> locations = [];
        static Shader Load(string fragment)
        {
            var shader = LoadShaderFromMemory(GraphicsShaders.PostVertex, fragment);
            if (!IsShaderValid(shader) || shader.Id == Rlgl.GetShaderIdDefault()) throw new InvalidDataException("Cannot compile graphics effects shader.");
            return shader;
        }
        int Location(string name) { if (!locations.TryGetValue(name, out int at)) locations[name] = at = GetShaderLocation(Shader, name); return at; }
        public void Float(string name, float value) => SetShaderValue(Shader, Location(name), value, ShaderUniformDataType.Float);
        public void Int(string name, int value) => SetShaderValue(Shader, Location(name), value, ShaderUniformDataType.Int);
        public void Vec2(string name, Vector2 value) => SetShaderValue(Shader, Location(name), value, ShaderUniformDataType.Vec2);
        public void Vec4(string name, Vector4 value) => SetShaderValue(Shader, Location(name), value, ShaderUniformDataType.Vec4);
        public void Array(string name, Vector4[] values)
        { if (values.Length > 0) SetShaderValueV(Shader, Location(name), values, ShaderUniformDataType.Vec4, values.Length); }
        public void Texture(string name, Texture2D texture) => SetShaderValueTexture(Shader, Location(name), texture);
        public void Dispose() => UnloadShader(Shader);
    }
    GraphicsSettings settings = new();
    PlaySession? owner;
    int previousHealth, width, height;
    float time, damageTime, damage;
    RenderTexture2D scene, focused, bright, blurH, blurV, sunMask, rays;
    ProgramShader? brightShader, blurShader, raysShader, focusShader, composite;
    bool focusApplied;
    Vector4[] heatSources = [];
    Vector4 strengths;
    public GraphicsSettings Settings => settings;
    public float DamagePulse => damage;
    public float Time => time;
    public WorldVisualFrame? LastFrame { get; private set; }
    public void Configure(GraphicsSettings value, PlaySession play)
    {
        value.Validate(); settings = value; owner = play; previousHealth = play.PlayerHealth; time = damageTime = damage = 0;
    }
    public void Update(PlaySession play, float dt)
    {
        if (!ReferenceEquals(owner, play)) Configure(settings, play);
        if (!float.IsFinite(dt) || dt < 0) return;
        time = (time + dt) % 3600;
        damageTime = Math.Max(0, damageTime - dt);
        if (play.PlayerHealth > previousHealth) damageTime = 0;
        if (play.PlayerHealth < previousHealth && play.PlayerHitFlash > 0) damageTime = settings.DamageDuration;
        previousHealth = play.PlayerHealth;
        damage = settings.Enabled && settings.Damage ? damageTime / settings.DamageDuration : 0;
    }
    public Camera3D View(Camera3D view)
    {
        if (damage <= 0 || settings.CameraShake <= 0) return view;
        var forward = Vector3.Normalize(view.Target - view.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, view.Up)); var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var offset = (right * MathF.Sin(time * 47.3f) + up * MathF.Cos(time * 63.1f)) * damage * settings.CameraShake;
        view.Position += offset; view.Target += offset; return view;
    }
    void EnsureTargets(int w, int h)
    {
        if (composite == null)
        {
            try
            {
                brightShader = new(GraphicsShaders.BrightPass); blurShader = new(GraphicsShaders.Blur);
                raysShader = new(GraphicsShaders.Rays); focusShader = new(GraphicsShaders.DepthOfField);
                composite = new(GraphicsShaders.Composite);
            }
            catch { Dispose(); throw; }
        }
        if (width == w && height == h && scene.Id != 0) return;
        ReleaseTargets(); width = w; height = h;
        try
        {
            scene = SceneTarget(w, h); focused = Target(w, h); bright = Target(Math.Max(1,w/2),Math.Max(1,h/2));
            blurH = Target(bright.Texture.Width,bright.Texture.Height); blurV = Target(bright.Texture.Width,bright.Texture.Height);
            sunMask = Target(bright.Texture.Width,bright.Texture.Height); rays = Target(bright.Texture.Width,bright.Texture.Height);
        }
        catch { ReleaseTargets(); throw; }
    }
    static RenderTexture2D Target(int w, int h)
    {
        var target = LoadRenderTexture(w,h);
        if (!IsRenderTextureValid(target)) throw new InvalidDataException("Cannot create graphics effects framebuffer.");
        SetTextureFilter(target.Texture, TextureFilter.Bilinear); SetTextureWrap(target.Texture, TextureWrap.Clamp); return target;
    }
    static RenderTexture2D SceneTarget(int w, int h)
    {
        // Raylib's default render target has an unsampleable depth renderbuffer.
        // A depth texture reuses the world draw, including alpha-tested sprites, for autofocus.
        var target = new RenderTexture2D { Id = Rlgl.LoadFramebuffer() };
        try
        {
            if (target.Id == 0) throw new InvalidDataException("Cannot create scene framebuffer.");
            var image = GenImageColor(w, h, Color.Black);
            try { target.Texture = LoadTextureFromImage(image); }
            finally { UnloadImage(image); }
            if (!IsTextureValid(target.Texture)) throw new InvalidDataException("Cannot create scene color texture.");
            Rlgl.FramebufferAttach(target.Id, target.Texture.Id, FramebufferAttachType.ColorChannel0, FramebufferAttachTextureType.Texture2D, 0);
            target.Depth = new Texture2D { Id = Rlgl.LoadTextureDepth(w, h, false), Width = w, Height = h, Mipmaps = 1 };
            Rlgl.FramebufferAttach(target.Id, target.Depth.Id, FramebufferAttachType.Depth, FramebufferAttachTextureType.Texture2D, 0);
            if (target.Depth.Id == 0 || !Rlgl.FramebufferComplete(target.Id))
                throw new InvalidDataException("Cannot create sampleable scene depth texture.");
            SetTextureFilter(target.Texture, TextureFilter.Bilinear); SetTextureWrap(target.Texture, TextureWrap.Clamp);
            SetTextureFilter(target.Depth, TextureFilter.Point); SetTextureWrap(target.Depth, TextureWrap.Clamp);
            return target;
        }
        catch { if (target.Id != 0) UnloadRenderTexture(target); throw; }
    }
    static void Quad(Texture2D texture, Rectangle destination) => DrawTexturePro(texture, new(0,0,texture.Width,-texture.Height), destination, Vector2.Zero, 0, Color.White);
    static void Clear(RenderTexture2D target) { BeginTextureMode(target); ClearBackground(Color.Black); EndTextureMode(); }
    static void Pass(RenderTexture2D target, Texture2D source, ProgramShader shader)
    {
        BeginTextureMode(target); ClearBackground(Color.Black); BeginShaderMode(shader.Shader);
        Quad(source, new(0,0,target.Texture.Width,target.Texture.Height)); EndShaderMode(); EndTextureMode();
    }
    static Vector2 UV(Vector2 screen, int w, int h) => new(screen.X/w, 1-screen.Y/h);
    public void Render(WorldRenderer renderer, MapData map, Camera3D camera, Vector3 player, int w, int h, float hour)
    {
        w = Math.Max(1,w); h = Math.Max(1,h); EnsureTargets(w,h);
        var frame = new WorldVisualFrame(settings,time,camera,hour,map.Type == MapType.OpenWorld); LastFrame = frame;
        BeginTextureMode(scene); ClearBackground(new(38,47,54,255)); BeginMode3D(camera);
        renderer.Draw(map,camera,false,player,w,h,hour,visuals: frame);
        EndMode3D(); EndTextureMode();
        var resolution = new Vector2(w,h);
        focusApplied = settings.Enabled && settings.DepthOfField && settings.MaxBlurRadius > 0;
        if (focusApplied)
        {
            focusShader!.Vec2("resolution", resolution);
            focusShader.Vec4("cameraPlanes", new((float)Rlgl.GetCullDistanceNear(), (float)Rlgl.GetCullDistanceFar(),
                camera.Projection == CameraProjection.Orthographic ? 1 : 0, 0));
            focusShader.Vec4("focusParameters", new(WorldFocus.Distance(camera, player, map.Player.Height, (float)w / h),
                settings.FocusRange, settings.FocusFalloff, settings.MaxBlurRadius * h / 1080f));
            BeginTextureMode(focused); ClearBackground(Color.Black); BeginShaderMode(focusShader.Shader);
            focusShader.Texture("depthMap", scene.Depth);
            Quad(scene.Texture, new(0,0,w,h)); EndShaderMode(); EndTextureMode();
        }
        var forward = Vector3.Normalize(camera.Target-camera.Position);
        var lights = settings.Enabled && settings.Bloom ? WorldLighting.Nearby(map,camera.Target,frame)
            .Where(l => Vector3.Dot(map.GroundPosition(l.X,l.Z)+Vector3.UnitY*l.Height-camera.Position,forward)>0).ToArray() : [];
        if (settings.Enabled && settings.Bloom && settings.BloomStrength > 0)
        {
            var sources = lights.Select(l =>
            {
                var p = map.GroundPosition(l.X,l.Z)+Vector3.UnitY*l.Height;
                var at = GetWorldToScreenEx(p,camera,w,h);
                var edge = GetWorldToScreenEx(p+Vector3.Normalize(Vector3.Cross(forward,camera.Up))*Math.Min(l.Radius*.22f,2),camera,w,h);
                return new Vector4(UV(at,w,h),Math.Max(6,Vector2.Distance(at,edge)),0);
            }).ToArray();
            brightShader!.Float("threshold",settings.BloomThreshold); brightShader.Vec2("resolution",resolution);
            brightShader.Int("sourceCount",sources.Length); brightShader.Array("sources",sources);
            brightShader.Array("sourceColors",lights.Select(l=>new Vector4(l.R/255f,l.G/255f,l.B/255f,l.Intensity)).ToArray());
            Pass(bright,scene.Texture,brightShader);
            blurShader!.Vec2("direction",new(1f/bright.Texture.Width,0)); Pass(blurH,bright.Texture,blurShader);
            blurShader.Vec2("direction",new(0,1f/bright.Texture.Height)); Pass(blurV,blurH.Texture,blurShader);
        }
        else Clear(blurV);
        if (settings.Enabled && settings.SunRays && frame.Sunlight > 0 && settings.SunRaysStrength > 0)
        {
            var sun = GetWorldToScreenEx(camera.Target+new Vector3(-10,18,-10),camera,w,h);
            sun = Vector2.Clamp(sun,new(-w*.15f,-h*.15f),new(w*1.15f,h*.5f));
            BeginTextureMode(sunMask); ClearBackground(Color.White);
            BeginMode3D(camera); renderer.DrawSunOccluders(map,camera,frame); EndMode3D(); EndTextureMode();
            raysShader!.Vec2("sunPosition",UV(sun,w,h)); Pass(rays,sunMask.Texture,raysShader);
        }
        else Clear(rays);
        heatSources = settings.Enabled && settings.HeatDistortion && settings.HeatStrength > 0 ? map.Objects
            .Where(o=>WorldLighting.IsFire(o.Asset) && Vector3.DistanceSquared(o.Position,camera.Target)<10000)
            .OrderBy(o=>Vector3.DistanceSquared(o.Position,camera.Target)).Take(16).Select(o=>
            {
                var p=map.GroundPosition(o)+Vector3.UnitY*(o.Height+.6f);
                if(Vector3.Dot(p-camera.Position,forward)<=0)return Vector4.Zero;
                var at=GetWorldToScreenEx(p,camera,w,h);
                var top=GetWorldToScreenEx(p+Vector3.UnitY*1.6f,camera,w,h);
                float ry=Math.Clamp(Vector2.Distance(at,top)/h,.015f,.3f);
                return new Vector4(UV(at,w,h),Math.Max(.01f,ry*h/w*.65f),ry);
            }).Where(v=>v.Z>0).ToArray() : [];
        strengths = new(settings.Enabled&&settings.Bloom?settings.BloomStrength:0,
            settings.Enabled&&settings.SunRays?settings.SunRaysStrength*frame.Sunlight:0,frame.Rain,damage*settings.DamageStrength);
        composite!.Vec2("resolution",resolution); composite.Vec4("strengths",strengths);
        composite.Vec4("colorGrade", settings.ColorGrading ? new(settings.Exposure,settings.Contrast,settings.Saturation,settings.Temperature) : new(1,1,1,0));
        composite.Float("vignetteStrength",settings.Vignette ? settings.VignetteStrength : 0);
        composite.Float("time",time); composite.Float("heatStrength",settings.HeatStrength);
        composite.Int("heatCount",heatSources.Length); composite.Array("heatSources",heatSources);
    }
    public void Draw(Rectangle bounds)
    {
        if (scene.Id == 0) return;
        if (!settings.Enabled) { Quad(scene.Texture,bounds); return; }
        BeginShaderMode(composite!.Shader);
        composite.Texture("bloomMap",blurV.Texture); composite.Texture("raysMap",rays.Texture);
        Quad(focusApplied ? focused.Texture : scene.Texture,bounds); EndShaderMode();
    }
    void ReleaseTargets()
    {
        foreach(var t in new[]{scene,focused,bright,blurH,blurV,sunMask,rays})if(t.Id!=0)UnloadRenderTexture(t);
        scene=focused=bright=blurH=blurV=sunMask=rays=default; width=height=0; focusApplied=false;
    }
    public void Dispose()
    {
        ReleaseTargets(); brightShader?.Dispose(); blurShader?.Dispose(); raysShader?.Dispose(); focusShader?.Dispose(); composite?.Dispose();
        brightShader=blurShader=raysShader=focusShader=composite=null;
    }
}
