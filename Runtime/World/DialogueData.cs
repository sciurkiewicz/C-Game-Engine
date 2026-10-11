using System.Text.Json.Serialization;

sealed class NpcData : SceneObject
{
    public string Name { get; set; } = "NPC";
    public DialogueTree Dialogue { get; set; } = DialogueTree.CreateDefault();
    public NpcData() { Height = 1.6f; }
}

[JsonConverter(typeof(JsonStringEnumConverter<DialogueKind>))]
enum DialogueKind { Start, Npc, Player }

sealed class DialogueNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DialogueKind Kind { get; set; }
    public string Text { get; set; } = "";
    public List<string> Next { get; set; } = [];
}

sealed class DialogueTree
{
    public const int MaxNodes = 256, MaxTextLength = 2000;
    public string StartId { get; set; } = "";
    public List<DialogueNode> Nodes { get; set; } = [];
    public DialogueNode Node(string id) => Nodes.First(n => n.Id == id);
    public static DialogueTree CreateDefault()
    {
        var start = new DialogueNode { Kind = DialogueKind.Start };
        var greeting = new DialogueNode { Kind = DialogueKind.Npc, Text = "Witaj! W czym mogę pomóc?" };
        var question = new DialogueNode { Kind = DialogueKind.Player, Text = "Co to za miejsce?" };
        var answer = new DialogueNode { Kind = DialogueKind.Npc, Text = "To Wildwood. Rozejrzyj się po okolicy." };
        var goodbye = new DialogueNode { Kind = DialogueKind.Player, Text = "Do zobaczenia." };
        start.Next.Add(greeting.Id); greeting.Next.AddRange([question.Id, goodbye.Id]); question.Next.Add(answer.Id);
        return new() { StartId = start.Id, Nodes = [start, greeting, question, answer, goodbye] };
    }
    public bool CanAddChild(string parentId, DialogueKind kind)
    {
        var parent = Node(parentId);
        if (Nodes.Count >= MaxNodes || kind is not (DialogueKind.Player or DialogueKind.Npc)) return false;
        return parent.Kind == DialogueKind.Npc
            ? kind == DialogueKind.Npc || parent.Next.All(id => Node(id).Kind == DialogueKind.Player)
            : kind == DialogueKind.Npc && parent.Next.Count == 0;
    }
    public DialogueNode AddChild(string parentId) => AddChild(parentId, Node(parentId).Kind == DialogueKind.Npc ? DialogueKind.Player : DialogueKind.Npc);
    public DialogueNode AddChild(string parentId, DialogueKind kind)
    {
        if (!CanAddChild(parentId, kind)) throw new InvalidOperationException("This node cannot have another reply of this type.");
        var parent = Node(parentId);
        var child = new DialogueNode { Kind = kind, Text = kind == DialogueKind.Player ? "Odpowiedź bohatera" : "Kwestia NPC" };
        if (parent.Kind == DialogueKind.Npc && kind == DialogueKind.Npc)
        {
            // Insert the line before the existing continuation or choices, keeping their IDs and branches.
            child.Next.AddRange(parent.Next); parent.Next.Clear();
        }
        Nodes.Add(child); parent.Next.Add(child.Id);
        return child;
    }
    public void RemoveBranch(string id)
    {
        if (id == StartId || Node(StartId).Next.Contains(id))
            throw new InvalidOperationException("Keep Start and the first NPC line.");
        Node(id); // Reject a stale selection before changing the graph.
        foreach (var node in Nodes) node.Next.RemoveAll(next => next == id);
        Nodes.RemoveAll(n => n.Id == id);
        RemoveUnreachable();
    }
    public void SetReplyTarget(string replyId, string? npcId)
    {
        var reply = Node(replyId);
        if (reply.Kind != DialogueKind.Player || (npcId != null && Node(npcId).Kind != DialogueKind.Npc))
            throw new ArgumentException("Select a player reply and an NPC line.");
        reply.Next.Clear();
        if (npcId != null) reply.Next.Add(npcId);
        RemoveUnreachable();
    }
    void RemoveUnreachable()
    {
        var nodes = Nodes.ToDictionary(n => n.Id);
        var reachable = Reachable(nodes);
        Nodes.RemoveAll(n => !reachable.Contains(n.Id));
    }
    HashSet<string> Reachable(Dictionary<string, DialogueNode> nodes)
    {
        var reachable = new HashSet<string>(); var pending = new Stack<string>(); pending.Push(StartId);
        while (pending.TryPop(out var id))
            if (reachable.Add(id)) foreach (string child in nodes[id].Next) pending.Push(child);
        return reachable;
    }
    public void Validate()
    {
        if (Nodes == null || Nodes.Count < 2 || Nodes.Count > MaxNodes || Nodes.Any(n => n == null ||
            string.IsNullOrWhiteSpace(n.Id) || !Enum.IsDefined(n.Kind) || n.Text == null || n.Text.Length > MaxTextLength ||
            n.Next == null || n.Next.Any(string.IsNullOrWhiteSpace) || n.Next.Distinct().Count() != n.Next.Count) ||
            Nodes.Select(n => n.Id).Distinct().Count() != Nodes.Count)
            throw new InvalidDataException("Invalid dialogue nodes.");
        var nodes = Nodes.ToDictionary(n => n.Id);
        if (StartId == null || !nodes.TryGetValue(StartId, out var start) || start.Kind != DialogueKind.Start ||
            start.Next.Count != 1 || Nodes.Count(n => n.Kind == DialogueKind.Start) != 1)
            throw new InvalidDataException("Dialogue requires one Start linked to an NPC line.");
        var parents = new Dictionary<string, int>();
        foreach (var node in Nodes)
        {
            if (node.Kind == DialogueKind.Player && node.Next.Count > 1)
                throw new InvalidDataException("A player reply can lead to one NPC line.");
            foreach (string id in node.Next)
            {
                if (!nodes.TryGetValue(id, out var child) || child.Kind == DialogueKind.Start ||
                    (node.Kind != DialogueKind.Npc && child.Kind != DialogueKind.Npc))
                    throw new InvalidDataException("Start and player replies must lead to NPC lines.");
                parents[id] = parents.GetValueOrDefault(id) + 1;
            }
            if (node.Kind == DialogueKind.Npc && node.Next.Any(id => nodes[id].Kind == DialogueKind.Npc) && node.Next.Count != 1)
                throw new InvalidDataException("An NPC line can have one NPC continuation or a list of player replies.");
        }
        if (parents.ContainsKey(StartId) || Nodes.Any(n => n.Kind == DialogueKind.Player && parents.GetValueOrDefault(n.Id) != 1))
            throw new InvalidDataException("Each player reply must belong to one NPC line.");
        // NPC lines can have multiple incoming links, including returns to earlier lines.
        // Runtime follows one choice or continuation per input; cycles never advance automatically.
        if (Reachable(nodes).Count != Nodes.Count) throw new InvalidDataException("Dialogue has unreachable nodes.");
    }
}

// Pure dialogue state shared by the runtime and editor playtest.
sealed class DialogueSession
{
    public NpcData? Speaker { get; private set; }
    public DialogueNode? Current { get; private set; }
    public bool Active => Speaker != null;
    public IReadOnlyList<DialogueNode> Choices => Speaker == null || Current == null ? [] :
        Current.Next.Select(Speaker.Dialogue.Node).Where(n => n.Kind == DialogueKind.Player).ToArray();
    public DialogueNode? Continuation => Speaker == null || Current == null ? null :
        Current.Next.Select(Speaker.Dialogue.Node).FirstOrDefault(n => n.Kind == DialogueKind.Npc);
    public void Begin(NpcData npc)
    {
        npc.Dialogue.Validate();
        Speaker = npc;
        Current = npc.Dialogue.Node(npc.Dialogue.Node(npc.Dialogue.StartId).Next.Single());
    }
    public void Choose(int index)
    {
        var choices = Choices;
        if (index < 0 || index >= choices.Count) return;
        var reply = choices[index];
        if (reply.Next.Count == 0) Close();
        else Current = Speaker!.Dialogue.Node(reply.Next.Single());
    }
    public void Advance()
    {
        if (!Active) return;
        if (Continuation is { } next) Current = next;
        else if (Choices.Count == 0) Close();
    }
    public void Close() { Speaker = null; Current = null; }
}
