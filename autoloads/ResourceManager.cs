using Godot;

namespace AIWarSandbox.Autoloads;

public partial class ResourceManager : Node
{
    public static ResourceManager Instance { get; private set; } = null!;

    public int Manpower { get; private set; } = 100;
    public int Supplies { get; private set; } = 100;

    public override void _Ready()
    {
        Instance = this;
    }

    public bool TrySpend(int manpowerCost, int suppliesCost)
    {
        if (Manpower < manpowerCost || Supplies < suppliesCost) return false;
        Manpower -= manpowerCost;
        Supplies -= suppliesCost;
        return true;
    }

    public void Reset(int manpower, int supplies)
    {
        Manpower = manpower;
        Supplies = supplies;
    }
}
