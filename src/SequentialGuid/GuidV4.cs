using System.Security.Cryptography;

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
		var bytes = new byte[16];
#if NETFRAMEWORK || NETSTANDARD2_0
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(bytes);
#else
		RandomNumberGenerator.Fill(bytes);
#endif
		// ver: bits 48-51 = 0b0100 (4)
		bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
		// var: bits 64-65 = 0b10
		bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

		// Swap from network byte order to .NET's mixed-endian Guid format
		return
#if NETFRAMEWORK || NETSTANDARD
			new(bytes.SwapByteOrder());
#else
			new(bytes, true);
#endif
	}
}
