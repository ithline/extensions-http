using Microsoft.CodeAnalysis;

namespace Ithline.Extensions.Http.SourceGeneration;

// <summary>
/// Descriptor for diagnostic instances using structural equality comparison.
/// Provides a work-around for https://github.com/dotnet/roslyn/issues/68291.
/// </summary>
public readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    public DiagnosticDescriptor Descriptor { get; private init; }
    public Location? Location { get; private init; }
    public object?[] MessageArgs { get; private init; }

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
        return Descriptor.Equals(other.Descriptor)
            && Location == other.Location
            && MessageArgs.SequenceEqual(other.MessageArgs);
    }

    public override readonly int GetHashCode()
    {
        HashCode hashCode = default;
        hashCode.Add(Descriptor);
        hashCode.Add(Location);

        foreach (var messageArg in MessageArgs)
        {
            hashCode.Add(messageArg);
        }

        return hashCode.ToHashCode();
    }
}
