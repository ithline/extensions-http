using Ithline.Extensions.Http.SourceGeneration.Specs;

namespace Ithline.Extensions.Http.SourceGeneration;

public sealed partial class RouteGenerator
{
    private sealed class TypeSpecBuilder
    {
        private List<MethodSpec>? _methods;

        public TypeSpecBuilder()
        {
        }

        public required TypeRef TypeRef { get; init; }
        public required TypeRef? ParentRef { get; init; }
        public required string TypeName { get; init; }
        public required string Keyword { get; init; }
        public required string? Namespace { get; init; }

        public void AddMethod(MethodSpec methodSpec)
        {
            _methods ??= [];
            _methods.Add(methodSpec);
        }

        public EquatableArray<MethodSpec>? GetMethods()
        {
            return _methods is null ? null : [.. _methods];
        }
    }
}
