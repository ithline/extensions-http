using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Ithline.Extensions.Http.SourceGeneration.Patterns;

internal static class RoutePatternParser
{
    private const char Separator = '/';
    private const char OpenBrace = '{';
    private const char CloseBrace = '}';
    private const char QuestionMark = '?';
    private const string PeriodString = ".";

    private static readonly char[] _invalidParameterNameChars = [Separator, OpenBrace, CloseBrace, QuestionMark, '*'];

    public static RoutePattern? Parse(
        string pattern,
        out DiagnosticDescriptor? descriptor,
        out object?[]? messageArgs)
    {
        if (!TrimPrefix(pattern, out var trimmedPattern))
        {
            descriptor = Descriptors.PatternIsNotValid;
            messageArgs = null;
            return null;
        }

        var context = new Context(trimmedPattern);
        var segments = new List<RoutePatternPathSegment>();

        while (context.MoveNext())
        {
            var i = context.Index;

            if (context.Current == Separator)
            {
                // If we get here is means that there's a consecutive '/' character.
                // Templates don't start with a '/' and parsing a segment consumes the separator.
                descriptor = Descriptors.PatternCannotHaveConsecutiveSeparators;
                messageArgs = null;
                return null;
            }

            if (!ParseSegment(context, segments, out descriptor, out messageArgs))
            {
                return null;
            }

            // A successful parse should always result in us being at the end or at a separator.
            Debug.Assert(context.AtEnd() || context.Current == Separator);

            if (context.Index <= i)
            {
                // This shouldn't happen, but we want to crash if it does.
                var message = "Infinite loop detected in the parser. Please open an issue.";
                throw new InvalidProgramException(message);
            }
        }

        if (IsAllValid(segments, out descriptor, out messageArgs))
        {
            return RoutePattern.Create(pattern, segments);
        }
        else
        {
            return null;
        }
    }

    private static bool ParseSegment(
        Context context,
        List<RoutePatternPathSegment> segments,
        out DiagnosticDescriptor? descriptor,
        out object?[]? messageArgs)
    {
        var parts = new List<RoutePatternPart>();

        while (true)
        {
            var i = context.Index;

            if (context.Current == OpenBrace)
            {
                if (!context.MoveNext())
                {
                    // This is a dangling open-brace, which is not allowed
                    descriptor = Descriptors.PatternHasMismatchedParameter;
                    messageArgs = null;
                    return false;
                }

                if (context.Current == OpenBrace)
                {
                    // This is an 'escaped' brace in a literal, like "{{foo"
                    context.Back();
                    if (!ParseLiteral(context, parts, out descriptor, out messageArgs))
                    {
                        return false;
                    }
                }
                else
                {
                    // This is a parameter
                    context.Back();
                    if (!ParseParameter(context, parts, out descriptor, out messageArgs))
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (!ParseLiteral(context, parts, out descriptor, out messageArgs))
                {
                    return false;
                }
            }

            if (context.Current == Separator || context.AtEnd())
            {
                // We've reached the end of the segment
                break;
            }

            if (context.Index <= i)
            {
                // This shouldn't happen, but we want to crash if it does.
                var message = "Infinite loop detected in the parser. Please open an issue.";
                throw new InvalidProgramException(message);
            }
        }

        if (IsSegmentValid(parts, out descriptor, out messageArgs))
        {
            segments.Add(new RoutePatternPathSegment { Parts = [.. parts] });
            return true;
        }
        else
        {
            return false;
        }
    }

    private static bool ParseParameter(
        Context context,
        List<RoutePatternPart> parts,
        out DiagnosticDescriptor? descriptor,
        out object?[]? messageArgs)
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
                        descriptor = Descriptors.PatternUnescapedBrace;
                        messageArgs = null;
                        return false;
                    }
                }
                else
                {
                    // This is a dangling open-brace, which is not allowed
                    // Example: "{p1:regex(^\d{"
                    descriptor = Descriptors.PatternHasMismatchedParameter;
                    messageArgs = null;
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
                descriptor = Descriptors.PatternHasMismatchedParameter;
                messageArgs = null;
                return false;
            }
        }

        var text = context.Capture();
        if (text is null or "{}")
        {
            descriptor = Descriptors.PatternHasInvalidParameterName;
            messageArgs = null;
            return false;
        }

        var parameter = text[1..^1].Replace("}}", "}").Replace("{{", "{");

        // At this point, we need to parse the raw name for inline constraint,
        // default values and optional parameters.

        var startIndex = 0;
        var endIndex = parameter.Length - 1;
        var encodeSlashes = true;

        var parameterKind = RoutePatternParameterKind.Standard;
        if (parameter.StartsWith("**", StringComparison.Ordinal))
        {
            encodeSlashes = false;
            parameterKind = RoutePatternParameterKind.CatchAll;
            startIndex += 2;
        }
        else if (parameter[0] == '*')
        {
            parameterKind = RoutePatternParameterKind.CatchAll;
            startIndex++;
        }

        if (parameter[endIndex] == '?')
        {
            if (parameterKind is RoutePatternParameterKind.CatchAll)
            {
                descriptor = Descriptors.PatternCatchAllCannotBeOptional;
                messageArgs = null;
                return false;
            }

            parameterKind = RoutePatternParameterKind.Optional;
            endIndex--;
        }

        // Parse parameter name
        var parameterName = ParseParameterName(parameter, startIndex, endIndex, out var currentIndex);
        if (parameterName.Length == 0 || parameterName.IndexOfAny(_invalidParameterNameChars) >= 0)
        {
            descriptor = Descriptors.PatternHasInvalidParameterName;
            messageArgs = null;
            return false;
        }

        if (!context.ParameterNames.Add(parameterName))
        {
            descriptor = Descriptors.PatternHasRepeatedParameter;
            messageArgs = [parameterName];
            return false;
        }

        ConsumeParameterConstraints(parameter, ref currentIndex, endIndex);

        parts.Add(new RoutePatternPartParameter
        {
            Name = parameterName,
            ParameterKind = parameterKind,
            EncodeSlashes = encodeSlashes,
        });

        descriptor = null;
        messageArgs = null;
        return true;
    }

    private static string ParseParameterName(string parameter, int startIndex, int endIndex, out int currentIndex)
    {
        currentIndex = startIndex;
        while (currentIndex <= endIndex)
        {
            var currentChar = parameter[currentIndex];

            if ((currentChar is ':' or '=') && startIndex != currentIndex)
            {
                // Parameter names are allowed to start with delimiters used to denote constraints or default values.
                // i.e. "=foo" or ":bar" would be treated as parameter names rather than default value or constraint
                // specifications.
                var parameterName = parameter.Substring(startIndex, currentIndex - startIndex);

                // Roll the index back and move to the constraint parsing stage.
                currentIndex--;

                return parameterName;
            }
            else if (currentIndex == endIndex)
            {
                return parameter.Substring(startIndex, currentIndex - startIndex + 1);
            }

            currentIndex++;
        }

        return string.Empty;
    }

    private static void ConsumeParameterConstraints(string text, ref int currentIndex, int endIndex)
    {
        var state = ParseState.Start;
        do
        {
            var currentChar = currentIndex > endIndex ? null : (char?)text[currentIndex];
            switch (state)
            {
                case ParseState.Start:
                    switch (currentChar)
                    {
                        case null:
                            state = ParseState.End;
                            break;
                        case ':':
                            state = ParseState.ParsingName;
                            break;
                        case '(':
                            state = ParseState.InsideParenthesis;
                            break;
                        case '=':
                            state = ParseState.End;
                            currentIndex--;
                            break;
                    }
                    break;
                case ParseState.InsideParenthesis:
                    switch (currentChar)
                    {
                        case null:
                            state = ParseState.End;
                            break;
                        case ')':
                            // Only consume a ')' token if
                            // (a) it is the last token
                            // (b) the next character is the start of the new constraint ':'
                            // (c) the next character is the start of the default value.

                            var nextChar = currentIndex + 1 > endIndex ? null : (char?)text[currentIndex + 1];
                            switch (nextChar)
                            {
                                case null:
                                    state = ParseState.End;
                                    break;
                                case ':':
                                    state = ParseState.Start;
                                    break;
                                case '=':
                                    state = ParseState.End;
                                    break;
                            }
                            break;
                        case ':':
                        case '=':
                            // In the original implementation, the Regex would've backtracked if it encountered an
                            // unbalanced opening bracket followed by (not necessarily immediately) a delimiter.
                            // Simply verifying that the parentheses will eventually be closed should suffice to
                            // determine if the terminator needs to be consumed as part of the current constraint
                            // specification.
                            var indexOfClosingParantheses = text.IndexOf(')', currentIndex + 1);
                            if (indexOfClosingParantheses == -1)
                            {
                                if (currentChar == ':')
                                {
                                    state = ParseState.ParsingName;
                                }
                                else
                                {
                                    state = ParseState.End;
                                    currentIndex--;
                                }
                            }
                            else
                            {
                                currentIndex = indexOfClosingParantheses;
                            }

                            break;
                    }
                    break;
                case ParseState.ParsingName:
                    switch (currentChar)
                    {
                        case null:
                            state = ParseState.End;
                            break;
                        case ':':
                            break;
                        case '(':
                            state = ParseState.InsideParenthesis;
                            break;
                        case '=':
                            state = ParseState.End;
                            currentIndex--;
                            break;
                    }
                    break;
            }

            currentIndex++;

        } while (state != ParseState.End);
    }

    private static bool ParseLiteral(
        Context context,
        List<RoutePatternPart> parts,
        out DiagnosticDescriptor? descriptor,
        out object?[]? messageArgs)
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
                    descriptor = Descriptors.PatternHasMismatchedParameter;
                    messageArgs = null;
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
                    descriptor = Descriptors.PatternHasMismatchedParameter;
                    messageArgs = null;
                    return false;
                }

                if (context.Current == CloseBrace)
                {
                    // This is an 'escaped' brace in a literal, like "{{foo" - keep going.
                }
                else
                {
                    // This is an unbalanced close-brace, which is not allowed
                    descriptor = Descriptors.PatternHasMismatchedParameter;
                    messageArgs = null;
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
        if (IsValidLiteral(decoded, out descriptor, out messageArgs))
        {
            parts.Add(new RoutePatternPartLiteral { Content = decoded });
            return true;
        }
        else
        {
            return false;
        }
    }

    private static bool IsAllValid(
        List<RoutePatternPathSegment> segments,
        out DiagnosticDescriptor? descriptor,
        out object?[]? messageArgs)
    {
        // A catch-all parameter must be the last part of the last segment
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            for (var j = 0; j < segment.Parts.Count; j++)
            {
                var part = segment.Parts[j];
                if (part is RoutePatternPartParameter { IsCatchAll: true } && (i != segments.Count - 1 || j != segment.Parts.Count - 1))
                {
                    descriptor = Descriptors.PatternCatchAllMustBeLast;
                    messageArgs = null;
                    return false;
                }
            }
        }

        descriptor = null;
        messageArgs = null;
        return true;
    }

    private static bool IsSegmentValid(
        List<RoutePatternPart> parts,
        out DiagnosticDescriptor? descriptor,
        out object?[]? messageArgs)
    {
        // If a segment has multiple parts, then it can't contain a catch all.
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i] is RoutePatternPartParameter { IsCatchAll: true } && parts.Count > 1)
            {
                descriptor = Descriptors.PatternCannotHaveCatchAllInMultiSegment;
                messageArgs = null;
                return false;
            }
        }

        // if a segment has multiple parts, then only the last one parameter can be optional
        // if it is following a optional separator.
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i] is RoutePatternPartParameter parameter && parameter.IsOptional && parts.Count > 1)
            {
                if (i != parts.Count - 1)
                {
                    // This optional parameter is not the last one in the segment
                    // Example:
                    // An optional parameter must be at the end of the segment. In the segment '{RouteValue?})',
                    // optional parameter 'RouteValue' is followed by ')'

                    descriptor = Descriptors.PatternOptionalParameterHasToBeLast;
                    messageArgs = [
                        RoutePatternPathSegment.DebuggerToString(parts),
                        parameter.Name,
                        parts[i + 1].DebuggerToString()];
                    return false;
                }

                // This optional parameter is the last part in the segment
                var previousPart = parts[i - 1];

                if (previousPart.IsSeparator)
                {
                    continue;
                }

                if (previousPart is not RoutePatternPartLiteral literal || literal.Content != PeriodString)
                {
                    // The optional parameter is preceded by a literal other than period.
                    // Example of error message:
                    // "In the segment '{RouteValue}-{param?}', the optional parameter 'param' is preceded
                    // by an invalid segment '-'. Only a period (.) can precede an optional parameter.

                    descriptor = Descriptors.PatternOptionalParameterCanOnlyBePrecededByPeriod;
                    messageArgs = [
                        RoutePatternPathSegment.DebuggerToString(parts),
                        parameter.Name,
                        parts[i - 1].DebuggerToString()];
                    return false;
                }

                parts[i - 1] = new RoutePatternPartSeparator { Content = literal.Content };
            }
        }

        // A segment cannot contain two consecutive parameters
        var isLastSegmentParameter = false;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (part.IsParameter && isLastSegmentParameter)
            {
                descriptor = Descriptors.PatternCannotHaveConsecutiveParameters;
                messageArgs = null;
                return false;
            }

            isLastSegmentParameter = part.IsParameter;
        }

        descriptor = null;
        messageArgs = null;
        return true;
    }

    private static bool IsValidLiteral(
        string literal,
        out DiagnosticDescriptor? descriptor,
        out object?[]? messageArgs)
    {
        if (literal.Contains(QuestionMark))
        {
            descriptor = Descriptors.PatternHasInvalidLiteral;
            messageArgs = null;
            return false;
        }

        descriptor = null;
        messageArgs = null;
        return true;
    }

    private static bool TrimPrefix(string routePattern, [NotNullWhen(true)] out string? result)
    {
        if (routePattern.StartsWith("~/", StringComparison.Ordinal))
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

    private sealed class Context
    {
        private readonly string _template;
        private int _index;
        private int? _mark;

        public Context(string template)
        {
            _template = template;

            _index = -1;
        }

        public int Index => _index;
        public char Current => _index < _template.Length && _index >= 0 ? _template[_index] : (char)0;
        public HashSet<string> ParameterNames { get; } = new(StringComparer.OrdinalIgnoreCase);

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

    private enum ParseState
    {
        Start,
        ParsingName,
        InsideParenthesis,
        End
    }
}
