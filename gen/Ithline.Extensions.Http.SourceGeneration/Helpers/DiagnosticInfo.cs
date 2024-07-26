using Microsoft.CodeAnalysis;

namespace Ithline.Extensions.Http.SourceGeneration;
// <summary>
/// Descriptor for diagnostic instances using structural equality comparison.
/// Provides a work-around for https://github.com/dotnet/roslyn/issues/68291.
/// </summary>
internal readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    public DiagnosticDescriptor Descriptor { get; private init; }
    public object?[] MessageArgs { get; private init; }
    public Location? Location { get; private init; }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, object?[]? messageArgs)
    {
        var trimmedLocation = location is null ? null : GetTrimmedLocation(location);

        return new DiagnosticInfo
        {
            Descriptor = descriptor,
            Location = trimmedLocation,
            MessageArgs = messageArgs ?? []
        };

        // Creates a copy of the Location instance that does not capture a reference to Compilation.
        static Location GetTrimmedLocation(Location location)
        {
            return Location.Create(location.SourceTree?.FilePath ?? "", location.SourceSpan, location.GetLineSpan().Span);
        }
    }

    public Diagnostic CreateDiagnostic()
    {
        return Diagnostic.Create(Descriptor, Location, MessageArgs);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is DiagnosticInfo info && this.Equals(info);
    }

    public readonly bool Equals(DiagnosticInfo other)
    {
        return Descriptor.Equals(other.Descriptor) &&
            MessageArgs.SequenceEqual(other.MessageArgs) &&
            Location == other.Location;
    }

    public override readonly int GetHashCode()
    {
        var hashCode = Descriptor.GetHashCode();
        foreach (var messageArg in MessageArgs)
        {
            hashCode = HashCode.Combine(hashCode, messageArg?.GetHashCode() ?? 0);
        }

        hashCode = HashCode.Combine(hashCode, Location?.GetHashCode() ?? 0);
        return hashCode;
    }
}
