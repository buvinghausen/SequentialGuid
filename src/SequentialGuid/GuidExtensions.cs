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
public static class GuidExtensions
{
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
			var bytes = id.ToByteArray();
			var ticks = bytes.ToTicks();
			if (ticks is { IsDateTime: true })
				return ticks.Value.ToDateTime();

			// Could be sql guid so normalize byte order and re-run
			ticks = bytes.FromSqlByteOrder().ToTicks();
			return ticks is { IsDateTime: true }
				? ticks.Value.ToDateTime()
				: null;
		}

		/// <summary>
		/// Converts a <see cref="Guid"/> to a <see cref="SqlGuid"/> by rearranging its byte order
		/// to match the sorting order used by SQL Server.
		/// </summary>
		/// <returns>A <see cref="SqlGuid"/> representation of the provided <see cref="Guid"/>.</returns>
		public SqlGuid ToSqlGuid() =>
			new(id.ToByteArray().ToSqlByteOrder());

		internal long ToUnixMs() =>
			id.ToByteArray().Rfc9562V7UnixMs;
	}
}
