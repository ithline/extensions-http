using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Ithline.Extensions.Http.SourceGeneration;

/// <summary>
/// An equatable value representing type identity.
/// </summary>
[DebuggerDisplay("Name = {Name}")]
public sealed class TypeRef : IEquatable<TypeRef>
{
    private static readonly SymbolDisplayFormat _fullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public TypeRef(ITypeSymbol type)
    {
        Name = type.Name;
        FullyQualifiedName = type.ToDisplayString(_fullyQualifiedFormat);
        TypeKind = type.TypeKind;
        SpecialType = type.OriginalDefinition.SpecialType;

        IsValueType = type.IsValueType;
        IsEnumerable = type.IsEnumerable();
        IsNullable = (IsValueType && SpecialType is SpecialType.System_Nullable_T)
            || (!IsValueType && type.NullableAnnotation is NullableAnnotation.Annotated);
    }

    public string Name { get; }
    public string FullyQualifiedName { get; }
    public TypeKind TypeKind { get; }
    public SpecialType SpecialType { get; }

    public bool IsValueType { get; }
    public bool IsNullable { get; }
    public bool IsEnumerable { get; }

    public bool Equals(TypeRef? other) => other != null && FullyQualifiedName == other.FullyQualifiedName;
    public override bool Equals(object? obj) => this.Equals(obj as TypeRef);
    public override int GetHashCode() => FullyQualifiedName.GetHashCode();

    public override string ToString()
    {
        return FullyQualifiedName;
    }
}
