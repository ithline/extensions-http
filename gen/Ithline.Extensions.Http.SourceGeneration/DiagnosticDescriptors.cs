using Microsoft.CodeAnalysis;

namespace Ithline.Extensions.Http.SourceGeneration;

internal static class DiagnosticDescriptors
{
    private const string Category = "Performance";

    public static DiagnosticDescriptor LimitedSourceGeneration { get; } = new DiagnosticDescriptor(
        id: "ITH0001",
        title: "Source generator limitation reached.",
        messageFormat: "Source generator limitation reached.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ClassMustBePartial { get; } = new DiagnosticDescriptor(
        id: "ITH0100",
        title: "Class must be partial.",
        messageFormat: "Class must be partial.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ClassCannotBeNested { get; } = new DiagnosticDescriptor(
        id: "ITH0101",
        title: "Class cannot be nested.",
        messageFormat: "Class cannot be nested.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ClassCannotBeAbstract { get; } = new DiagnosticDescriptor(
        id: "ITH0102",
        title: "Class cannot be abstract.",
        messageFormat: "Class cannot be abstract.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidSignature { get; } = new DiagnosticDescriptor(
        id: "ITH0300",
        title: "Member or property has invalid signature.",
        messageFormat: "Member or property has invalid signature.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidRoutePattern { get; } = new DiagnosticDescriptor(
        id: "ITH0301",
        title: "Route pattern is not valid.",
        messageFormat: "Route pattern is not valid.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor Dummy { get; } = new DiagnosticDescriptor(
        id: "ITH9999",
        title: "Dummy.",
        messageFormat: "Dummy.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
