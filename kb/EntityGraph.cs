using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AIWarSandbox.Kb;

public partial class EntityGraph : Node
{
    public static EntityGraph Instance { get; private set; } = null!;

    private readonly Dictionary<int, TacticalEntity> _entities = new();
    private readonly List<int> _pendingRemoval = new();

    public IReadOnlyDictionary<int, TacticalEntity> All => _entities;

    public override void _Ready()
    {
        Instance = this;
    }

    public TacticalEntity Register(Node godotNode, EntityType type, string name, bool isFriendly)
    {
        int id = (int)godotNode.GetInstanceId();
        if (_entities.TryGetValue(id, out var existing))
            return existing;

        var pos = godotNode is Node3D n3 ? n3.GlobalPosition : Vector3.Zero;
        var entity = new TacticalEntity(id, type, name, pos, isFriendly, godotNode);
        _entities[id] = entity;
        return entity;
    }

    public void Unregister(int entityId)
    {
        _entities.Remove(entityId);
    }

    public TacticalEntity? Get(int id)
    {
        return _entities.TryGetValue(id, out var e) ? e : null;
    }

    public List<TacticalEntity> Query(Func<TacticalEntity, bool> predicate)
    {
        return _entities.Values.Where(predicate).ToList();
    }

    public List<TacticalEntity> NearestHostile(Vector3 pos, float maxDist)
    {
        float maxSqr = maxDist * maxDist;
        var result = new List<TacticalEntity>();
        foreach (var e in _entities.Values)
        {
            if (e.IsFriendly) continue;
            if (e.Position.DistanceSquaredTo(pos) <= maxSqr)
                result.Add(e);
        }
        return result;
    }

    public List<TacticalEntity> ByType(EntityType type)
    {
        var result = new List<TacticalEntity>();
        foreach (var e in _entities.Values)
            if (e.Type == type)
                result.Add(e);
        return result;
    }

    public void AddRelation(int from, EntityRelation rel, int to)
    {
        if (_entities.TryGetValue(from, out var src))
            src.AddRelation(rel, to);
    }

    public void RemoveRelation(int from, EntityRelation rel, int to)
    {
        if (_entities.TryGetValue(from, out var src))
            src.RemoveRelation(rel, to);
    }

    public override void _Process(double delta)
    {
        _pendingRemoval.Clear();
        foreach (var kv in _entities)
        {
            var entity = kv.Value;
            if (entity.GodotNode.TryGetTarget(out var node))
            {
                if (GodotObject.IsInstanceValid(node) && node is Node3D n3)
                    entity.Position = n3.GlobalPosition;
            }
            else
            {
                _pendingRemoval.Add(kv.Key);
            }
        }
        foreach (var id in _pendingRemoval)
            _entities.Remove(id);
    }
}
