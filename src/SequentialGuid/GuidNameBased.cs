using System.Security.Cryptography;

namespace SequentialGuid;

internal static class GuidNameBased
{
	internal static class Namespaces
	{
		internal static readonly Guid Dns = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
		internal static readonly Guid Url = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
		internal static readonly Guid Oid = new("6ba7b812-9dad-11d1-80b4-00c04fd430c8");
		internal static readonly Guid X500 = new("6ba7b814-9dad-11d1-80b4-00c04fd430c8");
	}

	internal static Guid Create(Guid namespaceId, byte[] name, HashAlgorithmName algorithmName, byte version)
	{
#if NETFRAMEWORK
		// Legacy .NET Framework does not support incremental hash so do the full-blown hash
		// and set the resulting value to digest to match so the downstream bit setting and return work
		using var hash = HashAlgorithm.Create(algorithmName.Name)!;
		hash.TransformBlock(namespaceId.ToByteArray().SwapByteOrder(), 0, 16, null, 0);
		hash.TransformFinalBlock(name, 0, name.Length);
		var digest = hash.Hash;
#else
		using var hash = IncrementalHash.CreateHash(algorithmName);
		hash.AppendData(namespaceId
#if NETSTANDARD
			.ToByteArray().SwapByteOrder()
#else
			.ToByteArray(true)
#endif
		);
		hash.AppendData(name);
		var digest = hash.GetHashAndReset();
#endif
		digest.SetRfc9562Version(version);
		digest.SetRfc9562Variant();
		return
#if NET6_0_OR_GREATER
			new(digest.AsSpan(0, 16), true);
#else
			new(digest.SwapByteOrder());
#endif
	}
}
