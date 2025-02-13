using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ithline.Extensions.Http.SourceGeneration;

[Generator]
public sealed class RouteGenerator : IIncrementalGenerator
{
    private static readonly char[] _invalidParameterNameChars = [Separator, OpenBrace, CloseBrace, QuestionMark, '*'];
    private static readonly string _generatedCodeAttribute = $"GeneratedCodeAttribute(\"{typeof(RouteGenerator).Assembly.GetName().Name}\", \"{typeof(RouteGenerator).Assembly.GetName().Version}\")";

    private const string RouteHelper = "__GeneratedRouteHelper";

    private const char Separator = '/';
    private const char OpenBrace = '{';
    private const char CloseBrace = '}';
    private const char QuestionMark = '?';
    private const string PeriodString = ".";

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

        TypeRef returnType;
        FragmentParameter? fragmentParameter = null;
        List<RouteMethodParameter> parameters = [];
        if (context.TargetSymbol is IMethodSymbol methodSymbol)
        {
            if (methodSymbol.IsGenericMethod
                || methodSymbol.Arity != 0
                || methodSymbol.IsExtensionMethod
                || methodSymbol.IsAbstract
                || !methodSymbol.IsStatic
                || !methodSymbol.IsPartialDefinition
                || methodSymbol.ReturnType.SpecialType is not SpecialType.System_String)
            {
                // return invalid signature
                return DiagnosticData.Create(DiagnosticDescriptors.InvalidSignature, memberSyntax);
            }

            returnType = TypeRef.Create(methodSymbol.ReturnType);

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

                var parameterTypeRef = TypeRef.Create(parameterSymbol.Type);
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
                    var queryName = queryAttribute.GetNamedArgumentValue<string>("Name");
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
                    parameters.Add(new PatternParameter
                    {
                        Name = parameterSymbol.Name,
                        Type = parameterTypeRef,
                        Number = number++,
                    });
                }
            }
        }
        else if (context.TargetSymbol is IPropertySymbol propertySymbol)
        {
            if (propertySymbol.IsAbstract
                || !propertySymbol.IsStatic
                || !memberSyntax.Modifiers.Any(SyntaxKind.PartialKeyword) // TODO: Switch to using regexPropertySymbol.IsPartialDefinition when available
                || propertySymbol.SetMethod is not null
                || propertySymbol.Type.SpecialType is not SpecialType.System_String)
            {
                // return valid signature
                return DiagnosticData.Create(DiagnosticDescriptors.InvalidSignature, memberSyntax);
            }

            returnType = TypeRef.Create(propertySymbol.Type);
        }
        else
        {
            return null;
        }

        // extract attribute configuration
        var generatedRouteAttribute = context.Attributes[0];
        if (generatedRouteAttribute.ConstructorArguments.Length != 1)
        {
            return null;
        }

        var rawPattern = generatedRouteAttribute.ConstructorArguments[0].Value as string;

        if (!Helpers.TrimUrlPrefix(rawPattern, out var pattern) || string.IsNullOrWhiteSpace(pattern))
        {
            // pattern is not valid
            return DiagnosticData.Create(DiagnosticDescriptors.InvalidRoutePattern, memberSyntax);
        }

        var parsingContext = new PatternParsingContext(pattern, parameters);
        var segments = ParsePattern(parsingContext);
        if (segments is null)
        {
            // invalid pattern
            return DiagnosticData.Create(DiagnosticDescriptors.InvalidRoutePattern, memberSyntax);
        }

        for (var i = 0; i < parameters.Count; i++)
        {
            // fix unmatched pattern parameters to query
            if (parameters[i] is PatternParameter patternParameter && !parsingContext.IsParameterBound(patternParameter))
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
            if (parameters[i] is PatternParameter { ParameterKind: PatternParameterKind.Standard, Type.NullableAnnotation: NullableAnnotation.Annotated })
            {
                // invalid signature
                return DiagnosticData.Create(DiagnosticDescriptors.InvalidSignature, memberSyntax);
            }
        }

        return new RouteMethod(
            ParseType(parentSyntax),
            context.TargetSymbol is IPropertySymbol,
            context.TargetSymbol.Name,
            memberSyntax.Modifiers.ToString(),
            returnType,
            rawPattern,
            generatedRouteAttribute.GetNamedArgumentValue<bool>("LowercaseUrls"),
            generatedRouteAttribute.GetNamedArgumentValue<bool>("LowercaseQueryStrings"),
            generatedRouteAttribute.GetNamedArgumentValue<bool>("AppendTrailingSlash"),
            [.. parameters],
            [.. segments],
            fragmentParameter);

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
    private static IReadOnlyList<PatternSegment>? ParsePattern(PatternParsingContext context)
    {
        var segments = new List<PatternSegment>();
        while (context.MoveNext())
        {
            var i = context.Index;

            if (context.Current == Separator)
            {
                // If we get here is means that there's a consecutive '/' character.
                // Templates don't start with a '/' and parsing a segment consumes the separator.
                return null;
            }

            if (ParseSegment() is not PatternSegment segment)
            {
                return null;
            }

            segments.Add(segment);

            // A successful parse should always result in us being at the end or at a separator.
            Debug.Assert(context.AtEnd() || context.Current == Separator);

            if (context.Index <= i)
            {
                // This shouldn't happen, but we want to crash if it does.
                var message = "Infinite loop detected in the parser. Please open an issue.";
                throw new InvalidProgramException(message);
            }
        }

        // validate segments
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            for (var j = 0; j < segment.Count; j++)
            {
                var part = segment[j];

                // we only care about catch-all parameters
                if (part is not PatternParameter { IsCatchAll: true })
                {
                    continue;
                }

                // if catch-all is not last element in last segment, we fail
                if (i != segments.Count - 1 || j != segment.Count - 1)
                {
                    return null;
                }
            }
        }

        return segments;

        PatternSegment? ParseSegment()
        {
            var parts = new List<IPatternSegmentPart>();
            while (true)
            {
                var i = context.Index;
                if (context.Current == OpenBrace)
                {
                    // This is a dangling open-brace, which is not allowed
                    if (!context.MoveNext())
                    {
                        return null;
                    }

                    // This is an 'escaped' brace in a literal, like "{{foo"
                    if (context.Current == OpenBrace)
                    {
                        context.Back();
                        if (ParseLiteral() is not PatternLiteral literal)
                        {
                            return null;
                        }

                        parts.Add(literal);
                    }
                    else
                    {
                        // this is a parameter
                        context.Back();
                        if (ParseParameter() is not PatternParameter parameter)
                        {
                            return null;
                        }

                        parts.Add(parameter);
                    }
                }
                else
                {
                    if (ParseLiteral() is not PatternLiteral literal)
                    {
                        return null;
                    }

                    parts.Add(literal);
                }

                // We've reached the end of the segment
                if (context.Current == Separator || context.AtEnd())
                {
                    break;
                }

                if (context.Index <= i)
                {
                    // This shouldn't happen, but we want to crash if it does.
                    var message = "Infinite loop detected in the parser. Please open an issue.";
                    throw new InvalidProgramException(message);
                }
            }

            // if we have 0 or 1 parts, we good
            if (parts.Count <= 1)
            {
                return new PatternSegment(parts);
            }

            // validate segment
            List<IPatternSegmentPart>? toRemove = null;
            var previousSegmentParameter = false;
            for (var i = 0; i < parts.Count; i++)
            {
                // If a segment has multiple parts, then it can't contain a catch all.
                if (parts[i] is PatternParameter { IsCatchAll: true })
                {
                    return null;
                }

                // if current part is not parameter, we good
                if (parts[i] is not PatternParameter parameter)
                {
                    previousSegmentParameter = false;
                    continue;
                }

                // if previous value was parameter, we must fail
                if (previousSegmentParameter)
                {
                    return null;
                }
                previousSegmentParameter = true;

                // if parameter is required, we good
                if (!parameter.IsOptional)
                {
                    continue;
                }

                // if optional parameter is not the last part in segment, we have problem
                if (i != parts.Count - 1)
                {
                    return null;
                }

                // if optional parameter is preceded by separator '.', we good
                var previousPart = parts[i - 1];
                if (previousPart is PatternLiteral literal && literal.Content == PeriodString)
                {
                    // we set parameter to apply optional separator if parameter is emitted
                    parameter.HasOptionalSeparator = true;

                    // we remove current literal, because it is inlined in parameter
                    toRemove ??= [];
                    toRemove.Add(literal);

                    continue;
                }

                // The optional parameter is preceded by a literal other than period.
                // Example of error message:
                // "In the segment '{RouteValue}-{param?}', the optional parameter 'param' is preceded
                // by an invalid segment '-'. Only a period (.) can precede an optional parameter.
                return null;
            }

            foreach (var part in toRemove ?? [])
            {
                parts.Remove(part);
            }

            return new PatternSegment(parts);
        }

        PatternLiteral? ParseLiteral()
        {
            context.Mark();

            while (true)
            {
                if (context.Current == Separator)
                {
                    // End of the segment
                    break;
                }
                else if (context.Current == OpenBrace)
                {
                    if (!context.MoveNext())
                    {
                        // This is a dangling open-brace, which is not allowed
                        return null;
                    }

                    if (context.Current == OpenBrace)
                    {
                        // This is an 'escaped' brace in a literal, like "{{foo" - keep going.
                    }
                    else
                    {
                        // We've just seen the start of a parameter, so back up.
                        context.Back();
                        break;
                    }
                }
                else if (context.Current == CloseBrace)
                {
                    if (!context.MoveNext())
                    {
                        // This is a dangling close-brace, which is not allowed
                        return null;
                    }

                    if (context.Current == CloseBrace)
                    {
                        // This is an 'escaped' brace in a literal, like "{{foo" - keep going.
                    }
                    else
                    {
                        // This is an unbalanced close-brace, which is not allowed
                        return null;
                    }
                }

                if (!context.MoveNext())
                {
                    break;
                }
            }

            var encoded = context.Capture()!;
            var decoded = encoded.Replace("}}", "}").Replace("{{", "{");

            if (decoded.Contains(QuestionMark))
            {
                return null;
            }

            return new PatternLiteral(decoded);
        }

        PatternParameter? ParseParameter()
        {
            Debug.Assert(context.Current == OpenBrace);
            context.Mark();

            context.MoveNext();

            while (true)
            {
                if (context.Current == OpenBrace)
                {
                    // This is a dangling open-brace, which is not allowed
                    // Example: "{p1:regex(^\d{"
                    if (!context.MoveNext())
                    {
                        return null;
                    }

                    // This is an open brace inside of a parameter, it has to be escaped
                    // If we see something like "{p1:regex(^\d{3", we will come here.
                    if (context.Current != OpenBrace)
                    {
                        return null;
                    }
                }
                else if (context.Current == CloseBrace)
                {
                    // When we encounter Closed brace here, it either means end of the parameter or it is a closed
                    // brace in the parameter, in that case it needs to be escaped.
                    // Example: {p1:regex(([}}])\w+}. First pair is escaped one and last marks end of the parameter
                    if (!context.MoveNext())
                    {
                        // This is the end of the string -and we have a valid parameter
                        break;
                    }

                    if (context.Current == CloseBrace)
                    {
                        // This is an 'escaped' brace in a parameter name
                    }
                    else
                    {
                        // This is the end of the parameter
                        break;
                    }
                }

                if (!context.MoveNext())
                {
                    // This is a dangling open-brace, which is not allowed
                    return null;
                }
            }

            var text = context.Capture();
            if (text is null or "{}")
            {
                return null;
            }

            var parameterPattern = text[1..^1]
                .Replace("}}", "}")
                .Replace("{{", "{")
                .AsSpan();

            // At this point, we need to parse the raw name for inline constraint,
            // default values and optional parameters.

            var encodeSlashes = true;
            var parameterKind = PatternParameterKind.Standard;
            if (parameterPattern.StartsWith("**", StringComparison.Ordinal))
            {
                encodeSlashes = false;
                parameterKind = PatternParameterKind.CatchAll;
                parameterPattern = parameterPattern.Slice(2);
            }
            else if (parameterPattern[0] == '*')
            {
                parameterKind = PatternParameterKind.CatchAll;
                parameterPattern = parameterPattern.Slice(1);
            }

            // parameter is optional
            if (parameterPattern[^1] == '?')
            {
                // cannot have catch-all optional parameter
                if (parameterKind is PatternParameterKind.CatchAll)
                {
                    return null;
                }

                parameterKind = PatternParameterKind.Optional;
                parameterPattern = parameterPattern[0..^1];
            }

            // Parse parameter name
            var parameterName = ParseParameterName(parameterPattern);
            if (parameterName.Length == 0 || parameterName.IndexOfAny(_invalidParameterNameChars) >= 0)
            {
                return null;
            }

            // we try to find a parameter
            // if parameter is already marked as query or fragment, we fail
            // if we cannot find unconsumed route parameter, we fail
            if (context.TryBindParameter(parameterName, out var routeParameter))
            {
                routeParameter.EncodeSlashes = encodeSlashes;
                routeParameter.ParameterKind = parameterKind;
                return routeParameter;
            }

            return null;
        }

        static string ParseParameterName(ReadOnlySpan<char> parameter)
        {
            for (var i = 0; i < parameter.Length; i++)
            {
                if (parameter[i] is ':' or '=' && i > 0)
                {
                    return parameter.Slice(0, i).ToString();
                }
            }
            return parameter.ToString();
        }
    }

    private static void EmitRouteMethod(CodeWriter writer, RouteMethod method)
    {
        var parent = method.DeclaringType;

        // emit namespace
        if (!string.IsNullOrWhiteSpace(parent.Namespace))
        {
            writer.WriteLine($"namespace {parent.Namespace}");
            writer.StartBlock();
        }

        // emit type hierarchy
        var parentClasses = new Stack<string>();
        while (parent is not null)
        {
            parentClasses.Push($"partial {parent.Keyword} {parent.TypeName}");
            parent = parent.Parent;
        }

        while (parentClasses.Count > 0)
        {
            writer.WriteLine(parentClasses.Pop());
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

        if (method.IsProperty)
        {
            writer.Write(" => ");
            EmitInlineBody(writer, method);
            writer.WriteLine(";");
        }
        else if (method.Parameters.Count == 0)
        {
            writer.Write("() => ");
            EmitInlineBody(writer, method);
            writer.WriteLine(";");
        }
        else
        {
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
            EmitBuilderBody(writer, method);
            writer.EndBlock();
        }

        // close all scopes
        while (writer.Indent > 0)
        {
            writer.EndBlock();
        }

        static void EmitInlineBody(CodeWriter writer, RouteMethod method)
        {
            string? last = null;

            writer.Write('"');
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

            writer.Write('"');
        }

        static void EmitBuilderBody(CodeWriter writer, RouteMethod method)
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
                    else if (part is PatternParameter parameter)
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

                        // if we have to encode slashes, we emit split first and then default encode will handle the rest
                        if (!parameter.EncodeSlashes)
                        {
                            var index = GetNextTemp();
                            writer.WriteLine($"int {index};");
                            writer.WriteLine($"while (({index} = {temp_span}.IndexOf('/')) >= 0)");
                            writer.StartBlock();
                            AppendEncode($"{temp_span}.Slice(0, {index})");
                            writer.WriteLine($"{temp_span} = {temp_span}.Slice({index} + 1);");
                            AppendSlash();
                            writer.EndBlock();
                        }

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
                writer.WriteLine($"""{temp_sb}.Append("{queryName}");""");
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
        bool IsProperty,
        string MemberName,
        string? Modifiers,
        TypeRef ReturnType,
        string RawPattern,
        bool LowercaseUrls,
        bool LowercaseQueryStrings,
        bool AppendTrailingSlash,
        EquatableArray<RouteMethodParameter> Parameters,
        EquatableArray<PatternSegment> Segments,
        FragmentParameter? Fragment);

    private sealed record RouteType(string? Namespace, string TypeName, string Keyword)
    {
        public RouteType? Parent { get; set; }
    }

    private abstract record RouteMethodParameter
    {
        public required string Name { get; init; }
        public required TypeRef Type { get; init; }
        public required int Number { get; init; }
    }

    private sealed record QueryParameter : RouteMethodParameter
    {
        public required string QueryName { get; init; }
        public required bool IsLowercase { get; init; }
    }

    private sealed record FragmentParameter : RouteMethodParameter { }

    private sealed class PatternSegment : IReadOnlyList<IPatternSegmentPart>, IEquatable<PatternSegment>
    {
        private readonly IPatternSegmentPart[] _parts;

        public PatternSegment(IEnumerable<IPatternSegmentPart> parts)
        {
            _parts = parts.ToArray();
        }

        public int Count => _parts.Length;
        public bool IsSimple => Count == 1;
        public IPatternSegmentPart this[int index] => _parts[index];

        public bool Equals(PatternSegment other) => other is not null && MemoryExtensions.SequenceEqual(_parts.AsSpan(), other._parts);
        public override bool Equals(object obj) => obj is PatternSegment other && this.Equals(other);
        public override int GetHashCode()
        {
            HashCode hashCode = default;

            foreach (var item in _parts)
            {
                hashCode.Add(item);
            }

            return hashCode.ToHashCode();
        }

        public override string ToString() => ToString(_parts);
        public static string ToString(IEnumerable<IPatternSegmentPart> parts) => string.Join(string.Empty, parts);

        public IEnumerator<IPatternSegmentPart> GetEnumerator() => _parts.OfType<IPatternSegmentPart>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    private interface IPatternSegmentPart : IEquatable<IPatternSegmentPart> { }

    private sealed record PatternLiteral : IPatternSegmentPart
    {
        public PatternLiteral(string content)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        /// <summary>
        /// Gets the text content.
        /// </summary>
        public string Content { get; }

        public bool Equals(IPatternSegmentPart other)
        {
            return other is PatternLiteral literal && this.Equals(literal);
        }

        public override string ToString()
        {
            return Content;
        }
    }

    private sealed record PatternParameter : RouteMethodParameter, IPatternSegmentPart
    {
        public bool EncodeSlashes { get; set; } = true;
        public bool HasOptionalSeparator { get; set; }
        public PatternParameterKind ParameterKind { get; set; }
        public bool IsCatchAll => ParameterKind is PatternParameterKind.CatchAll;
        public bool IsOptional => ParameterKind is PatternParameterKind.Optional;

        public bool Equals(IPatternSegmentPart other) => other is PatternParameter obj && this.Equals(obj);

        public override string ToString()
        {
            var builder = new StringBuilder();
            if (HasOptionalSeparator)
            {
                builder.Append('.');
            }

            builder.Append('{');

            if (IsCatchAll)
            {
                builder.Append('*');
                if (!EncodeSlashes)
                {
                    builder.Append('*');
                }
            }

            builder.Append(Name);

            if (IsOptional)
            {
                builder.Append('?');
            }

            builder.Append('}');
            return builder.ToString();
        }
    }

    private enum PatternParameterKind
    {
        /// <summary>
        /// The <see cref="PatternParameterKind"/> of a standard parameter
        /// without optional or catch all behavior.
        /// </summary>
        Standard,

        /// <summary>
        /// The <see cref="PatternParameterKind"/> of an optional parameter.
        /// </summary>
        Optional,

        /// <summary>
        /// The <see cref="PatternParameterKind"/> of a catch-all parameter.
        /// </summary>
        CatchAll,
    }

    private sealed class PatternParsingContext
    {
        private readonly HashSet<string> _parametersBound = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PatternParameter> _parameters;

        private readonly string _template;
        [SuppressMessage("Style", "IDE0032:Use auto property")]
        private int _index;
        private int? _mark;

        public PatternParsingContext(string pattern, IEnumerable<RouteMethodParameter> parameters)
        {
            _template = pattern;
            _parameters = parameters
                .OfType<PatternParameter>()
                .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

            _index = -1;
        }

        public int Index => _index;
        public char Current => _index < _template.Length && _index >= 0 ? _template[_index] : (char)0;

        public bool IsParameterBound(RouteMethodParameter parameter)
        {
            return _parametersBound.Contains(parameter.Name);
        }

        public bool TryBindParameter(string parameterName, [NotNullWhen(true)] out PatternParameter? parameter)
        {
            if (_parameters.Remove(parameterName, out parameter) && parameter is not null)
            {
                _parametersBound.Add(parameter.Name);
                return true;
            }

            parameter = null;
            return false;
        }

        public bool Back()
        {
            return --_index >= 0;
        }
        public bool AtEnd()
        {
            return _index >= _template.Length;
        }
        public bool MoveNext()
        {
            return ++_index < _template.Length;
        }
        public void Mark()
        {
            Debug.Assert(_index >= 0);

            // Index is always the index of the character *past* Current - we want to 'mark' Current.
            _mark = _index;
        }
        public string? Capture()
        {
            if (_mark is int mark)
            {
                var value = _template.Substring(mark, _index - mark);
                _mark = null;
                return value;
            }
            else
            {
                return null;
            }
        }
    }
}
