namespace VisualCompositor.Audio.Model;

/// <summary>Hit sound additions (whistle/finish/clap) layered on top of the normal hit sound.</summary>
[Flags]
public enum HitSoundAddition
{
    None = 0,
    Normal = 1,
    Whistle = 2,
    Finish = 4,
    Clap = 8,
}
