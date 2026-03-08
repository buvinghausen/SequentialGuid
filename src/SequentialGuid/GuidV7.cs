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
/// <para>Monotonic state is maintained lock-free via a single <see cref="long"/> that packs the
/// 48-bit timestamp into bits 63–12 and the 12-bit counter into bits 11–0, allowing atomic
/// updates with <see cref="Interlocked.CompareExchange(ref long, long, long)"/>.</para>
/// </remarks>
public static class GuidV7
{
	// Packed monotonic state for RFC 9562 §6.2 Method 1 — Fixed Bit-Length Dedicated Counter.
	// bits 63-12: last unix-ms timestamp (arithmetic right-shift to extract)
	// bits 11-0:  12-bit counter
	// Initialized to long.MinValue so any valid (non-negative) timestamp reads as newer.
	private static long s_state = long.MinValue;

	/// <summary>
	/// Creates a new UUID version 7 using the current UTC time, with byte ordering
	/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	public static Guid NewSqlGuid() =>
		NewGuid().ToSqlGuid().Value;

	/// <summary>
	/// Creates a new UUID version 7 from a <see cref="DateTimeOffset"/> timestamp, with byte ordering
	/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <param name="timestamp">The timestamp whose millisecond-precision Unix Epoch value is embedded in the UUID.</param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	public static Guid NewSqlGuid(DateTimeOffset timestamp) =>
		NewGuid(timestamp).ToSqlGuid().Value;

	/// <summary>
	/// Creates a new UUID version 7 from a <see cref="DateTime"/> timestamp, with byte ordering
	/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <param name="timestamp">
	/// The timestamp whose millisecond-precision Unix Epoch value is embedded in the UUID.
	/// Must not have <see cref="DateTimeKind.Unspecified"/> kind.
	/// </param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind.
	/// </exception>
	public static Guid NewSqlGuid(DateTime timestamp) =>
		NewGuid(timestamp).ToSqlGuid().Value;

	/// <summary>
	/// Creates a new UUID version 7 from a Unix Epoch millisecond timestamp, with byte ordering
	/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <param name="unixMilliseconds">
	/// The number of milliseconds since 1970-01-01T00:00:00Z. Must be non-negative and
	/// fit in 48 bits (maximum value 281474976710655, valid until the year 10889 AD).
	/// </param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="unixMilliseconds"/> is negative or exceeds the 48-bit maximum.
	/// </exception>
	public static Guid NewSqlGuid(long unixMilliseconds) =>
		NewGuid(unixMilliseconds).ToSqlGuid().Value;

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
	/// Creates a new UUID version 7 from a <see cref="DateTime"/> timestamp.
	/// </summary>
	/// <param name="timestamp">
	/// The timestamp whose millisecond-precision Unix Epoch value is embedded in the UUID.
	/// Must not have <see cref="DateTimeKind.Unspecified"/> kind.
	/// </param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind.
	/// </exception>
	public static Guid NewGuid(DateTime timestamp) =>
		timestamp.Kind == DateTimeKind.Unspecified
			? throw new ArgumentException("DateTimeKind.Unspecified not supported", nameof(timestamp))
			: NewGuid(new DateTimeOffset(timestamp));

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
		// s_state packs both pieces of monotonic state into a single long for lock-free CAS:
		//   bits 63-12  unix-ms timestamp (recover with arithmetic right-shift by 12)
		//   bits 11-0   12-bit counter
		//
		// Three cases:
		//   new timestamp  > last → new tick: seed counter from random bytes, use provided timestamp
		//   new timestamp == last → same tick: increment counter (advance timestamp on overflow)
		//   new timestamp  < last → clock moved backward / historical value: bypass counter,
		//                           embed provided timestamp as-is with pure random rand_a
		long effectiveTimestamp;
		int counter;
		bool useCounter;

		var current = Volatile.Read(ref s_state);
		while (true)
		{
			var lastTimestamp = current >> 12;
			long nextState;

			if (unixMilliseconds > lastTimestamp)
			{
				// New tick: seed counter; MSB (bit 11) = 0 as rollover guard; bits 10-0 from random bytes.
				// bytes[6] bits 2-0 → counter bits 10-8; bytes[7] → counter bits 7-0.
				var seed = ((bytes[6] & 0x07) << 8) | bytes[7];
				nextState = (unixMilliseconds << 12) | (long)seed;
				effectiveTimestamp = unixMilliseconds;
				counter = seed;
				useCounter = true;
			}
			else if (unixMilliseconds == lastTimestamp)
			{
				var nextCounter = (int)(current & 0xFFF) + 1;
				if (nextCounter > 0xFFF)
				{
					// Counter exhausted: borrow 1 ms from the future and reset.
					effectiveTimestamp = lastTimestamp + 1;
					nextState = effectiveTimestamp << 12;
					counter = 0;
				}
				else
				{
					effectiveTimestamp = lastTimestamp;
					nextState = (lastTimestamp << 12) | (long)nextCounter;
					counter = nextCounter;
				}
				useCounter = true;
			}
			else
			{
				// Provided timestamp is behind the internal clock (clock rollback or
				// deliberate historical value). Embed it as given with pure random rand_a
				// so that callers receive exactly the timestamp they requested.
				effectiveTimestamp = unixMilliseconds;
				counter = 0;
				useCounter = false;
				break;
			}

			var observed = Interlocked.CompareExchange(ref s_state, nextState, current);
			if (observed == current)
				break; // CAS succeeded
			current = observed; // lost the race; retry with latest state
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
			// rand_a bits 11-8 = counter bits 11-8
			bytes[6] = (byte)(counter >> 8);
			// rand_a bits 7-0 = counter bits 7-0
			bytes[7] = (byte)(counter & 0xFF);
		}

		bytes.SetRfc9562Version(7);
		bytes.SetRfc9562Variant();

		// Swap from network byte order to .NET's mixed-endian Guid format
		return
#if NETFRAMEWORK || NETSTANDARD
			new(bytes.SwapByteOrder());
#else
			new(bytes, true);
#endif
	}
}
