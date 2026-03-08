using System.Data.SqlTypes;
using System.Runtime.CompilerServices;
using SequentialGuid;

#pragma warning disable IDE0130 // Namespace does not match folder structure
// ReSharper disable once CheckNamespace
namespace System;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for working with <see cref="Guid"/> objects,
/// including conversions and operations related to timestamps and SQL Server sorting order.
/// </summary>
[SkipLocalsInit]
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
#if !NETFRAMEWORK && !NETSTANDARD
			Span<byte> bytes = stackalloc byte[16];
			id.TryWriteBytes(bytes);
			var ticks = (bytes).ToTicks();
#else
			var bytes = id.ToByteArray();
			var ticks = bytes.ToTicks();
#endif
			if (ticks is { IsDateTime: true })
				return ticks.Value.ToDateTime();
			// Could be sql guid so normalize byte order and re-run
#if !NETFRAMEWORK && !NETSTANDARD
			Span<byte> sqlBytes = stackalloc byte[16];
			(bytes).WriteFromSqlByteOrder(sqlBytes);
			ticks = (sqlBytes).ToTicks();
#else
			ticks = bytes.FromSqlByteOrder().ToTicks();
#endif
			return ticks is { IsDateTime: true }
				? ticks.Value.ToDateTime()
				: null;
		}

		/// <summary>
		/// Converts a <see cref="Guid"/> to a <see cref="SqlGuid"/> by rearranging its byte order
		/// to match the sorting order used by SQL Server.
		/// </summary>
		/// <returns>A <see cref="SqlGuid"/> representation of the provided <see cref="Guid"/>.</returns>
		public SqlGuid ToSqlGuid()
		{
#if !NETFRAMEWORK && !NETSTANDARD
			Span<byte> src = stackalloc byte[16];
			id.TryWriteBytes(src);
			Span<byte> dst = stackalloc byte[16];
			(src).WriteToSqlByteOrder(dst);
			return new(new Guid(dst));
#else
			return new(id.ToByteArray().ToSqlByteOrder());
#endif
		}

		internal long ToUnixMs()
		{
#if !NETFRAMEWORK && !NETSTANDARD
			Span<byte> bytes = stackalloc byte[16];
			id.TryWriteBytes(bytes);
			return (bytes).Rfc9562V7UnixMs;
#else
			return id.ToByteArray().Rfc9562V7UnixMs;
#endif
		}
	}
}
