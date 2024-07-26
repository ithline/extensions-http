using System.Collections;
using Ithline.Extensions.Http;

namespace SrcGenTest;

internal static partial class AppRoutes
{
    [GeneratedRoute("/product/")]
    public static partial string Static();

    [GeneratedRoute("/product/{slug}", AppendTrailingSlash = true)]
    public static partial string EscapeRequired(string slug);

    [GeneratedRoute("/PRODUCT/{slug}", LowercaseUrls = true)]
    public static partial string EscapeRequiredQuery(string slug, [QueryName("p")] int? page = null);

    [GeneratedRoute("/product/{slug?}")]
    public static partial string EscapeOptional(string? slug);

    [GeneratedRoute("/product/{slug?}")]
    public static partial string EscapeOptionalQuery(string? slug, int? page = null);

    [GeneratedRoute("/product/{productId:int}")]
    public static partial string InterpolateRequired(int productId);

    [GeneratedRoute("/product/{productId:int}", LowercaseQueryStrings = true)]
    public static partial string InterpolateRequiredQuery(int productId, [QueryName("P")] int? page = null);

    [GeneratedRoute("/product/{productId:int}.{format}")]
    public static partial string InterpolatedRequiredFormatRequired(int productId, int format);

    [GeneratedRoute("/product/{productId:int}.{format?}")]
    public static partial string InterpolatedRequiredFormatOptional(int productId, string? format);

    [GeneratedRoute("/product/{productId:int?}")]
    public static partial string InterpolateOptional(int? productId);

    [GeneratedRoute("/product/{productId:int?}")]
    public static partial string InterpolateOptionalQuery(int? productId, int? page);

    [GeneratedRoute("/product/{**catchAll}")]
    public static partial string CatchAll(string? catchAll = null);

    [GeneratedRoute("/{**catchAll}")]
    public static partial string CatchAllQuery(string? catchAll = null, int? page = null, [QueryName("minp")] decimal? minPrice = null, [QueryName("max p")] decimal? maxPrice = null);

    [GeneratedRoute("/product")]
    public static partial string Enumerable(IEnumerable filter);

    [GeneratedRoute("/product")]
    public static partial string EnumerableT(IEnumerable<string> filter);

    [GeneratedRoute("/product")]
    public static partial string Params([QueryName("f")] params string[] filter);
}
