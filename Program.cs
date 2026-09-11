using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

if (args.Contains("--camera-test"))
{
    CameraChecks.Run();
    return;
}

// A small 2.5D playground: flat, upright sprites in a real 3D world.
SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
InitWindow(1280, 800, "Wildwood | Raylib playground");
SetWindowMinSize(800, 540);
SetTargetFPS(60);
var sprites = new Dictionary<string, RenderTexture2D>();
foreach (var kind in new[] { "hero", "hero-back", "pine", "rock", "bush", "empty", "grass" })
    sprites[kind] = Art.Create(kind);

var random = new Random(42);
var objects = new List<Prop>();
for (int i = 0; i < 190; i++)
{
    var position = new Vector3(random.NextSingle() * 66 - 33, 0, random.NextSingle() * 66 - 33);
    if (position.Length() < 3.5f) continue;
    string kind = (i % 7) switch { 0 => "rock", 1 => "bush", 2 => "grass", 3 => "grass", _ => "pine" };
    float height = kind switch { "pine" => 3.8f + random.NextSingle() * 2, "rock" => 1.9f, "bush" => 2.3f, _ => 1.4f };
    objects.Add(new Prop(position, kind, height));
}
objects.Add(new Prop(new Vector3(2, 0, 1), "bush", 2.3f));
var ground = Enumerable.Range(0, 1800).Select(_ => new Vector3(random.NextSingle() * 72 - 36, .015f, random.NextSingle() * 72 - 36)).ToArray();
Vector3 player = Vector3.Zero;
float walk = 0;
var followCamera = new FollowCamera(player);
bool facingBack = false, facingLeft = false;
int berries = 0, frames = 0;
bool smoke = args.Contains("--smoke-test");
if (args.Contains("--rotated")) followCamera.Rotate(2);
if (args.Contains("--close")) followCamera.Zoom(100);
if (args.Contains("--far")) followCamera.Zoom(-100);
followCamera.Snap(player);
try
{
    while (!WindowShouldClose())
    {
        float dt = Math.Min(GetFrameTime(), .05f);
        if (IsKeyPressed(KeyboardKey.Q)) followCamera.Rotate(-1);
        if (IsKeyPressed(KeyboardKey.E)) followCamera.Rotate(1);
        float zoomSteps = GetMouseWheelMove();
        if (IsKeyPressed(KeyboardKey.Equal) || IsKeyPressedRepeat(KeyboardKey.Equal) || IsKeyPressed(KeyboardKey.KpAdd) || IsKeyPressedRepeat(KeyboardKey.KpAdd)) zoomSteps++;
        if (IsKeyPressed(KeyboardKey.Minus) || IsKeyPressedRepeat(KeyboardKey.Minus) || IsKeyPressed(KeyboardKey.KpSubtract) || IsKeyPressedRepeat(KeyboardKey.KpSubtract)) zoomSteps--;
        followCamera.Zoom(zoomSteps);
        if (IsKeyPressed(KeyboardKey.R)) followCamera.Reset();
        Vector3 input = new(
            (IsKeyDown(KeyboardKey.D) || IsKeyDown(KeyboardKey.Right) ? 1 : 0) - (IsKeyDown(KeyboardKey.A) || IsKeyDown(KeyboardKey.Left) ? 1 : 0),
            0,
            (IsKeyDown(KeyboardKey.S) || IsKeyDown(KeyboardKey.Down) ? 1 : 0) - (IsKeyDown(KeyboardKey.W) || IsKeyDown(KeyboardKey.Up) ? 1 : 0));
        Vector3 move = followCamera.Right * input.X + followCamera.Backward * input.Z;
        if (move.LengthSquared() > 0)
        {
            move = Vector3.Normalize(move) * (IsKeyDown(KeyboardKey.LeftShift) ? 6 : 3.6f) * dt;
            // Slide along solid props instead of stopping both axes.
            Vector3 next = player + new Vector3(move.X, 0, 0);
            if (CanWalk(next)) player = next;
            next = player + new Vector3(0, 0, move.Z);
            if (CanWalk(next)) player = next;
            walk += dt * 12;
            facingBack = input.Z < 0;
            if (input.X != 0) facingLeft = input.X < 0;
        }
        else walk = 0;
        player.X = Math.Clamp(player.X, -33, 33);
        player.Z = Math.Clamp(player.Z, -33, 33);
        followCamera.Update(player, dt);
        var camera = followCamera.View;
        Prop? nearby = objects.Where(p => p.Kind == "bush" && !p.Collected && Vector3.Distance(p.Position, player) < 2.2f)
            .MinBy(p => Vector3.DistanceSquared(p.Position, player));
        if (nearby != null && IsKeyPressed(KeyboardKey.Space)) { nearby.Collected = true; berries += 3; }

        BeginDrawing();
        ClearBackground(new Color(39, 46, 38, 255));
        BeginMode3D(camera);
        DrawPlane(Vector3.Zero, new Vector2(74, 74), new Color(117, 123, 78, 255));
        // Organic ground patches and a continuous winding dirt trail lie on the XZ plane.
        for (int i = 0; i < ground.Length; i++)
        {
            var p = ground[i];
            if (i % 6 == 0)
                GroundDisc(p, 1 + (i % 11) * .17f, new Color(111 + i % 9, 117 + i % 8, 73, 255));
            DrawLine3D(p + new Vector3(-.05f, .003f, .09f), p + new Vector3(.06f, .003f, -.09f), new Color(97, 104, 64, 255));
        }
        for (float z = -37; z < 37; z += .24f)
        {
            float x = MathF.Sin(z * .15f) * 4;
            GroundDisc(new Vector3(x, .024f, z), 1.05f + MathF.Sin(z * 2) * .12f, new Color(147, 137, 93, 255));
        }
        foreach (var p in objects) Shadow(p.Position, p.Kind == "pine" ? .85f : .45f);
        Shadow(player, .4f);
        // Paint far-to-near so transparent sprite edges compose correctly.
        var drawables = objects.Select(p => (Position: p.Position, Kind: p.Collected ? "empty" : p.Kind, Height: p.Height))
            .Append((Position: player + new Vector3(0, MathF.Abs(MathF.Sin(walk)) * .065f, 0), Kind: facingBack ? "hero-back" : "hero", Height: 2.3f))
            .OrderByDescending(p => Vector3.Dot(p.Position - camera.Position, Vector3.Normalize(camera.Target - camera.Position)));
        foreach (var p in drawables)
        {
            var texture = sprites[p.Kind].Texture;
            float width = p.Height * 192f / 256;
            bool hero = p.Kind.StartsWith("hero");
            var source = hero && facingLeft ? new Rectangle(192, 256, -192, -256) : new Rectangle(0, 256, 192, -256);
            float distance = Vector3.Distance(p.Position, player);
            float fade = Math.Clamp((distance - 12) / 26, 0, .48f);
            Color tint = hero ? Color.White : new Color((int)(255 - fade * 100), (int)(255 - fade * 85), (int)(255 - fade * 115), 255);
            // Render textures are vertically flipped. The origin anchors feet at ground level.
            DrawBillboardPro(camera, texture, source, p.Position,
                Vector3.UnitY, new Vector2(width, p.Height), new Vector2(width / 2, 0), 0, tint);
        }
        EndMode3D();
        DrawRectangle(0, 0, GetScreenWidth(), 5, new Color(199, 169, 100, 255));
        Panel(24, 25, 255, 88);
        DrawText("W I L D W O O D", 42, 41, 25, Art.Paper);
        DrawText("a paper wilderness", 43, 78, 16, new Color(176, 180, 147, 255));
        Panel(GetScreenWidth() - 207, 25, 183, 88);
        DrawText("DAY 01", GetScreenWidth() - 184, 41, 22, Art.Paper);
        DrawText($"BERRIES  {berries:00}", GetScreenWidth() - 184, 78, 18, new Color(215, 145, 124, 255));
        Panel(24, GetScreenHeight() - 81, GetScreenWidth() - 48, 57);
        DrawText("WASD: move   SHIFT: run   SPACE: pick berries   Q / E: rotate", 42, GetScreenHeight() - 68, 17, Art.Paper);
        DrawText("Wheel / +/-: zoom    R: reset camera    ESC: quit", 42, GetScreenHeight() - 46, 15, new Color(176, 180, 147, 255));
        if (nearby != null && !nearby.Collected)
        {
            Vector2 anchor = GetWorldToScreen(player + new Vector3(0, 2.9f, 0), camera);
            DrawRectangle((int)anchor.X - 88, (int)anchor.Y - 10, 176, 32, Art.Ink);
            DrawText("[SPACE] Pick", (int)anchor.X - 76, (int)anchor.Y, 18, Art.Paper);
        }
        EndDrawing();
        frames++;
        if (smoke && frames == 12) TakeScreenshot("wildwood-smoke.png");
        if (smoke && frames >= 15) break;
    }
}
finally
{
    foreach (var sprite in sprites.Values) UnloadRenderTexture(sprite);
    CloseWindow();
}

bool CanWalk(Vector3 point) => !objects.Any(p => (p.Kind == "pine" || p.Kind == "rock") &&
    Vector3.DistanceSquared(point, p.Position) < (p.Kind == "pine" ? .55f * .55f : .65f * .65f));
void GroundDisc(Vector3 center, float radius, Color color)
{
    for (int i = 0; i < 16; i++)
    {
        float a = i * MathF.Tau / 16, b = (i + 1) * MathF.Tau / 16;
        DrawTriangle3D(center, center + new Vector3(MathF.Cos(b), 0, MathF.Sin(b)) * radius,
            center + new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * radius, color);
    }
}
void Shadow(Vector3 point, float size)
{
    // Layered soft-looking contact shadows plus a short directional cast shadow.
    for (int i = 3; i >= 0; i--)
        GroundDisc(point + new Vector3(-i * size * .22f, .04f + (3 - i) * .001f, i * size * .12f),
            size * (1 + i * .09f), new Color(80 + i * 5, 87 + i * 5, 54 + i * 4, 255));
}
void Panel(int x, int y, int w, int h)
{
    DrawRectangle(x + 3, y + 4, w, h, new Color(24, 29, 25, 90));
    DrawRectangle(x, y, w, h, new Color(36, 43, 35, 240));
    DrawRectangleLines(x, y, w, h, new Color(147, 137, 91, 255));
}
sealed class Prop(Vector3 position, string kind, float height)
{
    public Vector3 Position = position;
    public string Kind = kind;
    public float Height = height;
    public bool Collected;
}

static class Art
{
    public static readonly Color Ink = new(39, 38, 31, 255);
    public static readonly Color Paper = new(232, 219, 177, 255);
    static Color C(int r, int g, int b) => new(r, g, b, 255);
    static void Line(float x, float y, float x2, float y2, float width, Color c) => DrawLineEx(new(x, y), new(x2, y2), width, c);
    static void Oval(int x, int y, float rx, float ry, Color c)
    {
        DrawEllipse(x, y, rx + 3, ry + 3, Ink);
        DrawEllipse(x, y, rx, ry, c);
    }
    static void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        DrawTriangle(a, b, c, color);
        Line(a.X, a.Y, b.X, b.Y, 3, Ink); Line(b.X, b.Y, c.X, c.Y, 3, Ink); Line(c.X, c.Y, a.X, a.Y, 3, Ink);
    }
    public static RenderTexture2D Create(string kind)
    {
        var target = LoadRenderTexture(192, 256);
        BeginTextureMode(target);
        ClearBackground(Color.Blank);
        switch (kind)
        {
            case "hero":
            case "hero-back":
                Line(80, 187, 73, 240, 13, Ink); Line(109, 188, 120, 240, 13, Ink);
                Oval(67, 244, 15, 7, C(62, 52, 41)); Oval(125, 244, 15, 7, C(62, 52, 41));
                Line(72, 137, 51, 189, 13, Ink); Line(119, 137, 139, 186, 13, Ink);
                Oval(50, 192, 7, 10, Paper); Oval(140, 190, 7, 10, Paper);
                Triangle(new(96, 116), new(65, 192), new(128, 192), C(137, 65, 49));
                Line(95, 142, 95, 188, 3, Ink);
                DrawCircle(102, 156, 2, Paper); DrawCircle(102, 174, 2, Paper);
                Oval(95, 88, 36, 43, Paper);
                Triangle(new(51, 67), new(70, 79), new(77, 23), Ink);
                Triangle(new(71, 62), new(98, 58), new(104, 12), Ink);
                Triangle(new(96, 56), new(136, 70), new(124, 22), Ink);
                if (kind == "hero-back")
                {
                    Oval(95, 88, 34, 40, C(53, 46, 36));
                    Line(77, 73, 83, 117, 2, C(88, 73, 49));
                    Line(98, 66, 101, 119, 2, C(88, 73, 49));
                    Oval(96, 161, 22, 28, C(117, 98, 59));
                    Line(77, 153, 114, 153, 3, Ink);
                }
                else
                {
                Oval(81, 90, 7, 11, C(244, 234, 203)); Oval(111, 90, 7, 11, C(244, 234, 203));
                DrawEllipse(83, 93, 3, 6, Ink); DrawEllipse(109, 93, 3, 6, Ink);
                Line(93, 97, 89, 111, 2, Ink); Line(86, 119, 106, 117, 2, Ink);
                }
                break;
            case "pine":
                Triangle(new(94, 72), new(79, 252), new(111, 252), C(100, 75, 47));
                Triangle(new(96, 68), new(18, 202), new(175, 189), C(61, 77, 56));
                Triangle(new(96, 37), new(30, 155), new(162, 147), C(73, 90, 63));
                Triangle(new(94, 6), new(48, 106), new(145, 100), C(89, 105, 70));
                for (int i = 0; i < 7; i++)
                {
                    Line(91, 53 + i * 23, 62 - i * 4, 87 + i * 21, 2, C(46, 62, 46));
                    Line(103, 58 + i * 22, 124 + i * 4, 86 + i * 20, 2, C(46, 62, 46));
                }
                Line(94, 210, 91, 248, 3, Ink);
                break;
            case "rock":
                Oval(98, 189, 76, 58, C(117, 121, 110));
                Triangle(new(65, 128), new(26, 199), new(115, 177), C(153, 153, 134));
                Triangle(new(115, 177), new(108, 245), new(169, 211), C(90, 99, 94));
                Line(65, 128, 132, 145, 3, Ink); Line(45, 220, 84, 231, 2, Ink);
                break;
            case "bush":
            case "empty":
                Line(96, 190, 96, 254, 9, Ink);
                Oval(59, 191, 39, 40, C(74, 89, 50)); Oval(129, 190, 39, 43, C(82, 98, 56));
                Oval(95, 161, 42, 48, C(100, 111, 61));
                for (int i = 0; i < 8; i++)
                {
                    int x = 48 + (i * 37 % 101), y = 150 + i * 19 % 68;
                    Line(x, y, x + 9, y - 7, 2, C(52, 70, 43));
                    if (kind == "bush") { Oval(x, y + 8, 6, 7, C(165, 65, 67)); DrawCircle(x - 2, y + 5, 2, Paper); }
                }
                break;
            case "grass":
                for (int i = 0; i < 9; i++)
                {
                    float x = 30 + i * 16, y = 105 + i * 37 % 85;
                    Triangle(new(x, y), new(82 + i * 3, 253), new(x + 11, y + 48), i % 2 == 0 ? C(162, 153, 82) : C(129, 137, 71));
                }
                break;
        }
        EndTextureMode();
        SetTextureFilter(target.Texture, TextureFilter.Bilinear);
        return target;
    }
}
