using Godot;

namespace AIWarSandbox.Units;

/// <summary>Lightweight combat VFX helpers (muzzle flash, hit spark, command ping).</summary>
public static class CombatFx
{
    public static void MuzzleFlash(Node parent, Vector3 pos)
    {
        if (parent == null) return;
        var flash = new MeshInstance3D
        {
            Name = "MuzzleFlash",
            Mesh = new SphereMesh { Radius = 0.35f, Height = 0.7f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.85f, 0.3f),
                EmissionEnabled = true,
                Emission = new Color(1f, 0.7f, 0.2f),
                EmissionEnergyMultiplier = 3f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            }
        };
        parent.AddChild(flash);
        flash.GlobalPosition = pos;
        FadeAndFree(flash, 0.12f);
    }

    public static void HitSpark(Node parent, Vector3 pos)
    {
        if (parent == null) return;
        var spark = new MeshInstance3D
        {
            Name = "HitSpark",
            Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.4f, 0.1f),
                EmissionEnabled = true,
                Emission = new Color(1f, 0.3f, 0.05f),
                EmissionEnergyMultiplier = 2.5f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            }
        };
        parent.AddChild(spark);
        spark.GlobalPosition = pos;
        FadeAndFree(spark, 0.2f);
    }

    public static void CommandPing(Node parent, Vector3 pos, Color color)
    {
        if (parent == null) return;
        var ring = new MeshInstance3D
        {
            Name = "CmdPing",
            Mesh = new TorusMesh { InnerRadius = 0.6f, OuterRadius = 1.0f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                EmissionEnabled = true,
                Emission = color,
                EmissionEnergyMultiplier = 1.5f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            }
        };
        parent.AddChild(ring);
        ring.GlobalPosition = pos + new Vector3(0, 0.15f, 0);
        FadeAndFree(ring, 0.7f, expand: true);
    }

    private static async void FadeAndFree(MeshInstance3D mi, float life, bool expand = false)
    {
        float t = 0f;
        var tree = mi.GetTree();
        if (tree == null) { mi.QueueFree(); return; }
        while (t < life && GodotObject.IsInstanceValid(mi))
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            float dt = (float)tree.Root.GetProcessDeltaTime();
            t += dt;
            float a = 1f - t / life;
            if (mi.MaterialOverride is StandardMaterial3D mat)
            {
                var c = mat.AlbedoColor;
                c.A = Mathf.Max(0f, a);
                mat.AlbedoColor = c;
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            }
            if (expand)
                mi.Scale = Vector3.One * (1f + t * 2f);
        }
        if (GodotObject.IsInstanceValid(mi)) mi.QueueFree();
    }
}
