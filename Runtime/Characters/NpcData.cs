sealed class NpcData : SceneObject
{
    public string Name { get; set; } = "NPC";
    public DialogueTree Dialogue { get; set; } = DialogueTree.CreateDefault();
    public NpcData() { Height = 1.6f; }
}
