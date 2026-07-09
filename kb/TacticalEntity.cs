using System;
using System.Collections.Generic;
using Godot;

namespace AIWarSandbox.Kb;

public enum EntityType
{
    Platform,
    Sensor,
    Weapon,
    Structure,
    SupplyPoint,
    TerrainFeature
}

public enum EntityRelation
{
    Observes,
    Targets,
    Threatens,
    Supplies,
    Supports,
    HostileTo
}

public class TacticalEntity
{
    public int Id { get; }
    public EntityType Type { get; }
    public string Name { get; set; }
    public Vector3 Position { get; set; }
    public bool IsFriendly { get; set; }
    public Dictionary<EntityRelation, List<int>> Relations { get; } = new();
    public WeakReference<Node> GodotNode { get; }

    public TacticalEntity(int id, EntityType type, string name, Vector3 position, bool isFriendly, Node? godotNode)
    {
        Id = id;
        Type = type;
        Name = name;
        Position = position;
        IsFriendly = isFriendly;
        GodotNode = new WeakReference<Node>(godotNode!);
    }

    public void AddRelation(EntityRelation rel, int otherId)
    {
        if (!Relations.TryGetValue(rel, out var list))
        {
            list = new List<int>();
            Relations[rel] = list;
        }
        if (!list.Contains(otherId))
            list.Add(otherId);
    }

    public void RemoveRelation(EntityRelation rel, int otherId)
    {
        if (Relations.TryGetValue(rel, out var list))
        {
            list.Remove(otherId);
            if (list.Count == 0)
                Relations.Remove(rel);
        }
    }

    public IReadOnlyList<int> GetRelations(EntityRelation rel)
    {
        return Relations.TryGetValue(rel, out var list)
            ? list
            : System.Array.Empty<int>();
    }

    public bool HasRelation(EntityRelation rel, int otherId)
    {
        return Relations.TryGetValue(rel, out var list) && list.Contains(otherId);
    }
}
