namespace Ithline.Extensions.Http.SourceGeneration.Patterns;

/// <summary>
/// Represents a path segment in a route pattern. Instances of <see cref="RoutePatternPathSegment"/> are
/// immutable.
/// </summary>
/// <remarks>
/// Route patterns are made up of URL path segments, delimited by <c>/</c>. A
/// <see cref="RoutePatternPathSegment"/> contains a group of
/// <see cref="RoutePatternPart"/> that represent the structure of a segment
/// in a route pattern.
/// </remarks>
public sealed record RoutePatternPathSegment
{
    /// <summary>
    /// Gets the list of parts in this segment.
    /// </summary>
    public required EquatableArray<RoutePatternPart> Parts { get; init; }

    /// <summary>
    /// Returns <c>true</c> if the segment contains a single part;
    /// otherwise returns <c>false</c>.
    /// </summary>
    public bool IsSimple => Parts.Count == 1;

    internal string DebuggerToString()
    {
        return DebuggerToString(Parts);
    }

    internal static string DebuggerToString(IEnumerable<RoutePatternPart> parts)
    {
        return string.Join(string.Empty, parts.Select(p => p.DebuggerToString()));
    }
}
