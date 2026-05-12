using System.Security.Cryptography;
using SequentialGuid.Extensions;

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
		// Legacy .NET Framework — full-blown hash then read .Hash
		using var hash = HashAlgorithm.Create(algorithmName.Name)!;
		hash.TransformBlock(namespaceId.ToByteArray().SwapByteOrder(), 0, 16, null, 0);
		hash.TransformFinalBlock(name, 0, name.Length);
		var digest = hash.Hash;
		digest.SetRfc9562Version(version);
		digest.SetRfc9562Variant();
		return new(digest.SwapByteOrder());
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
#if NET6_0_OR_GREATER
		// SHA-256 is the widest digest we use (32 bytes); SHA-1 fills the first 20.
		Span<byte> digest = stackalloc byte[32];
		hash.TryGetHashAndReset(digest, out _);
		var head = digest[..16];
		head.SetRfc9562Version(version);
		head.SetRfc9562Variant();
		return new(head, bigEndian: true);
#else
		var digest = hash.GetHashAndReset();
		digest.SetRfc9562Version(version);
		digest.SetRfc9562Variant();
		return new(digest.SwapByteOrder());
#endif
#endif
	}
}
