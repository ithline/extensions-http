namespace Ithline.Extensions.Http.SourceGeneration.Routes;

internal sealed record QueryParameter : ParameterBase
{
    public required string QueryName { get; init; }
    public required bool IsLowercase { get; init; }
}
