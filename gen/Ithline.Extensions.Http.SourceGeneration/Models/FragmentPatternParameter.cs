namespace Ithline.Extensions.Http.SourceGeneration.Models;

public sealed record FragmentPatternParameter : PatternParameter
{
    public override bool CanEmitInline => false;
}
