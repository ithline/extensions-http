using System.Text;

namespace Ithline.Extensions.Http.SourceGeneration.Models;

public sealed record RoutePatternParameter : PatternParameter, IPatternSegmentPart
{
    public bool EncodeSlashes { get; set; } = true;
    public bool HasOptionalSeparator { get; set; }
    public RoutePatternParameterKind ParameterKind { get; set; }
    public bool IsCatchAll => ParameterKind is RoutePatternParameterKind.CatchAll;
    public bool IsOptional => ParameterKind is RoutePatternParameterKind.Optional;

    public override bool CanEmitInline => (EncodeSlashes || IsInteger) && !IsNullable;

    public bool Equals(IPatternSegmentPart other)
    {
        return other is RoutePatternParameter separator && this.Equals(separator);
    }

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

        builder.Append(ParameterName);

        if (IsOptional)
        {
            builder.Append('?');
        }

        builder.Append('}');
        return builder.ToString();
    }
}
