namespace Ithline.Extensions.Http.SourceGeneration.Specs;

public sealed record SourceGenerationSpec
{
    public required EquatableArray<TypeSpec> Types { get; init; }

    public required TypeRef StringBuilder { get; init; }
    public required TypeRef GeneratedRouteHelper { get; init; }
}
