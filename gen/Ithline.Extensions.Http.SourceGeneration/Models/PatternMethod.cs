namespace Ithline.Extensions.Http.SourceGeneration.Models;

public sealed record PatternMethod
{
    public required string MethodName { get; init; }
    public required string? MethodModifiers { get; init; }
    public required PatternType ContainingType { get; init; }

    public required string RawPattern { get; init; }
    public required bool LowercaseUrls { get; init; }
    public required bool LowercaseQueryStrings { get; init; }
    public required bool AppendTrailingSlash { get; init; }

    public required EquatableArray<PatternParameter> Parameters { get; init; }
    public required EquatableArray<PatternSegment> Segments { get; init; }
    public required FragmentPatternParameter? Fragment { get; init; }
}
