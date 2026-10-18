using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

sealed class WorldProgress
{
    public MapData Original { get; set; } = new();
    public MapData State { get; set; } = new();
    public float PlayerX { get; set; }
    public float PlayerZ { get; set; }
    public int Health { get; set; }
    public float TimeOfDay { get; set; }
    public Dictionary<string, int> MonsterHealth { get; set; } = [];
    public Dictionary<string, GroundPoint2> MonsterSpawns { get; set; } = [];
    public HashSet<string> FiredTriggers { get; set; } = [];
    public void Validate()
    {
        if (Original == null || State == null) throw new InvalidDataException("Missing saved world.");
        Original.Validate(); State.Validate();
        if (!State.Contains(PlayerX, PlayerZ) || Health is < 0 or > PlaySession.PlayerMaxHealth ||
            !MapData.FiniteRange(TimeOfDay, 0, 24) || MonsterHealth == null || MonsterSpawns == null || FiredTriggers == null ||
            MonsterHealth.Count != State.Monsters.Count || State.Monsters.Any(m => !MonsterHealth.TryGetValue(m.Id, out int hp) || hp < 1 || hp > m.MaxHealth) ||
            FiredTriggers.Count > State.Triggers.Count || FiredTriggers.Any(id => !State.Triggers.Any(t => t.Id == id)))
            throw new InvalidDataException("Invalid saved player, monsters or trigger progress.");
        // Earlier progress files can infer authored spawns; new files also preserve trigger-spawned actors.
        if (MonsterSpawns.Count == 0)
            foreach (var monster in State.Monsters)
            {
                var original = Original.Monsters.FirstOrDefault(m => m.Id == monster.Id) ?? monster;
                MonsterSpawns.Add(monster.Id, new(original.X, original.Z));
            }
        if (MonsterSpawns.Count != State.Monsters.Count || State.Monsters.Any(m => !MonsterSpawns.TryGetValue(m.Id, out var point) ||
            point == null || !State.Contains(point.X, point.Z))) throw new InvalidDataException("Invalid saved monster spawn.");
    }
}

sealed class GameProgress
{
    public int Version { get; set; } = 1;
    public string EntryMap { get; set; } = "";
    public string CurrentMap { get; set; } = "";
    public Dictionary<string, WorldProgress> Worlds { get; set; } = [];
    public void Validate(string entryMap)
    {
        if (Version != 1 || EntryMap != entryMap || string.IsNullOrWhiteSpace(CurrentMap) || Worlds == null || Worlds.Count is < 1 or > 32 ||
            !Worlds.ContainsKey(CurrentMap) || Worlds.Any(w => string.IsNullOrWhiteSpace(w.Key) || w.Key.Length > 2048 || w.Value == null))
            throw new InvalidDataException("This progress file belongs to another map or is invalid.");
        foreach (var world in Worlds.Values) world.Validate();
    }
    public static string SavePath(string mapPath, bool editor = false)
    {
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(mapPath))))[..12].ToLowerInvariant();
        string name = Path.GetFileNameWithoutExtension(mapPath) + "-" + hash + ".json";
        return editor ? Path.Combine(ProjectPaths.Root, "Editor", "obj", "progress", name) : Path.Combine(ProjectPaths.Root, "Runtime", "Saves", name);
    }
}

sealed partial class PlaySession
{
    readonly string entryMapPath;
    public string CurrentMapPath { get; private set; }
    public string ProgressPath { get; }
    Dictionary<string, WorldProgress> visitedWorlds = [];
    public HashSet<string> FiredTriggers { get; private set; } = [];
    readonly HashSet<string> insideTriggers = [];
    public float TimeOfDay { get; private set; }
    public string Notice { get; private set; } = "";
    float noticeSeconds;
    public void ShowNotice(string message) { Notice = message; noticeSeconds = 5; }
    void ResetWorldState()
    {
        TimeOfDay = Map.Environment.TimeOfDay;
        FiredTriggers.Clear(); insideTriggers.Clear(); Notice = ""; noticeSeconds = 0;
    }
    public bool TryMovePlayer(Vector3 destination)
    {
        destination = Map.Clamp(destination);
        if (!WorldRenderer.CanWalk(Map, destination, Player)) return false;
        Player = destination; return true;
    }
    public void AdvanceWorld(float dt)
    {
        if (!float.IsFinite(dt) || dt <= 0 || !PlayerAlive || Dialogue.Active || InventoryOpen) return;
        if (Map.Environment.DayNightCycle) TimeOfDay = (TimeOfDay + dt * 24 / Map.Environment.DayLengthSeconds) % 24;
        var entered = Map.Triggers.Where(t => t.Enabled && t.Contains(Player)).ToArray();
        var previous = insideTriggers.ToHashSet(); insideTriggers.Clear();
        foreach (var trigger in entered) insideTriggers.Add(trigger.Id);
        foreach (var trigger in entered)
        {
            if (previous.Contains(trigger.Id) || trigger.Once && FiredTriggers.Contains(trigger.Id)) continue;
            FiredTriggers.Add(trigger.Id);
            var oldMap = Map;
            if (!ExecuteTrigger(trigger)) FiredTriggers.Remove(trigger.Id);
            if (!ReferenceEquals(oldMap, Map) || Dialogue.Active) break;
        }
    }
    bool ExecuteTrigger(TriggerData trigger)
    {
        switch (trigger.Action)
        {
            case TriggerAction.Message: ShowNotice(trigger.Text); return true;
            case TriggerAction.Dialogue:
                if (Map.Npcs.FirstOrDefault(n => n.Id == trigger.TargetId) is { } npc) { Dialogue.Begin(npc); return true; }
                break;
            case TriggerAction.ToggleDoor:
                if (Map.SceneEntities.FirstOrDefault(o => o.Id == trigger.TargetId && o.IsDoor) is { } door)
                { door.DoorOpen = !door.DoorOpen; ShowNotice(door.DoorOpen ? "Door opened." : "Door closed."); return true; }
                break;
            case TriggerAction.SpawnMonster:
                if (Map.Monsters.Count >= 1000) { ShowNotice("Monster limit reached."); return false; }
                var monster = JsonSerializer.Deserialize<MonsterData>(JsonSerializer.Serialize(trigger.MonsterTemplate))!;
                monster.Id = Guid.NewGuid().ToString("N"); monster.X = trigger.DestinationX; monster.Z = trigger.DestinationZ;
                Map.Monsters.Add(monster); monsters.Add(monster.Id, new(monster, Map.GroundPosition(monster))); return true;
            case TriggerAction.Teleport:
                Player = Map.GroundPosition(trigger.DestinationX, trigger.DestinationZ); Camera.Snap(Player); return true;
            case TriggerAction.ChangeMap:
                return ChangeMap(trigger.MapFile, trigger.DestinationX, trigger.DestinationZ);
        }
        ShowNotice("Trigger target is missing: " + trigger.Name); return false;
    }
    WorldProgress CaptureProgress() => new()
    {
        Original = initialMap.Restore(), State = Map, PlayerX = Player.X, PlayerZ = Player.Z, Health = PlayerHealth,
        TimeOfDay = TimeOfDay, FiredTriggers = FiredTriggers.ToHashSet(), MonsterHealth = monsters.ToDictionary(m => m.Key, m => m.Value.Health),
        MonsterSpawns = monsters.ToDictionary(m => m.Key, m => new GroundPoint2(m.Value.Spawn.X, m.Value.Spawn.Z))
    };
    void RestoreProgress(WorldProgress world)
    {
        initialMap = MapSnapshot.Capture(world.Original); Map = world.State;
        Player = Map.GroundPosition(world.PlayerX, world.PlayerZ);
        Dialogue.Close(); CloseInventory(); InitializeCombat();
        foreach (var actor in Map.Monsters)
        {
            var spawn = world.MonsterSpawns[actor.Id];
            monsters[actor.Id] = new(actor, Map.GroundPosition(spawn.X, spawn.Z)) { Health = world.MonsterHealth[actor.Id] };
        }
        PlayerHealth = world.Health; TimeOfDay = world.TimeOfDay;
        FiredTriggers = world.FiredTriggers.ToHashSet(); insideTriggers.Clear();
        // Loading while standing in a repeatable trigger must not execute it a second time.
        foreach (var trigger in Map.Triggers.Where(t => t.Enabled && t.Contains(Player))) insideTriggers.Add(trigger.Id);
        Camera.ApplySettings(Map.Runtime); Camera.Snap(Player);
    }
    public bool ChangeMap(string mapFile, float x, float z)
    {
        try
        {
            if (!ProjectPaths.ValidAssetId(mapFile) || !mapFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose a JSON file inside Runtime/Maps.");
            string path = Path.GetFullPath(Path.Combine(ProjectPaths.Root, "Runtime", "Maps", mapFile));
            if (path == CurrentMapPath)
            {
                if (!Map.Contains(x, z)) throw new InvalidDataException("Map entrance is outside the destination map.");
                Player = Map.GroundPosition(x, z); Camera.Snap(Player); return true;
            }
            WorldProgress? destination = visitedWorlds.GetValueOrDefault(path);
            var target = destination?.State ?? MapData.Load(path);
            if (!target.Contains(x, z)) throw new InvalidDataException("Map entrance is outside the destination map.");
            if (destination == null && visitedWorlds.Keys.Append(CurrentMapPath).Distinct().Count() >= 32) throw new InvalidDataException("Visited map limit reached.");
            var inventory = ScenePrefab.Clone(Inventory); int health = PlayerHealth;
            // Check carried items before changing either world; a malformed destination cannot partially load.
            var previousInventory = target.Player.Inventory;
            try { target.Player.Inventory = inventory; target.Validate(); }
            finally { target.Player.Inventory = previousInventory; }
            visitedWorlds[CurrentMapPath] = CaptureProgress();
            if (destination != null) RestoreProgress(destination);
            else
            {
                Map = target; Map.Player.Inventory = inventory;
                initialMap = MapSnapshot.Capture(Map); InitializeCombat(); ResetWorldState();
            }
            CurrentMapPath = path; Map.Player.Inventory = inventory; PlayerHealth = health;
            Player = Map.GroundPosition(x, z); insideTriggers.Clear(); Dialogue.Close(); CloseInventory();
            Camera.ApplySettings(Map.Runtime); Camera.Snap(Player); ShowNotice("Entered " + Path.GetFileNameWithoutExtension(path)); return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { ShowNotice("Cannot change map: " + ex.Message); return false; }
    }
    public void SaveProgress(string path)
    {
        var worlds = new Dictionary<string, WorldProgress>(visitedWorlds) { [CurrentMapPath] = CaptureProgress() };
        var progress = new GameProgress { EntryMap = entryMapPath, CurrentMap = CurrentMapPath, Worlds = worlds };
        progress.Validate(entryMapPath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        try
        {
            using (var stream = File.Create(temporary)) JsonSerializer.Serialize(stream, progress);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        ShowNotice("Progress saved. F9: load.");
    }
    public void LoadProgress(string path)
    {
        using var stream = File.OpenRead(path);
        var progress = JsonSerializer.Deserialize<GameProgress>(stream) ?? throw new InvalidDataException("Empty progress file.");
        progress.Validate(entryMapPath);
        RestoreProgress(progress.Worlds[progress.CurrentMap]);
        CurrentMapPath = progress.CurrentMap; visitedWorlds = progress.Worlds;
        ShowNotice("Progress loaded.");
    }
    void UpdateProgressInput(float dt)
    {
        noticeSeconds = Math.Max(0, noticeSeconds - dt); if (noticeSeconds == 0) Notice = "";
        try
        {
            if (Raylib_cs.Raylib.IsKeyPressed(Raylib_cs.KeyboardKey.F6)) SaveProgress(ProgressPath);
            if (Raylib_cs.Raylib.IsKeyPressed(Raylib_cs.KeyboardKey.F9)) LoadProgress(ProgressPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { ShowNotice("Progress: " + ex.Message); }
    }
}
