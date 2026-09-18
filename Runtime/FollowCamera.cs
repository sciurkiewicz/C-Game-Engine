using System.Numerics;
using Raylib_cs;

// Perspective 2.5D view with a fixed heading and zoom-dependent pitch.
sealed class FollowCamera
{
    public const float MinDistance = 15, MaxDistance = 50, DefaultDistance = 30;
    public const float DefaultYaw = MathF.PI / 2;
    const float ZoomStep = 4;
    readonly Vector3 targetOffset = new(0, 1.5f, 0);
    Vector3 focus;
    public float Distance { get; private set; } = DefaultDistance;
    public float TargetDistance { get; private set; } = DefaultDistance;
    public float PitchDegrees => 30 + 30 * (Distance - MinDistance) / (MaxDistance - MinDistance);
    public Vector3 Right => new(MathF.Cos(DefaultYaw), 0, -MathF.Sin(DefaultYaw));
    public Vector3 Backward => new(MathF.Sin(DefaultYaw), 0, MathF.Cos(DefaultYaw));
    public Camera3D View
    {
        get
        {
            float pitch = PitchDegrees * MathF.PI / 180;
            var horizontal = Backward;
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
    public void Zoom(float steps) => TargetDistance = Math.Clamp(TargetDistance - steps * ZoomStep, MinDistance, MaxDistance);
    public void Reset() => TargetDistance = DefaultDistance;
    public void Snap(Vector3 target)
    {
        focus = target + targetOffset;
        Distance = TargetDistance;
    }
    public void Update(Vector3 target, float dt)
    {
        dt = Math.Max(0, dt);
        focus = Vector3.Lerp(focus, target + targetOffset, Math.Clamp(dt * 4, 0, 1));
        Distance = Math.Abs(TargetDistance - Distance) < .01f
            ? TargetDistance : Distance + (TargetDistance - Distance) * Math.Clamp(dt, 0, 1);
    }
}
