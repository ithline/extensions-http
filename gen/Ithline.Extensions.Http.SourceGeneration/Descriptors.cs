using Microsoft.CodeAnalysis;

namespace Ithline.Extensions.Http.SourceGeneration;

internal static class Descriptors
{
    public static DiagnosticDescriptor LanguageVersionIsNotSupported { get; } = Create(
        id: 1000,
        title: "Language version is required to be at least C# 8",
        message: "The project's language version has to be at least 'C# 8'.");

    public static DiagnosticDescriptor MethodNameCannotStartWithUnderscore { get; } = Create(
        id: 1001,
        title: "Method names cannot start with _",
        message: "Method names cannot start with '_' character.");

    public static DiagnosticDescriptor MethodMustBeStaticPartial { get; } = Create(
        id: 1002,
        title: "Method must be static and partial",
        message: "Method must be static and partial.");

    public static DiagnosticDescriptor MethodCannotHaveBody { get; } = Create(
        id: 1003,
        title: "Method cannot have body",
        message: "Method cannot have body declared.");

    public static DiagnosticDescriptor MethodCannotBeGeneric { get; } = Create(
        id: 1004,
        title: "Method cannot be generic",
        message: "Method cannot be generic.");

    public static DiagnosticDescriptor MethodMustReturnString { get; } = Create(
        id: 1005,
        title: "Method must return System.String",
        message: "Method must return System.String.");

    public static DiagnosticDescriptor ParameterCannotHaveRefModifier { get; } = Create(
        id: 1006,
        title: "Argument is using unsupported parameter modifier",
        message: "Argument '{0}' is using an unsupported parameter modifier.");

    public static DiagnosticDescriptor ParameterNameCannotStartWithUnderscore { get; } = Create(
        id: 1007,
        title: "Argument names cannot start with _",
        message: "Argument names cannot start with '_' character.");

    public static DiagnosticDescriptor ParameterMustBeNullableIfOptionalOrQuery { get; } = Create(
        id: 1008,
        title: "Optional or query argument must be nullable",
        message: "Optional or query argument '{0}' must be either reference type or System.Nullable<T>.");

    public static DiagnosticDescriptor PatternIsNotValid { get; } = Create(
        id: 1010,
        title: "Route pattern is not valid",
        message: "Route pattern is not valid.");

    public static DiagnosticDescriptor PatternParameterMissingFromMethodArguments { get; } = Create(
        id: 1011,
        title: "Route pattern parameter has no corresponding method argument",
        message: "Route pattern paremeter '{0}' is not provided as argument to the method.");

    public static DiagnosticDescriptor PatternHasInvalidParameterName { get; } = Create(
        id: 1012,
        title: "Route pattern parameter name is invalid",
        message: "Route parameter name is invalid. Route parameter names must be non-empty and cannot contain these characters: '{{', '}}', '/'. The '?' character marks a parameter as optional, and can occur only at the end of the parameter. The '*' character marks a parameter as catch-all, and can occur only at the start of the parameter.");

    public static DiagnosticDescriptor PatternHasRepeatedParameter { get; } = Create(
        id: 1013,
        title: "Route parameter name appears more than one time in the route template",
        message: "Route parameter '{0}' appears more than one time in the route template.");

    public static DiagnosticDescriptor PatternHasInvalidLiteral { get; } = Create(
        id: 1014,
        title: "Route pattern literal section is invalid",
        message: "Route pattern literal section is invalid. Literal sections cannot contain the '?' character..");

    public static DiagnosticDescriptor PatternOptionalParameterCanOnlyBePrecededByPeriod { get; } = Create(
        id: 1015,
        title: "Only a period (.) can precede an optional parameter",
        message: "In the segment '{0}', the optional parameter '{1}' is preceded by an invalid segment '{2}'. Only a period (.) can precede an optional parameter.");

    public static DiagnosticDescriptor PatternOptionalParameterHasToBeLast { get; } = Create(
        id: 1016,
        title: "An optional parameter must be at the end of the segment",
        message: "An optional parameter must be at the end of the segment. In the segment '{0}', optional parameter '{1}' is followed by '{2}'.");

    public static DiagnosticDescriptor PatternCannotHaveConsecutiveParameters { get; } = Create(
        id: 1017,
        title: "A path segment cannot contain two consecutive parameters",
        message: "A path segment cannot contain two consecutive parameters. They must be separated by a '/' or by a literal string.");

    public static DiagnosticDescriptor PatternCatchAllMustBeLast { get; } = Create(
        id: 1018,
        title: "A catch-all parameter can only appear as the last segment of the route template",
        message: "A catch-all parameter can only appear as the last segment of the route template.");

    public static DiagnosticDescriptor PatternCatchAllCannotBeOptional { get; } = Create(
        id: 1019,
        title: "A catch-all parameter cannot be marked optional",
        message: "A catch-all parameter cannot be marked optional.");

    public static DiagnosticDescriptor PatternCannotHaveCatchAllInMultiSegment { get; } = Create(
        id: 1020,
        title: "A catch-all parameter must be the only value inside a path segment",
        message: "A path segment that contains more than one section, such as a literal section or a parameter, cannot contain a catch-all parameter.");

    public static DiagnosticDescriptor PatternHasMismatchedParameter { get; } = Create(
        id: 1021,
        title: "There is an incomplete parameter in the route template",
        message: "There is an incomplete parameter in the route template. Check that each '{' character has a matching '}' character.");

    public static DiagnosticDescriptor PatternUnescapedBrace { get; } = Create(
        id: 1022,
        title: "In a route parameter, '{' and '}' must be escaped with '{{' and '}}'",
        message: "In a route parameter, '{' and '}' must be escaped with '{{' and '}}'.");

    public static DiagnosticDescriptor PatternCannotHaveConsecutiveSeparators { get; } = Create(
        id: 1023,
        title: "The route template separator character '/' cannot appear consecutively",
        message: "The route template separator character '/' cannot appear consecutively. It must be separated by either a parameter or a literal value.");

    private static DiagnosticDescriptor Create(int id, string title, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
    {
        return new DiagnosticDescriptor(
            id: $"ITHL{id:D4}",
            title: title,
            messageFormat: message,
            category: nameof(RouteGenerator),
            defaultSeverity: severity,
            isEnabledByDefault: true);
    }
}
