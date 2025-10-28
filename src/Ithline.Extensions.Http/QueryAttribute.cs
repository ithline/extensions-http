namespace Ithline.Extensions.Http;

/// <summary>
/// Instructs the Ithline.Extensions.Http source generator to use the specified name for the query parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class QueryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAttribute"/> with the specified name.
    /// </summary>
    public QueryAttribute(string? name = null)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the name of the query key.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the value should be lowercase.
    /// </summary>
    public bool LowercaseValue { get; set; }
}
