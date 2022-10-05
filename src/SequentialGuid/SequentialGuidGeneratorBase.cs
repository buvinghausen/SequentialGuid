#if !NET6_0_OR_GREATER
using System.Diagnostics;
#endif
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace SequentialGuid;

/// <summary>
///     Base class that provides sequential guid generation capabilities based on the MongoDB object id specification
/// </summary>
/// <typeparam name="T">Child class</typeparam>
public abstract class SequentialGuidGeneratorBase<T> where T : SequentialGuidGeneratorBase<T>
{
	private static readonly Lazy<T> Lazy =
		new(() => (Activator.CreateInstance(typeof(T), true) as T)!);

	private readonly byte[] _machinePid;
	private int _increment;

	/// <summary>
	///     Protected constructor that seeds the necessary values from the machine name hash &amp; process id as well as seed
	///     the increment
	/// </summary>
	protected SequentialGuidGeneratorBase()
	{
		_increment = new Random().Next(500000);
		_machinePid = new byte[5];
// For newer frameworks use the preferred static function
#if NET6_0_OR_GREATER
		var hash = SHA512.HashData
#else
		using var algorithm = SHA512.Create();
		var hash = algorithm.ComputeHash		
#endif
			(Encoding.UTF8.GetBytes(Environment.MachineName));
		for (var i = 0; i < 3; i++)
			_machinePid[i] = hash[i];
		try
		{
			var pid =
// Older frameworks don't support the static value on the environment
// So get it off the old process path
#if NET6_0_OR_GREATER
				Environment.ProcessId
#else
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
	///     Singleton instance of the generator
	/// </summary>
	public static T Instance =>
		Lazy.Value;

	/// <summary>
	///     Returns a guid for the value of UtcNow
	/// </summary>
	/// <returns>Sequential guid</returns>
	public Guid NewGuid() =>
		NewGuid(DateTime.UtcNow.Ticks);

	/// <summary>
	///     Takes a date time parameter to encode in a sequential guid
	/// </summary>
	/// <param name="timestamp">
	///     Timestamp that must not be in unspecified kind and must be between the unix epoch and now to be
	///     considered valid
	/// </param>
	/// <returns>Sequential guid</returns>
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
		{
			throw new ArgumentException("Timestamp must be between January 1st, 1970 UTC and now",
				nameof(timestamp));
		}

		// Once we've gotten here we have a valid UTC tick count so yield the Guid
		return NewGuid(ticks);
	}

	/// <summary>
	///     Implementation that increments the counter and shreds the timestamp &amp; increment and constructs the guid
	/// </summary>
	/// <param name="timestamp">Timestamp in terms of Ticks</param>
	/// <returns></returns>
	internal virtual Guid NewGuid(long timestamp)
	{
		// only use low order 3 bytes
		var increment = Interlocked.Increment(ref _increment) & 0x00ffffff;
		return new Guid(
			(int)(timestamp >> 32),
			(short)(timestamp >> 16),
			(short)timestamp,
			_machinePid.Concat(
				new[] {(byte)(increment >> 16), (byte)(increment >> 8), (byte)increment}).ToArray()
		);
	}
}
