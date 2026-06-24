using VisualCompositor.Core.Model.Overrides;

namespace VisualCompositor.State.Actions;

public record SetVisualOverrideAction(VisualOverride Override) : EditorAction("Set Visual Override");
public record RemoveVisualOverrideAction(string OverrideId) : EditorAction("Remove Visual Override");
public record SetDiffVisibilityAction(DiffVisibility Visibility) : EditorAction("Set Diff Visibility");
public record RemoveDiffVisibilityAction(string LayerId) : EditorAction("Remove Diff Visibility");
