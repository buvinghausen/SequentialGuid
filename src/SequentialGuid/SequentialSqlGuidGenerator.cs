using System;

namespace SequentialGuid
{
	/// <inheritdoc />
	/// <summary>
	/// </summary>
	public sealed class SequentialSqlGuidGenerator : ISequentialGuidGenerator
	{
		/// <summary>
		/// 
		/// </summary>
		private static readonly Lazy<SequentialSqlGuidGenerator> Lazy = new Lazy<SequentialSqlGuidGenerator>(() => new SequentialSqlGuidGenerator());

		/// <summary>
		/// 
		/// </summary>
		private SequentialSqlGuidGenerator() { }

		/// <summary>
		/// 
		/// </summary>
		public static ISequentialGuidGenerator Instance => Lazy.Value;

		/// <inheritdoc />
		/// <summary>
		/// </summary>
		/// <returns></returns>
		public Guid NewGuid() => NewGuid(DateTime.UtcNow);

		/// <inheritdoc />
		/// <summary>
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		public Guid NewGuid(DateTime timestamp) => NewGuid(timestamp.Ticks);

		/// <inheritdoc />
		/// <summary>
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		public Guid NewGuid(long timestamp) => SequentialGuid.NewGuid(timestamp).ToSqlGuid().Value;
	}
}
