using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ithline.Extensions.Http.SourceGeneration;

internal sealed class KnownTypeSymbols
{
    public KnownTypeSymbols(CSharpCompilation compilation)
    {
        StringBuilder = compilation.GetBestTypeByMetadataName("System.Text.StringBuilder");
        GeneratedRouteHelper = compilation.GetBestTypeByMetadataName("Ithline.Extensions.Http.GeneratedRouteHelper");

        QueryNameAttribute = compilation.GetBestTypeByMetadataName("Ithline.Extensions.Http.QueryNameAttribute");
    }

    public INamedTypeSymbol? StringBuilder { get; }
    public INamedTypeSymbol? GeneratedRouteHelper { get; }
    public INamedTypeSymbol? QueryNameAttribute { get; }
}
