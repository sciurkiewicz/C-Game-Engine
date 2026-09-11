using System.Numerics;
using Raylib_cs;

// Surface-camera tuning from Don't Starve (not DST's cave/cutscene cameras).
// Reference: https://github.com/taichunmin/dont-starve-game-scripts/blob/master/cameras/followcamera.lua
// Independent implementation in our coordinate system: yaw 0 looks along -Z.
sealed class FollowCamera
{
    public const float MinDistance = 15, MaxDistance = 50, DefaultDistance = 30;
    public const float DefaultYaw = MathF.PI / 4;
    const float RotationStep = MathF.PI / 4, ZoomStep = 4;
    readonly Vector3 targetOffset = new(0, 1.5f, 0);
    Vector3 focus;
    float yaw = DefaultYaw;
    public float TargetYaw { get; private set; } = DefaultYaw;
    public float Distance { get; private set; } = DefaultDistance;
    public float TargetDistance { get; private set; } = DefaultDistance;
    public float PitchDegrees => 30 + 30 * (Distance - MinDistance) / (MaxDistance - MinDistance);
    // Use the final heading for movement; a held direction stays straight during a turn.
    public Vector3 Right => new(MathF.Cos(TargetYaw), 0, -MathF.Sin(TargetYaw));
    public Vector3 Backward => new(MathF.Sin(TargetYaw), 0, MathF.Cos(TargetYaw));
    public Camera3D View
    {
        get
        {
            float pitch = PitchDegrees * MathF.PI / 180;
            var horizontal = new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw));
            return new Camera3D
            {
                Target = focus,
                Position = focus + Distance * (horizontal * MathF.Cos(pitch) + Vector3.UnitY * MathF.Sin(pitch)),
                Up = Vector3.UnitY,
                FovY = 35,
                Projection = CameraProjection.Perspective
            };
        }
    }
    public FollowCamera(Vector3 target) => Snap(target);
    public void Rotate(int steps) => TargetYaw = Wrap(TargetYaw + steps * RotationStep);
    public void Zoom(float steps) => TargetDistance = Math.Clamp(TargetDistance - steps * ZoomStep, MinDistance, MaxDistance);
    public void Reset() { TargetYaw = DefaultYaw; TargetDistance = DefaultDistance; }
    public void Snap(Vector3 target)
    {
        focus = target + targetOffset;
        yaw = TargetYaw;
        Distance = TargetDistance;
    }
    public void Update(Vector3 target, float dt)
    {
        dt = Math.Max(0, dt);
        focus = Vector3.Lerp(focus, target + targetOffset, Math.Clamp(dt * 4, 0, 1));
        float angle = Wrap(TargetYaw - yaw);
        yaw = Math.Abs(angle) < .01f * MathF.PI / 180
            ? TargetYaw : Wrap(yaw + angle * Math.Clamp(dt * 20, 0, 1));
        Distance = Math.Abs(TargetDistance - Distance) < .01f
            ? TargetDistance : Distance + (TargetDistance - Distance) * Math.Clamp(dt, 0, 1);
    }
    static float Wrap(float value) => MathF.Atan2(MathF.Sin(value), MathF.Cos(value));
}
