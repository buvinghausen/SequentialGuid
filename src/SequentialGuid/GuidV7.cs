using System.Security.Cryptography;
using SequentialGuid.Extensions;

namespace SequentialGuid;

/// <summary>
/// Provides RFC 9562 UUID Version 7 generation using a Unix Epoch timestamp in milliseconds
/// to produce time-ordered, monotonically increasing <see cref="Guid"/> values.
/// </summary>
/// <remarks>
/// Implements RFC 9562 Section 6.2 Method 1 (Fixed Bit-Length Dedicated Counter): a 26-bit
/// monotonic counter occupies the 12-bit <c>rand_a</c> field (upper 12 bits) and the first
/// 14 bits of <c>rand_b</c> after the variant (lower 14 bits), guaranteeing sort order within
/// the same millisecond. The counter is a process-global, ever-incrementing value advanced via
/// <see cref="Interlocked.Increment(ref int)"/> and seeded randomly at startup, mirroring the
/// approach used by <see cref="GuidV8Time"/>. This design is race-free: concurrent callers each
/// claim a unique, strictly increasing counter slot regardless of the timestamp they supply.
/// The counter wraps every 67,108,864 increments; callers generating more than 67,108,864 UUIDs
/// within the same millisecond may observe out-of-order values at the wrap boundary.
/// </remarks>
public static class GuidV7
{
	// Process-global monotonic counter for RFC 9562 §6.2 Method 1 — Fixed Bit-Length Dedicated
	// Counter. Advanced via Interlocked.Increment; upper 12 bits written to rand_a, lower 14 bits
	// to the first 14 bits of rand_b (after variant). Masked to 26 bits (0x3FFFFFF).
	private static int s_counter;

	static GuidV7()
	{
#if NET6_0_OR_GREATER
		s_counter = RandomNumberGenerator
#else
		using var rng = RandomNumberGenerator.Create();
		s_counter = rng
#endif
			.GetInt32(0x200); // seed in [0, 512) to leave ample headroom before the 26-bit wrap
	}

	/// <summary>
	/// Gets the current date and time in Coordinated Universal Time (UTC).
	/// </summary>
	/// <remarks>This property provides the current UTC date and time with millisecond precision. The value is based
	/// on the system clock and may be affected by system time changes.</remarks>
	public static DateTime Timestamp =>
		DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).UtcDateTime;

	/// <summary>
	/// Creates a new UUID version 7 using the current UTC time, with byte ordering
	/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	public static Guid NewSqlGuid() =>
		NewGuid().ToSqlGuid();

	/// <summary>
	/// Creates a new UUID version 7 from a <see cref="DateTimeOffset"/> timestamp, with byte ordering
	/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <param name="timestamp">The timestamp whose millisecond-precision Unix Epoch value is embedded in the UUID.</param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	public static Guid NewSqlGuid(DateTimeOffset timestamp) =>
		NewGuid(timestamp).ToSqlGuid();

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
		NewGuid(timestamp).ToSqlGuid();

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
		NewGuid(unixMilliseconds).ToSqlGuid();

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

		// RFC 9562 §6.2 Method 1: claim a unique slot in the monotonic counter.
		// Mirrors GuidV8Time.NewGuid: no timestamp-tracking state, no CAS loop.
		var counter = Interlocked.Increment(ref s_counter) & 0x3FFFFFF; // 26-bit counter (12 rand_a + 14 rand_b)

		// Build 16 bytes in network (big-endian) byte order per RFC 9562 Section 5.7
		var bytes = new byte[16];

		// Fill rand_b tail (octets 10-15) with random data; octets 8-9 hold the counter extension
#if NET6_0_OR_GREATER
		RandomNumberGenerator.Fill(bytes.AsSpan(10));
#else
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(bytes, 10, 6);
#endif
		// unix_ts_ms: 48-bit big-endian millisecond timestamp (octets 0-5)
		bytes[0] = (byte)(unixMilliseconds >> 40);
		bytes[1] = (byte)(unixMilliseconds >> 32);
		bytes[2] = (byte)(unixMilliseconds >> 24);
		bytes[3] = (byte)(unixMilliseconds >> 16);
		bytes[4] = (byte)(unixMilliseconds >> 8);
		bytes[5] = (byte)unixMilliseconds;

		// rand_a: upper 12 bits of 26-bit counter (octets 6-7)
		bytes[6] = (byte)(counter >> 22);          // counter bits 25-22 (lower nibble; version takes upper)
		bytes[7] = (byte)((counter >> 14) & 0xFF); // counter bits 21-14

		// rand_b extension: lower 14 bits of counter (octets 8-9; variant takes upper 2 bits of octet 8)
		bytes[8] = (byte)((counter >> 8) & 0x3F);  // counter bits 13-8
		bytes[9] = (byte)(counter & 0xFF);          // counter bits 7-0

		bytes.SetRfc9562Version(7);
		bytes.SetRfc9562Variant();

		// Swap from network byte order to .NET's mixed-endian Guid format
		return
#if NET6_0_OR_GREATER
			new(bytes, true);
#else
			new(bytes.SwapByteOrder());
#endif
	}
}
