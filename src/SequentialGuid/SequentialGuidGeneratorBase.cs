namespace SequentialGuid;

/// <summary>
/// Provides a base implementation for generating RFC 9562 UUID Version 8 (time-based) values
/// that embed a 60-bit timestamp and a machine/process identifier, ensuring monotonically increasing ordering.
/// </summary>
/// <remarks>
/// Delegates UUID construction to <see cref="GuidV8Time"/>. Derived classes may override
/// <see cref="NewGuid(long)"/> to apply additional byte-order transformations (e.g. SQL Server ordering).
/// </remarks>
/// <typeparam name="T">The derived generator type used to implement the singleton pattern.</typeparam>
public abstract class SequentialGuidGeneratorBase<T> where T : SequentialGuidGeneratorBase<T>
{
	private static readonly Lazy<T> Lazy =
		new(() => (Activator.CreateInstance(typeof(T), true) as T)!);

	/// <summary>
	/// Initializes a new instance of the generator.
	/// </summary>
	protected SequentialGuidGeneratorBase() { }

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

	internal virtual Guid NewGuid(long timestamp) =>
		GuidV8Time.NewGuid(timestamp);
}
