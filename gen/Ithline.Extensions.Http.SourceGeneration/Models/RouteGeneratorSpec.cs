namespace Ithline.Extensions.Http.SourceGeneration.Models;

public sealed record RouteGeneratorSpec
{
    public required EquatableArray<PatternMethod> Methods { get; init; }
    public required EquatableArray<DiagnosticInfo> Diagnostics { get; init; }
}
