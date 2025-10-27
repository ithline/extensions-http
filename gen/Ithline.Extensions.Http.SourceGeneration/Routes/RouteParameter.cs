using System.Text;

namespace Ithline.Extensions.Http.SourceGeneration.Routes;

internal sealed record RouteParameter : ParameterBase, IPatternSegmentPart
{
    public bool EncodeSlashes { get; set; } = true;
    public bool HasOptionalSeparator { get; set; }
    public PatternParameterKind ParameterKind { get; set; }
    public bool IsCatchAll => ParameterKind is PatternParameterKind.CatchAll;
    public bool IsOptional => ParameterKind is PatternParameterKind.Optional;

    public bool Equals(IPatternSegmentPart other) => other is RouteParameter obj && this.Equals(obj);

    public override string ToString()
    {
        var builder = new StringBuilder();
        if (HasOptionalSeparator)
        {
            builder.Append('.');
        }

        builder.Append('{');

        if (IsCatchAll)
        {
            builder.Append('*');
            if (!EncodeSlashes)
            {
                builder.Append('*');
            }
        }

        builder.Append(Name);

        if (IsOptional)
        {
            builder.Append('?');
        }

        builder.Append('}');
        return builder.ToString();
    }
}
