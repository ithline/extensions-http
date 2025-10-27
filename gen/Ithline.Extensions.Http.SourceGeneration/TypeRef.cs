using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Ithline.Extensions.Http.SourceGeneration;

/// <summary>
/// An equatable value representing type identity.
/// </summary>
[DebuggerDisplay("Name = {Name}")]
public class TypeRef : IEquatable<TypeRef>
{
    public required string Name { get; init; }
    public required SpecialType SpecialType { get; init; }
    public required NullableAnnotation NullableAnnotation { get; init; }
    public required bool IsValueType { get; init; }

    public static TypeRef Create(Compilation compilation, ITypeSymbol symbol)
    {
        if (symbol is IArrayTypeSymbol array)
        {
            var elementType = Create(compilation, array.ElementType);
            return new ArrayTypeRef
            {
                Name = symbol.GetFullyQualifiedName(),
                ElementType = elementType,
                SpecialType = symbol.OriginalDefinition.SpecialType,
                NullableAnnotation = symbol.NullableAnnotation,
                IsValueType = false,
            };
        }

        var symbolKeyValuePair = compilation.GetBestTypeByMetadataName(Constants.KeyValuePair);
        if (symbol is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, symbolKeyValuePair))
        {
            return new KeyValueTypeRef
            {
                Name = symbol.GetFullyQualifiedName(),
                KeyType = Create(compilation, named.TypeArguments[0]),
                ValueType = Create(compilation, named.TypeArguments[1]),
                SpecialType = symbol.OriginalDefinition.SpecialType,
                NullableAnnotation = symbol.NullableAnnotation,
                IsValueType = symbol.IsValueType,
            };
        }

        return new TypeRef
        {
            Name = symbol.GetFullyQualifiedName(),
            SpecialType = symbol.OriginalDefinition.SpecialType,
            NullableAnnotation = symbol.NullableAnnotation,
            IsValueType = symbol.IsValueType,
        };
    }

    public bool IsNullAssignable()
    {
        return !IsValueType || NullableAnnotation is NullableAnnotation.Annotated;
    }

    public bool Equals(TypeRef? other) => other is not null && Name == other.Name;
    public override bool Equals(object obj) => this.Equals(obj as TypeRef);
    public override int GetHashCode() => Name.GetHashCode();

    public override string ToString()
    {
        return Name;
    }
}

public sealed class ArrayTypeRef : TypeRef
{
    public required TypeRef ElementType { get; init; }
}

public sealed class KeyValueTypeRef : TypeRef
{
    public required TypeRef KeyType { get; init; }
    public required TypeRef ValueType { get; init; }
}
