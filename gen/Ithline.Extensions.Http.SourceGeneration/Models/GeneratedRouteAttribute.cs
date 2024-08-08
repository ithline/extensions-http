using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ithline.Extensions.Http.SourceGeneration.Models;

internal sealed class GeneratedRouteAttribute
{
    private readonly MethodDeclarationSyntax _syntax;
    private readonly IMethodSymbol _symbol;

    private GeneratedRouteAttribute(
        ClassDeclarationSyntax classSyntax,
        MethodDeclarationSyntax methodSyntax,
        IMethodSymbol methodSymbol,
        string? pattern,
        bool lowercaseUrls,
        bool lowercaseQueryStrings,
        bool appendTrailingSlash)
    {
        ClassDeclaration = classSyntax;
        _syntax = methodSyntax;
        _symbol = methodSymbol;

        Pattern = pattern;
        LowercaseUrls = lowercaseUrls;
        LowercaseQueryStrings = lowercaseQueryStrings;
        AppendTrailingSlash = appendTrailingSlash;
    }

    public string Name => _symbol.Name;
    public bool IsStatic => _symbol.IsStatic;
    public bool IsPartial => _symbol.IsPartialDefinition;
    public bool IsGeneric => _symbol.IsGenericMethod;
    public bool HasBody => _symbol.PartialImplementationPart is not null;
    public string Modifiers => _syntax.Modifiers.ToString();
    public ITypeSymbol ReturnType => _symbol.ReturnType;
    public ImmutableArray<IParameterSymbol> Parameters => _symbol.Parameters;
    public ClassDeclarationSyntax ClassDeclaration { get; }

    public Location Location => _syntax.GetLocation();
    public Location ReturnTypeLocation => _syntax.ReturnType.GetLocation();
    public Location IdentifierLocation => _syntax.Identifier.GetLocation();

    public string? Pattern { get; }
    public bool LowercaseUrls { get; }
    public bool LowercaseQueryStrings { get; }
    public bool AppendTrailingSlash { get; }

    public static bool IsCandidateSyntaxNode(SyntaxNode node)
    {
        return TryExtractSyntaxNodes(node, out _, out _);
    }

    public static GeneratedRouteAttribute? Create(GeneratorAttributeSyntaxContext ctx)
    {
        if (!TryExtractSyntaxNodes(ctx.TargetNode, out var classSyntax, out var methodSyntax))
        {
            return null;
        }

        if (!TryExtractArguments(
            ctx.Attributes[0],
            out var pattern,
            out var lowercaseUrls,
            out var lowercaseQueryStrings,
            out var appendTrailingSlash))
        {
            return null;
        }

        if (ctx.TargetSymbol is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        return new GeneratedRouteAttribute(
            classSyntax: classSyntax,
            methodSyntax: methodSyntax,
            methodSymbol: methodSymbol,
            pattern: pattern,
            lowercaseUrls: lowercaseUrls,
            lowercaseQueryStrings: lowercaseQueryStrings,
            appendTrailingSlash: appendTrailingSlash);
    }

    private static bool TryExtractArguments(
        AttributeData attribute,
        out string? pattern,
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

        pattern = (string?)arg.Value;
        attribute.TryGetNamedArgumentValue("LowercaseUrls", out lowercaseUrls);
        attribute.TryGetNamedArgumentValue("LowercaseQueryStrings", out lowercaseQueryStrings);
        attribute.TryGetNamedArgumentValue("AppendTrailingSlash", out appendTrailingSlash);
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
