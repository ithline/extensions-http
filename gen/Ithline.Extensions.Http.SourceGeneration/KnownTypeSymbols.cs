using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ithline.Extensions.Http.SourceGeneration;

internal sealed class KnownTypeSymbols
{
    private KnownTypeSymbols()
    {
    }

    public required INamedTypeSymbol Convert { get; init; }
    public required INamedTypeSymbol SpanOfT { get; init; }
    public required INamedTypeSymbol ReadOnlySpanOfT { get; init; }
    public required INamedTypeSymbol OperationStatus { get; init; }
    public required INamedTypeSymbol CultureInfo { get; init; }
    public required INamedTypeSymbol StringBuilder { get; init; }
    public required INamedTypeSymbol UrlEncoder { get; init; }
    public required INamedTypeSymbol ObjectPool { get; init; }
    public required INamedTypeSymbol ObjectPoolOfT { get; init; }
    public required INamedTypeSymbol StringBuilderPooledObjectPolicy { get; init; }
    public required INamedTypeSymbol FragmentAttribute { get; init; }
    public required INamedTypeSymbol QueryAttribute { get; init; }

    public static KnownTypeSymbols? Create(Compilation compilation)
    {
        if (compilation is not CSharpCompilation csc)
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("System.Convert", out var convert))
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("System.Span`1", out var spanOfT))
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("System.ReadOnlySpan`1", out var readOnlySpanOfT))
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("System.Buffers.OperationStatus", out var operationStatus))
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("System.Globalization.CultureInfo", out var cultureInfo))
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("System.Text.StringBuilder", out var stringBuilder))
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("System.Text.Encodings.Web.UrlEncoder", out var urlEncoder))
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("Microsoft.Extensions.ObjectPool.ObjectPool", out var objectPool))
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("Microsoft.Extensions.ObjectPool.ObjectPool`1", out var objectPoolOfT))
        {
            return null;
        }

        if (!csc.TryGetBestTypeByMetadataName("Microsoft.Extensions.ObjectPool.StringBuilderPooledObjectPolicy", out var stringBuilderPooledObjectPolicy))
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
            Convert = convert,
            SpanOfT = spanOfT,
            ReadOnlySpanOfT = readOnlySpanOfT,
            OperationStatus = operationStatus,
            CultureInfo = cultureInfo,
            StringBuilder = stringBuilder,
            UrlEncoder = urlEncoder,
            ObjectPool = objectPool,
            ObjectPoolOfT = objectPoolOfT,
            StringBuilderPooledObjectPolicy = stringBuilderPooledObjectPolicy,
            FragmentAttribute = generatedFragmentAttribute,
            QueryAttribute = generatedQueryAttribute
        };
    }
}
