using System.Collections.Immutable;
using Ithline.Extensions.Http.SourceGeneration.Patterns;
using Ithline.Extensions.Http.SourceGeneration.Specs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ithline.Extensions.Http.SourceGeneration;

public sealed partial class RouteGenerator
{
    private sealed class Parser
    {
        private readonly Dictionary<TypeRef, TypeSpecBuilder> _typeBuilders = [];
        private readonly KnownTypeSymbols _symbols;
        private readonly bool _langVersionIsSupported;

        public Parser(KnownTypeSymbols typeSymbols, bool langVersionIsSupported)
        {
            _symbols = typeSymbols;
            _langVersionIsSupported = langVersionIsSupported;
        }

        public List<DiagnosticInfo>? Diagnostics { get; private set; }

        public SourceGenerationSpec? GetGeneratorSpec(ImmutableArray<MethodCandidate?> candidates, CancellationToken cancellationToken)
        {
            if (!_langVersionIsSupported)
            {
                this.RecordDiagnostic(Descriptors.LanguageVersionIsNotSupported, Location.None);
                return null;
            }

            if (candidates.IsDefaultOrEmpty)
            {
                return null;
            }

            if (_symbols.StringBuilder is null || _symbols.GeneratedRouteHelper is null)
            {
                return null;
            }

            foreach (var candidate in candidates)
            {
                if (candidate is null)
                {
                    continue;
                }

                this.ParseMethod(candidate, cancellationToken);
            }

            var typeSpecs = BuildSpecsOf(null);
            if (typeSpecs is { Count: > 0 } ts)
            {
                return new SourceGenerationSpec
                {
                    Types = ts,
                    StringBuilder = new TypeRef(_symbols.StringBuilder),
                    GeneratedRouteHelper = new TypeRef(_symbols.GeneratedRouteHelper),
                };
            }

            return null;

            EquatableArray<TypeSpec>? BuildSpecsOf(TypeRef? parentRef)
            {
                List<TypeSpec>? list = null;
                foreach (var builder in _typeBuilders.Values)
                {
                    if (builder.ParentRef != parentRef)
                    {
                        continue;
                    }

                    var types = BuildSpecsOf(builder.TypeRef);
                    var methods = builder.GetMethods();

                    // record only types with members
                    if (types is { Count: > 0 } || methods is { Count: > 0 })
                    {
                        list ??= [];
                        list.Add(new TypeSpec
                        {
                            TypeName = builder.TypeName,
                            Namespace = builder.Namespace,
                            Keyword = builder.Keyword,
                            Types = types,
                            Methods = methods,
                        });
                    }
                }

                return list is null ? null : [.. list];
            }
        }

        private void ParseMethod(MethodCandidate candidate, CancellationToken cancellationToken)
        {
            var methodSyntax = candidate.MethodSyntax;
            var methodSymbol = candidate.MethodSymbol;

            var methodName = methodSymbol.Name;

            // method cannot start with _ since that can lead to conflicting symbol names, because the generated symbols start with _
            if (methodName[0] is '_')
            {
                this.RecordDiagnostic(Descriptors.MethodNameCannotStartWithUnderscore, methodSyntax.Identifier.GetLocation());
                return;
            }

            // do not support generic methods
            if (methodSyntax.Arity > 0)
            {
                this.RecordDiagnostic(Descriptors.MethodCannotBeGeneric, methodSyntax.Identifier.GetLocation());
                return;
            }

            // method must return System.String
            if (methodSymbol.ReturnType.SpecialType is not SpecialType.System_String)
            {
                this.RecordDiagnostic(Descriptors.MethodMustReturnString, methodSyntax.ReturnType.GetLocation());
                return;
            }

            // method cannot have body
            var methodBody = methodSyntax.Body as CSharpSyntaxNode ?? methodSyntax.ExpressionBody;
            if (methodBody is not null)
            {
                this.RecordDiagnostic(Descriptors.MethodCannotHaveBody, methodBody.GetLocation());
                return;
            }

            // must be static partial
            if (!IsStaticPartial(methodSyntax))
            {
                this.RecordDiagnostic(Descriptors.MethodMustBeStaticPartial, methodSyntax.GetLocation());
                return;
            }

            // parse template
            var routePattern = RoutePatternParser.Parse(
                candidate.Pattern,
                out var routePatternDescriptor,
                out var routePatternMessageArgs);
            if (routePattern is null)
            {
                this.RecordDiagnostic(
                    routePatternDescriptor ?? Descriptors.PatternIsNotValid,
                    methodSyntax.GetLocation(),
                    routePatternMessageArgs);
                return;
            }

            var parameterSpecs = new List<MethodParameterSpec>();
            foreach (var parameter in methodSymbol.Parameters)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // semantic problem, just bail
                if (string.IsNullOrWhiteSpace(parameter.Name))
                {
                    return;
                }

                // if parameter is error type, just bail
                var parameterType = parameter.Type;
                if (parameterType is IErrorTypeSymbol)
                {
                    return;
                }

                if (parameter.Name[0] is '_')
                {
                    this.RecordDiagnostic(Descriptors.ParameterNameCannotStartWithUnderscore, parameter.Locations[0]);
                    return;
                }

                // do not support any parameter modifiers
                if (parameter.RefKind is not RefKind.None)
                {
                    this.RecordDiagnostic(Descriptors.ParameterCannotHaveRefModifier, parameter.Locations[0], parameter.Name);
                    return;
                }

                var patternParameter = routePattern.GetParameter(parameter.Name);
                var cannotBeNull = parameterType.IsValueType && parameterType.OriginalDefinition.SpecialType is not SpecialType.System_Nullable_T;
                if ((patternParameter is null || patternParameter.IsOptional) && cannotBeNull)
                {
                    this.RecordDiagnostic(Descriptors.ParameterMustBeNullableIfOptionalOrQuery, parameter.Locations[0], parameter.Name);
                    return;
                }

                string? queryName = null;
                foreach (var attribute in parameter.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _symbols.QueryNameAttribute))
                    {
                        continue;
                    }

                    if (attribute.ConstructorArguments.Length != 1)
                    {
                        continue;
                    }

                    var attributeValue = attribute.ConstructorArguments[0];
                    if (attributeValue.Kind is TypedConstantKind.Error)
                    {
                        return;
                    }

                    queryName = (string?)attributeValue.Value;
                    break;
                }

                parameterSpecs.Add(new MethodParameterSpec
                {
                    Name = parameter.Name,
                    QueryName = string.IsNullOrWhiteSpace(queryName) ? parameter.Name : queryName!.Trim(),
                    Type = new TypeRef(parameterType),
                    RequiresEscape = !parameterType.CanBeInlined(),
                    IsQueryParameter = patternParameter is null,
                    IsEnumerable = parameterType.IsEnumerable(),
                    IsParams = parameter.IsParams,
                });
            }

            // validate that all templated parameters are supplied as method arguments
            foreach (var routeParameter in routePattern.Parameters)
            {
                var found = false;
                foreach (var p in parameterSpecs)
                {
                    if (string.Equals(routeParameter.Name, p.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    this.RecordDiagnostic(Descriptors.PatternParameterMissingFromMethodArguments, methodSyntax.GetLocation(), routeParameter);
                    return;
                }
            }

            var typeBuilder = this.ResolveTypeBuilder(candidate.ClassSymbol, candidate.ClassSyntax);
            var methodSpec = new MethodSpec
            {
                Name = methodName,
                Modifiers = methodSyntax.Modifiers.ToString(),
                Pattern = routePattern,
                LowercaseUrls = candidate.LowercaseUrls,
                LowercaseQueryStrings = candidate.LowercaseQueryStrings,
                AppendTrailingSlash = candidate.AppendTrailingSlash,
                Parameters = [.. parameterSpecs],
            };
            typeBuilder.AddMethod(methodSpec);
        }

        private void RecordDiagnostic(DiagnosticDescriptor descriptor, Location location, params object?[]? messageArgs)
        {
            Diagnostics ??= [];
            Diagnostics.Add(DiagnosticInfo.Create(descriptor, location, messageArgs));
        }

        private TypeSpecBuilder ResolveTypeBuilder(INamedTypeSymbol classSymbol, TypeDeclarationSyntax classSyntax)
        {
            var typeRef = new TypeRef(classSymbol);
            if (!_typeBuilders.TryGetValue(typeRef, out var typeBuilder))
            {
                var namespaceName = ResolveNamespace(classSyntax);

                TypeSpecBuilder? parentBuilder = null;
                if (classSymbol.ContainingType is INamedTypeSymbol parentSymbol
                    && classSyntax.Parent is TypeDeclarationSyntax parentSyntax)
                {
                    parentBuilder = this.ResolveTypeBuilder(parentSymbol, parentSyntax);
                }

                _typeBuilders.Add(typeRef, typeBuilder = new TypeSpecBuilder
                {
                    TypeRef = typeRef,
                    ParentRef = parentBuilder?.TypeRef,
                    TypeName = GenerateTypeName(classSyntax),
                    Keyword = classSyntax.Keyword.ValueText,
                    Namespace = namespaceName,
                });
            }

            return typeBuilder;

            static string GenerateTypeName(TypeDeclarationSyntax typeDeclaration)
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
        }

        private static bool IsStaticPartial(MethodDeclarationSyntax methodDeclaration)
        {
            var @static = false;
            var @partial = false;

            foreach (var mod in methodDeclaration.Modifiers)
            {
                if (mod.IsKind(SyntaxKind.PartialKeyword))
                {
                    @partial = true;
                }
                else if (mod.IsKind(SyntaxKind.StaticKeyword))
                {
                    @static = true;
                }
            }

            return @static && @partial;
        }

        private static string? ResolveNamespace(TypeDeclarationSyntax classDeclaration)
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
    }
}
