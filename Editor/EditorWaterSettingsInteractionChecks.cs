using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunWaterSettingsInteractionChecks()
    {
        string output = Path.Combine(ProjectPaths.Root, "Editor", "obj");
        Verify(Path.GetFullPath(mapPath).StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "water appearance checks use a map in Editor/obj");
        Directory.CreateDirectory(output); SetWindowSize(1440,900);
        play=null; document.New(24,20); selectedId=null; selectedIds.Clear(); EndEditorGesture();
        for(int z=0;z<=document.Map.Depth;z++)
        for(int x=0;x<=document.Map.Width;x++)document.Map.SetVertexHeight(x,z,-2);
        document.Map.WaterLevel=0;
        document.Map.WaterCells=Enumerable.Repeat((byte)3,document.Map.Tiles.Length).ToArray();
        focus=System.Numerics.Vector3.Zero; ApplyRuntimeSettings(); TestFrame();
        ClickHeader("Water"); Click(GetScreenWidth()-Right+120,362);
        Verify(waterSettingsOpen,"Water inspector opens its appearance controls");
        var box=WaterSettingsBounds; int bx=(int)box.X,by=(int)box.Y;
        Click(bx+446,by+114);
        Verify(document.Map.WaterAppearance.R==70 && document.Map.WaterAppearance.G==202 &&
            document.Map.WaterAppearance.Clarity==6,"the tropical preset applies colors and optical depth together");
        int red=document.Map.WaterAppearance.R;
        Click(bx+317,by+203);
        Verify(document.Map.WaterAppearance.R==red+8,"the shallow color control edits the water material");
        Input(2,(int)KeyboardKey.LeftControl); Press(KeyboardKey.Z); Input(1,(int)KeyboardKey.LeftControl); TestFrame();
        Verify(document.Map.WaterAppearance.R==red,"Ctrl+Z works inside the water modal");
        Input(2,(int)KeyboardKey.LeftControl); Press(KeyboardKey.Y); Input(1,(int)KeyboardKey.LeftControl); TestFrame();
        Verify(document.Map.WaterAppearance.R==red+8,"Ctrl+Y restores the water material edit");
        float clarity=document.Map.WaterAppearance.Clarity;
        Click(bx+317,by+359);
        Verify(document.Map.WaterAppearance.Clarity>clarity,"the clarity control edits depth transparency");
        float reflection=document.Map.WaterAppearance.ReflectionStrength;
        Click(bx+563,by+359);
        Verify(document.Map.WaterAppearance.ReflectionStrength<reflection,"the reflection control edits reflection strength");
        Press(KeyboardKey.F5); Verify(play==null && waterSettingsOpen,"water settings block the Play shortcut behind the modal");
        Click(bx+466,by+563);
        var saved=document.Map.WaterAppearance;
        Verify(!document.Dirty && MapData.Load(mapPath).WaterAppearance==saved,"the modal saves authored water appearance with the map");
        SetWindowSize(1120,720); TestFrame(); TestFrame(); box=WaterSettingsBounds;
        Verify(box.X>=0 && box.Y>=0 && box.X+box.Width<=GetScreenWidth() && box.Y+box.Height<=GetScreenHeight(),
            "water settings fit inside the minimum editor window");
        WorldRenderer.Screenshot(Path.Combine(output,"water-settings.png"));
        Press(KeyboardKey.Escape); Verify(!waterSettingsOpen,"Escape closes water settings");
        SetWindowSize(1440,900); TestFrame(); TestFrame();
        ClickHeader("Load"); Verify(document.Map.WaterAppearance==saved,"loading the map restores authored water appearance");
        Console.WriteLine("Water settings UI checks passed: inspector, color presets, RGB/clarity/reflection, undo/redo, modal input isolation, save/load and minimum window.");
    }
}
