using System.Collections;

namespace Ithline.Extensions.Http.SourceGeneration.Routes;

internal sealed class PatternSegment : IReadOnlyList<IPatternSegmentPart>, IEquatable<PatternSegment>
{
    private readonly IPatternSegmentPart[] _parts;

    public PatternSegment(IEnumerable<IPatternSegmentPart> parts)
    {
        _parts = parts.ToArray();
    }

    public int Count => _parts.Length;
    public bool IsSimple => Count == 1;
    public IPatternSegmentPart this[int index] => _parts[index];

    public bool Equals(PatternSegment other) => other is not null && _parts.AsSpan().SequenceEqual(other._parts);
    public override bool Equals(object obj) => obj is PatternSegment other && this.Equals(other);
    public override int GetHashCode()
    {
        HashCode hashCode = default;

        foreach (var item in _parts)
        {
            hashCode.Add(item);
        }

        return hashCode.ToHashCode();
    }

    public override string ToString() => ToString(_parts);
    public static string ToString(IEnumerable<IPatternSegmentPart> parts) => string.Join(string.Empty, parts);

    public IEnumerator<IPatternSegmentPart> GetEnumerator() => _parts.OfType<IPatternSegmentPart>().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
