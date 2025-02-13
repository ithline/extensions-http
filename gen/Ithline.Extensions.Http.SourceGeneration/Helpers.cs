using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ithline.Extensions.Http.SourceGeneration;

internal static class Helpers
{
    public static string ToCamelCase(string s)
    {
        var chars = s.ToCharArray();
        FixCasing(chars);
        return new string(chars);

        static void FixCasing(Span<char> chars)
        {
            for (var i = 0; i < chars.Length; i++)
            {
                if (i == 1 && !char.IsUpper(chars[i]))
                {
                    break;
                }

                var hasNext = i + 1 < chars.Length;

                // Stop when next char is already lowercase.
                if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
                {
                    // If the next char is a space, lowercase current char before exiting.
                    if (chars[i + 1] == ' ')
                    {
                        chars[i] = char.ToLowerInvariant(chars[i]);
                    }

                    break;
                }

                chars[i] = char.ToLowerInvariant(chars[i]);
            }
        }
    }

    public static string GetTypeName(TypeDeclarationSyntax typeDeclaration)
    {
        var parameterList = typeDeclaration.TypeParameterList;
        if (parameterList is not null && parameterList.Parameters.Count != 0)
        {
            // The source generator produces a partial class that the compiler merges with the original
            // class definition in the user code. If the user applies attributes to the generic types
            // of the class, it is necessary to remove these attribute annotations from the generated
            // code. Failure to do so may result in a compilation error (CS0579: Duplicate attribute).
            for (var i = 0; i < parameterList.Parameters.Count; i++)
            {
                var parameter = parameterList.Parameters[i];

                if (parameter.AttributeLists.Count > 0)
                {
                    typeDeclaration = typeDeclaration.ReplaceNode(parameter, parameter.WithAttributeLists([]));
                }
            }
        }

        return typeDeclaration.Identifier.ToString() + typeDeclaration.TypeParameterList;
    }

    public static string? GetNamespace(TypeDeclarationSyntax classDeclaration)
    {
        var potentialNamespaceParent = classDeclaration.Parent;
        while (potentialNamespaceParent is not null
            and not NamespaceDeclarationSyntax
            and not FileScopedNamespaceDeclarationSyntax)
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        if (potentialNamespaceParent is not BaseNamespaceDeclarationSyntax namespaceParent)
        {
            return null;
        }

        var name = namespaceParent.Name.ToString();
        while (namespaceParent.Parent is NamespaceDeclarationSyntax parent)
        {
            name = $"{parent.Name}.{name}";
            namespaceParent = parent;
        }
        return name;
    }

    public static bool IsPartial(ClassDeclarationSyntax syntaxNode)
    {
        foreach (var mod in syntaxNode.Modifiers)
        {
            if (mod.IsKind(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TrimUrlPrefix([NotNullWhen(true)] string? routePattern, [NotNullWhen(true)] out string? result)
    {
        if (string.IsNullOrWhiteSpace(routePattern))
        {
            result = null;
            return false;
        }

        if (routePattern!.StartsWith("~/", StringComparison.Ordinal))
        {
            result = routePattern.Substring(2);
            return true;
        }
        else if (routePattern.StartsWith('/'))
        {
            result = routePattern.Substring(1);
            return true;
        }
        else if (!routePattern.StartsWith('~'))
        {
            result = routePattern;
            return true;
        }

        result = null;
        return false;
    }

}
