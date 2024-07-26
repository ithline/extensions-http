using SrcGenTest;

var builder = WebApplication.CreateSlimBuilder();

var app = builder.Build();

app.MapGet("/{slug:int?}", (int? slug) => TypedResults.Ok(slug));

string[] filter = ["a", "b", "c"];
string[] routes = [
    AppRoutes.Static(),
    AppRoutes.EscapeRequired("abc"),
    AppRoutes.EscapeRequiredQuery("abc"),
    AppRoutes.EscapeRequiredQuery("abc", 10),
    AppRoutes.InterpolateRequired(1),
    AppRoutes.InterpolateRequiredQuery(1, null),
    AppRoutes.InterpolateRequiredQuery(1, 10),
    AppRoutes.InterpolatedRequiredFormatRequired(1, 10),
    AppRoutes.InterpolatedRequiredFormatOptional(1, null),
    AppRoutes.InterpolatedRequiredFormatOptional(1, "txt"),
    AppRoutes.InterpolateOptional(1),
    AppRoutes.InterpolateOptionalQuery(1, null),
    AppRoutes.InterpolateOptionalQuery(1, 10),
    AppRoutes.CatchAll(null),
    AppRoutes.CatchAll("abc"),
    AppRoutes.CatchAll("abc/def"),
    AppRoutes.CatchAll("abc/def/ghi/"),
    AppRoutes.CatchAllQuery(null, minPrice: 10.5m, maxPrice: 20),
    AppRoutes.Enumerable(filter),
    AppRoutes.EnumerableT(filter),
    AppRoutes.Params(filter),
];

foreach (var s in routes)
{
    Console.WriteLine(s);
}
