namespace VisualCompositor.Core.Validation;

public enum DiagnosticSeverity
{
    HardError,
    Warning,
}

public sealed class Diagnostic
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DiagnosticSeverity Severity { get; init; }
    public string? Scope { get; init; }

    public override string ToString() => $"[{Severity}] {Code}: {Message}";
}

public sealed class DiagnosticCollection
{
    private readonly List<Diagnostic> _diagnostics = new();
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
    public bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.HardError);
    public bool HasWarnings => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);

    public void Add(Diagnostic diagnostic) => _diagnostics.Add(diagnostic);
    public void AddRange(IEnumerable<Diagnostic> diagnostics) => _diagnostics.AddRange(diagnostics);
    public void Clear() => _diagnostics.Clear();
}
