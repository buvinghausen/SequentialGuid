#if !NET6_0_OR_GREATER
using System.Diagnostics;
#endif
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace SequentialGuid;

/// <summary>
/// Provides a base implementation for generating RFC 9562 UUID Version 8 (time-based) values
/// that embed a 60-bit timestamp and a machine/process identifier, ensuring monotonically increasing ordering.
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
/// <typeparam name="T">The derived generator type used to implement the singleton pattern.</typeparam>
public abstract class SequentialGuidGeneratorBase<T> where T : SequentialGuidGeneratorBase<T>
{
	private static readonly Lazy<T> Lazy =
		new(() => (Activator.CreateInstance(typeof(T), true) as T)!);

	private readonly byte[] _machinePid;
	private int _increment;

	/// <summary>
	/// Initializes a new instance by seeding the increment counter and capturing the machine/process identifier.
	/// </summary>
	protected SequentialGuidGeneratorBase()
	{
#if NETFRAMEWORK || NETSTANDARD2_0
		// Fall back to the old Random create function
		using var rng = RandomNumberGenerator.Create();
		_increment = rng
#else
		// Use the RandomNumberGenerator static function where available
		_increment = RandomNumberGenerator
#endif
			.GetInt32(500000);
		_machinePid = new byte[5];
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

#pragma warning disable CA1000
	/// <summary>
	/// Gets the singleton instance of the generator.
	/// </summary>
	public static T Instance =>
		Lazy.Value;
#pragma warning restore CA1000

	/// <summary>
	/// Generates a new sequential <see cref="Guid"/> using the current UTC time as the timestamp.
	/// </summary>
	/// <returns>A new sequential <see cref="Guid"/>.</returns>
	public Guid NewGuid() =>
		NewGuid(DateTime.UtcNow.Ticks);

	/// <summary>
	/// Generates a new sequential <see cref="Guid"/> using the specified timestamp.
	/// </summary>
	/// <param name="timestamp">
	/// The timestamp to embed in the <see cref="Guid"/>. Must be a <see cref="DateTime"/> with
	/// <see cref="DateTimeKind.Utc"/> or <see cref="DateTimeKind.Local"/> kind, with a value
	/// between January 1st, 1970 UTC and now.
	/// </param>
	/// <returns>A new sequential <see cref="Guid"/>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	public Guid NewGuid(DateTime timestamp)
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

		// Once we've gotten here we have a valid UTC tick count so yield the Guid
		return NewGuid(ticks);
	}

	internal virtual Guid NewGuid(long timestamp)
	{
		// only use low order 3 bytes
		var increment = Interlocked.Increment(ref _increment) & 0x00ffffff;
		// RFC 9562 §B.1 UUIDv8 time-based layout:
		//   custom_a [48 bits] = timestamp[59:12] → Guid.a (32 bits) + Guid.b (16 bits)
		//   ver      [ 4 bits] = 0x8              → high nibble of Guid.c
		//   custom_b [12 bits] = timestamp[11:0]  → low 12 bits of Guid.c
		//   var      [ 2 bits] = 0b10             → top 2 bits of d[0]
		//   custom_c [62 bits] = machinePid + increment → d[0] (lower 6 bits) + d[1..7]
		return new(
			(int)(timestamp >> 28),
			(short)(timestamp >> 12),
			(short)(0x8000 | (timestamp & 0x0FFF)),
			[
				(byte)((_machinePid[0] & 0x3F) | 0x80),
				_machinePid[1], _machinePid[2], _machinePid[3], _machinePid[4],
				(byte)(increment >> 16), (byte)(increment >> 8), (byte)increment
			]
		);
	}
}
