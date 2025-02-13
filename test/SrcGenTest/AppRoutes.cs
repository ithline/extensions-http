using Ithline.Extensions.Http;
using Microsoft.Extensions.Primitives;

namespace SrcGenTest;

internal static partial class AppRoutes
{
    [GeneratedRoute("/products/{id}.{ext}")]
    public static partial string RouteOnlyRequired(int id, string ext);

    [GeneratedRoute("/segment1/{r1}/{r2}.{r3:int}/{**catchAll}")]
    public static partial string CatchAllWithQueries(
        string r1,
        string r2,
        int r3,
        string? catchAll,
        int? q1,
        decimal? q2,
        decimal q3,
         StringValues? q4);

    [GeneratedRoute("/abc")]
    public static partial string Abc { get; }
}
