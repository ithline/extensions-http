namespace Ithline.Extensions.Http.SourceGeneration.Models;

public abstract record PatternParameter
{
    public required string ParameterName { get; init; }
    public required TypeRef ParameterType { get; init; }
    public required bool IsInteger { get; init; }

    public abstract bool CanEmitInline { get; }
    public bool IsNullable => ParameterType.IsNullable;
}
