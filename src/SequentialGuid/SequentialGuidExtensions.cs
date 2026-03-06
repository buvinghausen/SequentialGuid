using System.Data.SqlTypes;
using SequentialGuid;

#pragma warning disable IDE0130 // Namespace does not match folder structure
// ReSharper disable once CheckNamespace
namespace System;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for working with <see cref="Guid"/> objects,
/// including conversions and operations related to timestamps and SQL Server sorting order.
/// </summary>
public static class SequentialGuidExtensions
{
	//See: https://www.sqlbi.com/blog/alberto/2007/08/31/how-are-guids-sorted-by-sql-server/
	//See: https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/comparing-guid-and-uniqueidentifier-values
	private static readonly int[] SqlGuidIndex = [12, 13, 14, 15, 10, 11, 8, 9, 7, 6, 3, 2, 1, 0, 5, 4];

	extension(Guid id)
	{
		/// <summary>
		/// Converts a <see cref="Guid"/> to a <see cref="DateTime"/> if the <see cref="Guid"/> contains a valid timestamp.
		/// </summary>
		/// <returns>
		/// A <see cref="DateTime"/> representing the timestamp embedded in the <see cref="Guid"/>,
		/// or <c>null</c> if the <see cref="Guid"/> does not contain a valid timestamp.
		/// </returns>
		public DateTime? ToDateTime()
		{
			var ticks = id.ToTicks();
			if (ticks.IsDateTime()) return ticks.ToDateTime();

			//Try conversion through sql guid
			ticks = new SqlGuid(id).ToGuid().ToTicks();
			return ticks.IsDateTime()
				? ticks.ToDateTime()
				: null;
		}

		/// <summary>
		/// Converts a <see cref="Guid"/> to a <see cref="SqlGuid"/> by rearranging its byte order
		/// to match the sorting order used by SQL Server.
		/// </summary>
		/// <returns>A <see cref="SqlGuid"/> representation of the provided <see cref="Guid"/>.</returns>
		public SqlGuid ToSqlGuid()
		{
			var bytes = id.ToByteArray();
			return new([.. SqlGuidIndex.Select(i => bytes[i])]);
		}

		private long ToTicks()
		{
			var bytes = id.ToByteArray();
			return bytes.IsRfc9562V8 ? bytes.Rfc9562V8Ticks : bytes.LegacyTicks;
		}
	}
}
