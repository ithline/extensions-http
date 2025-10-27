using System.Diagnostics;
using Ithline.Extensions.Http.SourceGeneration.Routes;

namespace Ithline.Extensions.Http.SourceGeneration.Parsing;
internal static class PatternParser
{
    private static readonly char[] _invalidParameterNameChars = [
        Constants.Separator,
        Constants.OpenBrace,
        Constants.CloseBrace,
        Constants.QuestionMark,
        '*'];

    public static IReadOnlyList<PatternSegment>? Parse(PatternParsingContext context)
    {
        var segments = new List<PatternSegment>();
        while (context.MoveNext())
        {
            var i = context.Index;

            if (context.Current == Constants.Separator)
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
            Debug.Assert(context.AtEnd() || context.Current == Constants.Separator);

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
                if (part is not RouteParameter { IsCatchAll: true })
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
                if (context.Current == Constants.OpenBrace)
                {
                    // This is a dangling open-brace, which is not allowed
                    if (!context.MoveNext())
                    {
                        return null;
                    }

                    // This is an 'escaped' brace in a literal, like "{{foo"
                    if (context.Current == Constants.OpenBrace)
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
                        if (ParseParameter() is not RouteParameter parameter)
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
                if (context.Current == Constants.Separator || context.AtEnd())
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
                if (parts[i] is RouteParameter { IsCatchAll: true })
                {
                    return null;
                }

                // if current part is not parameter, we good
                if (parts[i] is not RouteParameter parameter)
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
                if (previousPart is PatternLiteral literal && literal.Content == Constants.PeriodString)
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
                if (context.Current == Constants.Separator)
                {
                    // End of the segment
                    break;
                }
                else if (context.Current == Constants.OpenBrace)
                {
                    if (!context.MoveNext())
                    {
                        // This is a dangling open-brace, which is not allowed
                        return null;
                    }

                    if (context.Current == Constants.OpenBrace)
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
                else if (context.Current == Constants.CloseBrace)
                {
                    if (!context.MoveNext())
                    {
                        // This is a dangling close-brace, which is not allowed
                        return null;
                    }

                    if (context.Current == Constants.CloseBrace)
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

            if (decoded.Contains(Constants.QuestionMark))
            {
                return null;
            }

            return new PatternLiteral(decoded);
        }

        RouteParameter? ParseParameter()
        {
            Debug.Assert(context.Current == Constants.OpenBrace);
            context.Mark();

            context.MoveNext();

            while (true)
            {
                if (context.Current == Constants.OpenBrace)
                {
                    // This is a dangling open-brace, which is not allowed
                    // Example: "{p1:regex(^\d{"
                    if (!context.MoveNext())
                    {
                        return null;
                    }

                    // This is an open brace inside of a parameter, it has to be escaped
                    // If we see something like "{p1:regex(^\d{3", we will come here.
                    if (context.Current != Constants.OpenBrace)
                    {
                        return null;
                    }
                }
                else if (context.Current == Constants.CloseBrace)
                {
                    // When we encounter Closed brace here, it either means end of the parameter or it is a closed
                    // brace in the parameter, in that case it needs to be escaped.
                    // Example: {p1:regex(([}}])\w+}. First pair is escaped one and last marks end of the parameter
                    if (!context.MoveNext())
                    {
                        // This is the end of the string -and we have a valid parameter
                        break;
                    }

                    if (context.Current == Constants.CloseBrace)
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

}
