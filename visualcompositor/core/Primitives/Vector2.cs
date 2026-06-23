namespace VisualCompositor.Core.Primitives;

/// <summary>2D vector with float components.</summary>
public readonly struct Vector2 : IEquatable<Vector2>
{
    public float X { get; init; }
    public float Y { get; init; }

    public Vector2(float x, float y) { X = x; Y = y; }

    public static Vector2 Zero => new(0, 0);
    public static Vector2 One => new(1, 1);

    public bool Equals(Vector2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Vector2 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(Vector2 left, Vector2 right) => left.Equals(right);
    public static bool operator !=(Vector2 left, Vector2 right) => !left.Equals(right);
    public override string ToString() => $"({X}, {Y})";
}
