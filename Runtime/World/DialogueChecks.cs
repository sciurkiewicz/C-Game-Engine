using System.Numerics;

static class DialogueChecks
{
    public static void Run()
    {
        var map = MapData.CreateEmpty(20, 12);
        var doc = new MapDocument(map, true);
        var npc = doc.PlaceNpc(null, new Vector3(1, 0, 0));
        npc.Name = "Strażnik Żółw";
        Check(doc.Dirty && !WorldRenderer.CanWalk(map, npc.Position), "NPC placement and collision");
        var tree = npc.Dialogue;
        var greeting = tree.Node(tree.Node(tree.StartId).Next.Single());
        var question = tree.Node(greeting.Next[0]);
        var answer = tree.Node(question.Next.Single());
        var reply = tree.AddChild(answer.Id); reply.Text = "Dziękuję!\nDo zobaczenia.";
        var continuation = tree.AddChild(reply.Id); continuation.Text = "Powodzenia.";
        tree.Validate();
        string json = map.Serialize();
        var loaded = MapData.Parse(json);
        Check(loaded.Serialize() == json && loaded.Npcs.Single().Name == "Strażnik Żółw", "NPC/tree round trip with Polish text");
        var session = new PlaySession(map);
        Check(session.NearbyNpc?.Id == npc.Id, "nearby NPC can be found");
        map.Npcs.Clear();
        Check(session.Map.Npcs.Count == 1, "playtest keeps an independent copy");
        var dialogue = session.Dialogue;
        dialogue.Begin(session.NearbyNpc!);
        Check(dialogue.Current?.Text == greeting.Text && dialogue.Choices.Count == 2, "Start opens the first NPC line and replies");
        dialogue.Choose(20);
        Check(dialogue.Current?.Id == greeting.Id, "invalid choice leaves conversation unchanged");
        dialogue.Choose(0);
        Check(dialogue.Current?.Id == answer.Id && dialogue.Choices.Single().Text == reply.Text, "choice follows the selected branch");
        dialogue.Choose(0);
        Check(dialogue.Current?.Id == continuation.Id && dialogue.Choices.Count == 0, "deep branch reaches its terminal NPC line");
        dialogue.Close();
        Check(!dialogue.Active && dialogue.Current == null && dialogue.Choices.Count == 0, "close clears conversation state");
        dialogue.Begin(session.NearbyNpc!); dialogue.Choose(1);
        Check(!dialogue.Active, "terminal player reply ends conversation");
        tree.RemoveBranch(reply.Id); tree.Validate();
        Check(answer.Next.Count == 0 && !tree.Nodes.Any(n => n.Id == continuation.Id), "deleting a branch removes all descendants");
        var distant = new PlaySession(MapData.Parse(json));
        distant.Map.Npcs.Single().X = 8;
        Check(distant.NearbyNpc == null, "NPC outside interaction distance is unavailable");
        Invalid(t => t.Node(t.StartId).Next.Clear(), "missing Start connection");
        Invalid(t => t.Node(t.StartId).Next[0] = "missing", "dangling edge");
        Invalid(t => t.Nodes.Add(t.Nodes[0]), "duplicate node ID");
        Invalid(t => t.Nodes.Add(new DialogueNode { Kind = DialogueKind.Start }), "multiple Start nodes");
        Invalid(t => t.Nodes.Add(new DialogueNode { Kind = DialogueKind.Player }), "unreachable node");
        Invalid(t => t.Node(t.Node(t.StartId).Next[0]).Next.Add(t.StartId), "cycle or invalid speaker transition");
        Invalid(t => t.Node(t.Node(t.StartId).Next[0]).Next.Add("missing"), "missing reply");
        Invalid(t => t.Nodes.First(n => n.Kind == DialogueKind.Player).Next.Add(t.Node(t.StartId).Next[0]), "player branching or shared child");
        // A disconnected alternating cycle has one parent per node and must still be rejected.
        Invalid(t =>
        {
            var a = new DialogueNode { Kind = DialogueKind.Npc };
            var b = new DialogueNode { Kind = DialogueKind.Player };
            a.Next.Add(b.Id); b.Next.Add(a.Id); t.Nodes.AddRange([a, b]);
        }, "disconnected cycle");
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(new MapData().Serialize())!;
        legacy.AsObject().Remove("Npcs");
        Check(MapData.Parse(legacy.ToJsonString()).Npcs.Count == 0, "existing maps without NPC fields still load");
        Console.WriteLine("Dialogue checks passed: NPC collision/range, tree editing/validation, branches, endings, Polish text, map compatibility and playtest isolation.");
    }
    static void Invalid(Action<DialogueTree> change, string description)
    {
        var tree = DialogueTree.CreateDefault(); change(tree);
        try { tree.Validate(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid dialogue accepted: " + description);
    }
    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Dialogue check failed: " + description);
    }
}
