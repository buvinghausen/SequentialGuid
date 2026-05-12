using System.Security.Cryptography;
using SequentialGuid.Extensions;

namespace SequentialGuid;

/// <summary>
/// Provides RFC 9562 UUID Version 4 generation using a cryptographically strong
/// random number generator to produce fully random <see cref="Guid"/> values.
/// </summary>
/// <remarks>
/// Implements RFC 9562 Section 5.4: all 122 free bits are filled with random data;
/// the mandatory version nibble (<c>0x4</c>) and variant bits (<c>10xxxxxx</c>)
/// occupy their required positions at octet 6 and octet 8 respectively.
/// </remarks>
public static class GuidV4
{
	/// <summary>
	/// Creates a new UUID version 4 using a cryptographically strong random number generator.
	/// </summary>
	/// <returns>A new random version 4 <see cref="Guid"/>.</returns>
	public static Guid NewGuid()
	{
		// Build 16 bytes in network (big-endian) byte order per RFC 9562 Section 5.4
#if NET6_0_OR_GREATER
		Span<byte> bytes = stackalloc byte[16];
		RandomNumberGenerator.Fill(bytes);
		bytes.SetRfc9562Version(4);
		bytes.SetRfc9562Variant();
		return new(bytes, bigEndian: true);
#else
		var bytes = new byte[16];
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(bytes);
		bytes.SetRfc9562Version(4);
		bytes.SetRfc9562Variant();
		return new(bytes.SwapByteOrder());
#endif
	}
}
