using System.Numerics;
using System.Text.Json.Nodes;

static class MonsterChecks
{
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Monster check failed: " + message); }
    static MapData Fixture(float x = 4)
    {
        var map = MapData.CreateEmpty(32, 32);
        map.Monsters.Add(new() { X = x }); return map;
    }
    static void Step(PlaySession session, int count, bool attack = false)
    { for (int i = 0; i < count; i++) session.UpdateCombat(.1f, attack); }
    public static void Run()
    {
        var doc = new MapDocument(MapData.CreateEmpty(16, 12), true);
        var actor = doc.PlaceMonster(null, new(3, 0, 0)); string id = actor.Id;
        Check(doc.Dirty && doc.Map.SceneEntities.Single() == actor && !WorldRenderer.CanWalk(doc.Map, actor.Position), "monster placement participates in selection and collision");
        doc.Undo(); Check(doc.Map.Monsters.Count == 0 && !doc.Dirty, "undo placement");
        doc.Redo(); actor = doc.Map.Monsters.Single(); Check(actor.Id == id, "redo preserves actor identity");
        doc.Checkpoint(); actor.Name = "Żmij"; actor.Asset = "characters/monster.png";
        actor.MaxHealth = 75; actor.Damage = 7; actor.MoveSpeed = 3; actor.DetectionRange = 8; actor.AttackRange = .75f; actor.AttackCooldown = .6f;
        string original = doc.Map.Serialize();
        var copy = doc.DuplicateMonster(actor);
        Check(copy.Id != id && copy.Name == actor.Name && copy.Asset == actor.Asset && copy.MaxHealth == 75 && copy.Damage == 7 &&
            copy.MoveSpeed == 3 && copy.DetectionRange == 8 && copy.AttackRange == .75f && copy.AttackCooldown == .6f, "duplication preserves all monster settings with an independent identity");
        doc.Undo(); Check(doc.Map.Serialize() == original, "undo duplication");
        string file = Path.Combine(Path.GetTempPath(), "wildwood-monster-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            doc.Save(file); Check(!doc.Dirty && MapData.Load(file).Serialize() == original, "monster save/load");
        }
        finally { if (File.Exists(file)) File.Delete(file); }
        var legacy = JsonNode.Parse(original)!; legacy.AsObject().Remove("Monsters");
        Check(MapData.Parse(legacy.ToJsonString()).Monsters.Count == 0, "older maps load without monsters");
        foreach (Action<MapData> corrupt in new Action<MapData>[]
        {
            m => m.Monsters = null!, m => m.Monsters.Add(null!), m => m.Monsters[0].Name = " ", m => m.Monsters[0].Name = new('x', 81),
            m => m.Monsters[0].Asset = "../monster.png", m => m.Monsters[0].X = 100,
            m => m.Monsters[0].MaxHealth = 0, m => m.Monsters[0].Damage = -1, m => m.Monsters[0].MoveSpeed = float.NaN,
            m => m.Monsters[0].DetectionRange = float.PositiveInfinity, m => m.Monsters[0].AttackRange = 0, m => m.Monsters[0].AttackCooldown = 0,
            m => m.Monsters.Add(new() { Id = m.Monsters[0].Id }), m => m.Npcs.Add(new() { Id = m.Monsters[0].Id }),
            m => m.Chests.Add(new() { Id = m.Monsters[0].Id }), m => m.Objects.Add(new() { Id = m.Monsters[0].Id, Asset = "objects/rock.png" })
        })
        {
            var invalid = Fixture(); corrupt(invalid);
            try { invalid.Validate(); throw new InvalidOperationException("Invalid monster accepted."); }
            catch (InvalidDataException) { }
        }

        var idle = new PlaySession(Fixture(9)); Step(idle, 20);
        Check(idle.Map.Monsters.Single().X == 9 && idle.Monsters.Single().Mode == MonsterMode.Idle && idle.PlayerHealth == 100, "outside detection range monsters stay idle");
        var source = Fixture(); string before = source.Serialize();
        var chase = new PlaySession(source); Step(chase, 1);
        Check(chase.Map.Monsters.Single().X < 4 && chase.Monsters.Single().Mode == MonsterMode.Chasing, "monster detects and approaches the player without colliding with itself");
        Check(source.Serialize() == before, "runtime movement cannot alter the editor map");
        var chaser = chase.Map.Monsters.Single(); chaser.X = -14;
        Step(chase, 1); Check(chaser.X > -14 && chase.Monsters.Single().Mode == MonsterMode.Returning, "leash sends the monster back to its spawn");

        var fight = new PlaySession(Fixture(.8f)); Step(fight, 1);
        Check(fight.PlayerHealth == 90 && fight.Monsters.Single().Mode == MonsterMode.Attacking, "monster deals configured damage in melee range");
        Step(fight, 9); Check(fight.PlayerHealth == 90, "attack cooldown prevents damage every frame");
        Step(fight, 1); Check(fight.PlayerHealth == 80, "monster can attack again after its cooldown");
        var paused = new PlaySession(Fixture(.8f)); paused.ToggleInventory(); Step(paused, 20, true);
        Check(paused.PlayerHealth == 100 && paused.Monsters.Single().Health == 30, "inventory pauses both sides of combat");
        paused.CloseInventory(); var npc = new NpcData { X = -1.5f }; paused.Map.Npcs.Add(npc); paused.Dialogue.Begin(npc); Step(paused, 20, true);
        Check(paused.PlayerHealth == 100 && paused.Monsters.Single().Health == 30, "dialogue pauses both sides of combat");
        paused.Dialogue.Close(); Step(paused, 1, true);
        Check(paused.PlayerHealth == 90 && paused.Monsters.Single().Health == 20, "combat resumes after closing panels");

        var distant = new PlaySession(Fixture(5)); Step(distant, 1, true);
        Check(distant.Monsters.Single().Health == 30, "player cannot damage a monster outside melee range");
        var kill = new PlaySession(Fixture(.8f)); var killed = kill.Monsters.Single(); Step(kill, 1, true);
        Step(kill, 3, true); Check(killed.Health == 20, "player attack has a cooldown");
        Step(kill, 5, true);
        Check(killed.Health == 0 && kill.Map.Monsters.Count == 0 && !kill.Monsters.Any() && WorldRenderer.CanWalk(kill.Map, new(.8f, 0, 0)), "dead monster disappears and stops blocking movement");
        kill.Restart(); Check(kill.Map.Monsters.Count == 1 && kill.Monsters.Single().Health == 30 && kill.PlayerHealth == 100, "restart restores monsters and player health");
        var lethal = Fixture(.8f); lethal.Monsters[0].Damage = 100;
        var death = new PlaySession(lethal); Step(death, 1);
        Check(!death.PlayerAlive && death.PlayerHealth == 0 && !death.OpenChest(new()), "player death blocks interaction");
        Step(death, 10, true); Check(death.Monsters.Single().Health == 30, "dead player cannot attack");
        death.ToggleInventory(); Check(!death.InventoryOpen, "dead player cannot open inventory");
        death.Restart(); Check(death.PlayerAlive && death.Map.Serialize() == lethal.Serialize(), "restart restores the complete starting map");

        var blocked = Fixture(.8f); blocked.Objects.Add(new() { Asset = "objects/wall.png", X = .4f, Height = 2 });
        var wallFight = new PlaySession(blocked); Step(wallFight, 1, true);
        Check(wallFight.PlayerHealth == 100 && wallFight.Monsters.Single().Health == 30, "solid obstacles block attacks in both directions");
        var routeMap = Fixture(-3.5f); routeMap.Monsters[0].Z = .5f;
        routeMap.Player.X = 1.5f; routeMap.Player.Z = .5f;
        routeMap.Objects.Add(new() { Asset = "objects/rock.png", X = -1.5f, Z = .5f, Height = 5 });
        var route = new PlaySession(routeMap);
        for (int i = 0; i < 100; i++)
        {
            Step(route, 1); var m = route.Map.Monsters.Single();
            Check(WorldRenderer.CanWalk(route.Map, route.Map.GroundPosition(m), null, m.Id, WorldRenderer.CollisionRadius(m)), "navigation never crosses a solid obstacle");
        }
        Check(route.Map.Monsters[0].X > 0 && route.PlayerHealth < 100, "monster navigates around a rock and reaches melee range");
        var water = Fixture(-3); water.WaterLevel = 1;
        for (int z = 0; z < water.Depth; z++) water.Tiles[z * water.Width + water.Width / 2 - 2] = "water";
        var waterSession = new PlaySession(water); Step(waterSession, 80);
        Check(waterSession.Map.Monsters[0].X < -2 && waterSession.PlayerHealth == 100, "deep water blocks pursuit and damage across a channel");
        var slope = Fixture(); slope.Heights = Enumerable.Range(0, slope.VertexCount).Select(i => (i % (slope.Width + 1)) * 2f - 32).ToArray();
        Check(!MonsterNavigation.CanTraverse(slope, slope.Monsters[0], slope.GroundPosition(4, 0), slope.GroundPosition(3, 0)), "steep slopes block monster movement");
        var elevated = Fixture(); elevated.Heights = Enumerable.Repeat(2f, elevated.VertexCount).ToArray();
        var grounded = new PlaySession(elevated); Step(grounded, 5);
        Check(grounded.Player.Y == 2 && grounded.Map.GroundPosition(grounded.Map.Monsters[0]).Y == 2, "monster and player remain grounded on raised terrain");
        Console.WriteLine("Monster checks passed: placement, stats, validation, undo/redo, duplicate, save/load, compatibility, aggro/leash, navigation, water/slopes, cooldowns, damage/death/restart, panel pause and playtest isolation.");
    }
}
