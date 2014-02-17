using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;

namespace Buvinghausen.SequentialGuid
{
	public static class ExtensionMethods
	{
		private static readonly Dictionary<short, short> ToSqlGuidMap;
		private static readonly Dictionary<short, short> ToGuidMap;
		private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		/// <summary>
		/// Constructor initializes the guid seqeuence mappings
		/// </summary>
		static ExtensionMethods()
		{
			//See: http://sqlblog.com/blogs/alberto_ferrari/archive/2007/08/31/how-are-guids-sorted-by-sql-server.aspx
			ToGuidMap = new Dictionary<short, short>
			{
				{0, 13}, {1, 12}, {2, 11}, {3, 10},
				{4, 15}, {5, 14},
				{6, 9}, {7, 8},
				{8, 6}, {9, 7},
				{10, 4}, {11, 5},
				{12, 0}, {13, 1}, {14, 2}, {15, 3}
			};
			//Invert map
			ToSqlGuidMap = ToGuidMap.ToDictionary(d => d.Value, d => d.Key);
		}

		/// <summary>
		/// Will return the value of DateTime.UtcNow at the time of the generation of the Guid will keep you from storing separate audit fields
		/// </summary>
		/// <param name="guid">A sequential Guid with the first 8 bytes containing the system ticks at time of generation</param>
		/// <returns>DateTime</returns>
		public static DateTime ToDateTime(this Guid guid)
		{
			try
			{
				var bytes = guid.ToByteArray();
				var timestamp = new DateTime(
					((long)bytes[3] << 56) +
					((long)bytes[2] << 48) +
					((long)bytes[1] << 40) +
					((long)bytes[0] << 32) +
					((long)bytes[5] << 24) +
					(bytes[4] << 16) +
					(bytes[7] << 8) +
					bytes[6]);
				if (timestamp <= DateTime.UtcNow && timestamp >= UnixEpoch)
					return timestamp; //timestamp in bounds so return
			}
			catch (ArgumentOutOfRangeException) { }
			//Parse as SqlGuid remap then retry
			return new SqlGuid(guid).ToDateTime();
		}

		/// <summary>
		/// Will return the value of DateTime.UtcNow at the time of the generation of the Guid will keep you from storing separate audit fields
		/// </summary>
		/// <param name="sqlGuid">A sequential SqlGuid with the first sorted 8 bytes containing the system ticks at time of generation</param>
		/// <returns>DateTime</returns>
		public static DateTime ToDateTime(this SqlGuid sqlGuid)
		{
			return sqlGuid.ToGuid().ToDateTime();
		}

		/// <summary>
		/// Will take a SqlGuid and reseqeuence to a Guid that will sort in the same order
		/// </summary>
		/// <param name="sqlGuid">Any SqlGuid</param>
		/// <returns>Guid</returns>
		public static Guid ToGuid(this SqlGuid sqlGuid)
		{
			var bytes = sqlGuid.ToByteArray();
			return new Guid(
				new[]
				{
					bytes[ToGuidMap[0]],
					bytes[ToGuidMap[1]],
					bytes[ToGuidMap[2]],
					bytes[ToGuidMap[3]],
					bytes[ToGuidMap[4]],
					bytes[ToGuidMap[5]],
					bytes[ToGuidMap[6]],
					bytes[ToGuidMap[7]],
					bytes[ToGuidMap[8]],
					bytes[ToGuidMap[9]],
					bytes[ToGuidMap[10]],
					bytes[ToGuidMap[11]],
					bytes[ToGuidMap[12]],
					bytes[ToGuidMap[13]],
					bytes[ToGuidMap[14]],
					bytes[ToGuidMap[15]]
				}
			);
		}

		/// <summary>
		/// Will take a Guid and will resequence it so that it will sort properly in SQL Server without fragmenting your tables
		/// </summary>
		/// <param name="guid">Any Guid</param>
		/// <returns>SqlGuid</returns>
		public static SqlGuid ToSqlGuid(this Guid guid)
		{
			var bytes = guid.ToByteArray();
			return new SqlGuid(
				new[]
				{
					bytes[ToSqlGuidMap[0]],
					bytes[ToSqlGuidMap[1]],
					bytes[ToSqlGuidMap[2]],
					bytes[ToSqlGuidMap[3]],
					bytes[ToSqlGuidMap[4]],
					bytes[ToSqlGuidMap[5]],
					bytes[ToSqlGuidMap[6]],
					bytes[ToSqlGuidMap[7]],
					bytes[ToSqlGuidMap[8]],
					bytes[ToSqlGuidMap[9]],
					bytes[ToSqlGuidMap[10]],
					bytes[ToSqlGuidMap[11]],
					bytes[ToSqlGuidMap[12]],
					bytes[ToSqlGuidMap[13]],
					bytes[ToSqlGuidMap[14]],
					bytes[ToSqlGuidMap[15]]
				}
			);
		}
	}
}