using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlTypes;
using System.Linq;

namespace SequentialGuid
{
	public static class ExtensionMethods
	{
		private static readonly IReadOnlyDictionary<byte, byte> ToSqlGuidMap;
		private static readonly IReadOnlyDictionary<byte, byte> ToGuidMap;

		/// <summary>
		/// Constructor initializes the guid seqeuence mappings
		/// </summary>
		static ExtensionMethods()
		{
			//See: http://sqlblog.com/blogs/alberto_ferrari/archive/2007/08/31/how-are-guids-sorted-by-sql-server.aspx
			ToGuidMap = new ReadOnlyDictionary<byte, byte>(new Dictionary<byte, byte>
			{
				{0, 13},
				{1, 12},
				{2, 11},
				{3, 10},
				{4, 15},
				{5, 14},
				{6, 9},
				{7, 8},
				{8, 6},
				{9, 7},
				{10, 4},
				{11, 5},
				{12, 0},
				{13, 1},
				{14, 2},
				{15, 3}
			});
			//Invert map
			ToSqlGuidMap =
				new ReadOnlyDictionary<byte, byte>(
					ToGuidMap.ToDictionary(d => d.Value, d => d.Key));
		}

		private static DateTime ToDateTime(this long ticks) =>
			new DateTime(ticks, DateTimeKind.Utc);

		/// <summary>
		/// Will return the value of DateTime.UtcNow at the time of the generation of the Guid will keep you from storing separate audit fields
		/// </summary>
		/// <param name="guid">A sequential Guid with the first 8 bytes containing the system ticks at time of generation</param>
		/// <returns>DateTime</returns>
		public static DateTime? ToDateTime(this Guid guid)
		{
			var ticks = guid.ToTicks();
			if (ticks.IsDateTime())
				return ticks.ToDateTime();

			//Try conversion through sql guid
			ticks = new SqlGuid(guid).ToGuid().ToTicks();
			return ticks.IsDateTime()
				? ticks.ToDateTime()
				: default(DateTime?);
		}

		/// <summary>
		/// Will return the value of DateTime.UtcNow at the time of the generation of the Guid will keep you from storing separate audit fields
		/// </summary>
		/// <param name="sqlGuid">A sequential SqlGuid with the first sorted 8 bytes containing the system ticks at time of generation</param>
		/// <returns>DateTime</returns>
		public static DateTime? ToDateTime(this SqlGuid sqlGuid) =>
			sqlGuid.ToGuid().ToDateTime();

		/// <summary>
		/// Will take a SqlGuid and reseqeuence to a Guid that will sort in the same order
		/// </summary>
		/// <param name="sqlGuid">Any SqlGuid</param>
		/// <returns>Guid</returns>
		public static Guid ToGuid(this SqlGuid sqlGuid)
		{
			var bytes = sqlGuid.ToByteArray();
			return new Guid(Enumerable.Range(0, 16)
				.Select(e => bytes[ToGuidMap[(byte)e]])
				.ToArray());
		}

		/// <summary>
		/// Will take a Guid and will resequence it so that it will sort properly in SQL Server without fragmenting your tables
		/// </summary>
		/// <param name="guid">Any Guid</param>
		/// <returns>SqlGuid</returns>
		public static SqlGuid ToSqlGuid(this Guid guid)
		{
			var bytes = guid.ToByteArray();
			return new SqlGuid(Enumerable.Range(0, 16)
				.Select(e => bytes[ToSqlGuidMap[(byte)e]])
				.ToArray());
		}

		internal static bool IsDateTime(this long ticks) =>
			ticks <= DateTime.UtcNow.Ticks && ticks >= SequentialGuid.UnixEpoch.Ticks;

		private static long ToTicks(this Guid guid)
		{
			var bytes = guid.ToByteArray();
			return ((long)bytes[3] << 56) +
				((long)bytes[2] << 48) +
				((long)bytes[1] << 40) +
				((long)bytes[0] << 32) +
				((long)bytes[5] << 24) +
				(bytes[4] << 16) +
				(bytes[7] << 8) +
				bytes[6];
		}
	}
}
