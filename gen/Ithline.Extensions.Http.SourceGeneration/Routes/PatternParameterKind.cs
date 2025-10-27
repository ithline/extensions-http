namespace Ithline.Extensions.Http.SourceGeneration.Routes;

internal enum PatternParameterKind
{
    /// <summary>
    /// The <see cref="PatternParameterKind"/> of a standard parameter
    /// without optional or catch all behavior.
    /// </summary>
    Standard,

    /// <summary>
    /// The <see cref="PatternParameterKind"/> of an optional parameter.
    /// </summary>
    Optional,

    /// <summary>
    /// The <see cref="PatternParameterKind"/> of a catch-all parameter.
    /// </summary>
    CatchAll,
}
