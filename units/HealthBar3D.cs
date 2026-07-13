using Godot;

namespace AIWarSandbox.Units;

/// <summary>
/// A lightweight billboarded health bar that floats above a <see cref="Unit"/>.
/// Two unshaded quads (dark background + colored fill) always face the camera.
/// The fill width tracks Health/MaxHealth and recolors green→yellow→red.
/// Added by <see cref="Unit"/> in its <c>_Ready</c>; polls the owner each frame.
/// </summary>
public partial class HealthBar3D : Node3D
{
    private Unit _owner = null!;
    private MeshInstance3D _fill = null!;
    private StandardMaterial3D _fillMat = null!;
    private const float Width = 1.4f;
    private const float Height = 0.18f;

    public static HealthBar3D For(Unit owner, float yOffset)
    {
        var bar = new HealthBar3D { _owner = owner, Name = "HealthBar", Position = new Vector3(0, yOffset, 0) };
        return bar;
    }

    public override void _Ready()
    {
        var bg = new MeshInstance3D
        {
            Name = "Bg",
            Mesh = new QuadMesh { Size = new Vector2(Width + 0.08f, Height + 0.06f) },
            MaterialOverride = MakeMat(new Color(0.05f, 0.05f, 0.05f, 0.85f)),
        };
        AddChild(bg);

        _fillMat = MakeMat(new Color(0.2f, 0.9f, 0.3f));
        _fill = new MeshInstance3D
        {
            Name = "Fill",
            Mesh = new QuadMesh { Size = new Vector2(Width, Height) },
            MaterialOverride = _fillMat,
            Position = new Vector3(0, 0, 0.001f), // avoid z-fighting with bg
        };
        AddChild(_fill);
    }

    private static StandardMaterial3D MakeMat(Color c) => new()
    {
        AlbedoColor = c,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        Transparency = c.A < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        NoDepthTest = false,
    };

    public override void _Process(double delta)
    {
        if (_owner == null || !IsInstanceValid(_owner) || _owner.State == UnitState.Dead)
        {
            Visible = false;
            return;
        }

        float ratio = _owner.MaxHealth > 0 ? Mathf.Clamp((float)_owner.Health / _owner.MaxHealth, 0f, 1f) : 0f;
        Visible = ratio < 0.999f; // only show once damaged, to reduce clutter

        // Shrink from the left: scale on X then shift so the left edge stays put.
        _fill.Scale = new Vector3(Mathf.Max(0.001f, ratio), 1f, 1f);
        _fill.Position = new Vector3(-(Width * (1f - ratio)) * 0.5f, 0f, 0.001f);

        _fillMat.AlbedoColor = ratio > 0.5f
            ? new Color(0.2f, 0.9f, 0.3f)
            : ratio > 0.25f ? new Color(0.95f, 0.8f, 0.2f) : new Color(0.95f, 0.25f, 0.2f);
    }
}
