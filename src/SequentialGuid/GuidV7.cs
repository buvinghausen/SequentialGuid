using System.Security.Cryptography;

namespace SequentialGuid;

/// <summary>
/// Provides RFC 9562 UUID Version 7 generation using a Unix Epoch timestamp in milliseconds
/// to produce time-ordered, monotonically increasing <see cref="Guid"/> values.
/// </summary>
public static class GuidV7
{
	private static readonly
#if NET9_0_OR_GREATER
		Lock
#else
		object
#endif
		Sync = new();
	private static long s_lastMs = -1;
	private static int s_counter;

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

		// RFC 9562 §6.2 Method 1: rand_a carries a randomly-seeded 12-bit counter.
		// A new ms tick reseeds the counter; the same tick increments it.
		// Counter overflow advances the stored timestamp by 1 ms and resets to 0.
		long ms;
		int counter;
		lock (Sync)
		{
			if (unixMilliseconds > s_lastMs)
			{
				s_lastMs = unixMilliseconds;
				s_counter = SeedCounter();
			}
			else
			{
				if (++s_counter > 0xFFF)
				{
					s_lastMs++;
					s_counter = 0;
				}
			}
			ms = s_lastMs;
			counter = s_counter;
		}

		// Build 16 bytes in network (big-endian) byte order per RFC 9562 Section 5.7
		var bytes = new byte[16];

		// unix_ts_ms: 48-bit big-endian millisecond timestamp (octets 0-5)
		bytes[0] = (byte)(ms >> 40);
		bytes[1] = (byte)(ms >> 32);
		bytes[2] = (byte)(ms >> 24);
		bytes[3] = (byte)(ms >> 16);
		bytes[4] = (byte)(ms >> 8);
		bytes[5] = (byte)ms;

		// ver: bits 48-51, set to 0b0111 (7); rand_a (counter): bits 52-63 (octets 6-7)
		bytes[6] = (byte)(0x70 | (counter >> 8));
		bytes[7] = (byte)counter;

		// Fill octets 8-15 with random data for rand_b (62 bits)
#if NETFRAMEWORK || NETSTANDARD2_0
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(bytes, 8, 8);
#else
		RandomNumberGenerator.Fill(bytes.AsSpan(8));
#endif

		// var: bits 64-65, set to 0b10 in the high two bits of octet 8
		bytes[8] = (byte)(0x80 | (bytes[8] & 0x3F));

		// Swap from network byte order to .NET's mixed-endian Guid format
		return new(SwapByteOrder(bytes));
	}

	// Seeds the 12-bit counter for a new millisecond tick using the lower 11 bits
	// (MSB left as 0 per RFC 9562 §6.2 rollover-guard guidance), giving range [0, 0x7FF].
	private static int SeedCounter()
	{
#if NETFRAMEWORK || NETSTANDARD2_0
		using var rng = RandomNumberGenerator.Create();
		return rng.GetInt32(0x800);
#else
		return RandomNumberGenerator.GetInt32(0x800);
#endif
	}

	// Converts between .NET mixed-endian and RFC 9562 network (big-endian) byte order.
	// Reverses Data1 (4 bytes), Data2 (2 bytes), and Data3 (2 bytes); Data4 is unchanged.
	// This mapping is self-inverse: applying it twice returns the original bytes.
	private static byte[] SwapByteOrder(byte[] b) =>
		[b[3], b[2], b[1], b[0], b[5], b[4], b[7], b[6], b[8], b[9], b[10], b[11], b[12], b[13], b[14], b[15]];
}
