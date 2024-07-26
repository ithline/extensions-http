using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ithline.Extensions.Http.SourceGeneration;

internal sealed class MethodCandidate
{
    private MethodCandidate(
        ClassDeclarationSyntax classSyntax,
        INamedTypeSymbol classSymbol,
        MethodDeclarationSyntax methodSyntax,
        IMethodSymbol methodSymbol,
        string pattern,
        bool lowercaseUrls,
        bool lowercaseQueryStrings,
        bool appendTrailingSlash)
    {
        ClassSyntax = classSyntax;
        ClassSymbol = classSymbol;
        MethodSyntax = methodSyntax;
        MethodSymbol = methodSymbol;
        Pattern = pattern;
        LowercaseUrls = lowercaseUrls;
        LowercaseQueryStrings = lowercaseQueryStrings;
        AppendTrailingSlash = appendTrailingSlash;
    }

    public ClassDeclarationSyntax ClassSyntax { get; }
    public INamedTypeSymbol ClassSymbol { get; }
    public MethodDeclarationSyntax MethodSyntax { get; }
    public IMethodSymbol MethodSymbol { get; }
    public string Pattern { get; }
    public bool LowercaseUrls { get; }
    public bool LowercaseQueryStrings { get; }
    public bool AppendTrailingSlash { get; }

    public static bool IsCandidateSyntaxNode(SyntaxNode node)
    {
        return TryExtractSyntaxNodes(node, out _, out _);
    }

    public static MethodCandidate? Create(GeneratorAttributeSyntaxContext ctx, CancellationToken cancellationToken)
    {
        if (!TryExtractSyntaxNodes(ctx.TargetNode, out var classSyntax, out var methodSyntax))
        {
            return null;
        }

        if (ctx.TargetSymbol is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classSyntax, cancellationToken);
        if (classSymbol is null)
        {
            return null;
        }

        string? pattern = null;
        var lowercaseUrls = false;
        var lowercaseQueryStrings = false;
        var appendTrailingSlash = false;
        foreach (var attributeData in ctx.Attributes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryExtractArguments(
                attributeData,
                out pattern,
                out lowercaseUrls,
                out lowercaseQueryStrings,
                out appendTrailingSlash))
            {
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        return new MethodCandidate(
            classSyntax,
            classSymbol,
            methodSyntax,
            methodSymbol,
            pattern!,
            lowercaseUrls,
            lowercaseQueryStrings,
            appendTrailingSlash);
    }

    private static bool TryExtractArguments(
        AttributeData attribute,
        [MaybeNullWhen(false)] out string pattern,
        out bool lowercaseUrls,
        out bool lowercaseQueryStrings,
        out bool appendTrailingSlash)
    {
        pattern = null;
        lowercaseUrls = false;
        lowercaseQueryStrings = false;
        appendTrailingSlash = false;

        // we need exactly 1 constructor argument
        if (attribute.ConstructorArguments.Length != 1)
        {
            return false;
        }

        var arg = attribute.ConstructorArguments[0];
        if (arg.Kind is TypedConstantKind.Error)
        {
            return false;
        }

        if (arg.IsNull)
        {
            return false;
        }

        pattern = (string)arg.Value!;
        foreach (var (name, value) in attribute.NamedArguments)
        {
            if (value.Kind is TypedConstantKind.Error)
            {
                return false;
            }

            if (name == "LowercaseUrls")
            {
                lowercaseUrls = (bool)value.Value!;
            }
            else if (name == "LowercaseQueryStrings")
            {
                lowercaseQueryStrings = (bool)value.Value!;
            }
            else if (name == "AppendTrailingSlash")
            {
                appendTrailingSlash = (bool)value.Value!;
            }
        }

        return true;
    }

    private static bool TryExtractSyntaxNodes(
        SyntaxNode node,
        [NotNullWhen(true)] out ClassDeclarationSyntax? classSyntax,
        [NotNullWhen(true)] out MethodDeclarationSyntax? methodSyntax)
    {
        if (node is MethodDeclarationSyntax mds && mds.Parent is ClassDeclarationSyntax cds)
        {
            classSyntax = cds;
            methodSyntax = mds;
            return true;
        }

        classSyntax = null;
        methodSyntax = null;
        return false;
    }
}
