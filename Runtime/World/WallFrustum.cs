using System.Numerics;
using Raylib_cs;

sealed class WallFrustum
{
    readonly Vector4[] planes = new Vector4[6];
    public void Configure(Camera3D camera, int width, int height)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, camera.Up)); var up = Vector3.Cross(right, forward);
        float aspect = Math.Max(1, width) / (float)Math.Max(1, height);
        void Plane(int i, Vector3 normal, float offset = 0)
        { normal = Vector3.Normalize(normal); planes[i] = new(normal, -Vector3.Dot(normal, camera.Position) + offset); }
        if (camera.Projection == CameraProjection.Orthographic)
        {
            Plane(0, right, camera.FovY * aspect / 2); Plane(1, -right, camera.FovY * aspect / 2);
            Plane(2, up, camera.FovY / 2); Plane(3, -up, camera.FovY / 2);
        }
        else
        {
            float tangent = MathF.Tan(camera.FovY * MathF.PI / 360);
            Plane(0, forward * (tangent * aspect) + right); Plane(1, forward * (tangent * aspect) - right);
            Plane(2, forward * tangent + up); Plane(3, forward * tangent - up);
        }
        Plane(4, forward, -.01f); Plane(5, -forward, 1000);
    }
    public bool Intersects(Vector3 min, Vector3 max)
    {
        foreach (var p in planes)
        {
            var corner = new Vector3(p.X >= 0 ? max.X : min.X, p.Y >= 0 ? max.Y : min.Y, p.Z >= 0 ? max.Z : min.Z);
            if (p.X * corner.X + p.Y * corner.Y + p.Z * corner.Z + p.W < -.02f) return false;
        }
        return true;
    }
}
