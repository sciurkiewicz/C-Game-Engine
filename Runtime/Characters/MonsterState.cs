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
