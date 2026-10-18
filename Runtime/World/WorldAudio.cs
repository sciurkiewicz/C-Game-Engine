using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

readonly record struct SpatialSound(float Volume, float Pan)
{
    public static SpatialSound Calculate(Vector3 listener, Vector3 source, Vector3 right, float radius, float volume)
    {
        var delta = source - listener; float distance = delta.Length();
        float attenuation = Math.Max(0, 1 - distance / Math.Max(.01f, radius));
        float side = distance < .01f ? 0 : Math.Clamp(Vector3.Dot(delta / distance, right), -1, 1);
        return new(Math.Clamp(volume * attenuation * attenuation, 0, 1), side);
    }
}

sealed class WorldAudio : IDisposable
{
    sealed record Voice(string Clip, Sound Sound);
    readonly Dictionary<string, Sound> clips = [];
    readonly Dictionary<string, Voice> voices = [];
    readonly HashSet<string> missing = [];
    bool initialized, ownsDevice;
    PlaySession? session;
    Vector3 previousPlayer;
    float stepDistance;
    public bool Available { get; private set; }
    public static string[] ClipIds()
    {
        string directory = Path.Combine(ProjectPaths.AssetRoot, "audio");
        return new[] { "fire", "water", "step/grass", "step/dirt", "step/sand", "step/stone" }.Concat(Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Where(p => Path.GetExtension(p).ToLowerInvariant() is ".wav" or ".ogg" or ".mp3")
                .Select(p => Path.GetRelativePath(ProjectPaths.AssetRoot, p).Replace('\\', '/')) : []).ToArray();
    }
    bool EnsureDevice()
    {
        if (initialized) return Available;
        initialized = true; ownsDevice = !IsAudioDeviceReady();
        if (ownsDevice) InitAudioDevice();
        Available = IsAudioDeviceReady(); return Available;
    }
    Sound? Clip(string id)
    {
        if (clips.TryGetValue(id, out var clip)) return clip;
        if (missing.Contains(id)) return null;
        Sound sound;
        if (id is "fire" or "water" || id.StartsWith("step/", StringComparison.Ordinal))
        {
            var wave = LoadWaveFromMemory(".wav", GenerateClip(id));
            try { sound = LoadSoundFromWave(wave); }
            finally { UnloadWave(wave); }
        }
        else
        {
            if (!id.StartsWith("audio/", StringComparison.Ordinal) || !ProjectPaths.ValidAssetId(id) || !File.Exists(Path.Combine(ProjectPaths.AssetRoot, id)))
            { missing.Add(id); return null; }
            sound = LoadSound(Path.Combine(ProjectPaths.AssetRoot, id));
        }
        if (!IsSoundValid(sound)) { missing.Add(id); return null; }
        clips.Add(id, sound); return sound;
    }
    public static byte[] GenerateClip(string id)
    {
        const int rate = 22050;
        bool step = id.StartsWith("step/", StringComparison.Ordinal);
        int samples = step ? rate / 6 : rate * 2;
        using var stream = new MemoryStream(44 + samples * 2); using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(samples * 2);
        var random = new Random(id switch { "fire" => 17, "water" => 43, "step/stone" => 29, _ => 53 });
        float low = 0;
        for (int i = 0; i < samples; i++)
        {
            float noise = (float)(random.NextDouble() * 2 - 1); low = low * .88f + noise * .12f;
            float t = i / (float)rate;
            float value = id == "fire" ? low * .3f + (random.NextDouble() > .998 ? noise * .7f : 0) :
                id == "water" ? low * .6f + MathF.Sin(t * 420 + MathF.Sin(t * 17) * 3) * .025f :
                (noise * (id == "step/stone" ? .3f : .12f) + MathF.Sin(t * (id == "step/sand" ? 380 : 630)) * .15f) * MathF.Exp(-t * 35);
            // Fade the seam of looping placeholders and the tail of footsteps.
            float envelope = Math.Clamp(i / 128f, 0, 1) * Math.Clamp((samples - i) / 128f, 0, 1);
            writer.Write((short)(Math.Clamp(value * envelope, -1, 1) * short.MaxValue));
        }
        return stream.ToArray();
    }
    public void Update(PlaySession play, float dt)
    {
        if (!play.Map.Environment.Audio || !EnsureDevice()) { Stop(); return; }
        if (!ReferenceEquals(session, play)) { Stop(); session = play; previousPlayer = play.Player; stepDistance = 0; }
        var sources = play.Map.Sounds.Where(s => s.Enabled).Select(s => (s.Id, s.Clip, Position: play.Map.GroundPosition(s.X, s.Z), s.Radius, s.Volume)).ToList();
        foreach (var fire in play.Map.Objects.Where(o => o.Asset.Contains("campfire", StringComparison.OrdinalIgnoreCase) || o.Asset.Contains("torch", StringComparison.OrdinalIgnoreCase)))
            sources.Add(("fire/" + fire.Id, "fire", play.Map.GroundPosition(fire), 12, .65f));
        Vector3? water = null; float nearest = float.MaxValue;
        int px = (int)(play.Player.X + play.Map.Width / 2f), pz = (int)(play.Player.Z + play.Map.Depth / 2f);
        for (int z = Math.Max(0, pz - 10); z < Math.Min(play.Map.Depth, pz + 11); z += 2)
        for (int x = Math.Max(0, px - 10); x < Math.Min(play.Map.Width, px + 11); x += 2)
            if (play.Map.WaterMask(z * play.Map.Width + x) != 0)
            {
                var point = new Vector3(x - play.Map.Width / 2f + .5f, play.Map.WaterLevel, z - play.Map.Depth / 2f + .5f);
                float distance = Vector3.DistanceSquared(point, play.Player);
                if (distance < nearest) { nearest = distance; water = point; }
            }
        if (water is { } surface) sources.Add(("water", "water", surface, 14, .55f));
        var active = new HashSet<string>();
        foreach (var source in sources)
        {
            var spatial = SpatialSound.Calculate(play.Player, source.Position, play.Camera.Right, source.Radius, source.Volume * play.Map.Environment.MasterVolume);
            if (spatial.Volume < .001f) continue;
            if (!voices.TryGetValue(source.Id, out var voice) || voice.Clip != source.Clip)
            {
                if (voice != null) { StopSound(voice.Sound); UnloadSoundAlias(voice.Sound); voices.Remove(source.Id); }
                if (Clip(source.Clip) is not { } clip) continue;
                voice = new(source.Clip, LoadSoundAlias(clip)); voices[source.Id] = voice;
            }
            active.Add(source.Id); SetSoundVolume(voice.Sound, spatial.Volume); SetSoundPan(voice.Sound, spatial.Pan);
            if (!IsSoundPlaying(voice.Sound)) PlaySound(voice.Sound);
        }
        foreach (var key in voices.Keys.Where(k => !active.Contains(k)).ToArray())
        { StopSound(voices[key].Sound); UnloadSoundAlias(voices[key].Sound); voices.Remove(key); }
        float moved = Vector3.Distance(previousPlayer, play.Player); previousPlayer = play.Player;
        if (moved < 2 && !play.InventoryOpen && !play.Dialogue.Active && play.PlayerAlive) stepDistance += moved;
        if (stepDistance >= .65f)
        {
            stepDistance %= .65f;
            int x = Math.Clamp((int)(play.Player.X + play.Map.Width / 2f), 0, play.Map.Width - 1), z = Math.Clamp((int)(play.Player.Z + play.Map.Depth / 2f), 0, play.Map.Depth - 1);
            string tile = play.Map.Tiles[z * play.Map.Width + x];
            if (Clip("step/" + (tile is "dirt" or "sand" or "stone" ? tile : "grass")) is { } footstep)
            { SetSoundVolume(footstep, play.Map.Environment.MasterVolume * .6f); SetSoundPan(footstep, 0); PlaySound(footstep); }
        }
    }
    public void Stop()
    {
        foreach (var voice in voices.Values) { StopSound(voice.Sound); UnloadSoundAlias(voice.Sound); }
        voices.Clear(); foreach (var sound in clips.Values) StopSound(sound);
        session = null;
    }
    public bool Preview(string id, float volume)
    {
        if (!EnsureDevice() || Clip(id) is not { } sound) return false;
        SetSoundVolume(sound, volume); SetSoundPan(sound, 0); PlaySound(sound); return true;
    }
    public void Dispose()
    {
        Stop(); foreach (var sound in clips.Values) UnloadSound(sound); clips.Clear();
        if (ownsDevice && IsAudioDeviceReady()) CloseAudioDevice();
    }
}
