namespace VisualCompositor.Core.Model;

/// <summary>A timeline marker (bookmark/point of interest).</summary>
public sealed class Marker
{
    public string Id { get; set; } = string.Empty;

    public double Time { get; set; }

    public string Label { get; set; } = string.Empty;

    public string? Color { get; set; }

    public Marker Clone() => new()
    {
        Id = Id,
        Time = Time,
        Label = Label,
        Color = Color,
    };
}
