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
        CheckRedirects();
        CheckNpcSequences();
        Console.WriteLine("Dialogue checks passed: NPC sequences, player choices, redirects/loops, shared destinations, deletion, undo/redo, save/load, endings, validation and playtest isolation.");
    }
    static void CheckNpcSequences()
    {
        var map = MapData.CreateEmpty(); var npc = new NpcData { X = 1 }; map.Npcs.Add(npc);
        var doc = new MapDocument(map, true); string original = map.Serialize(); var tree = npc.Dialogue;
        var first = tree.Node(tree.Node(tree.StartId).Next.Single());
        string[] choices = first.Next.ToArray(); var question = tree.Node(choices[0]); var answer = tree.Node(question.Next.Single());
        doc.Checkpoint();
        var second = tree.AddChild(first.Id, DialogueKind.Npc); second.Text = "Druga kwestia NPC.";
        Check(first.Next.Single() == second.Id && second.Next.SequenceEqual(choices), "NPC continuation keeps the original player choices and their IDs");
        var third = tree.AddChild(second.Id, DialogueKind.Npc); third.Text = "Trzecia kwestia NPC.";
        Check(second.Next.Single() == third.Id && third.Next.SequenceEqual(choices), "multiple NPC lines can precede player choices");
        var inserted = tree.AddChild(first.Id, DialogueKind.Npc); inserted.Text = "Kwestia wstawiona między istniejące kwestie.";
        Check(first.Next.Single() == inserted.Id && inserted.Next.Single() == second.Id, "inserting into an NPC sequence keeps its old continuation");
        var answer2 = tree.AddChild(answer.Id, DialogueKind.Npc);
        var answer3 = tree.AddChild(answer2.Id, DialogueKind.Npc);
        var back = tree.AddChild(answer3.Id, DialogueKind.Player); tree.SetReplyTarget(back.Id, first.Id);
        tree.Validate();
        Check(!tree.CanAddChild(first.Id, DialogueKind.Player) && tree.CanAddChild(third.Id, DialogueKind.Player) &&
            !tree.CanAddChild(question.Id, DialogueKind.Player) && !tree.CanAddChild(question.Id, DialogueKind.Npc) &&
            !tree.CanAddChild(third.Id, DialogueKind.Start), "reply types respect sequential NPC lines and player choices");
        string sequential = map.Serialize();
        try { tree.AddChild(first.Id, DialogueKind.Player); throw new Exception("Mixed replies accepted."); }
        catch (InvalidOperationException) { }
        Check(map.Serialize() == sequential, "an invalid mixed reply changes no data");
        var play = new PlaySession(map); var dialogue = play.Dialogue; dialogue.Begin(play.NearbyNpc!);
        for (int loop = 0; loop < 3; loop++)
        {
            Check(dialogue.Current?.Id == first.Id && dialogue.Choices.Count == 0 && dialogue.Continuation?.Id == inserted.Id, "NPC continuation is not a player choice");
            dialogue.Choose(0); Check(dialogue.Current?.Id == first.Id, "numbered choices cannot skip sequential NPC speech");
            dialogue.Advance(); Check(dialogue.Current?.Id == inserted.Id, "continue advances exactly one NPC line");
            dialogue.Advance(); Check(dialogue.Current?.Id == second.Id, "continue follows an inserted NPC line");
            dialogue.Advance(); Check(dialogue.Current?.Id == third.Id && dialogue.Choices.Count == 2 && dialogue.Continuation == null, "player choices appear after the NPC sequence");
            dialogue.Advance(); Check(dialogue.Current?.Id == third.Id, "continue cannot bypass player choices");
            dialogue.Choose(0); Check(dialogue.Current?.Id == answer.Id, "player choice starts another NPC sequence");
            dialogue.Advance(); Check(dialogue.Current?.Id == answer2.Id, "NPC continues after a player reply");
            dialogue.Advance(); Check(dialogue.Current?.Id == answer3.Id && dialogue.Choices.Single().Id == back.Id, "NPC sequence can end with a return option");
            dialogue.Choose(0);
        }
        dialogue.Advance(); dialogue.Advance(); dialogue.Advance(); dialogue.Choose(1);
        Check(!dialogue.Active, "terminal player reply still closes a conversation with NPC sequences");
        var terminal = new DialogueSession(); terminal.Begin(new NpcData()); terminal.Choose(0); terminal.Advance();
        Check(!terminal.Active, "continue closes a terminal NPC line");
        doc.Undo(); Check(doc.Map.Serialize() == original, "undo restores the dialogue before the NPC sequence");
        doc.Redo(); Check(doc.Map.Serialize() == sequential, "redo restores sequences and their original choice IDs");
        string file = Path.Combine(Path.GetTempPath(), "wildwood-dialogue-sequence-" + Guid.NewGuid().ToString("N") + ".json");
        try { doc.Save(file); Check(MapData.Load(file).Serialize() == sequential, "NPC sequences save and load"); }
        finally { if (File.Exists(file)) File.Delete(file); }
        tree = doc.Map.Npcs.Single().Dialogue; tree.RemoveBranch(inserted.Id); tree.Validate();
        Check(tree.Nodes.Count == 2 && tree.Node(first.Id).Next.Count == 0, "deleting a sequence removes disconnected lines and preserves Start and the first NPC");
        Check(play.Map.Serialize() == sequential, "editing a sequence preserves the playtest copy");
        Invalid(t =>
        {
            var continuation = new DialogueNode { Kind = DialogueKind.Npc }; t.Nodes.Add(continuation);
            t.Node(t.Node(t.StartId).Next.Single()).Next.Add(continuation.Id);
        }, "mixed NPC continuation and player choices");
        Invalid(t =>
        {
            var firstNpc = t.Node(t.Node(t.StartId).Next.Single()); firstNpc.Next.Clear();
            var a = new DialogueNode { Kind = DialogueKind.Npc }; var b = new DialogueNode { Kind = DialogueKind.Npc };
            t.Nodes.AddRange([a, b]); firstNpc.Next.AddRange([a.Id, b.Id]);
        }, "multiple NPC continuations");
        Invalid(t => t.Nodes.First(n => n.Kind == DialogueKind.Player).Next[0] = t.Nodes.Last(n => n.Kind == DialogueKind.Player).Id, "player reply leading to another player reply");
        // Cyclic NPC speech also waits for explicit continue input instead of advancing on its own.
        var cycleNpc = new NpcData(); var cycle = cycleNpc.Dialogue; var root = cycle.Node(cycle.Node(cycle.StartId).Next.Single());
        foreach (string child in root.Next.ToArray()) cycle.RemoveBranch(child);
        var nextNpc = cycle.AddChild(root.Id, DialogueKind.Npc); nextNpc.Next.Add(root.Id); cycle.Validate();
        var cyclicSession = new DialogueSession(); cyclicSession.Begin(cycleNpc);
        for (int i = 0; i < 20; i++) { cyclicSession.Advance(); Check(cyclicSession.Current?.Id == nextNpc.Id, "NPC cycle advances once"); cyclicSession.Advance(); }
        cyclicSession.Close(); Check(!cyclicSession.Active, "NPC-only cycles can be closed");
    }
    static void CheckRedirects()
    {
        var map = MapData.CreateEmpty(); var npc = new NpcData { X = 1 }; map.Npcs.Add(npc);
        var tree = npc.Dialogue; var greeting = tree.Node(tree.Node(tree.StartId).Next.Single());
        var question = tree.Node(greeting.Next[0]); var answer = tree.Node(question.Next.Single());
        var back = tree.AddChild(answer.Id); back.Text = "Wróć do wcześniejszych pytań.";
        tree.SetReplyTarget(back.Id, greeting.Id); tree.Validate();
        var doc = new MapDocument(map, true); string linked = map.Serialize();
        var play = new PlaySession(map); play.Dialogue.Begin(play.NearbyNpc!);
        for (int i = 0; i < 50; i++)
        {
            play.Dialogue.Choose(0); Check(play.Dialogue.Current?.Id == answer.Id, "loop enters the selected branch");
            play.Dialogue.Choose(0); Check(play.Dialogue.Current?.Id == greeting.Id && play.Dialogue.Choices.Count == 2, "reply returns to an earlier NPC line and restores its choices");
        }
        play.Dialogue.Choose(1); Check(!play.Dialogue.Active, "a looping conversation can still end normally");
        doc.Checkpoint(); tree.SetReplyTarget(back.Id, null); tree.Validate();
        Check(doc.Dirty && back.Next.Count == 0, "a redirect can be changed into an ending");
        doc.Undo(); Check(doc.Map.Serialize() == linked, "undo restores a redirect");
        doc.Redo(); Check(doc.Map.Npcs.Single().Dialogue.Node(back.Id).Next.Count == 0, "redo clears a redirect");
        doc.Undo();
        string file = Path.Combine(Path.GetTempPath(), "wildwood-dialogue-return-" + Guid.NewGuid().ToString("N") + ".json");
        try { doc.Save(file); Check(MapData.Load(file).Serialize() == linked, "cyclic dialogues save and load"); }
        finally { if (File.Exists(file)) File.Delete(file); }
        tree = doc.Map.Npcs.Single().Dialogue;
        tree.RemoveBranch(back.Id); tree.Validate();
        Check(tree.Nodes.Any(n => n.Id == greeting.Id) && tree.Nodes.Any(n => n.Id == answer.Id) && tree.Nodes.Count == 5,
            "deleting a return reply preserves its destination and ancestors");
        Check(play.Map.Serialize() == linked, "editor changes preserve the playtest's looping conversation");
        tree = MapData.Parse(linked).Npcs.Single().Dialogue;
        var shared = tree.AddChild(greeting.Id); tree.SetReplyTarget(shared.Id, answer.Id); tree.Validate();
        tree.RemoveBranch(question.Id); tree.Validate();
        Check(tree.Nodes.Any(n => n.Id == answer.Id) && tree.Node(shared.Id).Next.Single() == answer.Id,
            "removing one incoming branch preserves a shared NPC destination");
        tree.RemoveBranch(answer.Id); tree.Validate();
        Check(tree.Node(shared.Id).Next.Count == 0 && tree.Nodes.Any(n => n.Id == greeting.Id) && !tree.Nodes.Any(n => n.Id == back.Id),
            "deleting a destination clears incoming redirects and prunes unreachable replies");
        tree = MapData.Parse(linked).Npcs.Single().Dialogue;
        tree.SetReplyTarget(question.Id, greeting.Id); tree.Validate();
        Check(tree.Nodes.Count == 4 && tree.Node(question.Id).Next.Single() == greeting.Id && !tree.Nodes.Any(n => n.Id == answer.Id),
            "rewiring a continued reply removes only its unreachable old branch");
        string before = System.Text.Json.JsonSerializer.Serialize(tree);
        foreach (Action change in new Action[] {
            () => tree.SetReplyTarget(question.Id, tree.StartId),
            () => tree.SetReplyTarget(question.Id, question.Id),
            () => tree.SetReplyTarget(greeting.Id, greeting.Id),
            () => tree.SetReplyTarget(question.Id, "missing") })
        {
            bool rejected = false;
            try { change(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { rejected = true; }
            Check(rejected, "invalid redirect destination is rejected");
            Check(System.Text.Json.JsonSerializer.Serialize(tree) == before, "an invalid redirect changes no data");
        }
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
