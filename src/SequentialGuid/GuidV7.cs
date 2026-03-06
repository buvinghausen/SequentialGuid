using System.Security.Cryptography;

namespace SequentialGuid;

/// <summary>
/// Provides RFC 9562 UUID Version 7 generation using a Unix Epoch timestamp in milliseconds
/// to produce time-ordered, monotonically increasing <see cref="Guid"/> values.
/// </summary>
public static class GuidV7
{
	/// <summary>
	/// Creates a new UUID version 7 using the current UTC time.
	/// </summary>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/>.</returns>
	public static Guid NewGuid() =>
		NewGuid(DateTimeOffset.UtcNow);

	/// <summary>
	/// Creates a new UUID version 7 from a <see cref="DateTimeOffset"/> timestamp.
	/// </summary>
	/// <param name="timestamp">The timestamp whose millisecond-precision Unix Epoch value is embedded in the UUID.</param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/>.</returns>
	public static Guid NewGuid(DateTimeOffset timestamp) =>
		NewGuid(timestamp.ToUnixTimeMilliseconds());

	/// <summary>
	/// Creates a new UUID version 7 from a Unix Epoch millisecond timestamp.
	/// </summary>
	/// <param name="unixMilliseconds">
	/// The number of milliseconds since 1970-01-01T00:00:00Z. Must be non-negative and
	/// fit in 48 bits (maximum value 281474976710655, valid until the year 10889 AD).
	/// </param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="unixMilliseconds"/> is negative or exceeds the 48-bit maximum.
	/// </exception>
	public static Guid NewGuid(long unixMilliseconds)
	{
		if (unixMilliseconds < 0 || unixMilliseconds > 0x0000_FFFF_FFFF_FFFF)
			throw new ArgumentOutOfRangeException(nameof(unixMilliseconds),
				"Unix millisecond timestamp must be non-negative and fit within 48 bits.");

		// Build 16 bytes in network (big-endian) byte order per RFC 9562 Section 5.7
		var bytes = new byte[16];

		// unix_ts_ms: 48-bit big-endian millisecond timestamp (octets 0-5)
		bytes[0] = (byte)(unixMilliseconds >> 40);
		bytes[1] = (byte)(unixMilliseconds >> 32);
		bytes[2] = (byte)(unixMilliseconds >> 24);
		bytes[3] = (byte)(unixMilliseconds >> 16);
		bytes[4] = (byte)(unixMilliseconds >> 8);
		bytes[5] = (byte)unixMilliseconds;

		// Fill octets 6-15 with random data for rand_a (12 bits) and rand_b (62 bits)
#if NETFRAMEWORK || NETSTANDARD2_0
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(bytes, 6, 10);
#else
		RandomNumberGenerator.Fill(bytes.AsSpan(6));
#endif

		// ver: bits 48-51, set to 0b0111 (7) in the high nibble of octet 6
		bytes[6] = (byte)(0x70 | (bytes[6] & 0x0F));

		// var: bits 64-65, set to 0b10 in the high two bits of octet 8
		bytes[8] = (byte)(0x80 | (bytes[8] & 0x3F));

		// Swap from network byte order to .NET's mixed-endian Guid format
		return new(SwapByteOrder(bytes));
	}

	// Converts between .NET mixed-endian and RFC 9562 network (big-endian) byte order.
	// Reverses Data1 (4 bytes), Data2 (2 bytes), and Data3 (2 bytes); Data4 is unchanged.
	// This mapping is self-inverse: applying it twice returns the original bytes.
	private static byte[] SwapByteOrder(byte[] b) =>
		[b[3], b[2], b[1], b[0], b[5], b[4], b[7], b[6], b[8], b[9], b[10], b[11], b[12], b[13], b[14], b[15]];
}
