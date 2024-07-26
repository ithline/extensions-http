using Ithline.Extensions.Http.SourceGeneration.Patterns;

namespace Ithline.Extensions.Http.SourceGeneration.Specs;

public sealed record MethodSpec
{
    public required string Name { get; init; }
    public required string? Modifiers { get; init; }
    public required RoutePattern Pattern { get; init; }
    public required bool LowercaseUrls { get; init; }
    public required bool LowercaseQueryStrings { get; init; }
    public required bool AppendTrailingSlash { get; init; }
    public required EquatableArray<MethodParameterSpec> Parameters { get; init; }

    public MethodParameterSpec? GetParameter(string name)
    {
        foreach (var parameter in Parameters.AsSpan())
        {
            if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        return null;
    }
}
