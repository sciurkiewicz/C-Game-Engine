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
