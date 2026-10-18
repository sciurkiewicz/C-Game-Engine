using System.Numerics;
using Raylib_cs;

// The original perspective view and a true orthographic isometric view share follow/zoom controls.
sealed class FollowCamera
{
    public const float MinDistance = 15, MaxDistance = 50, DefaultDistance = 30;
    public const float DefaultYaw = MathF.PI / 2;
    public const float IsometricYaw = MathF.PI / 4;
    public static readonly float IsometricPitch = MathF.Atan(1 / MathF.Sqrt(2)) * 180 / MathF.PI;
    const float ZoomStep = 4;
    readonly Vector3 targetOffset = new(0, 1.5f, 0);
    Vector3 focus;
    float initialDistance = DefaultDistance;
    public ViewProjection Projection { get; set; }
    public float Distance { get; private set; } = DefaultDistance;
    public float TargetDistance { get; private set; } = DefaultDistance;
    public float PitchDegrees => Projection == ViewProjection.Isometric ? IsometricPitch :
        30 + 30 * (Distance - MinDistance) / (MaxDistance - MinDistance);
    float Yaw => Projection == ViewProjection.Isometric ? IsometricYaw : DefaultYaw;
    public Vector3 Right => new(MathF.Cos(Yaw), 0, -MathF.Sin(Yaw));
    public Vector3 Backward => new(MathF.Sin(Yaw), 0, MathF.Cos(Yaw));
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
                // Orthographic FovY is a world-space height. Match the perspective scale at the target.
                FovY = Projection == ViewProjection.Isometric ? 2 * Distance * MathF.Tan(35 * MathF.PI / 360) : 35,
                Projection = Projection == ViewProjection.Isometric ? CameraProjection.Orthographic : CameraProjection.Perspective
            };
        }
    }
    public FollowCamera(Vector3 target, RuntimeSettings? settings = null)
    {
        if (settings != null) ApplySettings(settings);
        Snap(target);
    }
    public void ApplySettings(RuntimeSettings settings)
    {
        settings.Validate();
        Projection = settings.Projection;
        initialDistance = TargetDistance = settings.InitialDistance;
    }
    public void Zoom(float steps) => TargetDistance = Math.Clamp(TargetDistance - steps * ZoomStep, MinDistance, MaxDistance);
    public void Reset() => TargetDistance = initialDistance;
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
