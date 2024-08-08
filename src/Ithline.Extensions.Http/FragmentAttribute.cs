namespace Ithline.Extensions.Http;

/// <summary>
/// Instructs the Ithline.Extensions.Http source generator to use the parameter as fragment.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class FragmentAttribute : Attribute
{
}
