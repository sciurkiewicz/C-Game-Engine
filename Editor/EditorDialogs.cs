using System.Numerics;
using System.Text;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    bool creatingMap;
    int newWidth = 48, newDepth = 48, dimensionFocus;
    bool replaceDimension;
    string widthInput = "48", depthInput = "48";
    string? dialogueNpcId, dialogueNodeId, replyLinkId;
    Vector2 treePan = new(20, 20);
    TextEdit? textEdit;
    sealed class TextEdit(string title, string value, int limit, bool multiline, Action<string> apply)
    {
        public string Title = title, Value = value;
        public int Limit = limit, Scroll, Caret = value.Length;
        public bool Multiline = multiline, Replace = true, ScrollToEnd;
        public Action<string> Apply = apply;
    }
    NpcData? DialogueNpc => document.Map.Npcs.FirstOrDefault(n => n.Id == dialogueNpcId);
    DialogueNode? DialogueNode => DialogueNpc?.Dialogue.Nodes.FirstOrDefault(n => n.Id == dialogueNodeId);
    static Color NodeColor(DialogueKind kind) => kind switch
    {
        DialogueKind.Start => Accent,
        DialogueKind.Npc => new(244, 192, 96, 255),
        _ => new(80, 183, 255, 255)
    };
    void OpenNewMap()
    {
        creatingMap = true;
        widthInput = document.Map.Width.ToString(); depthInput = document.Map.Depth.ToString();
        dimensionFocus = 0; replaceDimension = true;
        gesture = dragging = dragArmed = false;
    }
    bool ValidDimensions => int.TryParse(widthInput, out int width) && width >= MapData.MinSize && width <= MapData.MaxSize &&
        int.TryParse(depthInput, out int depth) && depth >= MapData.MinSize && depth <= MapData.MaxSize;
    void ConfirmNewMap()
    {
        if (!ValidDimensions) return;
        newWidth = int.Parse(widthInput); newDepth = int.Parse(depthInput); creatingMap = false;
        if (document.Dirty) pendingAction = "new";
        else Complete("new");
    }
    void UpdateNewMap()
    {
        if (IsKeyPressed(KeyboardKey.Escape)) { creatingMap = false; return; }
        if (IsKeyPressed(KeyboardKey.Tab)) { dimensionFocus = 1 - dimensionFocus; replaceDimension = true; }
        string value = dimensionFocus == 0 ? widthInput : depthInput;
        if (IsKeyPressed(KeyboardKey.Backspace) || IsKeyPressedRepeat(KeyboardKey.Backspace))
        { value = replaceDimension || value.Length == 0 ? "" : value[..^1]; replaceDimension = false; }
        for (int code = GetCharPressed(); code != 0; code = GetCharPressed())
            if (code >= '0' && code <= '9')
            {
                if (replaceDimension) { value = ""; replaceDimension = false; }
                if (value.Length < MapData.MaxSize.ToString().Length) value += (char)code;
            }
        if (dimensionFocus == 0) widthInput = value; else depthInput = value;
        if (IsKeyPressed(KeyboardKey.Enter)) ConfirmNewMap();
    }
    void DrawNewMap()
    {
        bool enabled = pendingAction == null;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new Color(0, 0, 0, 180));
        int x = GetScreenWidth() / 2 - 270, y = GetScreenHeight() / 2 - 170;
        DrawRectangle(x, y, 540, 340, Panel); DrawRectangleLines(x, y, 540, 340, Border);
        Label("New map", x + 24, y + 24, 24, Accent);
        Label($"Size in tiles / metres: {MapData.MinSize}-{MapData.MaxSize} on each side", x + 24, y + 64, 15, Muted);
        for (int i = 0; i < 2; i++)
        {
            int dx = x + 24 + i * 260;
            Label(i == 0 ? "Width (X)" : "Depth (Z)", dx, y + 108, 15, Text);
            string value = i == 0 ? widthInput : depthInput;
            if (Button(value + (dimensionFocus == i ? " |" : ""), dx + 46, y + 136, 142, dimensionFocus == i, enabled, true))
            { dimensionFocus = i; replaceDimension = true; }
            if (Button("-", dx, y + 136, 36, false, enabled, true)) AdjustDimension(i, -1);
            if (Button("+", dx + 198, y + 136, 36, false, enabled, true)) AdjustDimension(i, 1);
        }
        Label("Presets", x + 24, y + 190, 13, Muted);
        int px = x + 24;
        foreach (int size in new[] { 32, 128, 512, 1024, 2048 })
        {
            if (Button(size.ToString(), px, y + 213, 92, false, enabled, true))
            { widthInput = depthInput = size.ToString(); replaceDimension = true; }
            px += 100;
        }
        if (!ValidDimensions) Label($"Enter two sizes between {MapData.MinSize} and {MapData.MaxSize}.", x + 24, y + 254, 14, new Color(255, 177, 100, 255));
        if (Button("Create", x + 24, y + 286, 230, true, enabled && ValidDimensions, true)) ConfirmNewMap();
        if (Button("Cancel", x + 284, y + 286, 230, false, enabled, true)) creatingMap = false;
    }
    void AdjustDimension(int which, int delta)
    {
        string value = which == 0 ? widthInput : depthInput;
        int next = Math.Clamp((int.TryParse(value, out int size) ? size : 48) + delta, MapData.MinSize, MapData.MaxSize);
        if (which == 0) widthInput = next.ToString(); else depthInput = next.ToString();
        dimensionFocus = which; replaceDimension = true;
    }
    void OpenDialogue(NpcData npc)
    {
        dialogueNpcId = npc.Id; dialogueNodeId = npc.Dialogue.StartId;
        replyLinkId = null;
        treePan = new(20, 20); gesture = dragging = dragArmed = false;
    }
    Rectangle TreeArea => new(32, 112, GetScreenWidth() - 380, GetScreenHeight() - 174);
    void UpdateDialogueEditor()
    {
        if (DialogueNpc == null) { dialogueNpcId = replyLinkId = null; return; }
        if (IsKeyPressed(KeyboardKey.Escape))
        {
            if (replyLinkId != null) replyLinkId = null;
            else dialogueNpcId = null;
            return;
        }
        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        if (ctrl && IsKeyPressed(KeyboardKey.S)) Save();
        if (ctrl && IsKeyPressed(KeyboardKey.Z)) { replyLinkId = null; Undo(); }
        if (ctrl && IsKeyPressed(KeyboardKey.Y)) { replyLinkId = null; Redo(); }
        if (DialogueNpc == null) { dialogueNpcId = null; return; }
        if (DialogueNode == null) dialogueNodeId = DialogueNpc.Dialogue.StartId;
        if (CheckCollisionPointRec(GetMousePosition(), TreeArea))
        {
            float wheel = GetMouseWheelMove();
            if (IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift)) treePan.X += wheel * 45;
            else treePan.Y += wheel * 45;
            if (IsMouseButtonDown(MouseButton.Middle) || IsMouseButtonDown(MouseButton.Right)) treePan += GetMouseDelta();
        }
        if (replyLinkId == null && IsKeyPressed(KeyboardKey.Delete)) DeleteDialogueBranch();
    }
    Dictionary<string, Rectangle> LayoutTree(DialogueTree tree) => LayoutTree(tree, out _);
    Dictionary<string, Rectangle> LayoutTree(DialogueTree tree, out HashSet<(string From, string To)> branches)
    {
        var layout = DialogueGraphLayout.Build(tree, new Vector2(TreeArea.X, TreeArea.Y) + treePan);
        branches = layout.Branches; return layout.Rects;
    }
    void DrawDialogueEditor()
    {
        var npc = DialogueNpc;
        if (npc == null) { dialogueNpcId = null; return; }
        bool enabled = textEdit == null && pendingAction == null;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new Color(0, 0, 0, 210));
        DrawRectangle(16, 16, GetScreenWidth() - 32, GetScreenHeight() - 32, Panel);
        Label("DIALOGUE TREE / " + npc.Name, 32, 34, 22, Accent, GetScreenWidth() - 420);
        Label(replyLinkId != null ? "Click an NPC line to redirect this reply. Escape cancels." :
            "NPC can continue with NPC Reply or offer Player Reply options. Dashed arrows redirect.", 32, 74, 14, replyLinkId != null ? Accent : Muted);
        if (Button("Save", GetScreenWidth() - 320, 32, 80, false, enabled, true)) Save();
        if (Button("Close", GetScreenWidth() - 220, 32, 80, false, enabled, true)) dialogueNpcId = replyLinkId = null;
        if (Button("Home", GetScreenWidth() - 120, 32, 80, false, enabled, true)) treePan = new(20, 20);
        DrawRectangleRec(TreeArea, Background);
        var rects = LayoutTree(npc.Dialogue, out var branches);
        BeginScissorMode((int)TreeArea.X, (int)TreeArea.Y, (int)TreeArea.Width, (int)TreeArea.Height);
        foreach (var node in npc.Dialogue.Nodes)
        {
            var from = rects[node.Id];
            foreach (string child in node.Next)
            {
                var to = rects[child];
                if (branches.Contains((node.Id, child)))
                    DrawLineBezier(new(from.X + from.Width, from.Y + from.Height / 2), new(to.X, to.Y + to.Height / 2), 2, NodeColor(node.Kind));
                else DrawDialogueRedirect(from, to, node.Id == dialogueNodeId ? Accent : new Color(178, 142, 233, 255));
            }
        }
        string? clicked = null;
        foreach (var node in npc.Dialogue.Nodes)
        {
            var rect = rects[node.Id];
            bool selected = node.Id == dialogueNodeId;
            DrawRectangleRec(rect, selected ? new Color(48, 63, 77, 255) : Card);
            DrawRectangleLinesEx(rect, selected || (replyLinkId != null && node.Kind == DialogueKind.Npc) ? 2 : 1,
                replyLinkId != null && node.Kind == DialogueKind.Npc ? Accent : NodeColor(node.Kind));
            bool redirect = node.Next.Any(next => !branches.Contains((node.Id, next)));
            Label(node.Kind == DialogueKind.Start ? "START" : node.Kind == DialogueKind.Npc ? "NPC REPLY" : redirect ? "PLAYER REPLY / LINK" : "PLAYER REPLY",
                (int)rect.X + 12, (int)rect.Y + 10, 13, NodeColor(node.Kind), 172);
            var lines = UiText.Wrap(node.Kind == DialogueKind.Start ? "Conversation begins" : node.Text, 13, 172);
            for (int i = 0; i < Math.Min(2, lines.Count); i++) Label(lines[i], (int)rect.X + 12, (int)rect.Y + 33 + i * 18, 13, Text, 172);
            if (enabled && CheckCollisionPointRec(GetMousePosition(), TreeArea) && CheckCollisionPointRec(GetMousePosition(), rect) && IsMouseButtonPressed(MouseButton.Left)) clicked = node.Id;
        }
        EndScissorMode();
        if (clicked != null)
        {
            if (replyLinkId == null) dialogueNodeId = clicked;
            else if (npc.Dialogue.Node(clicked).Kind == DialogueKind.Npc)
            {
                document.Checkpoint(); npc.Dialogue.SetReplyTarget(replyLinkId, clicked);
                dialogueNodeId = replyLinkId; replyLinkId = null;
            }
        }
        Label("RMB / MMB drag: pan   |   Wheel: vertical   |   Shift + wheel: horizontal   |   Ctrl+Z / Y: undo / redo", 32, GetScreenHeight() - 46, 13, Muted);
        int x = GetScreenWidth() - 324;
        var selectedNode = DialogueNode;
        if (selectedNode == null) return;
        Label("NODE PROPERTIES", x, 114, 16, Text);
        Label(selectedNode.Kind == DialogueKind.Start ? "Start" : selectedNode.Kind == DialogueKind.Npc ? "NPC Reply" : "Player Reply", x, 150, 20, NodeColor(selectedNode.Kind));
        int by = Math.Max(446, GetScreenHeight() - 244);
        if (selectedNode.Kind != DialogueKind.Start)
        {
            if (Button("Edit text", x, 186, 282, true, enabled, true))
                BeginTextEdit("Dialogue text", selectedNode.Text, DialogueTree.MaxTextLength, true, value =>
                { document.Checkpoint(); selectedNode.Text = value; });
            var preview = UiText.Wrap(selectedNode.Text, 15, 282);
            int maxLines = selectedNode.Kind == DialogueKind.Player ? Math.Max(0, (by - 190 - 234) / 19) : 10;
            for (int i = 0; i < Math.Min(Math.Min(10, maxLines), preview.Count); i++) Label(preview[i], x, 234 + i * 19, 15, Text);
        }
        else Label("Start enters the first NPC line.", x, 192, 14, Muted);
        if (selectedNode.Kind == DialogueKind.Player)
        {
            Label("REPLY DESTINATION", x, by - 164, 14, Accent);
            Label(selectedNode.Next.Count == 0 ? "End conversation" : "NPC: " + npc.Dialogue.Node(selectedNode.Next.Single()).Text.Replace('\n', ' ').Replace('\r', ' '),
                x, by - 140, 14, Text, 282);
            Label("Changing it replaces the next branch.", x, by - 118, 12, Muted);
            if (Button(replyLinkId == null ? "Redirect to NPC" : "Cancel redirect", x, by - 92, 282, replyLinkId != null, enabled, true))
                replyLinkId = replyLinkId == null ? selectedNode.Id : null;
            if (Button("End after this reply", x, by - 50, 282, false, enabled && replyLinkId == null && selectedNode.Next.Count > 0, true))
            { document.Checkpoint(); npc.Dialogue.SetReplyTarget(selectedNode.Id, null); }
        }
        bool canEdit = enabled && replyLinkId == null;
        if (selectedNode.Kind == DialogueKind.Npc && selectedNode.Next.Count > 0)
            Label(npc.Dialogue.Node(selectedNode.Next[0]).Kind == DialogueKind.Npc ? "Add Player Reply at the last NPC line." : "NPC Reply goes before these choices.",
                x, by - 24, 12, Muted, 282);
        if (Button("+ Player Reply", x, by, 135, true, canEdit && npc.Dialogue.CanAddChild(selectedNode.Id, DialogueKind.Player), true))
            AddDialogueReply(DialogueKind.Player);
        if (Button("+ NPC Reply", x + 147, by, 135, true, canEdit && npc.Dialogue.CanAddChild(selectedNode.Id, DialogueKind.Npc), true))
            AddDialogueReply(DialogueKind.Npc);
        bool canDelete = selectedNode.Id != npc.Dialogue.StartId && !npc.Dialogue.Node(npc.Dialogue.StartId).Next.Contains(selectedNode.Id);
        if (Button("Delete branch", x, by + 42, 282, false, canEdit && canDelete, true)) DeleteDialogueBranch();
        var parent = npc.Dialogue.Nodes.FirstOrDefault(n => n.Next.Contains(selectedNode.Id));
        int sibling = parent?.Next.IndexOf(selectedNode.Id) ?? -1;
        if (Button("Reply up", x, by + 84, 135, false, canEdit && sibling > 0, true)) MoveReply(selectedNode.Id, -1);
        if (Button("Reply down", x + 147, by + 84, 135, false, canEdit && parent != null && sibling < parent.Next.Count - 1, true)) MoveReply(selectedNode.Id, 1);
        if (Button("Focus selected node", x, by + 126, 282, false, enabled, true)) FocusDialogueNode();
        Label($"{npc.Dialogue.Nodes.Count} / {DialogueTree.MaxNodes} nodes", x, by + 170, 13, Muted);
    }
    void AddDialogueReply(DialogueKind kind)
    {
        if (DialogueNpc == null || DialogueNode == null || !DialogueNpc.Dialogue.CanAddChild(DialogueNode.Id, kind)) return;
        document.Checkpoint(); dialogueNodeId = DialogueNpc.Dialogue.AddChild(DialogueNode.Id, kind).Id;
        FocusDialogueNode();
    }
    static void DrawDialogueRedirect(Rectangle from, Rectangle to, Color color)
    {
        var a = new Vector2(from.X + from.Width / 2, from.Y + from.Height);
        var b = new Vector2(to.X + to.Width / 2, to.Y + to.Height);
        float y = Math.Max(a.Y, b.Y) + 22;
        void Dashed(Vector2 start, Vector2 end)
        {
            var delta = end - start; float length = delta.Length();
            if (length < .01f) return;
            var direction = delta / length;
            for (float offset = 0; offset < length; offset += 10)
                DrawLineEx(start + direction * offset, start + direction * Math.Min(offset + 6, length), 2, color);
        }
        Dashed(a, new(a.X, y)); Dashed(new(a.X, y), new(b.X, y)); Dashed(new(b.X, y), b);
        DrawLineEx(b + new Vector2(-5, 8), b, 2, color); DrawLineEx(b + new Vector2(5, 8), b, 2, color);
    }
    void FocusDialogueNode()
    {
        if (DialogueNpc == null || DialogueNode == null) return;
        var rect = LayoutTree(DialogueNpc.Dialogue)[dialogueNodeId!];
        treePan += new Vector2(TreeArea.X + TreeArea.Width / 2 - rect.X - rect.Width / 2,
            TreeArea.Y + TreeArea.Height / 2 - rect.Y - rect.Height / 2);
    }
    void DeleteDialogueBranch()
    {
        var npc = DialogueNpc; var node = DialogueNode;
        if (npc == null || node == null || node.Id == npc.Dialogue.StartId || npc.Dialogue.Node(npc.Dialogue.StartId).Next.Contains(node.Id)) return;
        string parent = npc.Dialogue.Nodes.First(n => n.Next.Contains(node.Id)).Id;
        document.Checkpoint(); npc.Dialogue.RemoveBranch(node.Id); dialogueNodeId = parent;
    }
    void MoveReply(string id, int direction)
    {
        var parent = DialogueNpc!.Dialogue.Nodes.First(n => n.Next.Contains(id));
        int index = parent.Next.IndexOf(id), next = index + direction;
        if (next < 0 || next >= parent.Next.Count) return;
        document.Checkpoint(); (parent.Next[index], parent.Next[next]) = (parent.Next[next], parent.Next[index]);
    }
    void BeginTextEdit(string title, string value, int limit, bool multiline, Action<string> apply)
    {
        textEdit = new(title, value, limit, multiline, apply);
        // Discard characters entered before this field was opened.
        while (GetCharPressed() != 0) { }
    }
    void UpdateTextEdit()
    {
        var edit = textEdit!;
        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl) ||
            IsKeyDown(KeyboardKey.LeftSuper) || IsKeyDown(KeyboardKey.RightSuper);
        if (IsKeyPressed(KeyboardKey.Escape)) { textEdit = null; return; }
        if (ctrl && IsKeyPressed(KeyboardKey.A)) edit.Replace = true;
        if (ctrl && IsKeyPressed(KeyboardKey.V)) AppendEdit(GetClipboardText_());
        bool left = IsKeyPressed(KeyboardKey.Left) || IsKeyPressedRepeat(KeyboardKey.Left);
        bool right = IsKeyPressed(KeyboardKey.Right) || IsKeyPressedRepeat(KeyboardKey.Right);
        if (left || right)
        {
            edit.Caret = edit.Replace ? (left ? 0 : edit.Value.Length) :
                left ? PreviousCharacter(edit.Value, edit.Caret) : NextCharacter(edit.Value, edit.Caret);
            edit.Replace = false;
        }
        int lineStart = edit.Caret == 0 ? 0 : edit.Value.LastIndexOf('\n', edit.Caret - 1) + 1;
        int lineEnd = edit.Value.IndexOf('\n', edit.Caret);
        if (lineEnd < 0) lineEnd = edit.Value.Length;
        if (IsKeyPressed(KeyboardKey.Home)) { edit.Caret = ctrl ? 0 : lineStart; edit.Replace = false; }
        if (IsKeyPressed(KeyboardKey.End)) { edit.Caret = ctrl ? edit.Value.Length : lineEnd; edit.Replace = false; }
        if (IsKeyPressed(KeyboardKey.Up) && lineStart > 0)
        {
            int previousStart = lineStart < 2 ? 0 : edit.Value.LastIndexOf('\n', lineStart - 2) + 1;
            edit.Caret = Math.Min(lineStart - 1, previousStart + edit.Caret - lineStart); edit.Replace = false;
        }
        if (IsKeyPressed(KeyboardKey.Down) && lineEnd < edit.Value.Length)
        {
            int nextEnd = edit.Value.IndexOf('\n', lineEnd + 1);
            edit.Caret = Math.Min(nextEnd < 0 ? edit.Value.Length : nextEnd, lineEnd + 1 + edit.Caret - lineStart); edit.Replace = false;
        }
        if (edit.Caret < edit.Value.Length && char.IsLowSurrogate(edit.Value[edit.Caret])) edit.Caret++;
        if (IsKeyPressed(KeyboardKey.Backspace) || IsKeyPressedRepeat(KeyboardKey.Backspace))
        {
            if (edit.Replace) { edit.Value = ""; edit.Caret = 0; }
            else if (edit.Caret > 0)
            {
                int previous = PreviousCharacter(edit.Value, edit.Caret);
                edit.Value = edit.Value.Remove(previous, edit.Caret - previous); edit.Caret = previous;
            }
            edit.Replace = false;
            edit.ScrollToEnd = true;
        }
        if (IsKeyPressed(KeyboardKey.Delete) || IsKeyPressedRepeat(KeyboardKey.Delete))
        {
            if (edit.Replace) { edit.Value = ""; edit.Caret = 0; }
            else if (edit.Caret < edit.Value.Length) edit.Value = edit.Value.Remove(edit.Caret, NextCharacter(edit.Value, edit.Caret) - edit.Caret);
            edit.Replace = false;
        }
        for (int code = GetCharPressed(); code != 0; code = GetCharPressed())
            if (code >= 32 && !ctrl) AppendEdit(char.ConvertFromUtf32(code));
        if (IsKeyPressed(KeyboardKey.Enter))
        {
            if (edit.Multiline && !ctrl) AppendEdit("\n");
            else ApplyTextEdit();
        }
    }
    static int PreviousCharacter(string value, int caret) => caret <= 0 ? 0 : caret - (caret > 1 && char.IsLowSurrogate(value[caret - 1]) ? 2 : 1);
    static int NextCharacter(string value, int caret) => caret >= value.Length ? value.Length : caret + (char.IsHighSurrogate(value[caret]) ? 2 : 1);
    void AppendEdit(string value)
    {
        var edit = textEdit!;
        value = value.Replace("\r", "");
        if (!edit.Multiline) value = value.Replace("\n", " ");
        value = string.Concat(value.EnumerateRunes().Where(r => !Rune.IsControl(r) || r.Value == '\n').Select(r => r.ToString()));
        if (value.Length == 0) return;
        if (edit.Replace) { edit.Value = ""; edit.Caret = 0; edit.Replace = false; }
        foreach (var rune in value.EnumerateRunes())
        {
            if (edit.Value.Length + rune.Utf16SequenceLength > edit.Limit) break;
            edit.Value = edit.Value.Insert(edit.Caret, rune.ToString()); edit.Caret += rune.Utf16SequenceLength;
        }
        edit.ScrollToEnd = true;
    }
    void ApplyTextEdit()
    {
        var edit = textEdit!;
        if (!edit.Multiline && string.IsNullOrWhiteSpace(edit.Value)) return;
        edit.Apply(edit.Value); textEdit = null;
    }
    void DrawTextEdit()
    {
        var edit = textEdit!;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new Color(0, 0, 0, 180));
        int w = Math.Min(800, GetScreenWidth() - 100), h = edit.Multiline ? 480 : 280;
        int x = (GetScreenWidth() - w) / 2, y = (GetScreenHeight() - h) / 2;
        DrawRectangle(x, y, w, h, Panel); DrawRectangleLines(x, y, w, h, Border);
        Label(edit.Title, x + 24, y + 24, 23, Accent);
        Label("Type / paste text. Ctrl+A: select all. Arrows / Home / End: move cursor.", x + 24, y + 62, 14, Muted);
        var field = new Rectangle(x + 24, y + 96, w - 48, h - 188);
        DrawRectangleRec(field, Background); DrawRectangleLinesEx(field, 1, edit.Replace ? Accent : Border);
        if (pendingAction == null && IsMouseButtonPressed(MouseButton.Left) && CheckCollisionPointRec(GetMousePosition(), field))
        {
            var plainLines = UiText.Wrap(edit.Value, 18, w - 72);
            int row = Math.Clamp((GetMouseY() - y - 106 + edit.Scroll) / 24, 0, plainLines.Count - 1), search = 0;
            for (int i = 0; i <= row; i++)
            {
                int found = edit.Value.IndexOf(plainLines[i], search, StringComparison.Ordinal);
                if (found >= 0) search = found;
                if (i < row) search = Math.Min(edit.Value.Length, search + Math.Max(1, plainLines[i].Length));
            }
            string line = plainLines[row]; int offset = 0, mouseX = GetMouseX() - x - 36;
            foreach (Rune rune in line.EnumerateRunes())
            {
                int next = offset + rune.Utf16SequenceLength;
                if (mouseX < (UiText.Measure(line[..offset], 18) + UiText.Measure(line[..next], 18)) / 2) break;
                offset = next;
            }
            edit.Caret = Math.Min(edit.Value.Length, search + offset); edit.Replace = false;
        }
        var lines = UiText.Wrap(edit.Replace ? edit.Value : edit.Value.Insert(edit.Caret, "|"), 18, w - 72);
        if (CheckCollisionPointRec(GetMousePosition(), field)) edit.Scroll -= (int)(GetMouseWheelMove() * 35);
        int maxScroll = Math.Max(0, lines.Count * 24 - (int)field.Height + 16);
        if (edit.ScrollToEnd)
        {
            int caretY = (UiText.Wrap(edit.Value[..edit.Caret], 18, w - 72).Count - 1) * 24;
            if (caretY < edit.Scroll) edit.Scroll = caretY;
            if (caretY + 24 > edit.Scroll + field.Height - 16) edit.Scroll = caretY + 40 - (int)field.Height;
            edit.ScrollToEnd = false;
        }
        edit.Scroll = Math.Clamp(edit.Scroll, 0, maxScroll);
        BeginScissorMode((int)field.X + 8, (int)field.Y + 8, (int)field.Width - 16, (int)field.Height - 16);
        for (int i = 0; i < lines.Count; i++) Label(lines[i], x + 36, y + 106 + i * 24 - edit.Scroll, 18, edit.Replace ? Accent : Text);
        EndScissorMode();
        Label($"{edit.Value.Length} / {edit.Limit}   |   {(edit.Multiline ? "Ctrl+Enter: apply / Enter: new line" : "Enter: apply")}", x + 24, y + h - 74, 13, Muted);
        if (Button("Apply", x + 24, y + h - 48, w / 2 - 36, true, pendingAction == null && (edit.Multiline || !string.IsNullOrWhiteSpace(edit.Value)), true)) ApplyTextEdit();
        if (Button("Cancel", x + w / 2 + 12, y + h - 48, w / 2 - 36, false, pendingAction == null, true)) textEdit = null;
    }
}
