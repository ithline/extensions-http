using Microsoft.CodeAnalysis.CSharp;

namespace Ithline.Extensions.Http.SourceGeneration;

internal sealed class CompilationData
{
    public CompilationData(CSharpCompilation compilation)
    {
        LanguageVersionIsSupported = compilation.LanguageVersion >= LanguageVersion.CSharp8;

        TypeSymbols = new KnownTypeSymbols(compilation);
    }

    public bool LanguageVersionIsSupported { get; }
    public KnownTypeSymbols TypeSymbols { get; }
}
