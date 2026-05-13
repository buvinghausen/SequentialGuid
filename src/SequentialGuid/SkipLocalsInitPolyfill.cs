#if !NET6_0_OR_GREATER
// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Runtime.CompilerServices;
#pragma warning restore IDE0130 // Namespace does not match folder structure

// SkipLocalsInitAttribute is available from .NET 5+ / .NET Standard 2.1+.
// Provide a no-op shim so the attribute can be applied unconditionally.
[AttributeUsage(
	AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct |
	AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property |
	AttributeTargets.Event | AttributeTargets.Interface,
	Inherited = false)]
sealed class SkipLocalsInitAttribute : Attribute;
#endif
