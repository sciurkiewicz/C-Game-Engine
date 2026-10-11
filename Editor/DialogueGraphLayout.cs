using System.Numerics;
using Raylib_cs;

sealed record DialogueGraphLayout(Dictionary<string, Rectangle> Rects, HashSet<(string From, string To)> Branches)
{
    public static DialogueGraphLayout Build(DialogueTree tree, Vector2 origin)
    {
        // A spanning tree places each node once. Remaining graph edges are redirect arrows.
        var children = new Dictionary<string, List<string>>();
        var visited = new HashSet<string>();
        var branches = new HashSet<(string From, string To)>();
        void Visit(string id)
        {
            visited.Add(id); children[id] = [];
            foreach (string child in tree.Node(id).Next)
                if (!visited.Contains(child)) { children[id].Add(child); branches.Add((id, child)); Visit(child); }
        }
        Visit(tree.StartId);
        var rects = new Dictionary<string, Rectangle>(); int row = 0;
        float Place(string id, int depth)
        {
            var next = children[id]; float first = 0, last = 0;
            if (next.Count == 0) first = last = row++ * 104;
            else
                for (int i = 0; i < next.Count; i++)
                { float child = Place(next[i], depth + 1); if (i == 0) first = child; last = child; }
            float center = (first + last) / 2;
            rects[id] = new(origin.X + depth * 230, origin.Y + center, 196, 82);
            return center;
        }
        Place(tree.StartId, 0);
        return new(rects, branches);
    }
}

static class DialogueLayoutChecks
{
    public static void Run()
    {
        var tree = DialogueTree.CreateDefault();
        var greeting = tree.Node(tree.Node(tree.StartId).Next.Single());
        var answer = tree.Node(tree.Node(greeting.Next[0]).Next.Single());
        var back = tree.AddChild(answer.Id); tree.SetReplyTarget(back.Id, greeting.Id);
        var shared = tree.AddChild(greeting.Id); tree.SetReplyTarget(shared.Id, answer.Id);
        Check(tree, (back.Id, greeting.Id), (shared.Id, answer.Id));
        var sequence = tree.AddChild(greeting.Id, DialogueKind.Npc);
        var nextSequence = tree.AddChild(sequence.Id, DialogueKind.Npc);
        Check(tree, (back.Id, greeting.Id), (shared.Id, answer.Id));
        if (DialogueGraphLayout.Build(tree, Vector2.Zero).Rects[nextSequence.Id].X <= DialogueGraphLayout.Build(tree, Vector2.Zero).Rects[sequence.Id].X)
            throw new InvalidOperationException("Sequential NPC lines must appear in their dialogue order.");
        // A deep conversation with many return links must terminate layout and retain every card.
        var current = answer;
        while (tree.Nodes.Count + 3 <= DialogueTree.MaxNodes)
        {
            var reply = tree.AddChild(current.Id); var next = tree.AddChild(reply.Id);
            var returnReply = tree.AddChild(next.Id); tree.SetReplyTarget(returnReply.Id, greeting.Id); current = next;
        }
        Check(tree, (back.Id, greeting.Id));
        Console.WriteLine("Dialogue layout checks passed: NPC sequences, loops, shared destinations, unique non-overlapping cards and deep graphs near the node limit.");
    }
    static void Check(DialogueTree tree, params (string From, string To)[] redirects)
    {
        tree.Validate(); var layout = DialogueGraphLayout.Build(tree, new(32, 112));
        if (layout.Rects.Count != tree.Nodes.Count || layout.Branches.Count != tree.Nodes.Count - 1 || redirects.Any(layout.Branches.Contains))
            throw new InvalidOperationException("Dialogue layout omitted a node or treated a redirect as a new branch.");
        var rects = layout.Rects.Values.ToArray();
        for (int i = 0; i < rects.Length; i++)
        for (int j = i + 1; j < rects.Length; j++)
            if (rects[i].X < rects[j].X + rects[j].Width && rects[i].X + rects[i].Width > rects[j].X &&
                rects[i].Y < rects[j].Y + rects[j].Height && rects[i].Y + rects[i].Height > rects[j].Y)
                throw new InvalidOperationException("Dialogue layout cards overlap.");
    }
}
