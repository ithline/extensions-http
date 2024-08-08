using System.Collections;
using Microsoft.CodeAnalysis;

namespace Ithline.Extensions.Http.SourceGeneration;

internal sealed class DiagnosticCollector : IReadOnlyList<DiagnosticInfo>
{
    private readonly List<DiagnosticInfo> _list;

    public DiagnosticCollector()
    {
        _list = [];
    }

    public int Count => _list.Count;
    public DiagnosticInfo this[int index] => _list[index];

    public void Add(DiagnosticDescriptor descriptor, Location location, params object?[]? messageArgs)
    {
        _list.Add(DiagnosticInfo.Create(descriptor, location, messageArgs));
    }

    public IEnumerator<DiagnosticInfo> GetEnumerator()
    {
        return _list.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }
}
