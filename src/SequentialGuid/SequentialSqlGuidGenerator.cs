using System.Data.SqlTypes;

namespace SequentialGuid;

/// <summary>
/// Represents a generator for creating sequential <see cref="SqlGuid"/> values.
/// </summary>
/// <remarks>
/// This class extends <see cref="SequentialGuidGeneratorBase{T}"/> to provide functionality
/// for generating sequential <see cref="SqlGuid"/> values. Sequential GUIDs are particularly
/// useful in database scenarios where maintaining index performance is critical.
/// </remarks>
public sealed class SequentialSqlGuidGenerator : SequentialGuidGeneratorBase<SequentialSqlGuidGenerator>
{
	private SequentialSqlGuidGenerator() { }

	internal override Guid NewGuid(long timestamp) =>
		base.NewGuid(timestamp).ToSqlGuid().Value;

	/// <summary>
	/// Generates a new sequential <see cref="SqlGuid"/>.
	/// </summary>
	/// <returns>A new sequential <see cref="SqlGuid"/>.</returns>
	/// <remarks>
	/// The generated <see cref="SqlGuid"/> is based on a sequential <see cref="Guid"/> 
	/// and is particularly useful in database scenarios where maintaining index performance 
	/// is critical. This method ensures that the <see cref="SqlGuid"/> values are sequential 
	/// to optimize database indexing and retrieval operations.
	/// </remarks>
	public SqlGuid NewSqlGuid() =>
		new(NewGuid());


	/// <summary>
	/// Generates a new sequential <see cref="SqlGuid"/> based on the provided timestamp.
	/// </summary>
	/// <param name="timestamp">
	/// The <see cref="DateTime"/> value used to generate the <see cref="SqlGuid"/>. 
	/// The timestamp must be in UTC or convertible to UTC. 
	/// <see cref="DateTimeKind.Unspecified"/> is not supported.
	/// </param>
	/// <returns>
	/// A <see cref="SqlGuid"/> that incorporates the provided timestamp, ensuring sequential ordering.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown if the <paramref name="timestamp"/> is of kind <see cref="DateTimeKind.Unspecified"/> 
	/// or if the timestamp is outside the valid range (between January 1st, 1970 UTC and now).
	/// </exception>
	public SqlGuid NewSqlGuid(DateTime timestamp) =>
		new(NewGuid(timestamp));
}
