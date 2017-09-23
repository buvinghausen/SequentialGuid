using System;

namespace Buvinghausen.SequentialGuid
{
	/// <inheritdoc />
	/// <summary>
	/// </summary>
	public sealed class SequentialGuidGenerator : ISequentialGuidGenerator
	{
		/// <summary>
		/// 
		/// </summary>
		private static readonly Lazy<SequentialGuidGenerator> Lazy = new Lazy<SequentialGuidGenerator>(() => new SequentialGuidGenerator());

		/// <summary>
		/// 
		/// </summary>
		private SequentialGuidGenerator() { }

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
		public Guid NewGuid(long timestamp) => SequentialGuid.NewGuid(timestamp);
	}
}
