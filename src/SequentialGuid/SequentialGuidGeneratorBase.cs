#if !NET6_0_OR_GREATER
using System.Diagnostics;
#endif
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace SequentialGuid;

/// <summary>
/// Provides a base implementation for generating sequential <see cref="Guid"/> values
/// that embed a timestamp and machine/process identifier, ensuring monotonically increasing ordering.
/// </summary>
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
		if (!ticks.IsDateTime())
			throw new ArgumentException("Timestamp must be between January 1st, 1970 UTC and now",
				nameof(timestamp));

		// Once we've gotten here we have a valid UTC tick count so yield the Guid
		return NewGuid(ticks);
	}

	internal virtual Guid NewGuid(long timestamp)
	{
		// only use low order 3 bytes
		var increment = Interlocked.Increment(ref _increment) & 0x00ffffff;
		return new(
			(int)(timestamp >> 32),
			(short)(timestamp >> 16),
			(short)timestamp,
			[.. _machinePid, (byte)(increment >> 16), (byte)(increment >> 8), (byte)increment]
		);
	}
}
