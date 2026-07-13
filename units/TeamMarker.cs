using Godot;

namespace AIWarSandbox.Units;

/// <summary>Persistent faction ground ring so friend/foe reads at a glance (distinct from selection ring).</summary>
public partial class TeamMarker : MeshInstance3D
{
    public static TeamMarker For(Unit owner)
    {
        var color = owner.IsFriendly
            ? new Color(0.25f, 0.55f, 1f, 0.55f)
            : new Color(1f, 0.25f, 0.25f, 0.55f);
        float r = owner is Vehicle ? 1.3f : owner is Structure ? 2.8f : 0.7f;
        return new TeamMarker
        {
            Name = "TeamMarker",
            Mesh = new TorusMesh { InnerRadius = r * 0.85f, OuterRadius = r },
            Position = new Vector3(0, 0.05f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true,
                Emission = new Color(color.R, color.G, color.B),
                EmissionEnergyMultiplier = 0.6f,
            }
        };
    }
}
