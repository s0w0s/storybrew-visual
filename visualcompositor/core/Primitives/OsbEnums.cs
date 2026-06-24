namespace VisualCompositor.Core.Primitives;

public enum OsbLayer
{
    Background,
    Fail,
    Pass,
    Foreground,
    Overlay,
}

public enum OsbOrigin
{
    TopLeft,
    TopCentre,
    TopRight,
    CentreLeft,
    Centre,
    CentreRight,
    BottomLeft,
    BottomCentre,
    BottomRight,
}

public enum OsbLoopType
{
    LoopForever,
    LoopOnce,
}

public enum OsbEasing
{
    None,
    Out,
    In,
    InQuad,
    OutQuad,
    InOutQuad,
    InCubic,
    OutCubic,
    InOutCubic,
    InQuart,
    OutQuart,
    InOutQuart,
    InQuint,
    OutQuint,
    InOutQuint,
    InSine,
    OutSine,
    InOutSine,
    InExpo,
    OutExpo,
    InOutExpo,
    InCirc,
    OutCirc,
    InOutCirc,
    InElastic,
    OutElastic,
    OutElasticHalf,
    OutElasticQuarter,
    InOutElastic,
    InBack,
    OutBack,
    InOutBack,
    InBounce,
    OutBounce,
    InOutBounce,
}

public enum ParameterType
{
    None,
    FlipHorizontal,
    FlipVertical,
    AdditiveBlending,
}
