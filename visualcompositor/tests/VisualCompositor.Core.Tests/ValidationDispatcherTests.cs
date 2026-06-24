using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Validation;
using Xunit;

namespace VisualCompositor.Core.Tests;

public class ValidationDispatcherTests
{
    [Fact]
    public void PreOsbExport_OnlyRunsDocumentStateValidators()
    {
        var scopes = ValidationDispatcher.GetScopesForEntryPoint(ValidationEntryPoint.PreOsbExport);
        Assert.Contains(ValidationScope.DocumentState, scopes);
        Assert.DoesNotContain(ValidationScope.TransactionHistory, scopes);
        Assert.DoesNotContain(ValidationScope.Serialization, scopes);
    }

    [Fact]
    public void PreStorybrewCompSave_RunsAllScopes()
    {
        var scopes = ValidationDispatcher.GetScopesForEntryPoint(ValidationEntryPoint.PreStorybrewCompSave);
        Assert.Contains(ValidationScope.DocumentState, scopes);
        Assert.Contains(ValidationScope.TransactionHistory, scopes);
        Assert.Contains(ValidationScope.Serialization, scopes);
    }

    [Fact]
    public void DebugIntegrityScan_RunsAllScopes()
    {
        var scopes = ValidationDispatcher.GetScopesForEntryPoint(ValidationEntryPoint.DebugIntegrityScan);
        Assert.Contains(ValidationScope.DocumentState, scopes);
        Assert.Contains(ValidationScope.TransactionHistory, scopes);
        Assert.Contains(ValidationScope.Serialization, scopes);
    }

    [Fact]
    public void Create_RunsDocumentAndTransaction()
    {
        var scopes = ValidationDispatcher.GetScopesForEntryPoint(ValidationEntryPoint.Create);
        Assert.Contains(ValidationScope.DocumentState, scopes);
        Assert.Contains(ValidationScope.TransactionHistory, scopes);
        Assert.DoesNotContain(ValidationScope.Serialization, scopes);
    }

    [Fact]
    public void DeserializeLoad_RunsDocumentAndSerialization()
    {
        var scopes = ValidationDispatcher.GetScopesForEntryPoint(ValidationEntryPoint.DeserializeLoad);
        Assert.Contains(ValidationScope.DocumentState, scopes);
        Assert.Contains(ValidationScope.Serialization, scopes);
        Assert.DoesNotContain(ValidationScope.TransactionHistory, scopes);
    }

    [Fact]
    public void Validate_PreOsbExport_DetectsDocumentErrors()
    {
        var doc = new CompositionDocument();
        doc.Layers.Add(new Layer { Id = "", Name = "test", OsbLayer = OsbLayer.Background }); // empty id
        var diagnostics = ValidationDispatcher.Validate(doc, ValidationEntryPoint.PreOsbExport);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void Validate_ValidDocument_NoErrors()
    {
        var doc = new CompositionDocument();
        doc.Layers.Add(new Layer { Id = "layer_0", Name = "test", OsbLayer = OsbLayer.Background });
        var diagnostics = ValidationDispatcher.Validate(doc, ValidationEntryPoint.PreOsbExport);
        Assert.False(diagnostics.HasErrors);
    }
}
