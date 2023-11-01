using System.Data.SqlTypes;
#if NETFRAMEWORK || NETSTANDARD2_0
using System.Security.Cryptography;
#endif

// ReSharper disable once CheckNamespace
namespace System;

/// <summary>
///     Provides extension methods to return back timestamps from a guid as well as convert to &amp; from normal sorting
///     and SQL Server sorting
/// </summary>
public static class SequentialGuidExtensions
{
#if NETFRAMEWORK || NETSTANDARD2_0
	// Was added in .NET Standard 2.1 and later so we only need to provide it for .NET Framework
	private static readonly DateTime UnixEpoch =
		new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	// Create matching signature for old RNG class
	internal static int GetInt32(this RandomNumberGenerator generator, int toExclusive)
	{
		// where max is exclusive
		var bytes = new byte[sizeof(int)]; // 4 bytes
		generator.GetNonZeroBytes(bytes);
		return (BitConverter.ToInt32(bytes, 0) % toExclusive + toExclusive) % toExclusive;
	}
#endif
	//See: https://www.sqlbi.com/blog/alberto/2007/08/31/how-are-guids-sorted-by-sql-server/
	//See: https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/comparing-guid-and-uniqueidentifier-values
	private static readonly int[] GuidIndex = [13, 12, 11, 10, 15, 14, 9, 8, 6, 7, 4, 5, 0, 1, 2, 3];
	private static readonly int[] SqlGuidIndex = [12, 13, 14, 15, 10, 11, 8, 9, 7, 6, 3, 2, 1, 0, 5, 4];

	private static DateTime ToDateTime(this long ticks) =>
		new(ticks, DateTimeKind.Utc);

	/// <summary>
	///     Will return the value of DateTime.UtcNow at the time of the generation of the Guid will keep you from storing
	///     separate audit fields
	/// </summary>
	/// <param name="id">A sequential Guid with the first 8 bytes containing the system ticks at time of generation</param>
	/// <returns>DateTime?</returns>
	public static DateTime? ToDateTime(this Guid id)
	{
		var ticks = id.ToTicks();
		if (ticks.IsDateTime()) return ticks.ToDateTime();

		//Try conversion through sql guid
		ticks = new SqlGuid(id).ToGuid().ToTicks();
		return ticks.IsDateTime()
			? ticks.ToDateTime()
			: default(DateTime?);
	}

	/// <summary>
	///     Will return the value of DateTime.UtcNow at the time of the generation of the Guid will keep you from storing
	///     separate audit fields
	/// </summary>
	/// <param name="sqlGuid">
	///     A sequential SqlGuid with the first sorted 8 bytes containing the system ticks at time of
	///     generation
	/// </param>
	/// <returns>DateTime?</returns>
	public static DateTime? ToDateTime(this SqlGuid sqlGuid) =>
		sqlGuid.ToGuid().ToDateTime();

	/// <summary>
	///     Will take a SqlGuid and re-sequence to a Guid that will sort in the same order
	/// </summary>
	/// <param name="sqlGuid">Any SqlGuid</param>
	/// <returns>Guid</returns>
	public static Guid ToGuid(this SqlGuid sqlGuid)
	{
		var bytes = sqlGuid.ToByteArray()
#if NET6_0_OR_GREATER
				!
#endif
			;
		return new(GuidIndex.Select(i => bytes[i]).ToArray());
	}

	/// <summary>
	///     Will take a Guid and will re-sequence it so that it will sort properly in SQL Server without fragmenting your
	///     tables
	/// </summary>
	/// <param name="id">Any Guid</param>
	/// <returns>SqlGuid</returns>
	public static SqlGuid ToSqlGuid(this Guid id)
	{
		var bytes = id.ToByteArray();
		return new(SqlGuidIndex.Select(i => bytes[i]).ToArray());
	}

	internal static bool IsDateTime(this long ticks) =>
		ticks <= DateTime.UtcNow.Ticks &&
		ticks >=
#if NETFRAMEWORK || NETSTANDARD2_0
				 UnixEpoch
#else
				 DateTime.UnixEpoch
#endif
							.Ticks;

	private static long ToTicks(this Guid guid)
	{
		var bytes = guid.ToByteArray();
		return
			((long)bytes[3] << 56) +
			((long)bytes[2] << 48) +
			((long)bytes[1] << 40) +
			((long)bytes[0] << 32) +
			((long)bytes[5] << 24) +
			(bytes[4] << 16) +
			(bytes[7] << 8) +
			bytes[6];
	}
}
