using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using SequentialGuid.Extensions;
#if NET6_0_OR_GREATER
using System.Buffers;
#endif

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
	static int _counter;

	static GuidV7()
	{
#if NET6_0_OR_GREATER
		_counter = RandomNumberGenerator
#else
		using var rng = RandomNumberGenerator.Create();
		_counter = rng
#endif
			.GetInt32(0x200); // seed in [0, 512) to leave ample headroom before the 26-bit wrap
	}

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
		NewGuid(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

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
	[SkipLocalsInit]
	public static Guid NewGuid(long unixMilliseconds)
	{
		if (unixMilliseconds is < 0 or > 0x0000_FFFF_FFFF_FFFF)
			throw new ArgumentOutOfRangeException(nameof(unixMilliseconds),
				"Unix millisecond timestamp must be non-negative and fit within 48 bits.");

		// RFC 9562 §6.2 Method 1: claim a unique slot in the monotonic counter.
		var counter = Interlocked.Increment(ref _counter) & 0x3FFFFFF;

#if NET6_0_OR_GREATER
		Span<byte> bytes = stackalloc byte[16];
		RandomNumberGenerator.Fill(bytes[10..]);
#else
		var bytes = new byte[16];
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
		bytes[6] = (byte)(counter >> 22);
		bytes[7] = (byte)((counter >> 14) & 0xFF);

		// rand_b extension: lower 14 bits of counter (octets 8-9)
		bytes[8] = (byte)((counter >> 8) & 0x3F);
		bytes[9] = (byte)(counter & 0xFF);

		bytes.SetRfc9562Version(7);
		bytes.SetRfc9562Variant();

#if NET6_0_OR_GREATER
		return new(bytes, bigEndian: true);
#else
		return new(bytes.SwapByteOrder());
#endif
	}

#if NET6_0_OR_GREATER
	// Scratch buffer threshold for the batch random region (6 bytes per item),
	// mirroring the GuidNameBased stackalloc/ArrayPool pattern.
	const int StackThreshold = 256;

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values sharing a single
	/// current-UTC-time capture, ordered by a contiguous block of monotonic counter slots.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	public static void Fill(Span<Guid> destination) =>
		Fill(destination, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values that all embed
	/// <paramref name="unixMilliseconds"/>, ordered by a contiguous block of monotonic counter slots.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <param name="unixMilliseconds">
	/// The number of milliseconds since 1970-01-01T00:00:00Z. Must be non-negative and
	/// fit in 48 bits (maximum value 281474976710655, valid until the year 10889 AD).
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="unixMilliseconds"/> is negative or exceeds the 48-bit maximum,
	/// or when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	[SkipLocalsInit]
	public static void Fill(Span<Guid> destination, long unixMilliseconds)
	{
		if (unixMilliseconds is < 0 or > 0x0000_FFFF_FFFF_FFFF)
			throw new ArgumentOutOfRangeException(nameof(unixMilliseconds),
				"Unix millisecond timestamp must be non-negative and fit within 48 bits.");
		if (destination.Length > 0x400_0000)
			throw new ArgumentOutOfRangeException(nameof(destination),
				"Batch size must not exceed the 26-bit counter space (67,108,864).");
		if (destination.IsEmpty)
			return;

		var count = destination.Length;
		// RFC 9562 §6.2 Method 1: reserve a contiguous block of counter slots so the
		// whole batch is ordered and concurrent callers can never collide. The +1 mirrors
		// Interlocked.Increment semantics on the single-call path, which consumes the
		// post-increment value — the block is (old, old + count], never reusing old.
		var start = Interlocked.Add(ref _counter, count) - count + 1;

		// One RNG call covers every item's 6-byte random tail.
		var randLen = count * 6;
		Span<byte> stackBuf = stackalloc byte[StackThreshold];
		byte[]? rented = null;
		var rand = randLen <= StackThreshold
			? stackBuf[..randLen]
			: (rented = ArrayPool<byte>.Shared.Rent(randLen)).AsSpan(0, randLen);
		try
		{
			RandomNumberGenerator.Fill(rand);

			Span<byte> bytes = stackalloc byte[16];
			// unix_ts_ms: 48-bit big-endian millisecond timestamp (octets 0-5),
			// identical for every item — written once.
			bytes[0] = (byte)(unixMilliseconds >> 40);
			bytes[1] = (byte)(unixMilliseconds >> 32);
			bytes[2] = (byte)(unixMilliseconds >> 24);
			bytes[3] = (byte)(unixMilliseconds >> 16);
			bytes[4] = (byte)(unixMilliseconds >> 8);
			bytes[5] = (byte)unixMilliseconds;

			for (var i = 0; i < count; i++)
			{
				// start may be negative when _counter wraps near Int32.MaxValue; the mask
				// discards bits 26-31, which is correct regardless of sign.
				var counter = (start + i) & 0x3FFFFFF;

				// rand_a: upper 12 bits of 26-bit counter (octets 6-7)
				bytes[6] = (byte)(counter >> 22);
				bytes[7] = (byte)((counter >> 14) & 0xFF);

				// rand_b extension: lower 14 bits of counter (octets 8-9)
				bytes[8] = (byte)((counter >> 8) & 0x3F);
				bytes[9] = (byte)(counter & 0xFF);

				rand.Slice(i * 6, 6).CopyTo(bytes[10..]);

				// Must run after the counter writes: the counter stores raw bits in the
				// version nibble of octet 6 and the variant bits of octet 8; these OR the
				// RFC 9562 overlays back on top each iteration.
				bytes.SetRfc9562Version(7);
				bytes.SetRfc9562Variant();

				destination[i] = new(bytes, bigEndian: true);
			}
		}
		finally
		{
			if (rented is not null)
				ArrayPool<byte>.Shared.Return(rented);
		}
	}

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values in SQL Server byte
	/// order, sharing a single current-UTC-time capture.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination) =>
		FillSql(destination, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values in SQL Server byte
	/// order that all embed <paramref name="unixMilliseconds"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <param name="unixMilliseconds">
	/// The number of milliseconds since 1970-01-01T00:00:00Z. Must be non-negative and
	/// fit in 48 bits (maximum value 281474976710655, valid until the year 10889 AD).
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="unixMilliseconds"/> is negative or exceeds the 48-bit maximum,
	/// or when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination, long unixMilliseconds)
	{
		Fill(destination, unixMilliseconds);
		for (var i = 0; i < destination.Length; i++)
			destination[i] = destination[i].ToSqlGuid();
	}

	/// <summary>
	/// Creates an array of new UUID version 7 values sharing a single current-UTC-time capture.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 7 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space.
	/// </exception>
	public static Guid[] NewGuids(int count) =>
		NewGuids(count, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

	/// <summary>
	/// Creates an array of new UUID version 7 values that all embed <paramref name="unixMilliseconds"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <param name="unixMilliseconds">
	/// The number of milliseconds since 1970-01-01T00:00:00Z. Must be non-negative and
	/// fit in 48 bits (maximum value 281474976710655, valid until the year 10889 AD).
	/// </param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 7 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space,
	/// or when <paramref name="unixMilliseconds"/> is out of range.
	/// </exception>
	public static Guid[] NewGuids(int count, long unixMilliseconds)
	{
		if (count is < 0 or > 0x400_0000)
			throw new ArgumentOutOfRangeException(nameof(count),
				"Count must be between 0 and the 26-bit counter space (67,108,864).");
		if (count == 0)
			return [];
		var result = new Guid[count];
		Fill(result, unixMilliseconds);
		return result;
	}

	/// <summary>
	/// Creates an array of new UUID version 7 values in SQL Server byte order, sharing a
	/// single current-UTC-time capture.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <returns>An array of <paramref name="count"/> version 7 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space.
	/// </exception>
	public static Guid[] NewSqlGuids(int count) =>
		NewSqlGuids(count, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

	/// <summary>
	/// Creates an array of new UUID version 7 values in SQL Server byte order that all embed
	/// <paramref name="unixMilliseconds"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <param name="unixMilliseconds">
	/// The number of milliseconds since 1970-01-01T00:00:00Z. Must be non-negative and
	/// fit in 48 bits (maximum value 281474976710655, valid until the year 10889 AD).
	/// </param>
	/// <returns>An array of <paramref name="count"/> version 7 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space,
	/// or when <paramref name="unixMilliseconds"/> is out of range.
	/// </exception>
	public static Guid[] NewSqlGuids(int count, long unixMilliseconds)
	{
		var result = NewGuids(count, unixMilliseconds);
		for (var i = 0; i < result.Length; i++)
			result[i] = result[i].ToSqlGuid();
		return result;
	}

#if NET8_0_OR_GREATER
	/// <summary>
	/// Creates a new UUID version 7 using the current time of the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="provider">The clock supplying the timestamp.</param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	public static Guid NewGuid(TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewGuid(provider.GetUtcNow().ToUnixTimeMilliseconds());
	}

	/// <summary>
	/// Creates a new UUID version 7 using the current time of the supplied <see cref="TimeProvider"/>,
	/// with byte ordering suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <param name="provider">The clock supplying the timestamp.</param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	public static Guid NewSqlGuid(TimeProvider provider) =>
		NewGuid(provider).ToSqlGuid();

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values using a single
	/// timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	public static void Fill(Span<Guid> destination, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		Fill(destination, provider.GetUtcNow().ToUnixTimeMilliseconds());
	}

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values in SQL Server byte
	/// order using a single timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		FillSql(destination, provider.GetUtcNow().ToUnixTimeMilliseconds());
	}

	/// <summary>
	/// Creates an array of new UUID version 7 values using a single timestamp capture from the
	/// supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 7 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space.
	/// </exception>
	public static Guid[] NewGuids(int count, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewGuids(count, provider.GetUtcNow().ToUnixTimeMilliseconds());
	}

	/// <summary>
	/// Creates an array of new UUID version 7 values in SQL Server byte order using a single
	/// timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <returns>An array of <paramref name="count"/> version 7 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space.
	/// </exception>
	public static Guid[] NewSqlGuids(int count, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewSqlGuids(count, provider.GetUtcNow().ToUnixTimeMilliseconds());
	}
#endif
#endif
}
