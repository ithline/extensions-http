namespace Ithline.Extensions.Http.SourceGeneration.Models;

public sealed record PatternType
{
    public required string? Namespace { get; init; }
    public required string TypeName { get; init; }
    public required string Keyword { get; init; }
    public required PatternType? Parent { get; init; }
}
