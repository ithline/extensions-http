namespace Ithline.Extensions.Http.SourceGeneration.Routes;

internal sealed record PatternLiteral : IPatternSegmentPart
{
    public PatternLiteral(string content)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>
    /// Gets the text content.
    /// </summary>
    public string Content { get; }

    public bool Equals(IPatternSegmentPart other)
    {
        return other is PatternLiteral literal && this.Equals(literal);
    }

    public override string ToString()
    {
        return Content;
    }
}
