using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    public void RunDialogueSequenceInteractionChecks()
    {
        SetWindowSize(1440, 900); play = null; document.New(20, 12); tool = EditTool.Select; selectedId = null;
        focus = Vector3.Zero; camera.Snap(focus); TestFrame();
        var npc = document.PlaceNpc(null, new(1, 0, 0)); var tree = npc.Dialogue;
        var first = tree.Node(tree.Node(tree.StartId).Next.Single()); string firstId = first.Id;
        string[] oldChoices = first.Next.ToArray(); string answerId = tree.Node(oldChoices[0]).Next.Single();
        OpenDialogue(npc); dialogueNodeId = firstId; TestFrame();
        int x = GetScreenWidth() - 324, by = GetScreenHeight() - 244;
        Click(x + 210, by + 15); string secondId = dialogueNodeId!;
        Verify(DialogueNode?.Kind == DialogueKind.Npc && first.Next.Single() == secondId && DialogueNode.Next.SequenceEqual(oldChoices),
            "NPC Reply inserts a line while preserving existing player options");
        Click(x + 210, by + 15); string thirdId = dialogueNodeId!;
        Verify(DialogueNode?.Kind == DialogueKind.Npc && tree.Node(secondId).Next.Single() == thirdId && DialogueNode.Next.SequenceEqual(oldChoices),
            "NPC Reply creates another consecutive NPC line");
        Click(x + 65, by + 15); string extraReplyId = dialogueNodeId!;
        Verify(DialogueNode?.Kind == DialogueKind.Player && tree.Node(thirdId).Next.SequenceEqual(oldChoices.Append(extraReplyId)),
            "Player Reply adds an option after the NPC sequence");
        Click(x + 210, by + 15); string endId = dialogueNodeId!;
        Verify(DialogueNode?.Kind == DialogueKind.Npc && tree.Node(extraReplyId).Next.Single() == endId, "NPC Reply follows a player option");
        DialogueUndo(); tree = DialogueNpc!.Dialogue;
        Verify(tree.Node(extraReplyId).Next.Count == 0, "undo removes an added NPC Reply");
        DialogueUndo(true); tree = DialogueNpc!.Dialogue;
        Verify(tree.Node(extraReplyId).Next.Single() == endId, "redo restores the NPC Reply");
        dialogueNodeId = answerId; FocusDialogueNode(); TestFrame();
        Click(x + 210, by + 15); string answer2Id = dialogueNodeId!;
        Click(x + 210, by + 15); string answer3Id = dialogueNodeId!;
        Click(x + 65, by + 15); string backId = dialogueNodeId!;
        Click(x + 120, by - 77); Click(GetScreenWidth() - 80, 47); ClickDialogueNode(firstId);
        Verify(tree.Node(backId).Next.Single() == firstId, "NPC sequence can finish with a player option returning to the introduction");
        ClickDialogueNode(firstId); string before = document.Map.Serialize(); Click(x + 65, by + 15);
        Verify(document.Map.Serialize() == before, "Player Reply is disabled before an unfinished NPC sequence");
        tree.Validate(); TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "dialogue-sequence-editor.png"));
        Click(GetScreenWidth() - 280, 47); string saved = document.Map.Serialize();
        Verify(!document.Dirty && MapData.Load(mapPath).Serialize() == saved, "save/load preserves sequences and all original player options");
        document.Map.Save(Path.Combine(ProjectPaths.Root, "Editor", "obj", "dialogue-sequence-runtime.json"));
        Press(KeyboardKey.Escape); Press(KeyboardKey.F5); Press(KeyboardKey.E);
        Verify(play?.Dialogue.Current?.Id == firstId && play.Dialogue.Choices.Count == 0, "playtest opens the first NPC line without treating NPC speech as a player option");
        Press(KeyboardKey.One); Verify(play!.Dialogue.Current?.Id == firstId, "numbered keys cannot skip NPC lines");
        Press(KeyboardKey.Enter); Verify(play.Dialogue.Current?.Id == secondId, "Enter continues by one NPC line");
        Click((int)(viewport.X + viewport.Width / 2), (int)(viewport.Y + viewport.Height - 65));
        Verify(play.Dialogue.Current?.Id == thirdId && play.Dialogue.Choices.Count == 3, "clicking continue displays the next line and its player choices");
        Press(KeyboardKey.One); Verify(play.Dialogue.Current?.Id == answerId, "player option begins a new NPC sequence");
        Press(KeyboardKey.Enter); Verify(play.Dialogue.Current?.Id == answer2Id, "NPC continues after a player option");
        TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "dialogue-sequence-playtest.png"));
        Press(KeyboardKey.Enter); Verify(play.Dialogue.Current?.Id == answer3Id && play.Dialogue.Choices.Single().Id == backId, "last NPC line offers the return reply");
        Press(KeyboardKey.One); Verify(play.Dialogue.Current?.Id == firstId, "return restarts the earlier NPC sequence");
        Press(KeyboardKey.Enter); Press(KeyboardKey.Enter); Press(KeyboardKey.Three);
        Verify(play.Dialogue.Current?.Id == endId && play.Dialogue.Continuation == null && play.Dialogue.Choices.Count == 0, "extra Player Reply leads to its terminal NPC line");
        Press(KeyboardKey.Enter); Verify(play != null && !play.Dialogue.Active, "Enter ends a terminal NPC line without exiting playtest");
        Press(KeyboardKey.E); Press(KeyboardKey.Escape); Verify(play != null && !play.Dialogue.Active, "Escape closes during an NPC sequence");
        Press(KeyboardKey.F5); Verify(document.Map.Serialize() == saved && !document.Dirty, "playtest leaves the editor's dialogue unchanged");
        Console.WriteLine("Dialogue sequence interaction checks passed: Player Reply/NPC Reply, preserved options, undo/redo, save/load, Enter/click continuation, redirects, endings and playtest isolation.");
    }
}
