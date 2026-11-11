using System.Numerics;

sealed record FragmentPasteOptions
{
    public bool Terrain { get; set; } = true;
    public bool Actors { get; set; } = true;
    public bool Roads { get; set; } = true;
    public bool Walls { get; set; } = true;
    public bool WorldFeatures { get; set; } = true;
    public bool ReplaceContents { get; set; }
    public bool AlignWater { get; set; } = true;
    public float HeightOffset { get; set; }
    public float ElevationShift(MapFragment fragment, MapData map) => MapData.SnapElevation(HeightOffset +
        (Terrain && AlignWater && fragment.HasWater ? map.WaterLevel - fragment.WaterLevel : 0));
}

sealed record FragmentPasteResult(SceneObject[] Actors, int DisabledTriggers, bool Changed);

sealed partial class MapDocument
{
    public static string? FragmentPasteIssue(MapData map, MapFragment fragment, MapRegion region, FragmentPasteOptions options)
    {
        if (!options.Terrain && !options.Actors && !options.Roads && !options.Walls && !options.WorldFeatures) return "Choose at least one category to paste.";
        if (region.Width != fragment.Width || region.Depth != fragment.Depth || !region.Fits(map)) return "The whole fragment must fit inside the map.";
        if (!MapData.FiniteRange(options.HeightOffset, -96, 96)) return "Invalid fragment height offset.";
        float shift = options.ElevationShift(fragment, map);
        if (options.Terrain && (fragment.MinimumHeight + shift < MapData.MinElevation || fragment.MaximumHeight + shift > MapData.MaxElevation))
            return "This height offset would exceed the terrain height limits.";
        var bounds = region.WorldBounds(map);
        int Retained<T>(IEnumerable<T> entries, Func<T, float> x, Func<T, float> z, bool selected) =>
            options.ReplaceContents && selected ? entries.Count(e => !bounds.Contains(x(e), z(e))) : entries.Count();
        if (options.Actors && (Retained(map.Objects, e => e.X, e => e.Z, true) + fragment.Actors.Objects.Count > 10000 ||
            Retained(map.Npcs, e => e.X, e => e.Z, true) + fragment.Actors.Npcs.Count > 1000 ||
            Retained(map.Chests, e => e.X, e => e.Z, true) + fragment.Actors.Chests.Count > 1000 ||
            Retained(map.Monsters, e => e.X, e => e.Z, true) + fragment.Actors.Monsters.Count > 1000)) return "The fragment would exceed the map's actor limits.";
        if (options.WorldFeatures && (Retained(map.Lights, e => e.X, e => e.Z, true) + fragment.Lights.Count > 256 ||
            Retained(map.Sounds, e => e.X, e => e.Z, true) + fragment.Sounds.Count > 256 ||
            Retained(map.Triggers, e => e.X, e => e.Z, true) + fragment.Triggers.Count > 1000)) return "The fragment would exceed the map's world-feature limits.";
        if (options.Roads && !options.ReplaceContents && map.Roads.Count + fragment.Roads.Count > 128) return "The fragment would exceed the road limit (128).";
        if (options.Walls && !options.ReplaceContents && map.Walls.Count + fragment.Walls.Count > WallData.Limit) return "The fragment would exceed the wall limit (256).";
        return null;
    }
    public FragmentPasteResult PasteFragment(MapFragment fragment, MapRegion region, FragmentPasteOptions options)
    {
        fragment.Validate();
        if (FragmentPasteIssue(Map, fragment, region, options) is { } issue) throw new InvalidDataException(issue);
        var bounds = region.WorldBounds(Map); float shift = options.ElevationShift(fragment, Map);
        bool Replace(bool selected) => options.ReplaceContents && selected;
        List<T> Keep<T>(IEnumerable<T> entries, Func<T, float> x, Func<T, float> z, bool selected) =>
            entries.Where(e => !Replace(selected) || !bounds.Contains(x(e), z(e))).ToList();
        var metadata = new MapData
        {
            Width = Map.Width, Depth = Map.Depth, Tiles = Map.Tiles, Type = Map.Type, Player = Map.Player, Runtime = Map.Runtime, Environment = Map.Environment,
            WaterLevel = Map.WaterLevel, WaveHeight = Map.WaveHeight,
            Objects = Keep(Map.Objects, e => e.X, e => e.Z, options.Actors), Npcs = Keep(Map.Npcs, e => e.X, e => e.Z, options.Actors),
            Chests = Keep(Map.Chests, e => e.X, e => e.Z, options.Actors), Monsters = Keep(Map.Monsters, e => e.X, e => e.Z, options.Actors),
            Lights = Keep(Map.Lights, e => e.X, e => e.Z, options.WorldFeatures), Sounds = Keep(Map.Sounds, e => e.X, e => e.Z, options.WorldFeatures),
            Triggers = Keep(Map.Triggers, e => e.X, e => e.Z, options.WorldFeatures),
            Roads = Replace(options.Roads) ? FragmentRoads.Outside(Map.Roads, bounds, new(-Map.Width / 2f, -Map.Depth / 2f, Map.Width / 2f, Map.Depth / 2f)) : Map.Roads.ToList(),
            Walls = Replace(options.Walls) ? FragmentWalls.Outside(Map.Walls, bounds, new(-Map.Width / 2f, -Map.Depth / 2f, Map.Width / 2f, Map.Depth / 2f)) : Map.Walls.ToList(),
            WallCells = Map.WallCells.Where(c => !Replace(options.Walls) || c.X < region.X || c.X >= region.X + region.Width || c.Z < region.Z || c.Z >= region.Z + region.Depth).ToList()
        };
        var placed = options.Actors ? ScenePrefab.Clone(fragment.Actors) : new ScenePrefab();
        var ids = new Dictionary<string, string>();
        foreach (var entity in placed.Entities)
        {
            string previous = entity.Id; entity.Id = Guid.NewGuid().ToString("N"); ids.Add(previous, entity.Id);
            entity.X += bounds.MinX; entity.Z += bounds.MinZ;
            if (entity is ChestData chest) foreach (var item in chest.Contents.Items) item.Id = Guid.NewGuid().ToString("N");
        }
        metadata.Objects.AddRange(placed.Objects); metadata.Npcs.AddRange(placed.Npcs); metadata.Chests.AddRange(placed.Chests); metadata.Monsters.AddRange(placed.Monsters);
        if (options.Roads)
            foreach (var road in fragment.Roads)
            {
                var copy = ScenePrefab.Clone(road); copy.Id = Guid.NewGuid().ToString("N");
                copy.Points = copy.Points.Select(p => new GroundPoint2(p.X + bounds.MinX, p.Z + bounds.MinZ)).ToList(); metadata.Roads.Add(copy);
            }
        if (options.Walls)
            foreach (var wall in fragment.Walls)
            {
                var copy = ScenePrefab.Clone(wall); copy.Id = Guid.NewGuid().ToString("N");
                copy.Points = copy.Points.Select(p => new GroundPoint2(p.X + bounds.MinX, p.Z + bounds.MinZ)).ToList(); metadata.Walls.Add(copy);
            }
        int disabled = 0;
        if (options.Walls)
        {
            var cells = metadata.WallCells.ToDictionary(c => (c.X, c.Z));
            foreach (var c in fragment.WallCells) cells[(c.X + region.X, c.Z + region.Z)] = c.Copy(c.X + region.X, c.Z + region.Z);
            metadata.WallCells = cells.Values.OrderBy(c => c.Z).ThenBy(c => c.X).ToList();
        }
        if (options.WorldFeatures)
        {
            foreach (var light in fragment.Lights)
            { var copy = ScenePrefab.Clone(light); copy.Id = Guid.NewGuid().ToString("N"); copy.X += bounds.MinX; copy.Z += bounds.MinZ; metadata.Lights.Add(copy); }
            foreach (var sound in fragment.Sounds)
            { var copy = ScenePrefab.Clone(sound); copy.Id = Guid.NewGuid().ToString("N"); copy.X += bounds.MinX; copy.Z += bounds.MinZ; metadata.Sounds.Add(copy); }
            var targets = metadata.SceneEntities.ToDictionary(e => e.Id);
            foreach (var trigger in fragment.Triggers)
            {
                var copy = ScenePrefab.Clone(trigger.Data); copy.Id = Guid.NewGuid().ToString("N"); copy.MonsterTemplate.Id = Guid.NewGuid().ToString("N");
                copy.X += bounds.MinX; copy.Z += bounds.MinZ;
                if (ids.TryGetValue(copy.TargetId, out var target)) copy.TargetId = target;
                bool invalid = copy.Action == TriggerAction.Dialogue && (!targets.TryGetValue(copy.TargetId, out var npc) || npc is not NpcData) ||
                    copy.Action == TriggerAction.ToggleDoor && (!targets.TryGetValue(copy.TargetId, out var door) || !door.IsDoor);
                if (trigger.LocalDestination) { copy.DestinationX += bounds.MinX; copy.DestinationZ += bounds.MinZ; }
                if (copy.Action is TriggerAction.Teleport or TriggerAction.SpawnMonster && !Map.Contains(copy.DestinationX, copy.DestinationZ))
                {
                    var point = Map.Clamp(new(copy.DestinationX, 0, copy.DestinationZ)); copy.DestinationX = point.X; copy.DestinationZ = point.Z; invalid = true;
                }
                if (invalid && copy.Enabled) { copy.Enabled = false; disabled++; }
                metadata.Triggers.Add(copy);
            }
        }
        // Validate the complete proposed contents before touching terrain or the undo history.
        metadata.Validate();
        bool changed = metadata.SerializeMetadata() != Map.SerializeMetadata();
        if (options.Terrain)
        {
            for (int z = 0; z < region.Depth && !changed; z++)
            for (int x = 0; x < region.Width && !changed; x++)
            {
                int source = z * fragment.Width + x, destination = (region.Z + z) * Map.Width + region.X + x;
                changed = Map.Tiles[destination] != fragment.Tiles[source] || Map.WaterMask(destination) != fragment.WaterMask(source);
            }
            for (int z = 0; z <= region.Depth && !changed; z++)
            for (int x = 0; x <= region.Width && !changed; x++)
                changed = Map.VertexHeight(region.X + x, region.Z + z) != fragment.VertexHeight(x, z) + shift;
        }
        if (!changed) return new([], 0, false);
        Checkpoint();
        if (options.Terrain)
        {
            // Freeze legacy implicit water masks before replacing any material IDs.
            if (Map.WaterCells.Length == 0 && (fragment.HasWater || fragment.Tiles.Contains("water") || Map.Tiles.Contains("water")))
            { var water = Map.CopyWaterCells(); Map.WaterCells = water.Length == 0 ? new byte[Map.Tiles.Length] : water; }
            for (int z = 0; z < region.Depth; z++)
            for (int x = 0; x < region.Width; x++)
            {
                int source = z * fragment.Width + x, destination = (region.Z + z) * Map.Width + region.X + x;
                ChangeWater(destination, fragment.WaterMask(source));
                Paint(new(region.X + x + .5f - Map.Width / 2f, 0, region.Z + z + .5f - Map.Depth / 2f), 0, fragment.Tiles[source]);
            }
            for (int z = 0; z <= region.Depth; z++)
            for (int x = 0; x <= region.Width; x++) ChangeHeight(region.X + x, region.Z + z, fragment.VertexHeight(x, z) + shift);
        }
        Map.Objects = metadata.Objects; Map.Npcs = metadata.Npcs; Map.Chests = metadata.Chests; Map.Monsters = metadata.Monsters;
        Map.Lights = metadata.Lights; Map.Sounds = metadata.Sounds; Map.Triggers = metadata.Triggers; Map.Roads = metadata.Roads; Map.Walls = metadata.Walls; Map.WallCells = metadata.WallCells;
        return new(placed.Entities.ToArray(), disabled, true);
    }
}
