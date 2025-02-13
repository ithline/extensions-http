using System.Diagnostics.CodeAnalysis;

namespace Ithline.Extensions.Http;

/// <summary>
/// Instructs the Ithline.Extensions.Http source generator to generate an implementation of the specified route building method.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class GeneratedRouteAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedRouteAttribute"/> with the specified route pattern.
    /// </summary>
    /// <param name="pattern"></param>
    public GeneratedRouteAttribute([StringSyntax("Route")] string pattern)
    {
        Pattern = pattern;
    }

    /// <summary>
    /// Gets the route pattern.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to generate lower-case paths.
    /// </summary>
    public bool LowercaseUrls { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to generate lower-case query strings.
    /// </summary>
    public bool LowercaseQueryStrings { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to append a trailing '/' at the end of the path.
    /// </summary>
    public bool AppendTrailingSlash { get; set; }
}
