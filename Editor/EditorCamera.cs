using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Free navigation belongs to the editor; PlaySession keeps its own FollowCamera.
sealed class EditorCamera
{
    readonly FollowCamera preset;
    Vector3 position;
    float yaw, pitch;
    public bool IsFree { get; private set; }
    public float Speed { get; private set; } = 12;
    public float Distance => preset.Distance;
    public ViewProjection Projection { get => preset.Projection; set { preset.Projection = value; IsFree = false; } }
    public Vector3 Right => IsFree ? new(MathF.Cos(yaw), 0, -MathF.Sin(yaw)) : preset.Right;
    public Vector3 Backward => IsFree ? new(MathF.Sin(yaw), 0, MathF.Cos(yaw)) : preset.Backward;
    public Vector3 Forward => -Backward * MathF.Cos(pitch) - Vector3.UnitY * MathF.Sin(pitch);
    // Keep ground art aligned with the last fixed view while free navigation changes the camera.
    public Camera3D TerrainTextureView => preset.View;
    public Camera3D View => IsFree ? new Camera3D
    {
        Position = position, Target = position + Forward, Up = Vector3.UnitY,
        FovY = 35, Projection = CameraProjection.Perspective
    } : preset.View;

    public EditorCamera(Vector3 target) => preset = new(target);
    public void ApplySettings(RuntimeSettings settings) { preset.ApplySettings(settings); IsFree = false; }
    public void Update(Vector3 target, float dt) { if (!IsFree) preset.Update(target, dt); }
    public void Snap(Vector3 target)
    {
        preset.Snap(target);
        if (IsFree) position = target + Vector3.UnitY * 1.5f - Forward * preset.Distance;
    }
    public void EnterFree()
    {
        if (IsFree) return;
        var view = preset.View;
        var forward = Vector3.Normalize(view.Target - view.Position);
        position = view.Position;
        yaw = MathF.Atan2(-forward.X, -forward.Z);
        pitch = MathF.Asin(-forward.Y);
        IsFree = true;
    }
    public void Look(Vector2 delta)
    {
        yaw = MathF.IEEERemainder(yaw - delta.X * .004f, MathF.Tau);
        pitch = Math.Clamp(pitch + delta.Y * .004f, -85 * MathF.PI / 180, 85 * MathF.PI / 180);
    }
    public void Orbit(Vector3 target, Vector2 delta)
    {
        float distance = Math.Max(1, Vector3.Distance(position, target));
        Look(delta); position = target - Forward * distance;
    }
    public void Move(float right, float forward, float up, float dt, bool fast)
    {
        var direction = Right * right + Forward * forward + Vector3.UnitY * up;
        if (direction.LengthSquared() > 1) direction = Vector3.Normalize(direction);
        position += direction * Speed * (fast ? 3 : 1) * dt;
    }
    public void Pan(Vector2 delta, float viewHeight) => position +=
        (-Right * delta.X + Vector3.Normalize(Vector3.Cross(Right, Forward)) * delta.Y) * preset.Distance / Math.Max(1, viewHeight);
    public void Zoom(float steps)
    {
        if (IsFree) position += Forward * steps * Speed * .25f;
        else preset.Zoom(steps);
    }
    public void ChangeSpeed(float steps) => Speed = Math.Clamp(Speed * MathF.Pow(1.2f, steps), .5f, 100);
}

sealed partial class EditorApp
{
    bool cameraLooking;
    void EndCameraLook()
    {
        if (!cameraLooking) return;
        cameraLooking = false; EnableCursor();
    }
    void EnterFreeCamera()
    {
        EndEditorGesture(); camera.EnterFree();
        status = "Free camera: RMB + mouse / WASD, Q/E: height, Shift: faster, Alt+LMB: orbit, F: focus.";
    }
    void CycleEditorView()
    {
        EndEditorGesture();
        if (camera.IsFree) ChangeProjection(ViewProjection.Perspective);
        else if (camera.Projection == ViewProjection.Perspective) ChangeProjection(ViewProjection.Isometric);
        else EnterFreeCamera();
    }
    void FocusCamera(Vector3 point)
    {
        focus = document.Map.Clamp(point); camera.Snap(focus);
    }
    // Returns true while a mouse gesture belongs exclusively to navigation.
    bool UpdateEditorCamera(float dt, bool ctrl)
    {
        bool shift = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);
        if (camera.IsFree)
        {
            bool look = IsMouseButtonDown(MouseButton.Right) && (InViewport || cameraLooking);
            if (look && !cameraLooking) { cameraLooking = true; DisableCursor(); }
            if (!look) EndCameraLook();
            bool orbit = InViewport && (IsKeyDown(KeyboardKey.LeftAlt) || IsKeyDown(KeyboardKey.RightAlt)) && IsMouseButtonDown(MouseButton.Left);
            if (look || orbit)
            {
                if (orbit) camera.Orbit(Selection is { } selected ? document.Map.GroundPosition(selected) + Vector3.UnitY : focus, GetMouseDelta());
                else camera.Look(GetMouseDelta());
            }
            if (!ctrl)
                camera.Move((IsKeyDown(KeyboardKey.D) ? 1 : 0) - (IsKeyDown(KeyboardKey.A) ? 1 : 0),
                    (IsKeyDown(KeyboardKey.W) ? 1 : 0) - (IsKeyDown(KeyboardKey.S) ? 1 : 0),
                    (IsKeyDown(KeyboardKey.E) ? 1 : 0) - (IsKeyDown(KeyboardKey.Q) ? 1 : 0), dt, shift);
            if (InViewport || look)
            {
                if (look) camera.ChangeSpeed(GetMouseWheelMove());
                else camera.Zoom(GetMouseWheelMove());
                if (IsMouseButtonDown(MouseButton.Middle)) camera.Pan(GetMouseDelta(), viewport.Height);
            }
            return look || orbit || IsMouseButtonDown(MouseButton.Middle);
        }
        if (!ctrl)
        {
            float x = (IsKeyDown(KeyboardKey.D) ? 1 : 0) - (IsKeyDown(KeyboardKey.A) ? 1 : 0);
            float z = (IsKeyDown(KeyboardKey.S) ? 1 : 0) - (IsKeyDown(KeyboardKey.W) ? 1 : 0);
            focus += (camera.Right * x + camera.Backward * z) * dt * 12 * (shift ? 3 : 1);
        }
        if (InViewport)
        {
            camera.Zoom(GetMouseWheelMove());
            if (IsMouseButtonDown(MouseButton.Middle) || IsMouseButtonDown(MouseButton.Right))
            {
                var delta = GetMouseDelta();
                focus -= (camera.Right * delta.X + camera.Backward * delta.Y * 1.4f) * camera.Distance / viewport.Height;
            }
        }
        focus = document.Map.Clamp(focus); camera.Update(focus, dt);
        return false;
    }
}
