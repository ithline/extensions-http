using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CodeAnalysis;

internal static class SymbolExtensions
{
    public static bool TryGetAttribute(this ImmutableArray<AttributeData> attributes, INamedTypeSymbol attributeType, [NotNullWhen(true)] out AttributeData? matchedAttribute)
    {
        foreach (var attributeData in attributes)
        {
            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, attributeType))
            {
                matchedAttribute = attributeData;
                return true;
            }
        }

        matchedAttribute = null;
        return false;
    }
    public static T? GetNamedArgumentValue<T>(this AttributeData attribute, string argumentName)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.Key, argumentName, StringComparison.Ordinal))
            {
                var routeParameterNameConstant = namedArgument.Value;
                return (T?)routeParameterNameConstant.Value;
            }
        }
        return default;
    }
}
