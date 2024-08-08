using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ithline.Extensions.Http.SourceGeneration;

internal sealed class KnownTypeSymbols
{
    private KnownTypeSymbols()
    {
    }

    public required INamedTypeSymbol IEnumerable { get; init; }
    public required INamedTypeSymbol FragmentAttribute { get; init; }
    public required INamedTypeSymbol QueryAttribute { get; init; }

    public static KnownTypeSymbols? Create(Compilation compilation)
    {
        if (compilation is not CSharpCompilation csc)
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("System.Collections.IEnumerable", out var ienumerable))
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("Ithline.Extensions.Http.FragmentAttribute", out var generatedFragmentAttribute))
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("Ithline.Extensions.Http.QueryAttribute", out var generatedQueryAttribute))
        {
            return null;
        }

        return new KnownTypeSymbols
        {
            IEnumerable = ienumerable,
            FragmentAttribute = generatedFragmentAttribute,
            QueryAttribute = generatedQueryAttribute
        };
    }
}
