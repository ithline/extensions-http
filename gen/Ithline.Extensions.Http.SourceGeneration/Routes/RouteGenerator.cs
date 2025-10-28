using System.Diagnostics;
using Ithline.Extensions.Http.SourceGeneration.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ithline.Extensions.Http.SourceGeneration.Routes;

[Generator]
public sealed class RouteGenerator : IIncrementalGenerator
{
    private static readonly string _generatedCodeAttribute = $"GeneratedCodeAttribute(\"{typeof(RouteGenerator).Assembly.GetName().Name}\", \"{typeof(RouteGenerator).Assembly.GetName().Version}\")";

    private const string RouteHelper = "__GeneratedRouteHelper";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if LAUNCH_DEBUGGER
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Launch();
            }
#endif

        var results = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: Constants.GeneratedRouteAttribute,
                predicate: static (node, _) => node is MethodDeclarationSyntax
                    or PropertyDeclarationSyntax
                    or IndexerDeclarationSyntax
                    or AccessorDeclarationSyntax,
                transform: GetRouteSyntaxOrFailureDiagnostic)
            .Where(static m => m is not null)
            .Collect();

        context.RegisterSourceOutput(results, static (ctx, results) =>
        {
            var onlyFailures = true;
            foreach (var obj in results)
            {
                if (obj is DiagnosticData d)
                {
                    ctx.ReportDiagnostic(d.ToDiagnostic());
                }
                else
                {
                    onlyFailures = false;
                }
            }

            if (onlyFailures)
            {
                return;
            }

            using var sw = new StringWriter();
            using var cw = new CodeWriter(sw, 0);

            cw.WriteLine(Constants.FileHeader);

            foreach (var obj in results)
            {
                if (obj is not RouteMethod method)
                {
                    continue;
                }

                cw.WriteLine();
                EmitRouteMethod(cw, method);
            }

            cw.WriteLine();
            EmitHelperClass(cw);

            ctx.AddSource("GeneratedRoutes.g.cs", sw.ToString());
        });
    }

    private static object? GetRouteSyntaxOrFailureDiagnostic(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetNode is IndexerDeclarationSyntax or AccessorDeclarationSyntax)
        {
            // We allow these to be used as a target node for the sole purpose
            // of being able to flag invalid use when [GeneratedRegex] is applied incorrectly.
            // Otherwise, if the ForAttributeWithMetadataName call excluded these, [GeneratedRegex]
            // could be applied to them and we wouldn't be able to issue a diagnostic.
            return DiagnosticData.Create(DiagnosticDescriptors.InvalidSignature, context.TargetNode);
        }

        var memberSyntax = (MemberDeclarationSyntax)context.TargetNode;

        var semanticModel = context.SemanticModel;
        var compilation = semanticModel.Compilation;

        var symbolFragmentAttribute = compilation.GetBestTypeByMetadataName(Constants.FragmentAttribute);
        var symbolQueryAttribute = compilation.GetBestTypeByMetadataName(Constants.QueryAttribute);

        if (symbolFragmentAttribute is null || symbolQueryAttribute is null)
        {
            // required types aren't available
            return null;
        }

        if (memberSyntax.Parent is not TypeDeclarationSyntax parentSyntax)
        {
            return null;
        }

        FragmentParameter? fragmentParameter = null;
        List<ParameterBase> parameters = [];
        if (context.TargetSymbol is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        if (methodSymbol.IsGenericMethod
            || methodSymbol.Arity != 0
            || methodSymbol.IsAbstract
            || methodSymbol.IsExtensionMethod
            || !methodSymbol.IsPartialDefinition
            || methodSymbol.ReturnType.SpecialType is not SpecialType.System_String)
        {
            // return invalid signature
            return DiagnosticData.Create(DiagnosticDescriptors.InvalidSignature, memberSyntax);
        }

        // parse parameters
        var number = 0;
        foreach (var parameterSymbol in methodSymbol.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameterSymbol.Name)
                || parameterSymbol.Type is IErrorTypeSymbol
                || parameterSymbol.RefKind is not RefKind.None
                || parameterSymbol.IsParams
                || parameterSymbol.IsParamsArray
                || parameterSymbol.IsParamsCollection)
            {
                // return invalid signature
                return DiagnosticData.Create(DiagnosticDescriptors.InvalidSignature, memberSyntax);
            }

            var parameterAttributes = parameterSymbol.GetAttributes();
            if (parameterAttributes.TryGetAttribute(symbolQueryAttribute, out var queryAttribute)
                & parameterAttributes.TryGetAttribute(symbolFragmentAttribute, out var fragmentAttribute))
            {
                // parameter is marked as both query and fragment
                return DiagnosticData.Create(DiagnosticDescriptors.InvalidSignature, memberSyntax);
            }

            var parameterTypeRef = TypeRef.Create(compilation, parameterSymbol.Type);
            if (fragmentAttribute is not null)
            {
                if (parameterTypeRef.SpecialType is not SpecialType.System_String)
                {
                    // return invalid signature
                    return DiagnosticData.Create(DiagnosticDescriptors.InvalidSignature, memberSyntax);
                }

                if (fragmentParameter is not null)
                {
                    // invalid signature (only one fragment allowed)
                    return DiagnosticData.Create(DiagnosticDescriptors.InvalidSignature, memberSyntax);
                }

                parameters.Add(fragmentParameter = new FragmentParameter
                {
                    Name = parameterSymbol.Name,
                    Type = parameterTypeRef,
                    Number = number++,
                });
            }
            else if (queryAttribute is not null)
            {
                var queryName = queryAttribute.GetConstructorArgumentValue<string>(0);
                parameters.Add(new QueryParameter
                {
                    Name = parameterSymbol.Name,
                    Type = parameterTypeRef,
                    Number = number++,
                    QueryName = string.IsNullOrWhiteSpace(queryName) ? parameterSymbol.Name : queryName!,
                    IsLowercase = queryAttribute.GetNamedArgumentValue<bool>("LowercaseValue"),
                });
            }
            else
            {
                parameters.Add(new RouteParameter
                {
                    Name = parameterSymbol.Name,
                    Type = parameterTypeRef,
                    Number = number++,
                });
            }
        }

        // extract attribute configuration
        var generatedRouteAttribute = context.Attributes[0];
        if (generatedRouteAttribute.ConstructorArguments.Length != 1)
        {
            return null;
        }

        var rawPattern = generatedRouteAttribute.ConstructorArguments[0].Value as string;

        if (!Helpers.TrimUrlPrefix(rawPattern, out var pattern))
        {
            // pattern is not valid
            return DiagnosticData.Create(DiagnosticDescriptors.InvalidRoutePattern, memberSyntax);
        }

        var parsingContext = new PatternParsingContext(pattern, parameters);
        var segments = PatternParser.Parse(parsingContext);
        if (segments is null)
        {
            // invalid pattern
            return DiagnosticData.Create(DiagnosticDescriptors.InvalidRoutePattern, memberSyntax);
        }

        for (var i = 0; i < parameters.Count; i++)
        {
            // fix unmatched pattern parameters to query
            if (parameters[i] is RouteParameter patternParameter && !parsingContext.IsParameterBound(patternParameter))
            {
                parameters[i] = new QueryParameter
                {
                    Name = patternParameter.Name,
                    Type = patternParameter.Type,
                    Number = patternParameter.Number,
                    QueryName = patternParameter.Name,
                    IsLowercase = false,
                };
            }

            // required route parameters cannot be nullable
            if (parameters[i] is RouteParameter { ParameterKind: PatternParameterKind.Standard, Type.NullableAnnotation: NullableAnnotation.Annotated })
            {
                // invalid signature
                return DiagnosticData.Create(DiagnosticDescriptors.InvalidSignature, memberSyntax);
            }
        }

        return new RouteMethod(
            DeclaringType: ParseType(parentSyntax),
            MemberName: context.TargetSymbol.Name,
            Modifiers: memberSyntax.Modifiers.ToString(),
            ReturnType: TypeRef.Create(compilation, methodSymbol.ReturnType),
            RawPattern: rawPattern,
            LowercaseUrls: generatedRouteAttribute.GetNamedArgumentValue<bool>("LowercaseUrls"),
            LowercaseQueryStrings: generatedRouteAttribute.GetNamedArgumentValue<bool>("LowercaseQueryStrings"),
            AppendTrailingSlash: generatedRouteAttribute.GetNamedArgumentValue<bool>("AppendTrailingSlash"),
            Parameters: [.. parameters],
            Segments: [.. segments],
            Fragment: fragmentParameter);

        static RouteType ParseType(TypeDeclarationSyntax syntax)
        {
            var typeName = Helpers.GetTypeName(syntax);
            var ns = Helpers.GetNamespace(syntax);
            return new RouteType(ns, typeName, syntax.Keyword.ToString())
            {
                Parent = syntax.Parent is TypeDeclarationSyntax parent ? ParseType(parent) : null,
            };
        }
    }
    private static void EmitRouteMethod(CodeWriter writer, RouteMethod method)
    {
        // emit type hierarchy
        var scopes = new Stack<string>();

        var parent = method.DeclaringType;
        while (parent is not null)
        {
            scopes.Push($"partial {parent.Keyword} {parent.TypeName}");
            parent = parent.Parent;
        }

        if (!string.IsNullOrWhiteSpace(method.DeclaringType?.Namespace))
        {
            scopes.Push($"namespace {method.DeclaringType?.Namespace}");
        }

        var scopeCount = scopes.Count;
        while (scopes.Count > 0)
        {
            writer.WriteLine(scopes.Pop());
            writer.StartBlock();
        }

        // emit method
        writer.WriteLine($"[global::System.CodeDom.Compiler.{_generatedCodeAttribute}]");

        if (!string.IsNullOrWhiteSpace(method.Modifiers))
        {
            writer.Write(method.Modifiers);
            writer.Write(' ');
        }
        writer.Write(method.ReturnType);
        writer.Write(' ');
        writer.Write(method.MemberName);

        writer.Write("(");
        writer.Indent++;

        var first = true;
        foreach (var parameter in method.Parameters)
        {
            if (!first)
            {
                writer.Write(",");
            }
            writer.WriteLine();
            first = false;

            writer.Write(parameter.Type);
            writer.Write(" ");
            writer.Write(parameter.Name);
        }

        writer.WriteLine(")");
        writer.Indent--;

        writer.StartBlock();
        if (method.Parameters.Count == 0)
        {
            EmitInlineBody(writer, method);
        }
        else
        {
            EmitBuilderBody(writer, method);
        }
        writer.EndBlock();

        // close all scopes
        for (var i = 0; i < scopeCount; i++)
        {
            writer.EndBlock();
        }
    }
    private static void EmitInlineBody(CodeWriter writer, RouteMethod method)
    {
        string? last = null;

        writer.Write("return \"");
        foreach (var segment in method.Segments)
        {
            writer.Write(last = "/");
            foreach (var part in segment)
            {
                Debug.Assert(part is PatternLiteral);
                if (part is PatternLiteral literal)
                {
                    var content = method.LowercaseUrls
                        ? literal.Content.ToLowerInvariant()
                        : literal.Content;
                    writer.Write(content);

                    last = string.IsNullOrWhiteSpace(content) ? last : content;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(last) || (method.AppendTrailingSlash && last![^1] is not '/'))
        {
            writer.Write('/');
        }

        writer.WriteLine("\";");
    }
    private static void EmitBuilderBody(CodeWriter writer, RouteMethod method)
    {
        var tempCounter = 0;
        var localNames = method.Parameters.Select(t => t.Name).ToHashSet();
        var temp_sb = GetNextTemp();
        var temp_span = GetNextTemp();
        var temp_buffer = GetNextTemp();
        var temp_firstQuery = GetNextTemp();

        // init method state
        writer.WriteLine($"// {method.RawPattern}");
        writer.WriteLine($"global::{Constants.StringBuilder} {temp_sb} = {RouteHelper}.Rent();");

        // try
        writer.WriteLine("try");
        writer.StartBlock();

        // allocate encoding buffer
        writer.WriteLine($"global::System.ReadOnlySpan<char> {temp_span};");
        writer.WriteLine($"global::System.Span<char> {temp_buffer} = stackalloc char[64];");

        if (method.Parameters.OfType<QueryParameter>().Any())
        {
            writer.WriteLine($"bool {temp_firstQuery} = true;");
        }

        foreach (var segment in method.Segments)
        {
            var segmentStarted = true;

            writer.WriteLine();
            writer.WriteLine($"// /{segment}");
            foreach (var part in segment)
            {
                if (part is PatternLiteral literal)
                {
                    var content = method.LowercaseUrls
                        ? literal.Content.ToLowerInvariant()
                        : literal.Content;

                    if (segmentStarted)
                    {
                        content = '/' + content;
                    }
                    segmentStarted = false;

                    writer.WriteLine($"""{temp_sb}.Append("{content}");""");
                }
                else if (part is RouteParameter { EncodeSlashes: false } catchAll)
                {
                    var index = GetNextTemp();
                    var local = GetNextTemp();

                    writer.WriteLine($"int {index};");
                    AppendStringConvert(catchAll.Type, catchAll.Name, method.LowercaseUrls);
                    writer.WriteLine($"while (({index} = {temp_span}.IndexOf('/')) >= 0)");
                    writer.StartBlock();
                    writer.WriteLine($"global::System.ReadOnlySpan<char> {local} = {temp_span}.Slice(0, {index});");

                    writer.WriteLine($"if (!{local}.IsEmpty)");
                    writer.StartBlock();
                    AppendSlash();
                    AppendEncode($"{local}");
                    writer.EndBlock();
                    writer.WriteLine();

                    writer.WriteLine($"{temp_span} = {temp_span}.Slice({index} + 1);");
                    writer.EndBlock();

                    writer.WriteLine();

                    writer.WriteLine($"if (!{temp_span}.IsEmpty)");
                    writer.StartBlock();
                    AppendSlash();
                    AppendEncode(temp_span);
                    writer.EndBlock();
                }
                else if (part is RouteParameter parameter)
                {
                    // for optional parameters, we emit null checks
                    if (parameter.IsOptional)
                    {
                        AppendIfNotNull(parameter.Name);
                        writer.StartBlock();
                    }

                    // if parameter is first value in segment, we emit slash
                    if (segmentStarted)
                    {
                        AppendSlash();
                    }
                    segmentStarted = false;

                    // we emit separator in case we have value
                    if (parameter.HasOptionalSeparator)
                    {
                        writer.WriteLine($"{temp_sb}.Append('.');");
                    }

                    AppendStringConvert(parameter.Type, parameter.Name, method.LowercaseUrls);
                    AppendEncode(temp_span);

                    if (parameter.IsOptional)
                    {
                        writer.EndBlock();
                    }
                }
                else
                {
                    Debug.Fail("This is not valid case.");
                }
            }
        }

        // append trailing slash if empty or required slash
        writer.WriteLine();
        writer.WriteLine($"// append slash");
        writer.Write($"if ({temp_sb}.Length == 0");
        if (method.AppendTrailingSlash)
        {
            writer.Write($" || {temp_sb}[^1] is not '/'");
        }
        writer.WriteLine(")");
        writer.StartBlock();
        AppendSlash();
        writer.EndBlock();

        foreach (var parameter in method.Parameters.OfType<QueryParameter>())
        {
            var queryName = Uri.EscapeDataString(method.LowercaseQueryStrings
                ? parameter.QueryName.ToLowerInvariant()
                : parameter.QueryName);

            writer.WriteLine();

            if (parameter.Type.IsNullAssignable())
            {
                AppendIfNotNull(parameter.Name);
                writer.StartBlock();
            }

            if (parameter.Type is ArrayTypeRef { ElementType: var elementType })
            {
                if (elementType is KeyValueTypeRef kvp)
                {
                    var key = GetNextTemp();
                    var value = GetNextTemp();
                    writer.WriteLine($"foreach (var ({key}, {value}) in {parameter.Name})");
                    writer.StartBlock();

                    if (kvp.ValueType.IsNullAssignable())
                    {
                        AppendIfNotNull(value);
                        writer.StartBlock();
                    }

                    writer.WriteLine($"{temp_sb}.Append({temp_firstQuery} ? '?' : '&');");
                    writer.WriteLine($"""{temp_sb}.Append("{queryName}[");""");
                    AppendStringConvert(kvp.KeyType, key, false);
                    AppendEncode(temp_span);
                    writer.WriteLine($"""{temp_sb}.Append("]=");""");

                    AppendStringConvert(kvp.ValueType, value, method.LowercaseQueryStrings || parameter.IsLowercase);
                    AppendEncode(temp_span);

                    // emit first query
                    writer.WriteLine($"{temp_firstQuery} = false;");

                    if (kvp.ValueType.IsNullAssignable())
                    {
                        writer.EndBlock();
                    }

                    writer.EndBlock();
                }
                else
                {
                    var element = GetNextTemp();
                    writer.WriteLine($"foreach (var {element} in {parameter.Name})");
                    writer.StartBlock();

                    if (elementType.IsNullAssignable())
                    {
                        AppendIfNotNull(element);
                        writer.StartBlock();
                    }

                    AppendQueryName(queryName);
                    AppendStringConvert(elementType, element, method.LowercaseQueryStrings || parameter.IsLowercase);
                    AppendEncode(temp_span);

                    // emit first query
                    writer.WriteLine($"{temp_firstQuery} = false;");

                    if (elementType.IsNullAssignable())
                    {
                        writer.EndBlock();
                    }
                    writer.EndBlock();
                }
            }
            else
            {
                AppendQueryName(queryName);
                AppendStringConvert(parameter.Type, parameter.Name, method.LowercaseQueryStrings || parameter.IsLowercase);
                AppendEncode(temp_span);

                // emit first query
                writer.WriteLine($"{temp_firstQuery} = false;");
            }

            if (parameter.Type.IsNullAssignable())
            {
                writer.EndBlock();
            }
        }

        if (method.Fragment is FragmentParameter fragment)
        {
            writer.WriteLine();
            AppendIfNotNull(fragment.Name);
            writer.StartBlock();
            writer.WriteLine($"{temp_sb}.Append('#');");
            AppendStringConvert(fragment.Type, fragment.Name, false);
            AppendEncode(temp_span);
            writer.EndBlock();
        }

        writer.WriteLine($"return {temp_sb}.ToString();");

        // end try
        writer.EndBlock();

        // finally
        writer.WriteLine("finally");
        writer.StartBlock();
        writer.WriteLine($"{RouteHelper}.Return({temp_sb});");
        writer.EndBlock();

        string GetNextTemp()
        {
            string next;
            do
            {
                next = $"temp{tempCounter++}";
            }
            while (!localNames.Add(next));

            return next;
        }

        void AppendSlash()
        {
            writer.WriteLine($"{temp_sb}.Append('/');");
        }

        void AppendQueryName(string queryName)
        {
            writer.WriteLine($"{temp_sb}.Append({temp_firstQuery} ? '?' : '&');");
            writer.WriteLine($"""{temp_sb}.Append($"{queryName}=");""");
        }

        void AppendStringConvert(TypeRef type, string name, bool lowercase)
        {
            writer.Write($"{temp_span} = ");
            if (type.SpecialType is SpecialType.System_String)
            {
                writer.Write(name);
            }
            else
            {
                writer.Write($"global::System.Convert.ToString({name})");
            }

            if (lowercase)
            {
                writer.Write("?.ToLowerInvariant()");
            }
            writer.WriteLine(";");
        }

        void AppendEncode(string span)
        {
            writer.WriteLine($"{RouteHelper}.EncodeSpan({temp_sb}, {span}, {temp_buffer});");
        }

        void AppendIfNotNull(string value)
        {
            writer.WriteLine($"if ({value} is not null)");
        }
    }
    private static void EmitHelperClass(CodeWriter writer)
    {
        const string AggressiveInlining = "global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)";


        writer.WriteLine($"[global::System.CodeDom.Compiler.{_generatedCodeAttribute}]");
        writer.WriteLine($$"""
            file static class {{RouteHelper}}
            {
                private static readonly global::Microsoft.Extensions.ObjectPool.ObjectPool<global::{{Constants.StringBuilder}}> _pool = global::Microsoft.Extensions.ObjectPool.ObjectPool.Create(
                    new global::Microsoft.Extensions.ObjectPool.StringBuilderPooledObjectPolicy());
                private static readonly global::{{Constants.UrlEncoder}} _encoder = global::{{Constants.UrlEncoder}}.Default;

                [{{AggressiveInlining}}]
                public static global::{{Constants.StringBuilder}} Rent()
                {
                    return _pool.Get();
                }

                [{{AggressiveInlining}}]
                public static void Return(global::{{Constants.StringBuilder}} obj)
                {
                    _pool.Return(obj);
                }

                [{{AggressiveInlining}}]
                public static string Encode(string? value)
                {
                    return _encoder.Encode(value ?? string.Empty);
                }

                public static void EncodeSpan(global::{{Constants.StringBuilder}} sb, global::System.ReadOnlySpan<char> s, global::System.Span<char> buffer)
                {
                    global::System.Buffers.OperationStatus status;
                    global::{{Constants.UrlEncoder}} encoder = _encoder;
                    do
                    {
                        status = encoder.Encode(s, buffer, out int consumed, out int written, isFinalBlock: s.IsEmpty);

                        sb.Append(buffer.Slice(0, written));
                        s = s.Slice(consumed);
                    }
                    while (status != global::System.Buffers.OperationStatus.Done);
                }
            }
            """);
    }

    private sealed record RouteMethod(
        RouteType DeclaringType,
        string MemberName,
        string? Modifiers,
        TypeRef ReturnType,
        string RawPattern,
        bool LowercaseUrls,
        bool LowercaseQueryStrings,
        bool AppendTrailingSlash,
        EquatableArray<ParameterBase> Parameters,
        EquatableArray<PatternSegment> Segments,
        FragmentParameter? Fragment);

    private sealed record RouteType(string? Namespace, string TypeName, string Keyword)
    {
        public RouteType? Parent { get; set; }
    }
}
