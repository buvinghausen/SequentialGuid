#if !NET6_0_OR_GREATER
using System.Diagnostics;
#endif
using System.Security;
using System.Security.Cryptography;
using System.Text;

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
	private static readonly byte[] MachinePid;
	private static int s_increment;

	static GuidV8Time()
	{
#if NET6_0_OR_GREATER
		// Use the RandomNumberGenerator static function where available
		s_increment = RandomNumberGenerator
#else
		// Fall back to the old Random create function
		using var rng = RandomNumberGenerator.Create();
		s_increment = rng
#endif
			.GetInt32(500000);
		MachinePid = new byte[5];
#if NET6_0_OR_GREATER
		// For newer frameworks use the preferred static function
		var hash = SHA512.HashData
#else
		// For older frameworks use the old algorithm create function
		using var algorithm = SHA512.Create();
		var hash = algorithm.ComputeHash
#endif
			(Encoding.UTF8.GetBytes(Environment.MachineName));
		for (var i = 0; i < 3; i++)
			MachinePid[i] = hash[i];
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
			MachinePid[3] = (byte)(pid >> 8);
			MachinePid[4] = (byte)pid;
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
		NewGuid().ToSqlGuid().Value;

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
		NewGuid(timestamp).ToSqlGuid().Value;

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
		NewGuid(timestamp).ToSqlGuid().Value;

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
		if (!ticks.IsDateTime)
			throw new ArgumentException("Timestamp must be between January 1st, 1970 UTC and now",
				nameof(timestamp));

		return NewGuid(ticks);
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

	internal static Guid NewGuid(long timestamp)
	{
		// only use low order 22 bits
		var increment = Interlocked.Increment(ref s_increment) & 0x003fffff;
		// RFC 9562 §B.1 UUIDv8 time-based layout:
		//   custom_a [48 bits] = timestamp[59:12] → Guid.a (32 bits) + Guid.b (16 bits)
		//   ver      [ 4 bits] = 0x8              → high nibble of Guid.c
		//   custom_b [12 bits] = timestamp[11:0]  → low 12 bits of Guid.c
		//   var      [ 2 bits] = 0b10             → top 2 bits of d[0]
		//   custom_c [62 bits] = increment + machinePid → d[0] (lower 6 bits) + d[1..7]
		return new(
			(int)(timestamp >> 28),
			(short)(timestamp >> 12),
			(short)(0x8000 | (timestamp & 0x0FFF)),
			[
				(byte)(((increment >> 16) & 0x3F) | 0x80),
				(byte)(increment >> 8), (byte)increment,
				MachinePid[0], MachinePid[1], MachinePid[2], MachinePid[3], MachinePid[4]
			]
		);
	}
}
