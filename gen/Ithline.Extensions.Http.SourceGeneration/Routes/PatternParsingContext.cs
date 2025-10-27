using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Ithline.Extensions.Http.SourceGeneration.Routes;

namespace Ithline.Extensions.Http.SourceGeneration.Parsing;

internal sealed class PatternParsingContext
{
    private readonly HashSet<string> _parametersBound = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RouteParameter> _parameters;

    private readonly string _template;
    [SuppressMessage("Style", "IDE0032:Use auto property")]
    private int _index;
    private int? _mark;

    public PatternParsingContext(string pattern, IEnumerable<ParameterBase> parameters)
    {
        _template = pattern;
        _parameters = parameters
            .OfType<RouteParameter>()
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        _index = -1;
    }

    public int Index => _index;
    public char Current => _index < _template.Length && _index >= 0 ? _template[_index] : (char)0;

    public bool IsParameterBound(ParameterBase parameter)
    {
        return _parametersBound.Contains(parameter.Name);
    }

    public bool TryBindParameter(string parameterName, [NotNullWhen(true)] out RouteParameter? parameter)
    {
        if (_parameters.Remove(parameterName, out parameter) && parameter is not null)
        {
            _parametersBound.Add(parameter.Name);
            return true;
        }

        parameter = null;
        return false;
    }

    public bool Back()
    {
        return --_index >= 0;
    }
    public bool AtEnd()
    {
        return _index >= _template.Length;
    }
    public bool MoveNext()
    {
        return ++_index < _template.Length;
    }
    public void Mark()
    {
        Debug.Assert(_index >= 0);

        // Index is always the index of the character *past* Current - we want to 'mark' Current.
        _mark = _index;
    }
    public string? Capture()
    {
        if (_mark is int mark)
        {
            var value = _template.Substring(mark, _index - mark);
            _mark = null;
            return value;
        }
        else
        {
            return null;
        }
    }
}
