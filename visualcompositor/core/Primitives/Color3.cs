namespace VisualCompositor.Core.Primitives;

/// <summary>RGB color with float components in range [0, 1].</summary>
public readonly struct Color3 : IEquatable<Color3>
{
    public float R { get; init; }
    public float G { get; init; }
    public float B { get; init; }

    public Color3(float r, float g, float b) { R = r; G = g; B = b; }

    public static Color3 Black => new(0, 0, 0);
    public static Color3 White => new(1, 1, 1);

    public bool Equals(Color3 other) => R == other.R && G == other.G && B == other.B;
    public override bool Equals(object? obj) => obj is Color3 c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B);
    public static bool operator ==(Color3 left, Color3 right) => left.Equals(right);
    public static bool operator !=(Color3 left, Color3 right) => !left.Equals(right);
    public override string ToString() => $"({R}, {G}, {B})";
}
