using Ithline.Extensions.Http;

namespace SrcGenTest;

internal static partial class AppRoutes
{
    [GeneratedRoute("/products/{id}.{ext}")]
    public static partial string RouteOnlyRequired(int id, string ext);

    [GeneratedRoute("/segment1/{r1}/{r2}.{r3}/{**catchAll}")]
    public static partial string CatchAllWithQueries(
        string r1,
        string r2,
        int r3,
        string? catchAll,
        int? q1,
        decimal? q2,
        [Fragment] decimal? q3,
        string[]? q4);
}
