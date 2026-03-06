using System.Data.SqlTypes;

namespace SequentialGuid;

/// <summary>
/// Generates sequential <see cref="SqlGuid"/> values ordered according to SQL Server's
/// <c>uniqueidentifier</c> comparison rules, minimizing index fragmentation when used as primary keys.
/// </summary>
public sealed class SequentialSqlGuidGenerator : SequentialGuidGeneratorBase<SequentialSqlGuidGenerator>
{
	private SequentialSqlGuidGenerator() { }

	internal override Guid NewGuid(long timestamp) =>
		base.NewGuid(timestamp).ToSqlGuid().Value;

	/// <summary>
	/// Generates a new sequential <see cref="SqlGuid"/> using the current UTC time as the timestamp.
	/// </summary>
	/// <returns>A new sequential <see cref="SqlGuid"/>.</returns>
	public SqlGuid NewSqlGuid() =>
		new(NewGuid());
	
	/// <summary>
	/// Generates a new sequential <see cref="SqlGuid"/> using the specified timestamp.
	/// </summary>
	/// <param name="timestamp">
	/// The timestamp to embed in the <see cref="SqlGuid"/>. Must be a <see cref="DateTime"/> with
	/// <see cref="DateTimeKind.Utc"/> or <see cref="DateTimeKind.Local"/> kind, with a value
	/// between January 1st, 1970 UTC and now.
	/// </param>
	/// <returns>A new sequential <see cref="SqlGuid"/>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	public SqlGuid NewSqlGuid(DateTime timestamp) =>
		new(NewGuid(timestamp));
}
