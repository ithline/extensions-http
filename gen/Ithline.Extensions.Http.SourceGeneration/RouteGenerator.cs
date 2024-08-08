using System.Text;
using Ithline.Extensions.Http.SourceGeneration.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Ithline.Extensions.Http.SourceGeneration;

[Generator]
public sealed partial class RouteGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if LAUNCH_DEBUGGER
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Launch();
            }
#endif

        var knownTypes = context.CompilationProvider.Select((compilation, _) => KnownTypeSymbols.Create(compilation));
        var supportedLanguage = context.CompilationProvider.Select((compilation, _) => compilation is CSharpCompilation { LanguageVersion: > LanguageVersion.CSharp11 });

        var routeAttributes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: "Ithline.Extensions.Http.GeneratedRouteAttribute",
                predicate: (node, _) => GeneratedRouteAttribute.IsCandidateSyntaxNode(node),
                transform: (ctx, _) => GeneratedRouteAttribute.Create(ctx)!)
            .Where(attribute => attribute is not null)
            .Collect();

        var generatorSpec = routeAttributes
            .Combine(knownTypes)
            .Combine(supportedLanguage)
            .Select((values, ct) =>
            {
                var ((attributes, knownSymbols), supportedLanguage) = values;
                if (knownSymbols is null || !supportedLanguage)
                {
                    return null;
                }

                var methods = new List<PatternMethod>();
                var diagnosticCollector = new DiagnosticCollector();
                var parser = new RouteGeneratorParser(knownSymbols, diagnosticCollector);
                foreach (var attribute in attributes)
                {
                    if (parser.TryParseMethod(attribute, ct, out var method))
                    {
                        methods.Add(method);
                    }
                }
                return new RouteGeneratorSpec
                {
                    Methods = [.. methods],
                    Diagnostics = [.. diagnosticCollector],
                };
            });

        context.RegisterSourceOutput(generatorSpec, this.ReportDiagnosticsAndEmitSource);
    }

    /// <summary>
    /// Instrumentation helper for unit tests.
    /// </summary>
    public Action<RouteGeneratorSpec>? OnSourceEmitting { get; init; }

    private void ReportDiagnosticsAndEmitSource(SourceProductionContext ctx, RouteGeneratorSpec? spec)
    {
        if (spec is null)
        {
            return;
        }

        foreach (var di in spec.Diagnostics)
        {
            ctx.ReportDiagnostic(di.CreateDiagnostic());
        }

        OnSourceEmitting?.Invoke(spec);

        using var sw = new StringWriter();
        using var writer = new CodeWriter(sw, 0);

        writer.EmitFileHeader();
        writer.EmitMethods(spec.Methods);
        writer.EmitHelper();

        ctx.AddSource("GeneratedRoutes.g.cs", SourceText.From(sw.ToString(), Encoding.UTF8));
    }
}
