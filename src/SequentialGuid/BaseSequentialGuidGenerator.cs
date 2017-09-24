using System;

namespace SequentialGuid
{
	public abstract class BaseSequentialGuidGenerator<T> : ISequentialGuidGenerator
		where T : BaseSequentialGuidGenerator<T>, new()
	{
		private static readonly Lazy<ISequentialGuidGenerator> Lazy =
			new Lazy<ISequentialGuidGenerator>(() => new T());

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
		public Guid NewGuid(DateTime timestamp) => NewGuid(
			timestamp.Kind == DateTimeKind.Local
				? timestamp.ToUniversalTime().Ticks
				: timestamp.Ticks);

		protected abstract Guid NewGuid(long timestamp);
	}
}
