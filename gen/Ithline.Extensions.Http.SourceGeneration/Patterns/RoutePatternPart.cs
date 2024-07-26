namespace Ithline.Extensions.Http.SourceGeneration.Patterns;

public abstract record RoutePatternPart
{
    private protected RoutePatternPart()
    {
    }

    public bool IsLiteral => this is RoutePatternPartLiteral;
    public bool IsSeparator => this is RoutePatternPartSeparator;
    public bool IsParameter => this is RoutePatternPartParameter;

    internal abstract string DebuggerToString();
}
