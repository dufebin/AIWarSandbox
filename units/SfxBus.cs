using Godot;

namespace AIWarSandbox.Units;

/// <summary>
/// Lightweight procedural SFX via short generated tones (no external assets).
/// Called from Unit.Die / Weapon.Fire / UnitCommandController; unique; no data I/O.
/// </summary>
public static class SfxBus
{
    public enum Kind { Fire, Hit, Select, Death, Victory, Defeat }

    public static void Play(Node host, Kind kind)
    {
        if (host == null || host.GetTree() == null) return;
        float freq = kind switch
        {
            Kind.Fire => 880f,
            Kind.Hit => 220f,
            Kind.Select => 660f,
            Kind.Death => 110f,
            Kind.Victory => 523f,
            Kind.Defeat => 165f,
            _ => 440f
        };
        float dur = kind is Kind.Victory or Kind.Defeat ? 0.35f : 0.08f;
        var player = new AudioStreamPlayer3D
        {
            Name = "SfxBlip",
            Stream = MakeTone(freq, dur),
            VolumeDb = -8f,
            MaxDistance = 80f,
        };
        if (host is Node3D n3)
        {
            host.GetTree().CurrentScene?.AddChild(player);
            player.GlobalPosition = n3.GlobalPosition;
        }
        else
        {
            host.AddChild(player);
        }
        player.Play();
        FreeAfter(player, dur + 0.15f);
    }

    private static AudioStreamWav MakeTone(float freq, float seconds)
    {
        const int sampleRate = 22050;
        int count = Mathf.Max(64, (int)(sampleRate * seconds));
        var data = new byte[count * 2];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = 1f - t / seconds;
            short s = (short)(Mathf.Sin(Mathf.Tau * freq * t) * env * 12000f);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav
        {
            Data = data,
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Stereo = false,
        };
    }

    private static async void FreeAfter(AudioStreamPlayer3D p, float sec)
    {
        var tree = p.GetTree();
        if (tree == null) { p.QueueFree(); return; }
        float t = 0f;
        while (t < sec && GodotObject.IsInstanceValid(p))
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            t += (float)tree.Root.GetProcessDeltaTime();
        }
        if (GodotObject.IsInstanceValid(p)) p.QueueFree();
    }
}
