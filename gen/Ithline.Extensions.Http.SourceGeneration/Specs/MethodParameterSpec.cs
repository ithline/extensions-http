namespace Ithline.Extensions.Http.SourceGeneration.Specs;

public sealed record MethodParameterSpec
{
    public required string Name { get; init; }
    public required string QueryName { get; init; }
    public required TypeRef Type { get; init; }
    public required bool RequiresEscape { get; init; }
    public required bool IsQueryParameter { get; init; }
    public required bool IsEnumerable { get; init; }
    public required bool IsParams { get; init; }
}
