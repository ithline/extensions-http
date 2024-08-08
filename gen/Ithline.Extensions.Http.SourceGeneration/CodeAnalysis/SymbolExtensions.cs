using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CodeAnalysis;

internal static class SymbolExtensions
{
    public static ITypeSymbol UnwrapTypeSymbol(this ITypeSymbol typeSymbol, bool unwrapArray = false, bool unwrapNullable = false)
    {
        INamedTypeSymbol? unwrappedTypeSymbol = null;

        // If it is an array, and unwrapArray = true, unwrap it before unwrapping nullable.
        if (unwrapArray && typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
        {
            unwrappedTypeSymbol = arrayTypeSymbol.ElementType as INamedTypeSymbol;
        }
        else if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
        {
            unwrappedTypeSymbol = namedTypeSymbol;
        }

        // If it is nullable, unwrap it.
        if (unwrapNullable && unwrappedTypeSymbol?.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            unwrappedTypeSymbol = unwrappedTypeSymbol.TypeArguments[0] as INamedTypeSymbol;
        }

        return unwrappedTypeSymbol ?? typeSymbol;
    }

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

    public static bool Implements(this ITypeSymbol type, ITypeSymbol interfaceType)
    {
        foreach (var t in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(t, interfaceType))
            {
                return true;
            }
        }
        return false;
    }

    public static bool TryGetNamedArgumentValue<T>(this AttributeData attribute, string argumentName, out T? argumentValue)
    {
        argumentValue = default;
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.Key, argumentName, StringComparison.Ordinal))
            {
                var routeParameterNameConstant = namedArgument.Value;
                argumentValue = (T?)routeParameterNameConstant.Value;
                return true;
            }
        }
        return false;
    }
}
