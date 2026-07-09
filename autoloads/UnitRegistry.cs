using System.Collections.Generic;
using Godot;
using AIWarSandbox.Units;

namespace AIWarSandbox.Autoloads;

public partial class UnitRegistry : Node
{
    public static UnitRegistry Instance { get; private set; } = null!;

    private readonly List<Unit> _units = new();
    private readonly List<Unit> _friendly = new();
    private readonly List<Unit> _enemy = new();

    public IReadOnlyList<Unit> All => _units;
    public IReadOnlyList<Unit> Friendly => _friendly;
    public IReadOnlyList<Unit> Enemy => _enemy;

    public override void _Ready()
    {
        Instance = this;
    }

    public void Register(Unit unit)
    {
        if (_units.Contains(unit)) return;
        _units.Add(unit);
        (unit.IsFriendly ? _friendly : _enemy).Add(unit);
    }

    public void Unregister(Unit unit)
    {
        _units.Remove(unit);
        _friendly.Remove(unit);
        _enemy.Remove(unit);
    }

    public void Clear()
    {
        _units.Clear();
        _friendly.Clear();
        _enemy.Clear();
    }
}
