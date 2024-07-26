using System.Text;

namespace Ithline.Extensions.Http.SourceGeneration.Patterns;

public sealed record RoutePatternPartParameter : RoutePatternPart
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the <see cref="RoutePatternParameterKind"/> of this parameter.
    /// </summary>
    public required RoutePatternParameterKind ParameterKind { get; init; }

    /// <summary>
    /// Returns <c>true</c> if this part is a catch-all parameter.
    /// Otherwise returns <c>false</c>.
    /// </summary>
    public bool IsCatchAll => ParameterKind is RoutePatternParameterKind.CatchAll;

    /// <summary>
    /// Returns <c>true</c> if this part is an optional parameter.
    /// Otherwise returns <c>false</c>.
    /// </summary>
    public bool IsOptional => ParameterKind is RoutePatternParameterKind.Optional;

    /// <summary>
    /// Gets the value indicating if slashes in current parameter's value should be encoded.
    /// </summary>
    public required bool EncodeSlashes { get; init; }

    internal override string DebuggerToString()
    {
        var builder = new StringBuilder();
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
