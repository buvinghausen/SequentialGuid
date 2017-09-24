using System;

namespace SequentialGuid
{
	/// <inheritdoc />
	/// <summary>
	/// </summary>
	public sealed class SequentialGuidGenerator : BaseSequentialGuidGenerator<SequentialGuidGenerator>
	{
		private SequentialGuidGenerator() { }

		/// <summary>
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		internal override Guid NewGuid(long timestamp) =>
			SequentialGuid.NewGuid(timestamp);
	}
}
