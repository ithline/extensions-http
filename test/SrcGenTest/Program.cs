using SrcGenTest;

Console.WriteLine(AppRoutes.CatchAllWithQueries("//abc/def//"));
Console.WriteLine(AppRoutes.DynamicQuery([
    KeyValuePair.Create("1", "x"),
    KeyValuePair.Create("2", "y")
    ]));
