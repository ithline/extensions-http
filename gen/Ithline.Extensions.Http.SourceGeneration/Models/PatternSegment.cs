using System.Collections;

namespace Ithline.Extensions.Http.SourceGeneration.Models;

public sealed class PatternSegment : IReadOnlyList<IPatternSegmentPart>, IEquatable<PatternSegment>
{
    private readonly IPatternSegmentPart[] _parts;

    public PatternSegment(IEnumerable<IPatternSegmentPart> parts)
    {
        _parts = parts.ToArray();
    }

    public int Count => _parts.Length;
    public bool IsSimple => Count == 1;
    public IPatternSegmentPart this[int index] => _parts[index];

    public bool Equals(PatternSegment other)
    {
        if (other is null)
        {
            return false;
        }

        return MemoryExtensions.SequenceEqual(_parts.AsSpan(), other._parts);
    }

    public override bool Equals(object obj)
    {
        return obj is PatternSegment other && this.Equals(other);
    }

    public override int GetHashCode()
    {
        HashCode hashCode = default;

        foreach (var item in _parts)
        {
            hashCode.Add(item);
        }

        return hashCode.ToHashCode();
    }

    public override string ToString()
    {
        return ToString(_parts);
    }

    public static string ToString(IEnumerable<IPatternSegmentPart> parts)
    {
        return string.Join(string.Empty, parts);
    }

    public IEnumerator<IPatternSegmentPart> GetEnumerator()
    {
        foreach (var part in _parts)
        {
            yield return part;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }
}
