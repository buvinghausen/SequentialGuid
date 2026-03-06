using System.Security.Cryptography;

namespace SequentialGuid;

/// <summary>
/// Provides RFC 9562 UUID Version 7 generation using a Unix Epoch timestamp in milliseconds
/// to produce time-ordered, monotonically increasing <see cref="Guid"/> values.
/// </summary>
/// <remarks>
/// Implements RFC 9562 Section 6.2 Method 1 (Fixed Bit-Length Dedicated Counter): the 12-bit
/// <c>rand_a</c> field immediately following the timestamp is used as a monotonic counter that
/// is randomly seeded at each new millisecond tick and incremented for every subsequent UUID
/// generated within the same millisecond, guaranteeing sort order across high-frequency batches.
/// When the provided timestamp is older than the last seen timestamp (e.g. historical test
/// vectors or a clock rollback) the counter is bypassed and the field is filled with random data.
/// </remarks>
public static class GuidV7
{
	// RFC 9562 §6.2 Method 1 — Fixed Bit-Length Dedicated Counter state
	private static long s_lastTimestamp = long.MinValue;
	private static int s_counter;
	private static readonly
#if NET9_0_OR_GREATER
		Lock
#else
		object
#endif
		SyncRoot = new();

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
		if (unixMilliseconds is < 0 or > 0x0000_FFFF_FFFF_FFFF)
			throw new ArgumentOutOfRangeException(nameof(unixMilliseconds),
				"Unix millisecond timestamp must be non-negative and fit within 48 bits.");

		// Build 16 bytes in network (big-endian) byte order per RFC 9562 Section 5.7
		var bytes = new byte[16];

		// Fill octets 6-15 with random data up front; bytes[6-7] also seed the counter
		// when a new timestamp tick begins (see counter logic below).
#if NETFRAMEWORK || NETSTANDARD2_0
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(bytes, 6, 10);
#else
		RandomNumberGenerator.Fill(bytes.AsSpan(6));
#endif

		// RFC 9562 §6.2 Method 1: determine counter value and effective timestamp.
		//
		// Three cases:
		//   new timestamp  > last → new tick: seed counter from random bytes, use provided timestamp
		//   new timestamp == last → same tick: increment counter (advance timestamp on overflow)
		//   new timestamp  < last → clock moved backward / historical value: bypass counter,
		//                           embed provided timestamp as-is with pure random rand_a
		long effectiveTimestamp;
		int counter;
		bool useCounter;

		lock (SyncRoot)
		{
			if (unixMilliseconds > s_lastTimestamp)
			{
				s_lastTimestamp = unixMilliseconds;
				// Seed: MSB (bit 11) = 0 as rollover guard; bits 10-0 from random bytes.
				// bytes[6] bits 2-0 → counter bits 10-8; bytes[7] → counter bits 7-0.
				s_counter = ((bytes[6] & 0x07) << 8) | bytes[7];
				useCounter = true;
				counter = s_counter;
				effectiveTimestamp = unixMilliseconds;
			}
			else if (unixMilliseconds == s_lastTimestamp)
			{
				if (++s_counter > 0xFFF)
				{
					// Counter exhausted: borrow 1 ms from the future and reset.
					s_lastTimestamp++;
					s_counter = 0;
				}
				useCounter = true;
				counter = s_counter;
				effectiveTimestamp = s_lastTimestamp;
			}
			else
			{
				// Provided timestamp is behind the internal clock (clock rollback or
				// deliberate historical value).  Embed it as given with pure random rand_a
				// so that callers receive exactly the timestamp they requested.
				useCounter = false;
				counter = 0;
				effectiveTimestamp = unixMilliseconds;
			}
		}

		// unix_ts_ms: 48-bit big-endian millisecond timestamp (octets 0-5)
		bytes[0] = (byte)(effectiveTimestamp >> 40);
		bytes[1] = (byte)(effectiveTimestamp >> 32);
		bytes[2] = (byte)(effectiveTimestamp >> 24);
		bytes[3] = (byte)(effectiveTimestamp >> 16);
		bytes[4] = (byte)(effectiveTimestamp >> 8);
		bytes[5] = (byte)effectiveTimestamp;

		if (useCounter)
		{
			// ver: bits 48-51 = 0b0111 (7); rand_a bits 11-8 = counter bits 11-8
			bytes[6] = (byte)(0x70 | ((counter >> 8) & 0x0F));
			// rand_a bits 7-0 = counter bits 7-0
			bytes[7] = (byte)(counter & 0xFF);
		}
		else
		{
			// ver: bits 48-51 = 0b0111 (7); rand_a = pure random (no counter)
			bytes[6] = (byte)(0x70 | (bytes[6] & 0x0F));
		}

		// var: bits 64-65, set to 0b10 in the high two bits of octet 8
		bytes[8] = (byte)(0x80 | (bytes[8] & 0x3F));

		// Swap from network byte order to .NET's mixed-endian Guid format
		return
#if NETFRAMEWORK || NETSTANDARD
			new(bytes.SwapByteOrder());
#else
			new(bytes, true);
#endif
	}
}
