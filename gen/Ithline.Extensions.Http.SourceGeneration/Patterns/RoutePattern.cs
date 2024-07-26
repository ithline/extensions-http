namespace Ithline.Extensions.Http.SourceGeneration.Patterns;

public sealed record RoutePattern
{
    /// <summary>
    /// Gets the raw text supplied when parsing the route pattern. May be null.
    /// </summary>
    public required string RawText { get; init; }

    /// <summary>
    /// Gets the list of route parameters.
    /// </summary>
    public required EquatableArray<RoutePatternPartParameter> Parameters { get; init; }

    /// <summary>
    /// Gets the list of path segments.
    /// </summary>
    public required EquatableArray<RoutePatternPathSegment> PathSegments { get; init; }

    /// <summary>
    /// Creates a new instance of <see cref="RoutePattern"/> from a collection of segments.
    /// </summary>
    /// <param name="rawText">The raw text to associate with the route pattern. May be null.</param>
    /// <param name="segments">The collection of segments.</param>
    /// <returns>The <see cref="RoutePattern"/>.</returns>
    public static RoutePattern Create(string rawText, IEnumerable<RoutePatternPathSegment> segments)
    {
        List<RoutePatternPartParameter>? parameters = null;
        var updatedSegments = segments.ToArray();
        for (var i = 0; i < updatedSegments.Length; i++)
        {
            var segment = updatedSegments[i];
            for (var j = 0; j < segment.Parts.Count; j++)
            {
                if (segment.Parts[j] is RoutePatternPartParameter parameter)
                {
                    parameters ??= [];
                    parameters.Add(parameter);
                }
            }
        }

        return new RoutePattern
        {
            RawText = rawText,
            Parameters = parameters is null ? [] : [.. parameters],
            PathSegments = [.. updatedSegments],
        };
    }

    /// <summary>
    /// Gets the parameter matching the given name.
    /// </summary>
    /// <param name="name">The name of the parameter to match.</param>
    /// <returns>The matching parameter or <c>null</c> if no parameter matches the given name.</returns>
    public RoutePatternPartParameter? GetParameter(string name)
    {
        foreach (var parameter in Parameters.AsSpan())
        {
            if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        return null;
    }
}
