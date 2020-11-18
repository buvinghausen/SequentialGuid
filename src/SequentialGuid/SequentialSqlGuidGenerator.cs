using System;
using System.Data.SqlTypes;

namespace SequentialGuid
{
	/// <summary>
	///     <para>Generate guid values that will sort sequentially over time in a SQL Server index</para>
	///     <para>Supports SQL Server endianness</para>
	/// </summary>
	public sealed class SequentialSqlGuidGenerator : SequentialGuidGeneratorBase<SequentialSqlGuidGenerator>
	{
		private SequentialSqlGuidGenerator() { }

		internal override Guid NewGuid(long timestamp)
		{
			return base.NewGuid(timestamp).ToSqlGuid().Value;
		}

		/// <summary>
		///     Returns a guid for the value of UtcNow
		/// </summary>
		/// <returns>Sequential SQL guid</returns>
		public SqlGuid NewSqlGuid()
		{
			return new(NewGuid());
		}

		/// <summary>
		///     Takes a date time parameter to encode in a sequential SQL guid
		/// </summary>
		/// <param name="timestamp">
		///     Timestamp that must not be in unspecified kind and must be between the unix epoch and now to be
		///     considered valid
		/// </param>
		/// <returns>Sequential SQL guid</returns>
		public SqlGuid NewSqlGuid(DateTime timestamp)
		{
			return new(NewGuid(timestamp));
		}
	}
}
