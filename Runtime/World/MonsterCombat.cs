using System.Numerics;

enum MonsterMode { Idle, Chasing, Attacking, Returning }

sealed class MonsterState(MonsterData actor, Vector3 spawn)
{
    public MonsterData Actor { get; } = actor;
    public Vector3 Spawn { get; } = spawn;
    public int Health { get; internal set; } = actor.MaxHealth;
    public MonsterMode Mode { get; internal set; }
    public float HitFlash { get; internal set; }
    internal float Cooldown, RouteCooldown;
    internal Vector3 RouteTarget;
    internal List<Vector3> Route = [];
}

sealed partial class PlaySession
{
    MapSnapshot initialMap;
    readonly Dictionary<string, MonsterState> monsters = [];
    public IEnumerable<MonsterState> Monsters => monsters.Values;
    public const int PlayerMaxHealth = 100, PlayerDamage = 10;
    public const float PlayerAttackRange = 1.8f, PlayerAttackCooldown = .4f;
    public int PlayerHealth { get; private set; } = PlayerMaxHealth;
    public bool PlayerAlive => PlayerHealth > 0;
    public float PlayerHitFlash { get; private set; }
    public float PlayerAttackFlash { get; private set; }
    float playerAttackTimer;

    void InitializeCombat()
    {
        monsters.Clear();
        foreach (var monster in Map.Monsters)
            monsters.Add(monster.Id, new(monster, Map.GroundPosition(monster)));
        PlayerHealth = PlayerMaxHealth; PlayerHitFlash = PlayerAttackFlash = playerAttackTimer = 0;
    }
    public void Restart()
    {
        Dialogue.Close(); CloseInventory();
        Map = initialMap.Restore(); Player = Map.GroundPosition(Map.Player.X, Map.Player.Z);
        InitializeCombat(); ResetWorldState(); Camera.ApplySettings(Map.Runtime); Camera.Snap(Player);
    }

    // Also used by deterministic checks, without a Raylib window or input state.
    public void UpdateCombat(float dt, bool attack = false)
    {
        if (!PlayerAlive || Dialogue.Active || InventoryOpen || !float.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, .1f);
        playerAttackTimer = Math.Max(0, playerAttackTimer - dt);
        PlayerHitFlash = Math.Max(0, PlayerHitFlash - dt);
        PlayerAttackFlash = Math.Max(0, PlayerAttackFlash - dt);
        foreach (var state in monsters.Values)
        {
            state.Cooldown = Math.Max(0, state.Cooldown - dt);
            state.RouteCooldown = Math.Max(0, state.RouteCooldown - dt);
            state.HitFlash = Math.Max(0, state.HitFlash - dt);
        }
        if (attack && playerAttackTimer <= .00001f)
        {
            playerAttackTimer = PlayerAttackCooldown; PlayerAttackFlash = .15f;
            var target = monsters.Values.Where(m => Vector3.Distance(Player, Map.GroundPosition(m.Actor)) <= PlayerAttackRange &&
                    ClearStrike(Player, Map.GroundPosition(m.Actor), m.Actor.Id))
                .OrderBy(m => Vector3.DistanceSquared(Player, Map.GroundPosition(m.Actor))).FirstOrDefault();
            if (target != null)
            {
                target.Health = Math.Max(0, target.Health - PlayerDamage); target.HitFlash = .18f;
                target.Mode = MonsterMode.Chasing;
                if (target.Health == 0) { Map.Monsters.Remove(target.Actor); monsters.Remove(target.Actor.Id); }
            }
        }
        foreach (var state in monsters.Values)
        {
            var actor = state.Actor; var position = Map.GroundPosition(actor);
            float distance = Vector3.Distance(position, Player);
            bool engaged = state.Mode is MonsterMode.Chasing or MonsterMode.Attacking;
            float leash = actor.DetectionRange * 2;
            if (state.Mode == MonsterMode.Returning || Vector3.Distance(position, state.Spawn) > leash || distance > actor.DetectionRange * (engaged ? 1.5f : 1))
            {
                if (Vector3.Distance(position, state.Spawn) < .08f) { state.Mode = MonsterMode.Idle; state.Route.Clear(); }
                else { state.Mode = MonsterMode.Returning; MoveMonster(state, state.Spawn, dt); }
                continue;
            }
            float reach = actor.AttackRange + WorldRenderer.CollisionRadius(actor) + .25f;
            if (distance <= reach && ClearStrike(position, Player, actor.Id))
            {
                state.Mode = MonsterMode.Attacking; state.Route.Clear();
                if (state.Cooldown <= .00001f)
                {
                    state.Cooldown = actor.AttackCooldown;
                    PlayerHealth = Math.Max(0, PlayerHealth - actor.Damage); PlayerHitFlash = .2f;
                    if (!PlayerAlive) { Dialogue.Close(); CloseInventory(); break; }
                }
            }
            else { state.Mode = MonsterMode.Chasing; MoveMonster(state, Player, dt); }
        }
    }
    bool ClearStrike(Vector3 from, Vector3 to, string ignoredActor)
    {
        // Check a segment at body height: a wall, high ridge or another solid actor blocks melee.
        var a = from + Vector3.UnitY * .65f; var b = to + Vector3.UnitY * .65f;
        int samples = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(a, b) / .15f));
        for (int i = 1; i < samples; i++)
        {
            var p = Vector3.Lerp(a, b, (float)i / samples);
            if (Map.HeightAt(p.X, p.Z) > p.Y) return false;
            if (Map.SceneEntities.Any(o => o.Id != ignoredActor && CollisionGeometry.Blocks(o, new(p.X, p.Z), new(p.X, p.Z), 0) &&
                p.Y >= Map.HeightAt(o.X, o.Z) && p.Y <= Map.HeightAt(o.X, o.Z) + o.Height)) return false;
        }
        return true;
    }
    void MoveMonster(MonsterState state, Vector3 target, float dt)
    {
        var actor = state.Actor; var position = Map.GroundPosition(actor);
        if (Vector3.DistanceSquared(state.RouteTarget, target) > 1)
        { state.Route.Clear(); state.RouteCooldown = 0; }
        Vector3 destination = state.Route.Count > 0 ? state.Route[0] : target;
        if (state.Route.Count > 0 && HorizontalDistance(position, destination) < .08f)
        { state.Route.RemoveAt(0); destination = state.Route.Count > 0 ? state.Route[0] : target; }
        var delta = destination - position; delta.Y = 0;
        float length = delta.Length(); if (length < .001f) return;
        var proposed = position + delta / length * Math.Min(length, actor.MoveSpeed * dt);
        var next = Map.GroundPosition(proposed.X, proposed.Z);
        if (MonsterNavigation.CanTraverse(Map, actor, position, next) &&
            HorizontalDistance(next, Player) >= WorldRenderer.CollisionRadius(actor) + .25f)
        { actor.X = next.X; actor.Z = next.Z; return; }
        if (state.RouteCooldown > 0) return;
        state.RouteTarget = target; state.RouteCooldown = .6f;
        state.Route = MonsterNavigation.FindRoute(Map, actor, position, target);
    }
    static float HorizontalDistance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
}
