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
    public DialogueNode AddChild(string parentId)
    {
        var parent = Node(parentId);
        if (Nodes.Count >= MaxNodes || (parent.Kind != DialogueKind.Npc && parent.Next.Count != 0))
            throw new InvalidOperationException("This node cannot have another child.");
        var kind = parent.Kind == DialogueKind.Npc ? DialogueKind.Player : DialogueKind.Npc;
        var child = new DialogueNode { Kind = kind, Text = kind == DialogueKind.Player ? "Odpowiedź bohatera" : "Kwestia NPC" };
        Nodes.Add(child); parent.Next.Add(child.Id);
        return child;
    }
    public void RemoveBranch(string id)
    {
        if (id == StartId || Node(StartId).Next.Contains(id))
            throw new InvalidOperationException("Keep Start and the first NPC line.");
        var removed = new HashSet<string>();
        var pending = new Stack<string>(); pending.Push(id);
        while (pending.TryPop(out var current))
            if (removed.Add(current)) foreach (string child in Node(current).Next) pending.Push(child);
        foreach (var node in Nodes) node.Next.RemoveAll(removed.Contains);
        Nodes.RemoveAll(n => removed.Contains(n.Id));
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
                if (!nodes.TryGetValue(id, out var child) || child.Kind != (node.Kind == DialogueKind.Npc ? DialogueKind.Player : DialogueKind.Npc))
                    throw new InvalidDataException("Dialogue must alternate NPC lines and player replies.");
                parents[id] = parents.GetValueOrDefault(id) + 1;
            }
        }
        if (parents.ContainsKey(StartId) || Nodes.Any(n => n.Id != StartId && parents.GetValueOrDefault(n.Id) != 1))
            throw new InvalidDataException("Each dialogue node must have exactly one parent.");
        var visited = new HashSet<string>(); var stack = new Stack<string>(); stack.Push(StartId);
        while (stack.TryPop(out var id))
        {
            if (!visited.Add(id)) throw new InvalidDataException("Dialogue cycles are not allowed.");
            foreach (string child in nodes[id].Next) stack.Push(child);
        }
        if (visited.Count != Nodes.Count) throw new InvalidDataException("Dialogue has unreachable nodes.");
    }
}

// Pure dialogue state shared by the runtime and editor playtest.
sealed class DialogueSession
{
    public NpcData? Speaker { get; private set; }
    public DialogueNode? Current { get; private set; }
    public bool Active => Speaker != null;
    public IReadOnlyList<DialogueNode> Choices => Speaker == null || Current == null ? [] :
        Current.Next.Select(Speaker.Dialogue.Node).ToArray();
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
    public void Close() { Speaker = null; Current = null; }
}
