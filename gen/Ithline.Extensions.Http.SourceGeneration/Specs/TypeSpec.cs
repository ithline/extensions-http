namespace Ithline.Extensions.Http.SourceGeneration.Specs;

public sealed record TypeSpec
{
    public required string? Namespace { get; init; }
    public required string TypeName { get; init; }
    public required string Keyword { get; init; }
    public required EquatableArray<TypeSpec>? Types { get; init; }
    public required EquatableArray<MethodSpec>? Methods { get; init; }
}
