#if !NET6_0_OR_GREATER
using System.Diagnostics;
#endif
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace SequentialGuid;

/// <summary>
/// Provides a base class for generating sequential <see cref="Guid"/> values.
/// </summary>
/// <typeparam name="T">
/// The type of the derived generator class. This type must inherit from 
/// <see cref="SequentialGuidGeneratorBase{T}"/>.
/// </typeparam>
/// <remarks>
/// This abstract class serves as the foundation for creating sequential GUID generators.
/// It ensures that GUIDs are generated in a sequential manner, which can improve performance
/// in scenarios such as database indexing. The class includes mechanisms for generating
/// machine-specific identifiers and increment values, ensuring compatibility across different
/// .NET versions.
/// </remarks>
public abstract class SequentialGuidGeneratorBase<T> where T : SequentialGuidGeneratorBase<T>
{
	private static readonly Lazy<T> Lazy =
		new(() => (Activator.CreateInstance(typeof(T), true) as T)!);

	private readonly byte[] _machinePid;
	private int _increment;

	/// <summary>
	/// Initializes a new instance of the <see cref="SequentialGuidGeneratorBase{T}"/> class.
	/// </summary>
	/// <remarks>
	/// This constructor sets up the initial state of the generator, including generating a machine-specific identifier
	/// and initializing an increment value. It ensures compatibility across different .NET versions by using appropriate
	/// APIs for cryptographic operations and process identification.
	/// </remarks>
	/// <exception cref="SecurityException">
	/// Thrown if access to process information is restricted due to security settings.
	/// </exception>
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
	/// Gets the singleton instance of the <typeparamref name="T"/> generator.
	/// </summary>
	/// <value>
	/// The singleton instance of the <typeparamref name="T"/> generator, ensuring a single shared instance
	/// across the application for generating sequential GUIDs.
	/// </value>
	/// <remarks>
	/// This property uses a thread-safe lazy initialization pattern to create the instance of the generator.
	/// It ensures that the generator is only instantiated once and is reused throughout the application.
	/// </remarks>
	public static T Instance =>
		Lazy.Value;
#pragma warning restore CA1000
	
	/// <summary>
	/// Generates a new sequential <see cref="Guid"/>.
	/// </summary>
	/// <returns>A new sequential <see cref="Guid"/>.</returns>
	/// <remarks>
	/// The generated <see cref="Guid"/> is based on the current UTC timestamp and is designed to be sequential,
	/// which can improve performance in certain scenarios, such as database indexing.
	/// </remarks>
	public Guid NewGuid() =>
		NewGuid(DateTime.UtcNow.Ticks);
	
	/// <summary>
	/// Generates a new <see cref="Guid"/> based on the provided timestamp.
	/// </summary>
	/// <param name="timestamp">
	/// The <see cref="DateTime"/> value used to generate the <see cref="Guid"/>. 
	/// The timestamp must be in UTC or convertible to UTC. 
	/// <see cref="DateTimeKind.Unspecified"/> is not supported.
	/// </param>
	/// <returns>
	/// A <see cref="Guid"/> that incorporates the provided timestamp, ensuring sequential ordering.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown if the <paramref name="timestamp"/> is of kind <see cref="DateTimeKind.Unspecified"/> 
	/// or if the timestamp is outside the valid range (between January 1st, 1970 UTC and now).
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
			[.. _machinePid, .. new[] { (byte)(increment >> 16), (byte)(increment >> 8), (byte)increment }]
		);
	}
}
