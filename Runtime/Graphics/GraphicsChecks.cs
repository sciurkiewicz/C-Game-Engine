using System.Numerics;
using System.Text.Json;

static class GraphicsChecks
{
    public static void Run()
    {
        var settings = new GraphicsSettings(); settings.Validate();
        foreach(var invalid in new[] { settings with { FogStrength = float.NaN },settings with { FogEnd = 1 },
            settings with { WindStrength = -1 },settings with { BloomStrength = 10 },settings with { SunRaysStrength = float.PositiveInfinity },
            settings with { OutlineWidth = 0 },settings with { HeatStrength = 1 },settings with { RainIntensity = 2 },
            settings with { Wetness = -1 },settings with { DamageDuration = 0 },settings with { CameraShake = 100 },
            settings with { LightFlickerStrength = 2 },settings with { LightFlickerSpeed = -1 },
            settings with { FocusRange = 0 },settings with { FocusFalloff = float.NaN },settings with { MaxBlurRadius = 25 },
            settings with { Exposure = 0 },settings with { Contrast = 2 },settings with { Saturation = -1 },
            settings with { Temperature = float.PositiveInfinity },settings with { VignetteStrength = -1 } })
            ExpectInvalid(invalid.Validate);
        var camera = new FollowCamera(Vector3.Zero).View;
        float focus = WorldFocus.Distance(camera, Vector3.Zero, 2, 1.6f);
        Check(Math.Abs(focus - Vector3.Dot(Vector3.UnitY - camera.Position, Vector3.Normalize(camera.Target-camera.Position))) < .001f,
            "focus tracks the middle of the hero along the camera depth axis");
        Check(WorldFocus.Distance(camera, new(10000,0,0), 2, 1.6f) == Vector3.Distance(camera.Position,camera.Target),
            "off-screen player previews focus on the camera target");
        var raised = camera; raised.Position += new Vector3(0,7,0); raised.Target += new Vector3(0,7,0);
        Check(Math.Abs(WorldFocus.Distance(raised,new(0,7,0),2,1.6f)-focus)<.001f,"terrain height preserves the focal distance");
        var outside = new WorldVisualFrame(settings,1,camera,12,true);
        var inside = outside with { Outdoors = false };
        Check(outside.Rain>0&&outside.Wetness>0&&outside.Wind>0&&outside.Sunlight>0,"outdoor effects are enabled");
        Check(inside.Rain==0&&inside.Wetness==0&&inside.Wind==0&&inside.Sunlight==0,"weather and sun stay outside interiors");
        Check((outside with { Hour = 0 }).Sunlight==0,"sun rays turn off at night");
        var disabled=outside with { Settings=settings with { Enabled=false } };
        Check(disabled.Rain==0&&disabled.Wetness==0&&disabled.Wind==0,"master switch disables world shader effects");
        Check(WorldVisualFrame.Vegetation("objects/vegetation/trees/oak.png")&&WorldVisualFrame.Vegetation("objects/grass/tuft.png")&&
            !WorldVisualFrame.Vegetation("characters/hero.png"),"wind selects plants rather than characters");
        Check(!WorldVisualFrame.Vegetation(new NpcData { Asset="objects/vegetation/bushes/bush.png" }),
            "an NPC keeps its pose even if a vegetation image is used as its placeholder");
        Check(WorldLighting.IsFire("objects/props/camp/ognisko.png")&&WorldLighting.IsFire("objects/torch.png")&&!WorldLighting.IsFire("objects/rocks/large/rock4.png"),
            "fire effects recognize the supplied Polish campfire asset");
        var map=MapData.CreateEmpty(16,16); map.Monsters.Add(new(){X=.8f}); string source=map.Serialize();
        var play=new PlaySession(map); using var fx=new WorldVisualEffects(); fx.Configure(settings,play);
        var original=play.Camera.View; play.UpdateCombat(.1f); fx.Update(play,.01f);
        Check(fx.DamagePulse>0&&fx.View(original).Position!=original.Position,"a real hit triggers shake and damage feedback");
        Check(play.Camera.View.Position==original.Position&&play.Camera.View.Target==original.Target,"shake does not modify the game camera or movement axes");
        fx.Update(play,2); Check(fx.DamagePulse==0,"damage feedback decays instead of remaining on the screen");
        Check(map.Serialize()==source,"visual effects preserve authored map data");
        string folder=Path.Combine(Path.GetTempPath(),"wildwood-graphics-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            string file=Path.Combine(folder,"effects.json"); File.WriteAllText(file,JsonSerializer.Serialize(settings));
            Check(GraphicsSettings.Load(file)==settings,"all toggles and intensities roundtrip through editable JSON");
            File.WriteAllText(file,"{\"FogStrenght\":1}"); ExpectInvalid(()=>GraphicsSettings.Load(file));
            File.WriteAllText(file,"{\"RainIntensity\":-1}"); ExpectInvalid(()=>GraphicsSettings.Load(file));
        }
        finally { Directory.Delete(folder,true); }
        DynamicLightingChecks.Run();
        Console.WriteLine("Graphics checks passed: configuration, validation, player focus/terrain height, outdoor/night gating, vegetation/fire detection, damage/shake, decay and map isolation.");
    }
    static void ExpectInvalid(Action action)
    { try { action(); } catch(InvalidDataException) { return; } throw new InvalidOperationException("Invalid graphics settings accepted."); }
    static void Check(bool valid,string message)
    { if(!valid)throw new InvalidOperationException("Graphics check failed: "+message); }
}
