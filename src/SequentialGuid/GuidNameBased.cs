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
		using var sha = HashAlgorithm.Create(algorithmName.Name)!;
		var nsBytes = namespaceId.ToByteArray().SwapByteOrder();
		sha.TransformBlock(nsBytes, 0, 16, null, 0);
		sha.TransformFinalBlock(name, 0, name.Length);
		var digest = sha.Hash;
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
