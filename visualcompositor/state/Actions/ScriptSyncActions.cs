using VisualCompositor.Core.Model.ScriptSync;

namespace VisualCompositor.State.Actions;

public record RegisterScriptAction(ScriptSource Source) : EditorAction("Register Script");
public record RemoveScriptAction(string ScriptId) : EditorAction("Remove Script");
public record SetProvenanceAction(ScriptProvenance Provenance) : EditorAction("Set Provenance");
public record ClearProvenanceAction(string EntityId) : EditorAction("Clear Provenance");
public record MarkEntityModifiedAction(string EntityId) : EditorAction("Mark Entity Modified");
