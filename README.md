# Ithline.Extensions.Http
Simple package to generate route builders using C# source generators.

The generator parses provided route pattern and generates method implementation that
- interpolates method parameters inside the template,
- serializes extra parameters as query arguments if provided (must be `null` if optional).

Each method can be configured to lowercase the route path and/or query strings, and append trailing slash to the path.

Generator handles parameter encoding, `IEnumerable` parameters and optional route and query parameters.

## Installation

Add a [NuGet package](https://www.nuget.org/packages/Ithline.Extensions.Http) reference to the project:<br>
`<PackageReference Include="Ithline.Extensions.Http" Version="<version>" />`

## Usage example
```cs
using Ithline.Extensions.Http;

partial class AppRoutes
{
    [GeneratedRoute("/my-basic/{route:guid}")]
    public static partial string MyBasicRoute(Guid route, [QueryName("p")] int? page = null);
}
```
