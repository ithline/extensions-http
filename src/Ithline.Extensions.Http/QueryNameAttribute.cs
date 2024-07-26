namespace Ithline.Extensions.Http;

/// <summary>
/// Instructs the Ithline.Extensions.Http source generator to use the specified name for the query parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class QueryNameAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryNameAttribute"/> with the specified name.
    /// </summary>
    /// <param name="name">Name of the query key.</param>
    public QueryNameAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the name of the query key.
    /// </summary>
    public string Name { get; }
}
