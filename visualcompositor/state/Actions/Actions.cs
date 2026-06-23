using VisualCompositor.Core.Primitives;

namespace VisualCompositor.State.Actions;

public abstract record EditorAction(string Description);

public record SetCurrentTimeAction(double Time) : EditorAction("Set Time");
public record SetSelectionAction(List<string> SpriteIds, List<string> LayerIds) : EditorAction("Set Selection");
public record SetViewportAction(double Zoom, Vector2 Pan) : EditorAction("Set Viewport");
public record LoadDocumentAction(VisualCompositor.Core.Model.CompositionDocument Document) : EditorAction("Load Document");
public record EditKeyframeAction(string LayerId, string SpriteId, string PropertyName, int KeyframeIndex, double Time, string ValueJson) : EditorAction("Edit Keyframe");
public record AddKeyframeAction(string LayerId, string SpriteId, string PropertyName, double Time, string ValueJson, OsbEasing Easing) : EditorAction("Add Keyframe");
public record RemoveKeyframeAction(string LayerId, string SpriteId, string PropertyName, int KeyframeIndex) : EditorAction("Remove Keyframe");
public record MoveBlockAction(string LayerId, string BlockId, int NewIndex) : EditorAction("Move Block");
public record RemoveBlockAction(string LayerId, string BlockId) : EditorAction("Remove Block");
public record ExpandBlockAction(string LayerId, string BlockId) : EditorAction("Expand Block");
public record UndoAction() : EditorAction("Undo");
public record RedoAction() : EditorAction("Redo");
