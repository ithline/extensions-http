using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Ithline.Extensions.Http.SourceGeneration.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ithline.Extensions.Http.SourceGeneration;

internal sealed class RouteGeneratorParser
{
    private static readonly char[] _invalidParameterNameChars = [Separator, OpenBrace, CloseBrace, QuestionMark, '*'];
    private static readonly SymbolDisplayFormat _fullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private const char Separator = '/';
    private const char OpenBrace = '{';
    private const char CloseBrace = '}';
    private const char QuestionMark = '?';
    private const string PeriodString = ".";

    private readonly KnownTypeSymbols _symbols;
    private readonly DiagnosticCollector _diagnostics;

    public RouteGeneratorParser(KnownTypeSymbols symbols, DiagnosticCollector diagnostics)
    {
        _symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public bool TryParseMethod(
        GeneratedRouteAttribute attribute,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out PatternMethod? result)
    {
        // do not support generic methods
        if (attribute.IsGeneric)
        {
            _diagnostics.Add(Descriptors.MethodCannotBeGeneric, attribute.IdentifierLocation);

            result = null;
            return false;
        }

        // method must return System.String
        if (attribute.ReturnType.SpecialType is not SpecialType.System_String)
        {
            _diagnostics.Add(Descriptors.MethodMustReturnString, attribute.ReturnTypeLocation);

            result = null;
            return false;
        }

        // method cannot have body
        if (attribute.HasBody)
        {
            _diagnostics.Add(Descriptors.MethodCannotHaveBody, attribute.Location);

            result = null;
            return false;
        }

        if (!attribute.IsStatic || !attribute.IsPartial)
        {
            _diagnostics.Add(Descriptors.MethodMustBeStaticPartial, attribute.Location);

            result = null;
            return false;
        }

        var parameters = new List<PatternParameter>();
        foreach (var parameterSymbol in attribute.Parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.TryParseParameter(parameterSymbol, out var parameter))
            {
                result = null;
                return false;
            }

            parameters.Add(parameter);
        }

        // pattern prefix is not valid
        if (!TrimPrefix(attribute.Pattern, out var trimmedPattern))
        {
            _diagnostics.Add(Descriptors.PatternIsNotValid, attribute.Location);

            result = null;
            return false;
        }

        var parsingContext = new PatternParsingContext(
            pattern: trimmedPattern,
            location: attribute.Location,
            parameters: parameters);

        var segments = this.TryParsePattern(parsingContext);
        if (segments is null)
        {
            result = null;
            return false;
        }

        // fix unmatched route parameters to query
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i] is RoutePatternParameter routeParameter && !parsingContext.IsParameterBound(routeParameter))
            {
                parameters[i] = new QueryPatternParameter
                {
                    ParameterName = routeParameter.ParameterName,
                    QueryName = routeParameter.ParameterName,
                    ParameterType = routeParameter.ParameterType,
                    IsNullable = routeParameter.IsNullable,
                    IsEnumerable = routeParameter.IsEnumerable,
                    IsInteger = routeParameter.IsInteger,
                    IsString = routeParameter.IsString,
                    IsLowercase = false,
                };
            }
        }

        // validate parameter optionality
        FragmentPatternParameter? fragmentParameter = null;
        foreach (var parameter in parameters)
        {
            // query parameter must be nullable
            //if (parameter is QueryPatternParameter { IsNullable: false, IsEnumerable: false })
            //{
            //    _diagnostics.Add(Descriptors.ParameterMustBeNullableIfOptionalOrQuery, attribute.Location, parameter.ParameterName);

            //    result = null;
            //    return false;
            //}

            // optional route parameters must be nullable
            //if (parameter is RoutePatternParameter { IsOptional: true, IsNullable: false })
            //{
            //    _diagnostics.Add(Descriptors.ParameterMustBeNullableIfOptionalOrQuery, attribute.Location, parameter.ParameterName);

            //    result = null;
            //    return false;
            //}

            // required route parameters cannot be nullable
            if (parameter is RoutePatternParameter { ParameterKind: RoutePatternParameterKind.Standard, IsNullable: true })
            {
                _diagnostics.Add(Descriptors.RequiredRouteParameterCannotBeNullable, attribute.Location, parameter.ParameterName);

                result = null;
                return false;
            }

            if (parameter is FragmentPatternParameter fp)
            {
                if (fragmentParameter is not null)
                {
                    _diagnostics.Add(Descriptors.MethodCanHaveOnlyOneFragmentParameter, attribute.Location, parameter.ParameterName);

                    result = null;
                    return false;
                }

                fragmentParameter = fp;
            }
        }

        var containingType = ParseType(attribute.ClassDeclaration);
        if (containingType is null)
        {
            // TODO: diagnostic

            result = null;
            return false;
        }

        result = new PatternMethod
        {
            MethodName = attribute.Name,
            MethodModifiers = attribute.Modifiers,
            ContainingType = containingType,

            RawPattern = attribute.Pattern,
            LowercaseUrls = attribute.LowercaseUrls,
            LowercaseQueryStrings = attribute.LowercaseQueryStrings,
            AppendTrailingSlash = attribute.AppendTrailingSlash,
            Segments = [.. segments],
            Parameters = [.. parameters],
            Fragment = fragmentParameter,
        };
        return true;
    }

    private bool TryParseParameter(IParameterSymbol symbol, [NotNullWhen(true)] out PatternParameter? result)
    {
        // semantic problem, just bail
        if (string.IsNullOrWhiteSpace(symbol.Name))
        {
            result = null;
            return false;
        }

        var parameterType = symbol.Type;

        // if parameter is error type, just bail
        if (parameterType is IErrorTypeSymbol)
        {
            result = null;
            return false;
        }

        // do not support parameter names with _ prefix
        if (symbol.Name[0] is '_')
        {
            _diagnostics.Add(Descriptors.ParameterNameCannotStartWithUnderscore, symbol.Locations[0]);

            result = null;
            return false;
        }

        // do not support any parameter modifiers
        if (symbol.RefKind is not RefKind.None)
        {
            _diagnostics.Add(Descriptors.ParameterCannotHaveRefModifier, symbol.Locations[0], symbol.Name);

            result = null;
            return false;
        }

        // do not support params
        if (symbol.IsParams || symbol.IsParamsArray || symbol.IsParamsCollection)
        {
            _diagnostics.Add(Descriptors.MethodParameterCannotBeParams, symbol.Locations[0], symbol.Name);

            result = null;
            return false;
        }

        var typeName = parameterType.ToDisplayString(_fullyQualifiedFormat);
        var unwrappedType = parameterType.UnwrapTypeSymbol(unwrapNullable: true);
        var isString = unwrappedType.SpecialType is SpecialType.System_String;
        var isInteger = unwrappedType.SpecialType
            is SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64;
        var isEnumerable = unwrappedType.Implements(_symbols.IEnumerable);
        var isNullable = parameterType.NullableAnnotation is NullableAnnotation.Annotated;

        var attributes = symbol.GetAttributes();
        if (attributes.TryGetAttribute(_symbols.QueryAttribute, out var generatedQueryData)
            & attributes.TryGetAttribute(_symbols.FragmentAttribute, out var generatedFragmentData))
        {
            _diagnostics.Add(Descriptors.ParameterIsMarkedAsQueryAndFragment, symbol.Locations[0], symbol.Name);

            result = null;
            return false;
        }
        else if (generatedQueryData is not null)
        {
            _ = generatedQueryData.TryGetNamedArgumentValue("Name", out string? queryName);
            _ = generatedQueryData.TryGetNamedArgumentValue("LowercaseValue", out bool lowercaseValue);

            result = new QueryPatternParameter
            {
                ParameterName = symbol.Name,
                QueryName = string.IsNullOrWhiteSpace(queryName) ? symbol.Name : queryName!,
                ParameterType = typeName,
                IsNullable = isNullable,
                IsEnumerable = isEnumerable,
                IsInteger = isInteger,
                IsString = isString,
                IsLowercase = lowercaseValue,
            };
            return true;
        }
        else if (generatedFragmentData is not null)
        {
            result = new FragmentPatternParameter
            {
                ParameterName = symbol.Name,
                ParameterType = typeName,
                IsNullable = isNullable,
                IsEnumerable = isEnumerable,
                IsInteger = isInteger,
                IsString = isString,
            };
            return true;
        }
        else
        {
            result = new RoutePatternParameter
            {
                ParameterName = symbol.Name,
                ParameterType = typeName,
                IsNullable = isNullable,
                IsEnumerable = isEnumerable,
                IsInteger = isInteger,
                IsString = isString,
            };
            return true;
        }
    }

    private IReadOnlyList<PatternSegment>? TryParsePattern(PatternParsingContext context)
    {
        var segments = new List<PatternSegment>();
        while (context.MoveNext())
        {
            var i = context.Index;

            if (context.Current == Separator)
            {
                // If we get here is means that there's a consecutive '/' character.
                // Templates don't start with a '/' and parsing a segment consumes the separator.
                _diagnostics.Add(Descriptors.PatternCannotHaveConsecutiveSeparators, context.Location);
                return null;
            }

            if (!this.TryParsePatternSegment(context, out var segment))
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
                if (part is not RoutePatternParameter { IsCatchAll: true })
                {
                    continue;
                }

                // if catch-all is not last element in last segment, we fail
                if (i != segments.Count - 1 || j != segment.Count - 1)
                {
                    _diagnostics.Add(Descriptors.PatternCatchAllMustBeLast, context.Location);
                    return null;
                }
            }
        }

        return segments;
    }
    private bool TryParsePatternSegment(PatternParsingContext context, [NotNullWhen(true)] out PatternSegment? result)
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
                    _diagnostics.Add(Descriptors.PatternHasMismatchedParameter, context.Location);

                    result = null;
                    return false;
                }

                // This is an 'escaped' brace in a literal, like "{{foo"
                if (context.Current == OpenBrace)
                {
                    context.Back();
                    if (!this.TryParsePatternLiteral(context, out var literal))
                    {
                        result = null;
                        return false;
                    }

                    parts.Add(literal);
                }
                else
                {
                    // this is a parameter
                    context.Back();
                    if (!this.TryParsePatternParameter(context, out var parameter))
                    {
                        result = null;
                        return false;
                    }

                    parts.Add(parameter);
                }
            }
            else
            {
                if (!this.TryParsePatternLiteral(context, out var literal))
                {
                    result = null;
                    return false;
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
            result = new PatternSegment(parts);
            return true;
        }

        // validate segment
        List<IPatternSegmentPart>? toRemove = null;
        var previousSegmentParameter = false;
        for (var i = 0; i < parts.Count; i++)
        {
            // If a segment has multiple parts, then it can't contain a catch all.
            if (parts[i] is RoutePatternParameter { IsCatchAll: true })
            {
                _diagnostics.Add(Descriptors.PatternCannotHaveCatchAllInMultiSegment, context.Location);

                result = null;
                return false;
            }

            // if current part is not parameter, we good
            if (parts[i] is not RoutePatternParameter parameter)
            {
                previousSegmentParameter = false;
                continue;
            }

            // if previous value was parameter, we must fail
            if (previousSegmentParameter)
            {
                _diagnostics.Add(Descriptors.PatternCannotHaveConsecutiveParameters, context.Location);

                result = null;
                return false;
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
                _diagnostics.Add(
                    Descriptors.PatternOptionalParameterHasToBeLast,
                    context.Location,
                    PatternSegment.ToString(parts),
                    parameter.ParameterName,
                    parts[i + 1]);

                result = null;
                return false;
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

            _diagnostics.Add(
                Descriptors.PatternOptionalParameterCanOnlyBePrecededByPeriod,
                context.Location,
                PatternSegment.ToString(parts),
                parameter.ParameterName,
                previousPart);

            result = null;
            return false;
        }

        foreach (var part in toRemove ?? [])
        {
            parts.Remove(part);
        }

        result = new PatternSegment(parts);
        return true;
    }
    private bool TryParsePatternLiteral(PatternParsingContext context, [NotNullWhen(true)] out PatternLiteral? literal)
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
                    _diagnostics.Add(Descriptors.PatternHasMismatchedParameter, context.Location);

                    literal = null;
                    return false;
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
                    _diagnostics.Add(Descriptors.PatternHasMismatchedParameter, context.Location);

                    literal = null;
                    return false;
                }

                if (context.Current == CloseBrace)
                {
                    // This is an 'escaped' brace in a literal, like "{{foo" - keep going.
                }
                else
                {
                    // This is an unbalanced close-brace, which is not allowed
                    _diagnostics.Add(Descriptors.PatternHasMismatchedParameter, context.Location);

                    literal = null;
                    return false;
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
            _diagnostics.Add(Descriptors.PatternHasInvalidLiteral, context.Location);

            literal = null;
            return false;
        }

        literal = new PatternLiteral(decoded);
        return true;
    }
    private bool TryParsePatternParameter(PatternParsingContext context, [NotNullWhen(true)] out RoutePatternParameter? result)
    {
        Debug.Assert(context.Current == OpenBrace);
        context.Mark();

        context.MoveNext();

        while (true)
        {
            if (context.Current == OpenBrace)
            {
                // This is an open brace inside of a parameter, it has to be escaped
                if (context.MoveNext())
                {
                    if (context.Current != OpenBrace)
                    {
                        // If we see something like "{p1:regex(^\d{3", we will come here.
                        _diagnostics.Add(Descriptors.PatternUnescapedBrace, context.Location);

                        result = null;
                        return false;
                    }
                }
                else
                {
                    // This is a dangling open-brace, which is not allowed
                    // Example: "{p1:regex(^\d{"
                    _diagnostics.Add(Descriptors.PatternHasMismatchedParameter, context.Location);

                    result = null;
                    return false;
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
                _diagnostics.Add(Descriptors.PatternHasMismatchedParameter, context.Location);

                result = null;
                return false;
            }
        }

        var text = context.Capture();
        if (text is null or "{}")
        {
            _diagnostics.Add(Descriptors.PatternHasInvalidParameterName, context.Location);

            result = null;
            return false;
        }

        var parameterPattern = text[1..^1]
            .Replace("}}", "}")
            .Replace("{{", "{")
            .AsSpan();

        // At this point, we need to parse the raw name for inline constraint,
        // default values and optional parameters.

        var encodeSlashes = true;
        var parameterKind = RoutePatternParameterKind.Standard;
        if (parameterPattern.StartsWith("**", StringComparison.Ordinal))
        {
            encodeSlashes = false;
            parameterKind = RoutePatternParameterKind.CatchAll;
            parameterPattern = parameterPattern.Slice(2);
        }
        else if (parameterPattern[0] == '*')
        {
            parameterKind = RoutePatternParameterKind.CatchAll;
            parameterPattern = parameterPattern.Slice(1);
        }

        // parameter is optional
        if (parameterPattern[^1] == '?')
        {
            // cannot have catch-all optional parameter
            if (parameterKind is RoutePatternParameterKind.CatchAll)
            {
                _diagnostics.Add(Descriptors.PatternCatchAllCannotBeOptional, context.Location);

                result = null;
                return false;
            }

            parameterKind = RoutePatternParameterKind.Optional;
            parameterPattern = parameterPattern[0..^1];
        }

        // Parse parameter name
        var parameterName = ParseParameterName(parameterPattern);
        if (parameterName.Length == 0 || parameterName.IndexOfAny(_invalidParameterNameChars) >= 0)
        {
            _diagnostics.Add(Descriptors.PatternHasInvalidParameterName, context.Location);

            result = null;
            return false;
        }

        // we try to find a parameter
        if (!context.TryGetParameter(parameterName, out var patternParameter))
        {
            _diagnostics.Add(Descriptors.PatternParameterMissingFromMethodArguments, context.Location, parameterName);

            result = null;
            return false;
        }

        // if parameter is already marked as query or fragment, we fail
        if (patternParameter is not RoutePatternParameter routeParameter)
        {
            _diagnostics.Add(Descriptors.PatternParameterIsMarkedAsQueryOrFragment, context.Location, parameterName);

            result = null;
            return false;
        }

        // if we cannot find unconsumed route parameter, we fail
        if (!context.TryBindParameter(routeParameter))
        {
            _diagnostics.Add(Descriptors.PatternHasRepeatedParameter, context.Location, parameterName);

            result = null;
            return false;
        }

        result = routeParameter;
        result.EncodeSlashes = encodeSlashes;
        result.ParameterKind = parameterKind;
        return true;

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
    private static PatternType? ParseType(TypeDeclarationSyntax? syntax)
    {
        if (syntax is null)
        {
            return null;
        }

        var typeName = ResolveTypeName(syntax);
        var ns = ResolveNamespace(syntax);
        var parent = ParseType(syntax.Parent as TypeDeclarationSyntax);

        return new PatternType
        {
            TypeName = typeName,
            Namespace = ns,
            Keyword = syntax.Keyword.ToString(),
            Parent = parent,
        };

        static string ResolveTypeName(TypeDeclarationSyntax typeDeclaration)
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

        static string? ResolveNamespace(TypeDeclarationSyntax classDeclaration)
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

    private static bool TrimPrefix([NotNullWhen(true)] string? routePattern, [NotNullWhen(true)] out string? result)
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

    private sealed class PatternParsingContext
    {
        private readonly HashSet<string> _parametersBound = new(StringComparer.OrdinalIgnoreCase);
        private readonly IReadOnlyList<PatternParameter> _parameters;

        private readonly string _template;
        [SuppressMessage("Style", "IDE0032:Use auto property")]
        private int _index;
        private int? _mark;

        public PatternParsingContext(string pattern, Location location, IReadOnlyList<PatternParameter> parameters)
        {
            _template = pattern;
            Location = location;
            _parameters = parameters;

            _index = -1;
        }

        public int Index => _index;
        public char Current => _index < _template.Length && _index >= 0 ? _template[_index] : (char)0;
        public Location Location { get; }

        public bool IsParameterBound(RoutePatternParameter parameter)
        {
            return _parametersBound.Contains(parameter.ParameterName);
        }

        public bool TryBindParameter(RoutePatternParameter parameter)
        {
            return _parametersBound.Add(parameter.ParameterName);
        }

        public bool TryGetParameter(string parameterName, [NotNullWhen(true)] out PatternParameter? result)
        {
            foreach (var parameter in _parameters)
            {
                if (string.Equals(parameterName, parameter.ParameterName, StringComparison.OrdinalIgnoreCase))
                {
                    result = parameter;
                    return true;
                }
            }

            result = null;
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
