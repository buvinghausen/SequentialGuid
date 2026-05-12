using System.Runtime.CompilerServices;
using SequentialGuid;
using SequentialGuid.Extensions;

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
	private static readonly Guid s_maxValue = new("ffffffff-ffff-ffff-ffff-ffffffffffff");

	extension(Guid id)
	{
		/// <summary>Gets the RFC 9562 §5.10 max UUID — all bits set to 1.</summary>
		public static Guid MaxValue => s_maxValue;

		/// <summary>
		/// Converts a <see cref="Guid"/> to a <see cref="DateTime"/> if the <see cref="Guid"/> contains a valid timestamp.
		/// </summary>
		/// <returns>
		/// A <see cref="DateTime"/> representing the timestamp embedded in the <see cref="Guid"/>,
		/// or <c>null</c> if the <see cref="Guid"/> does not contain a valid timestamp.
		/// </returns>
		public DateTime? ToDateTime()
		{
#if NET6_0_OR_GREATER
			Span<byte> bytes = stackalloc byte[16];
			id.TryWriteBytes(bytes);
			var ticks = bytes.ToTicks();
#else
			var bytes = id.ToByteArray();
			var ticks = bytes.ToTicks();
#endif
			if (ticks is { IsDateTime: true })
				return ticks.Value.ToDateTime();
			// Could be sql guid so normalize byte order and re-run
#if NET6_0_OR_GREATER
			Span<byte> sqlBytes = stackalloc byte[16];
			bytes.WriteFromSqlByteOrder(sqlBytes);
			ticks = sqlBytes.ToTicks();
#else
			ticks = bytes.FromSqlByteOrder().ToTicks();
#endif
			return ticks is { IsDateTime: true }
				? ticks.Value.ToDateTime()
				: null;
		}

		/// <summary>
		/// Converts a <see cref="Guid"/> to its SQL Server byte order equivalent.
		/// </summary>
		/// <returns>
		/// A <see cref="Guid"/> with its bytes reordered to match SQL Server's internal sorting order.
		/// </returns>
		public Guid ToSqlGuid()
		{
#if NET6_0_OR_GREATER
			Span<byte> src = stackalloc byte[16];
			id.TryWriteBytes(src);
			Span<byte> dst = stackalloc byte[16];
			src.WriteToSqlByteOrder(dst);
			return new(dst);
#else
			return new(id.ToByteArray().ToSqlByteOrder());
#endif
		}

		/// <summary>
		/// Converts a SQL Server byte order <see cref="Guid"/> back to its standard byte order equivalent.
		/// </summary>
		/// <returns>
		/// A <see cref="Guid"/> with its bytes reordered from SQL Server's internal sorting order to the standard ordering.
		/// </returns>
		public Guid FromSqlGuid()
		{
#if NET6_0_OR_GREATER
			Span<byte> src = stackalloc byte[16];
			id.TryWriteBytes(src);
			Span<byte> dst = stackalloc byte[16];
			src.WriteFromSqlByteOrder(dst);
			return new(dst);
#else
			return new(id.ToByteArray().FromSqlByteOrder());
#endif
		}
	}
}
