#if NETFRAMEWORK || NETSTANDARD
// SkipLocalsInitAttribute is available from .NET 5+ / .NET Standard 2.1+.
// Provide a no-op shim so the attribute can be applied unconditionally.
namespace System.Runtime.CompilerServices
{
	[AttributeUsage(
		AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct |
		AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property |
		AttributeTargets.Event | AttributeTargets.Interface,
		Inherited = false)]
	internal sealed class SkipLocalsInitAttribute : Attribute { }
}
#endif
