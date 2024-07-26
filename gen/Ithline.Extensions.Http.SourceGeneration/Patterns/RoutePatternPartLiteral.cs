namespace Ithline.Extensions.Http.SourceGeneration.Patterns;

public sealed record RoutePatternPartLiteral : RoutePatternPart
{
    /// <summary>
    /// Gets the text content.
    /// </summary>
    public required string Content { get; init; }

    internal override string DebuggerToString()
    {
        return Content;
    }
}
