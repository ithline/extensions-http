namespace Ithline.Extensions.Http.SourceGeneration.Models;

public sealed record QueryPatternParameter : PatternParameter
{
    public required string QueryName { get; init; }
    public required bool IsLowercase { get; init; }

    public override bool CanEmitInline => false;
}
