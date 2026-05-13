#if NET6_0_OR_GREATER
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using SequentialGuid.Extensions;

namespace SequentialGuid;

static class GuidNameBased
{
	internal static class Namespaces
	{
		internal static readonly Guid Dns = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
		internal static readonly Guid Url = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
		internal static readonly Guid Oid = new("6ba7b812-9dad-11d1-80b4-00c04fd430c8");
		internal static readonly Guid X500 = new("6ba7b814-9dad-11d1-80b4-00c04fd430c8");
	}

#if NET6_0_OR_GREATER
	// Stack/pool buffer sized to hold the 16-byte namespace plus the UTF-8 encoded name.
	const int StackThreshold = 256;

	[SkipLocalsInit]
	[SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
		Justification = "RFC 9562 §A.4 mandates SHA-1 for UUIDv5 name-based identifiers; this is a specification requirement, not a security primitive.")]
	internal static Guid Create(Guid namespaceId, ReadOnlySpan<char> name, HashAlgorithmName algorithmName, byte version)
	{
		// Worst-case UTF-8 size for the name; actual encoded length is captured from GetBytes.
		var maxTotal = checked(16 + Encoding.UTF8.GetMaxByteCount(name.Length));

		Span<byte> stackBuf = stackalloc byte[StackThreshold];
		byte[]? rented = null;
		var buffer = maxTotal <= StackThreshold
			? stackBuf
			: (rented = ArrayPool<byte>.Shared.Rent(maxTotal)).AsSpan(0, maxTotal);
		try
		{
			WriteNamespace(namespaceId, buffer);
			var nameByteLen = Encoding.UTF8.GetBytes(name, buffer[16..]);
			return HashAndFinalize(buffer[..(16 + nameByteLen)], algorithmName, version);
		}
		finally
		{
			if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
		}
	}

	[SkipLocalsInit]
	[SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
		Justification = "RFC 9562 §A.4 mandates SHA-1 for UUIDv5 name-based identifiers; this is a specification requirement, not a security primitive.")]
	internal static Guid Create(Guid namespaceId, ReadOnlySpan<byte> name, HashAlgorithmName algorithmName, byte version)
	{
		var totalLen = checked(16 + name.Length);

		Span<byte> stackBuf = stackalloc byte[StackThreshold];
		byte[]? rented = null;
		var input = totalLen <= StackThreshold
			? stackBuf[..totalLen]
			: (rented = ArrayPool<byte>.Shared.Rent(totalLen)).AsSpan(0, totalLen);
		try
		{
			WriteNamespace(namespaceId, input);
			name.CopyTo(input[16..]);
			return HashAndFinalize(input, algorithmName, version);
		}
		finally
		{
			if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
		}
	}

	internal static Guid Create(Guid namespaceId, string name, HashAlgorithmName algorithmName, byte version) =>
		Create(namespaceId, name.AsSpan(), algorithmName, version);

	internal static Guid Create(Guid namespaceId, byte[] name, HashAlgorithmName algorithmName, byte version) =>
		Create(namespaceId, (ReadOnlySpan<byte>)name, algorithmName, version);

	static void WriteNamespace(Guid namespaceId, Span<byte> destination)
	{
#if NET8_0_OR_GREATER
		namespaceId.TryWriteBytes(destination[..16], bigEndian: true, out _);
#else
		// NET6/7: TryWriteBytes(span, bigEndian) doesn't exist; write native order then swap in-place
		namespaceId.TryWriteBytes(destination[..16]);
		destination[..16].SwapGuidBytesInPlace();
#endif
	}

	[SkipLocalsInit]
	[SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
		Justification = "RFC 9562 §A.4 mandates SHA-1 for UUIDv5 name-based identifiers; this is a specification requirement, not a security primitive.")]
	static Guid HashAndFinalize(ReadOnlySpan<byte> input, HashAlgorithmName algorithmName, byte version)
	{
		// SHA-256 is the widest digest we use (32 bytes); SHA-1 fills the first 20.
		Span<byte> digest = stackalloc byte[32];
		if (algorithmName == HashAlgorithmName.SHA1)
			SHA1.HashData(input, digest);
		else
			SHA256.HashData(input, digest);

		var head = digest[..16];
		head.SetRfc9562Version(version);
		head.SetRfc9562Variant();
		return new(head, bigEndian: true);
	}
#else
	internal static Guid Create(Guid namespaceId, string name, HashAlgorithmName algorithmName, byte version) =>
		Create(namespaceId, System.Text.Encoding.UTF8.GetBytes(name), algorithmName, version);

	[SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
		Justification = "RFC 9562 §A.4 mandates SHA-1 for UUIDv5 name-based identifiers; this is a specification requirement, not a security primitive.")]
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
		// netstandard2.0 — TryGetHashAndReset(Span<byte>, out int) is .NET 5+ / netstandard 2.1+
		using var hash = IncrementalHash.CreateHash(algorithmName);
		hash.AppendData(namespaceId.ToByteArray().SwapByteOrder());
		hash.AppendData(name);
		var digest = hash.GetHashAndReset();
		digest.SetRfc9562Version(version);
		digest.SetRfc9562Variant();
		return new(digest.SwapByteOrder());
#endif
	}
#endif
}
