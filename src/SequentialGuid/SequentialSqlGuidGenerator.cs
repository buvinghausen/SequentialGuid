using System;

namespace SequentialGuid
{
	/// <inheritdoc />
	/// <summary>
	/// </summary>
	public sealed class SequentialSqlGuidGenerator : BaseSequentialGuidGenerator<SequentialSqlGuidGenerator>
	{
		/// <summary>
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		protected override Guid NewGuid(long timestamp) =>
			SequentialGuid.NewGuid(timestamp).ToSqlGuid().Value;
	}
}
