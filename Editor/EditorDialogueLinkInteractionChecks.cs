using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

sealed partial class EditorApp
{
    void ClickDialogueNode(string id)
    {
        var rect = LayoutTree(DialogueNpc!.Dialogue)[id]; Click((int)rect.X + 32, (int)rect.Y + 30);
    }
    void DialogueUndo(bool redo = false)
    {
        Input(2, (int)KeyboardKey.LeftControl); Press(redo ? KeyboardKey.Y : KeyboardKey.Z);
        Input(1, (int)KeyboardKey.LeftControl); TestFrame();
    }
    public void RunDialogueLinkInteractionChecks()
    {
        SetWindowSize(1440, 900); play = null; document.New(20, 12); tool = EditTool.Select; selectedId = null;
        focus = Vector3.Zero; camera.Snap(focus); TestFrame();
        var npc = document.PlaceNpc(null, new(1, 0, 0));
        var tree = npc.Dialogue; var greeting = tree.Node(tree.Node(tree.StartId).Next.Single());
        var question = tree.Node(greeting.Next[0]); var answer = tree.Node(question.Next.Single());
        string npcId = npc.Id, greetingId = greeting.Id, questionId = question.Id, answerId = answer.Id;
        OpenDialogue(npc); dialogueNodeId = answerId; TestFrame();
        int x = GetScreenWidth() - 324, by = GetScreenHeight() - 244;
        Click(x + 120, by + 15);
        Verify(DialogueNode?.Kind == DialogueKind.Player && tree.Nodes.Count == 6, "add a reply for returning to an earlier line");
        string backId = dialogueNodeId!;
        Click(x + 100, 200); AppendEdit("Wróć do pytań.");
        Input(2, (int)KeyboardKey.LeftControl); Press(KeyboardKey.Enter); Input(1, (int)KeyboardKey.LeftControl); TestFrame();
        Click(GetScreenWidth() - 80, 47); // Home: make the earlier NPC line visible.
        Click(x + 120, by - 77);
        Verify(replyLinkId == backId, "Redirect to NPC enters destination selection");
        string before = document.Map.Serialize();
        ClickDialogueNode(tree.StartId);
        Verify(replyLinkId == backId && document.Map.Serialize() == before, "Start cannot be selected as a reply destination");
        ClickDialogueNode(greetingId);
        Verify(replyLinkId == null && tree.Node(backId).Next.Single() == greetingId, "clicking an earlier NPC creates a redirect");
        tree.Validate(); Verify(LayoutTree(tree).Count == tree.Nodes.Count, "cyclic dialogue layout renders every node exactly once");
        TestFrame(); TestFrame();
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "dialogue-return-editor.png"));
        DialogueUndo(); tree = DialogueNpc!.Dialogue;
        Verify(tree.Node(backId).Next.Count == 0, "undo removes the redirect");
        DialogueUndo(true); tree = DialogueNpc!.Dialogue;
        Verify(tree.Node(backId).Next.Single() == greetingId, "redo restores the redirect");
        Click(x + 120, by - 77); Press(KeyboardKey.Escape);
        Verify(replyLinkId == null && dialogueNpcId == npcId && tree.Node(backId).Next.Single() == greetingId,
            "Escape cancels destination selection without closing the dialogue editor");
        Click(x + 120, by - 35);
        Verify(tree.Node(backId).Next.Count == 0, "End after this reply clears a return link");
        DialogueUndo(); tree = DialogueNpc!.Dialogue;
        Press(KeyboardKey.Delete); tree = DialogueNpc!.Dialogue;
        Verify(tree.Nodes.Count == 5 && tree.Nodes.Any(n => n.Id == greetingId), "deleting a return branch preserves the earlier dialogue");
        DialogueUndo(); tree = DialogueNpc!.Dialogue;
        ClickDialogueNode(questionId); Click(x + 120, by - 77); ClickDialogueNode(greetingId);
        Verify(tree.Nodes.Count == 4 && tree.Node(questionId).Next.Single() == greetingId && !tree.Nodes.Any(n => n.Id == answerId),
            "redirect can replace an existing continuation");
        DialogueUndo(); tree = DialogueNpc!.Dialogue;
        Verify(tree.Nodes.Count == 6 && tree.Node(backId).Next.Single() == greetingId, "undo restores the replaced continuation and its return link");
        Click(GetScreenWidth() - 280, 47);
        string saved = document.Map.Serialize();
        Verify(MapData.Load(mapPath).Serialize() == saved && !document.Dirty, "save/load retains dialogue cycles");
        document.Map.Save(Path.Combine(ProjectPaths.Root, "Editor", "obj", "dialogue-return-runtime.json"));
        Press(KeyboardKey.Escape); Press(KeyboardKey.F5); Press(KeyboardKey.E);
        Verify(play?.Dialogue.Current?.Id == greetingId, "playtest opens the first NPC line");
        for (int i = 0; i < 3; i++)
        {
            Press(KeyboardKey.One); Verify(play!.Dialogue.Current?.Id == answerId, "reply enters the next NPC line");
            Press(KeyboardKey.One); Verify(play!.Dialogue.Current?.Id == greetingId && play.Dialogue.Choices.Count == 2,
                "keyboard choice returns to the earlier line with its original options");
        }
        WorldRenderer.Screenshot(Path.Combine(ProjectPaths.Root, "Editor", "obj", "dialogue-return-playtest.png"));
        Press(KeyboardKey.Two); Verify(play != null && !play.Dialogue.Active, "looped conversation still has a working exit option");
        Press(KeyboardKey.F5); Verify(document.Map.Serialize() == saved && !document.Dirty, "playtest redirects preserve the editor map");
        Console.WriteLine("Dialogue redirect interaction checks passed: destination selection, loops/layout, cancel, endings, deletion, replacement, undo/redo, save/load and repeated runtime choices.");
    }
}
