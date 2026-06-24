namespace VisualCompositor.Core.Primitives;

/// <summary>How an open-ended segment (null end time) extends.</summary>
public enum OpenEndedMode
{
    ExplicitEnd,
    UntilLayerEnd,
    UntilCompositionEnd,
}

/// <summary>Block edit access level.</summary>
public enum BlockEditAccess
{
    ReadOnly,
    BlockEditable,
    CommandEditable,
}

/// <summary>Block materialization state.</summary>
public enum BlockMaterializationState
{
    PreservedBlock,
    ExpandedVirtual,
    ExpandedToKeyframes,
}

/// <summary>CommandRecord lifecycle.</summary>
public enum CommandRecordLifecycle
{
    ImportedUnchanged,
    Modified,
    Split,
    Merged,
    Deleted,
    Created,
}

/// <summary>RawBlock anchor kind.</summary>
public enum RawBlockAnchorKind
{
    LayerStart,
    AfterCommand,
}

/// <summary>Bezier curve export mode.</summary>
public enum BezierExportMode
{
    WarnOnly,
    FitNearestOsuEasing,
    BakeToSegments,
}

/// <summary>Render quality preset.</summary>
public enum RenderQuality
{
    FullPreview,
    InteractiveScrub,
    FastScrub,
    TimelineThumbnail,
}
