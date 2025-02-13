using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Ithline.Extensions.Http.SourceGeneration;

/// <summary>
/// An equatable value representing type identity.
/// </summary>
[DebuggerDisplay("Name = {Name}")]
public class TypeRef : IEquatable<TypeRef>
{
    public TypeRef(string name, SpecialType specialType, NullableAnnotation nullableAnnotation, bool isValueType)
    {
        Name = name;
        SpecialType = specialType;
        NullableAnnotation = nullableAnnotation;
        IsValueType = isValueType;
    }

    public string Name { get; }
    public SpecialType SpecialType { get; }
    public NullableAnnotation NullableAnnotation { get; }
    public bool IsValueType { get; }

    public static TypeRef Create(ITypeSymbol symbol)
    {
        var name = symbol.GetFullyQualifiedName();
        var specialType = symbol.OriginalDefinition.SpecialType;
        var nullableAnotation = symbol.NullableAnnotation;

        if (symbol is IArrayTypeSymbol array)
        {
            var elementType = Create(array.ElementType);
            return new ArrayTypeRef(name, specialType, nullableAnotation, elementType);
        }

        var isValueType = symbol.IsValueType;
        return new TypeRef(name, specialType, nullableAnotation, isValueType);
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
    public ArrayTypeRef(string fullyQualifiedName, SpecialType specialType, NullableAnnotation nullableAnnotation, TypeRef elementType)
        : base(fullyQualifiedName, specialType, nullableAnnotation, isValueType: false)
    {
        ElementType = elementType;
    }

    public TypeRef ElementType { get; }
}
