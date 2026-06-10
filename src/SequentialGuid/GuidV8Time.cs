#if !NET6_0_OR_GREATER
using System.Diagnostics;
#endif
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using SequentialGuid.Extensions;

namespace SequentialGuid;

/// <summary>
/// Provides RFC 9562 UUID Version 8 (time-based) generation that embeds a 60-bit timestamp
/// and a machine/process identifier, ensuring monotonically increasing ordering.
/// </summary>
/// <remarks>
/// Implements the time-based UUIDv8 layout described in RFC 9562 Appendix B.1.
/// The 60 least-significant bits of the .NET <see cref="DateTime.Ticks"/> timestamp are distributed
/// across <c>custom_a</c> (48 bits) and <c>custom_b</c> (12 bits), with the mandatory version
/// nibble (<c>0x8</c>) and variant bits (<c>10xxxxxx</c>) occupying their required positions.
/// The remaining 62 bits of <c>custom_c</c> hold a machine/process identifier and a monotonic counter.
/// For all .NET <see cref="DateTime"/> values through approximately the year 3662, the top 4 bits of
/// <see cref="DateTime.Ticks"/> are zero, so the 60-bit truncation is lossless.
/// </remarks>
public static class GuidV8Time
{
	static readonly byte[] _machinePid;
	static int _increment;

	static GuidV8Time()
	{
#if NET6_0_OR_GREATER
		// Use the RandomNumberGenerator static function where available
		_increment = RandomNumberGenerator
#else
		// Fall back to the old Random create function
		using var rng = RandomNumberGenerator.Create();
		_increment = rng
#endif
			.GetInt32(500000);
		_machinePid = new byte[5];
#if NET6_0_OR_GREATER
		// For newer frameworks use the preferred static function
		var hash = SHA256.HashData
#else
		// For older frameworks use the old algorithm create function
		using var algorithm = SHA256.Create();
		var hash = algorithm.ComputeHash
#endif
			(Encoding.UTF8.GetBytes(Environment.MachineName));
		for (var i = 0; i < 3; i++)
			_machinePid[i] = hash[i];
		try
		{
			var pid =
#if NET6_0_OR_GREATER
					// For newer frameworks prefer to use the static property on the Environment
					Environment.ProcessId
#else
					// For older frameworks get the process id the old school way
					Process.GetCurrentProcess().Id
#endif
				;
			// use low order two bytes only
			_machinePid[3] = (byte)(pid >> 8);
			_machinePid[4] = (byte)pid;
		}
		catch (SecurityException)
		{
		}
	}

	/// <summary>
	/// Creates a new UUID version 8 using the current UTC time, with byte ordering
	/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <returns>A new time-ordered version 8 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	public static Guid NewSqlGuid() =>
		NewGuid().ToSqlGuid();

	/// <summary>
	/// Creates a new UUID version 8 from a <see cref="DateTime"/> timestamp, with byte ordering
	/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <param name="timestamp">
	/// The timestamp to embed in the UUID. Must have <see cref="DateTimeKind.Utc"/> or
	/// <see cref="DateTimeKind.Local"/> kind, with a value between January 1st, 1970 UTC and now.
	/// </param>
	/// <returns>A new time-ordered version 8 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	public static Guid NewSqlGuid(DateTime timestamp) =>
		NewGuid(timestamp).ToSqlGuid();

	/// <summary>
	/// Creates a new UUID version 8 from a <see cref="DateTimeOffset"/> timestamp, with byte ordering
	/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <param name="timestamp">
	/// The timestamp to embed in the UUID. Its UTC equivalent must be between January 1st, 1970 UTC and now.
	/// </param>
	/// <returns>A new time-ordered version 8 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when the UTC equivalent of <paramref name="timestamp"/> is outside the valid range.
	/// </exception>
	public static Guid NewSqlGuid(DateTimeOffset timestamp) =>
		NewGuid(timestamp).ToSqlGuid();

	/// <summary>
	/// Creates a new UUID version 8 using the current UTC time.
	/// </summary>
	/// <returns>A new time-ordered version 8 <see cref="Guid"/>.</returns>
	public static Guid NewGuid() =>
		NewGuid(DateTime.UtcNow.Ticks);

	/// <summary>
	/// Creates a new UUID version 8 from a <see cref="DateTime"/> timestamp.
	/// </summary>
	/// <param name="timestamp">
	/// The timestamp to embed in the UUID. Must have <see cref="DateTimeKind.Utc"/> or
	/// <see cref="DateTimeKind.Local"/> kind, with a value between January 1st, 1970 UTC and now.
	/// </param>
	/// <returns>A new time-ordered version 8 <see cref="Guid"/>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	public static Guid NewGuid(DateTime timestamp)
	{
		var ticks = timestamp.Kind switch
		{
			DateTimeKind.Utc => timestamp.Ticks, // use ticks as is
			DateTimeKind.Local => timestamp.ToUniversalTime().Ticks, // convert to UTC
			_ => throw new ArgumentException("DateTimeKind.Unspecified not supported", nameof(timestamp))
		};

		// run validation after tick conversion
		return !ticks.IsDateTime
			? throw new ArgumentException("Timestamp must be between January 1st, 1970 UTC and now",
				nameof(timestamp))
			: NewGuid(ticks);
	}

	/// <summary>
	/// Creates a new UUID version 8 from a <see cref="DateTimeOffset"/> timestamp.
	/// </summary>
	/// <param name="timestamp">
	/// The timestamp to embed in the UUID. Its UTC equivalent must be between January 1st, 1970 UTC and now.
	/// </param>
	/// <returns>A new time-ordered version 8 <see cref="Guid"/>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when the UTC equivalent of <paramref name="timestamp"/> is outside the valid range.
	/// </exception>
	public static Guid NewGuid(DateTimeOffset timestamp) =>
		NewGuid(timestamp.UtcDateTime);

	[SkipLocalsInit]
	internal static Guid NewGuid(long timestamp)
	{
		// only use low order 22 bits
		var increment = Interlocked.Increment(ref _increment) & 0x003fffff;

#if NET6_0_OR_GREATER
		Span<byte>
#else
		byte[]
#endif
		bytes =
		[
			// custom_a: timestamp bits [59:12] → octets 0-5
			(byte)(timestamp >> 52),
			(byte)(timestamp >> 44),
			(byte)(timestamp >> 36),
			(byte)(timestamp >> 28),
			(byte)(timestamp >> 20),
			(byte)(timestamp >> 12),
			// custom_b: timestamp bits [11:0] → octets 6-7 (version takes upper nibble of octet 6)
			(byte)((timestamp >> 8) & 0x0F),
			(byte)timestamp,
			// custom_c: increment[21:0] + _machinePid → octets 8-15 (variant takes upper 2 bits of octet 8)
			(byte)((increment >> 16) & 0x3F),
			(byte)(increment >> 8),
			(byte)increment,
			_machinePid[0],
			_machinePid[1],
			_machinePid[2],
			_machinePid[3],
			_machinePid[4],
		];

		bytes.SetRfc9562Version(8);
		bytes.SetRfc9562Variant();

#if NET6_0_OR_GREATER
		return new(bytes, bigEndian: true);
#else
		return new(bytes.SwapByteOrder());
#endif
	}

#if NET6_0_OR_GREATER
	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values sharing a single
	/// current-UTC-time capture, ordered by a contiguous block of monotonic counter slots.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void Fill(Span<Guid> destination) =>
		FillCore(destination, DateTime.UtcNow.Ticks);

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values that all embed
	/// <paramref name="timestamp"/>, ordered by a contiguous block of monotonic counter slots.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <param name="timestamp">
	/// The timestamp to embed in every UUID. Must have <see cref="DateTimeKind.Utc"/> or
	/// <see cref="DateTimeKind.Local"/> kind, with a value between January 1st, 1970 UTC and now.
	/// </param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void Fill(Span<Guid> destination, DateTime timestamp) =>
		FillCore(destination, ToValidatedTicks(timestamp));

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values in SQL Server byte
	/// order, sharing a single current-UTC-time capture.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination) =>
		FillSqlCore(destination, DateTime.UtcNow.Ticks);

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values in SQL Server byte
	/// order that all embed <paramref name="timestamp"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <param name="timestamp">
	/// The timestamp to embed in every UUID. Must have <see cref="DateTimeKind.Utc"/> or
	/// <see cref="DateTimeKind.Local"/> kind, with a value between January 1st, 1970 UTC and now.
	/// </param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination, DateTime timestamp) =>
		FillSqlCore(destination, ToValidatedTicks(timestamp));

	/// <summary>
	/// Creates an array of new UUID version 8 values sharing a single current-UTC-time capture.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 8 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewGuids(int count)
	{
		ValidateCount(count);
		if (count == 0)
			return [];
		var result = new Guid[count];
		FillCore(result, DateTime.UtcNow.Ticks);
		return result;
	}

	/// <summary>
	/// Creates an array of new UUID version 8 values that all embed <paramref name="timestamp"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <param name="timestamp">
	/// The timestamp to embed in every UUID. Must have <see cref="DateTimeKind.Utc"/> or
	/// <see cref="DateTimeKind.Local"/> kind, with a value between January 1st, 1970 UTC and now.
	/// </param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 8 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewGuids(int count, DateTime timestamp)
	{
		ValidateCount(count);
		if (count == 0)
			return [];
		var result = new Guid[count];
		Fill(result, timestamp);
		return result;
	}

	/// <summary>
	/// Creates an array of new UUID version 8 values in SQL Server byte order, sharing a
	/// single current-UTC-time capture.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <returns>An array of <paramref name="count"/> version 8 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewSqlGuids(int count)
	{
		var result = NewGuids(count);
		for (var i = 0; i < result.Length; i++)
			result[i] = result[i].ToSqlGuid();
		return result;
	}

	/// <summary>
	/// Creates an array of new UUID version 8 values in SQL Server byte order that all embed
	/// <paramref name="timestamp"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <param name="timestamp">
	/// The timestamp to embed in every UUID. Must have <see cref="DateTimeKind.Utc"/> or
	/// <see cref="DateTimeKind.Local"/> kind, with a value between January 1st, 1970 UTC and now.
	/// </param>
	/// <returns>An array of <paramref name="count"/> version 8 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewSqlGuids(int count, DateTime timestamp)
	{
		var result = NewGuids(count, timestamp);
		for (var i = 0; i < result.Length; i++)
			result[i] = result[i].ToSqlGuid();
		return result;
	}

	// Converts a DateTime to validated UTC ticks, mirroring the single-call NewGuid(DateTime) rules.
	static long ToValidatedTicks(DateTime timestamp)
	{
		var ticks = timestamp.Kind switch
		{
			DateTimeKind.Utc => timestamp.Ticks, // use ticks as is
			DateTimeKind.Local => timestamp.ToUniversalTime().Ticks, // convert to UTC
			_ => throw new ArgumentException("DateTimeKind.Unspecified not supported", nameof(timestamp))
		};
		return !ticks.IsDateTime
			? throw new ArgumentException("Timestamp must be between January 1st, 1970 UTC and now",
				nameof(timestamp))
			: ticks;
	}

	static void ValidateCount(int count)
	{
		if (count is < 0 or > 0x40_0000)
			throw new ArgumentOutOfRangeException(nameof(count),
				"Count must be between 0 and the 22-bit counter space (4,194,304).");
	}

	static void FillSqlCore(Span<Guid> destination, long timestamp)
	{
		FillCore(destination, timestamp);
		for (var i = 0; i < destination.Length; i++)
			destination[i] = destination[i].ToSqlGuid();
	}

	[SkipLocalsInit]
	static void FillCore(Span<Guid> destination, long timestamp)
	{
		if (destination.Length > 0x40_0000)
			throw new ArgumentOutOfRangeException(nameof(destination),
				"Batch size must not exceed the 22-bit counter space (4,194,304).");
		if (destination.IsEmpty)
			return;

		var count = destination.Length;
		// RFC 9562 §6.2 Method 1: reserve a contiguous block of counter slots so the
		// whole batch is ordered and concurrent callers can never collide. The +1 mirrors
		// Interlocked.Increment semantics on the single-call path, which consumes the
		// post-increment value — the block is (old, old + count], never reusing old.
		var start = Interlocked.Add(ref _increment, count) - count + 1;

		Span<byte> bytes = stackalloc byte[16];
		// custom_a: timestamp bits [59:12] → octets 0-5; custom_b: bits [11:0] → octets 6-7.
		// Identical for every item — written once, as is the machine/pid fingerprint.
		bytes[0] = (byte)(timestamp >> 52);
		bytes[1] = (byte)(timestamp >> 44);
		bytes[2] = (byte)(timestamp >> 36);
		bytes[3] = (byte)(timestamp >> 28);
		bytes[4] = (byte)(timestamp >> 20);
		bytes[5] = (byte)(timestamp >> 12);
		bytes[6] = (byte)((timestamp >> 8) & 0x0F);
		bytes[7] = (byte)timestamp;
		bytes[11] = _machinePid[0];
		bytes[12] = _machinePid[1];
		bytes[13] = _machinePid[2];
		bytes[14] = _machinePid[3];
		bytes[15] = _machinePid[4];
		bytes.SetRfc9562Version(8); // octet 6 is per-batch; version set once

		for (var i = 0; i < count; i++)
		{
			// start may be negative when _increment wraps near Int32.MaxValue; the mask
			// discards bits 22-31, which is correct regardless of sign.
			var increment = (start + i) & 0x003fffff;

			// custom_c: increment[21:0] → octets 8-10 (variant takes upper 2 bits of octet 8)
			bytes[8] = (byte)((increment >> 16) & 0x3F);
			bytes[9] = (byte)(increment >> 8);
			bytes[10] = (byte)increment;
			bytes.SetRfc9562Variant(); // octet 8 is rewritten per item

			destination[i] = new(bytes, bigEndian: true);
		}
	}

#if NET8_0_OR_GREATER
	/// <summary>
	/// Creates a new UUID version 8 using the current time of the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="provider">The clock supplying the timestamp.</param>
	/// <returns>A new time-ordered version 8 <see cref="Guid"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	public static Guid NewGuid(TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewGuid(provider.GetUtcNow().UtcDateTime);
	}

	/// <summary>
	/// Creates a new UUID version 8 using the current time of the supplied <see cref="TimeProvider"/>,
	/// with byte ordering suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <param name="provider">The clock supplying the timestamp.</param>
	/// <returns>A new time-ordered version 8 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	public static Guid NewSqlGuid(TimeProvider provider) =>
		NewGuid(provider).ToSqlGuid();

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values using a single
	/// timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void Fill(Span<Guid> destination, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		Fill(destination, provider.GetUtcNow().UtcDateTime);
	}

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values in SQL Server byte
	/// order using a single timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		FillSql(destination, provider.GetUtcNow().UtcDateTime);
	}

	/// <summary>
	/// Creates an array of new UUID version 8 values using a single timestamp capture from the
	/// supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 8 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewGuids(int count, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewGuids(count, provider.GetUtcNow().UtcDateTime);
	}

	/// <summary>
	/// Creates an array of new UUID version 8 values in SQL Server byte order using a single
	/// timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <returns>An array of <paramref name="count"/> version 8 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewSqlGuids(int count, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewSqlGuids(count, provider.GetUtcNow().UtcDateTime);
	}
#endif
#endif
}
