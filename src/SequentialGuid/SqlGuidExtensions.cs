using System.Data.SqlTypes;

namespace SequentialGuid;

/// <summary>
/// Provides extension methods for working with <see cref="SqlGuid"/> objects,
/// including conversions and operations related to timestamps.
/// </summary>
public static class SqlGuidExtensions
{
	/// <param name="sqlGuid">The <see cref="SqlGuid"/> to extract the timestamp from.</param>
	extension(SqlGuid sqlGuid)
	{
		/// <summary>
		/// Converts a <see cref="SqlGuid"/> to a <see cref="DateTime"/> if the <see cref="SqlGuid"/> contains a valid timestamp.
		/// </summary>
		/// <returns>
		/// A <see cref="DateTime"/> representing the timestamp embedded in the <see cref="SqlGuid"/>,
		/// or <c>null</c> if the <see cref="SqlGuid"/> does not contain a valid timestamp.
		/// </returns>
		public DateTime? ToDateTime() =>
			sqlGuid.ToGuid().ToDateTime();

		/// <summary>
		/// Converts a <see cref="SqlGuid"/> to a <see cref="Guid"/> by rearranging its byte order
		/// from SQL Server sorting order to standard .NET order.
		/// </summary>
		/// <returns>A <see cref="Guid"/> representation of the specified <see cref="SqlGuid"/>.</returns>
		public Guid ToGuid() =>
			new (sqlGuid.ToByteArray()!.FromSqlByteOrder());
	}
}
