using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

static class CharacterChecks
{
    public static void Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "wildwood-character-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "player.json"), save = Path.Combine(directory, "progress.json");
            File.WriteAllText(path, """
                { "Name": "Łucja", "Description": "Strażniczka lasu", "MaxHealth": 250, "Damage": 17,
                  "WalkSpeed": 2, "RunSpeed": 4, "AttackRange": 2.4, "AttackCooldown": 0.6, "InteractionRange": 5 }
                """);
            var definition = PlayerDefinition.Load(path);
            Check(definition.Name == "Łucja" && definition.Description == "Strażniczka lasu", "loads character information from JSON");
            var source = MapData.CreateEmpty(32, 32); string original = source.Serialize();
            var walking = new PlaySession(source, character: definition);
            walking.MovePlayer(Vector2.UnitX, false, 1);
            Check(Math.Abs(walking.Player.Length() - 2) < .001f, "JSON controls walking speed");
            var running = new PlaySession(source, character: definition);
            running.MovePlayer(Vector2.UnitX, true, 1);
            Check(Math.Abs(running.Player.Length() - 4) < .001f, "JSON controls running speed");
            var diagonal = new PlaySession(source, character: definition);
            diagonal.MovePlayer(Vector2.One, false, 1);
            Check(Math.Abs(diagonal.Player.Length() - 2) < .001f, "diagonal movement keeps the configured speed");
            diagonal.ToggleInventory(); var position = diagonal.Player;
            diagonal.MovePlayer(Vector2.One, true, 1);
            Check(diagonal.Player == position, "inventory pauses movement");
            diagonal.CloseInventory(); diagonal.Dialogue.Begin(new()); diagonal.MovePlayer(Vector2.One, true, 1);
            Check(diagonal.Player == position, "dialogue pauses movement");
            diagonal.Dialogue.Close(); diagonal.MovePlayer(new(float.NaN, 0), false, 1); diagonal.MovePlayer(Vector2.One, false, float.NaN);
            Check(diagonal.Player == position, "invalid movement input leaves the player in place");

            var arena = MapData.CreateEmpty(32, 32);
            arena.Monsters.Add(new() { X = .8f, MaxHealth = 150, Damage = 7 });
            var fight = new PlaySession(arena, character: definition);
            fight.UpdateCombat(.1f, true);
            Check(fight.PlayerHealth == 243 && fight.Monsters.Single().Health == 133, "configured max HP and damage affect combat");
            for (int i = 0; i < 5; i++) fight.UpdateCombat(.1f, true);
            Check(fight.Monsters.Single().Health == 133, "configured cooldown blocks repeated hits");
            fight.UpdateCombat(.1f, true);
            Check(fight.Monsters.Single().Health == 116, "configured cooldown allows the next hit");
            var distant = MapData.CreateEmpty(32, 32); distant.Monsters.Add(new() { X = 2.2f });
            var reach = new PlaySession(distant, character: definition); reach.UpdateCombat(.1f, true);
            Check(reach.Monsters.Single().Health == 13, "configured attack range reaches beyond the default range");
            var interaction = MapData.CreateEmpty(32, 32); interaction.Npcs.Add(new() { X = 4 }); interaction.Chests.Add(new() { Z = 4 });
            var talking = new PlaySession(interaction, character: definition);
            Check(talking.NearbyNpc != null && talking.OpenChest(talking.Map.Chests.Single()), "configured interaction range applies to NPCs and chests");

            fight.SaveProgress(save); string saved = File.ReadAllText(save);
            var restored = new PlaySession(arena, character: definition); restored.LoadProgress(save);
            Check(restored.PlayerHealth == 243 && restored.Monsters.Single().Health == 116, "progress roundtrip supports HP above 100");
            var rebalanced = new PlaySession(arena); rebalanced.LoadProgress(save);
            Check(rebalanced.PlayerHealth == 100, "loading after a profile change clamps health to the current maximum");
            var legacy = JsonNode.Parse(saved)!; var world = legacy["Worlds"]![restored.CurrentMapPath]!.AsObject();
            world.Remove("MaxHealth"); world["Health"] = 80; File.WriteAllText(save, legacy.ToJsonString());
            rebalanced.LoadProgress(save);
            Check(rebalanced.PlayerHealth == 80, "legacy progress without a saved HP maximum remains compatible");
            world["Health"] = 101; File.WriteAllText(save, legacy.ToJsonString());
            ExpectInvalid(() => rebalanced.LoadProgress(save));
            Check(rebalanced.PlayerHealth == 80, "invalid saved HP cannot partially overwrite a session");
            fight.Restart();
            Check(fight.PlayerHealth == 250 && fight.Monsters.Single().Health == 150 && fight.Character == definition,
                "restart retains the character profile and restores full health");
            Check(source.Serialize() == original, "character mechanics preserve authored map data");

            File.WriteAllText(path, "{\"Name\":\"Nowy bohater\"}");
            var edited = PlayerDefinition.Load(path);
            Check(edited.MaxHealth == 100 && edited.WalkSpeed == 3.6f && fight.Character.Name == "Łucja",
                "partial profiles use defaults and editing a file preserves active sessions");
            foreach (string invalid in new[] { "null", "{ broken", "{\"MaxHelth\":200}", "{\"Name\":null}", "{\"Name\":\" \"}",
                "{\"Description\":null}", "{\"MaxHealth\":0}", "{\"Damage\":-1}", "{\"WalkSpeed\":0}", "{\"RunSpeed\":1}",
                "{\"AttackRange\":0}", "{\"AttackCooldown\":0}", "{\"InteractionRange\":1000}" })
            { File.WriteAllText(path, invalid); ExpectInvalid(() => PlayerDefinition.Load(path)); }
            ExpectInvalid(() => new PlaySession(source, character: definition with { WalkSpeed = float.NaN }));
            ExpectInvalid(() => new PlaySession(source, character: definition with { AttackRange = float.PositiveInfinity }));
            File.WriteAllText(path, JsonSerializer.Serialize(definition));
            Check(PlayerDefinition.Load(path) == definition, "profile information and stats roundtrip without loss");
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine("Character checks passed: JSON information/validation, movement, diagonal speed, combat stats/cooldowns, interaction range, progress compatibility, restart and map isolation.");
    }
    static void ExpectInvalid(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid character or saved HP accepted.");
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Character check failed: " + message); }
}
