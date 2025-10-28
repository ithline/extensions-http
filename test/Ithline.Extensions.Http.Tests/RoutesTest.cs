using Xunit;

namespace Ithline.Extensions.Http;

public sealed class RoutesTest
{
    [Fact]
    public void Constant_ReturnsItself()
    {
        Assert.Equal("/", AppRoutes.Root());
        Assert.Equal("/abc", AppRoutes.Constant());
    }

    [Theory]
    [InlineData("/abc", null)]
    [InlineData("/abc?a=0", 0)]
    [InlineData("/abc?a=10", 10)]
    public void ConstantQuery_ReturnsAppendedQueryArg(string expected, int? a)
    {
        Assert.Equal(expected, AppRoutes.ConstantQuery(a));
    }

    [Theory]
    [InlineData("/abc/xx/def", "xx")]
    [InlineData("/abc/cra/def", "cra")]
    public void Route_ReturnsGeneratedWithInterpolatedRouteArgs(string expected, string a)
    {
        Assert.Equal(expected, AppRoutes.Route(a));
    }

    [Theory]
    [InlineData("/abc/def", null)]
    [InlineData("/abc/def/xx", "xx")]
    public void RouteOptional_ReturnsGeneratedWithInterpolatedRouteArgs(string expected, string? a)
    {
        Assert.Equal(expected, AppRoutes.RouteOptional(a));
    }

    [Theory]
    [InlineData("/abc", null)]
    [InlineData("/abc", "/")]
    [InlineData("/abc/def", "def")]
    [InlineData("/abc/def/ghi", "/def/ghi")]
    public void CatchAll_ReturnsUnescapedArgsAppendedToThePath(string expected, string? catchAll)
    {
        Assert.Equal(expected, AppRoutes.CatchAll(catchAll));
    }

    [Theory]
    [InlineData("/abc/a", "a", null, null, null)]
    [InlineData("/abc/a?q1=b", "a", null, "b", null)]
    [InlineData("/abc/a?q1=b&q2=2", "a", null, "b", 2)]
    [InlineData("/abc/a?q2=2", "a", null, null, 2)]
    [InlineData("/abc/a/1", "a", 1, null, null)]
    [InlineData("/abc/a/1?q2=3", "a", 1, null, 3)]
    [InlineData("/abc/a/1?q1=b&q2=2", "a", 1, "b", 2)]
    public void PathWithQuery_ReturnsCombinedPathAndQuery(string expected, string p1, int? p2, string? q1, int? q2)
    {
        Assert.Equal(expected, AppRoutes.PathWithQuery(p1, p2, q1, q2));
    }

    [Theory]
    [InlineData("/abc")]
    [InlineData("/abc?array=1", 1)]
    [InlineData("/abc?array=1&array=2&array=3", 1, 2, 3)]
    public void Array_ReturnsListOfValuesAppended(string expected, params int[] array)
    {
        Assert.Equal(expected, AppRoutes.Array(array));
    }
}

internal static partial class AppRoutes
{
    [GeneratedRoute("/")]
    public static partial string Root();

    [GeneratedRoute("/abc")]
    public static partial string Constant();

    [GeneratedRoute("/abc")]
    public static partial string ConstantQuery(int? a);

    [GeneratedRoute("/abc/{a}/def")]
    public static partial string Route(string a);

    [GeneratedRoute("/abc/def/{a?}")]
    public static partial string RouteOptional(string? a);

    [GeneratedRoute("/abc/{**catchAll}")]
    public static partial string CatchAll(string? catchAll);

    [GeneratedRoute("/abc/{p1}/{p2?}")]
    public static partial string PathWithQuery(string p1, int? p2 = null, string? q1 = null, int? q2 = null);

    [GeneratedRoute("/abc")]
    public static partial string Array(int[]? array);
}
