using System;

namespace SequentialGuid
{
	/// <inheritdoc />
	/// <summary>
	/// </summary>
	public sealed class SequentialSqlGuidGenerator : BaseSequentialGuidGenerator<SequentialSqlGuidGenerator>
	{
		private SequentialSqlGuidGenerator() { }

		/// <summary>
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		internal override Guid NewGuid(long timestamp) =>
			SequentialGuid.NewGuid(timestamp).ToSqlGuid().Value;
	}
}
