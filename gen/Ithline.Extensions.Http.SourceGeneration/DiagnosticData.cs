using Microsoft.CodeAnalysis;

namespace Ithline.Extensions.Http.SourceGeneration;

public sealed class DiagnosticData : IEquatable<DiagnosticData>
{
    private DiagnosticData(DiagnosticDescriptor descriptor, Location location, object? args = null)
    {
        Descriptor = descriptor;
        Location = location;
        Args = args;
    }

    public DiagnosticDescriptor Descriptor { get; }
    public Location Location { get; }
    public object? Args { get; }

    public static DiagnosticData Create(DiagnosticDescriptor descriptor, SyntaxNode syntaxNode, object? args = null)
    {
        return new DiagnosticData(descriptor, GetComparableLocation(syntaxNode), args);
    }

    public Diagnostic ToDiagnostic()
    {
        return Diagnostic.Create(Descriptor, Location, Args);
    }

    public override bool Equals(object? obj)
    {
        return obj is DiagnosticData info && this.Equals(info);
    }
    public bool Equals(DiagnosticData other)
    {
        return Descriptor.Equals(other.Descriptor)
            && Location == other.Location
            && Equals(Args, other.Args);
    }
    public override int GetHashCode()
    {
        return HashCode.Combine(Descriptor, Location, Args);
    }

    // Get a Location object that doesn't store a reference to the compilation.
    // That allows it to compare equally across compilations.
    private static Location GetComparableLocation(SyntaxNode syntax)
    {
        var location = syntax.GetLocation();
        return Location.Create(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, location.GetLineSpan().Span);
    }
}
