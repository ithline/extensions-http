namespace Ithline.Extensions.Http.SourceGeneration.Routes;

internal abstract record ParameterBase
{
    public required string Name { get; init; }
    public required TypeRef Type { get; init; }
    public required int Number { get; init; }
}
