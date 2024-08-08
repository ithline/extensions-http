namespace Ithline.Extensions.Http.SourceGeneration.Models;

public abstract record PatternParameter
{
    public required string ParameterName { get; init; }
    public required string ParameterType { get; init; }

    public required bool IsNullable { get; init; }
    public required bool IsEnumerable { get; init; }
    public required bool IsInteger { get; init; }
    public required bool IsString { get; init; }
}
