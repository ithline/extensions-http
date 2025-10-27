using Ithline.Extensions.Http;

namespace SrcGenTest;

internal static partial class AppRoutes
{
    [GeneratedRoute("/products/{id}.{ext}")]
    public static partial string RouteOnlyRequired(int id, string ext);

    [GeneratedRoute("/{**catchAll}")]
    public static partial string CatchAllWithQueries(string? catchAll);

    [GeneratedRoute("/")]
    public static partial string DynamicQuery([Query(Name = "dasdga")] KeyValuePair<string, string>[] a);
}
