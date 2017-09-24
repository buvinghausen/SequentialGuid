using System;
using System.Threading;

namespace SequentialGuid
{
	public abstract class BaseSequentialGuidGenerator<T> where T :
		BaseSequentialGuidGenerator<T>
	{
		private static readonly ThreadLocal<T> Lazy =
			new ThreadLocal<T>(() =>
				Activator.CreateInstance(typeof(T), true) as T);

		/// <summary>
		/// 
		/// </summary>
		public static T Instance => Lazy.Value;

		/// <summary>
		/// </summary>
		/// <returns></returns>
		public Guid NewGuid() => NewGuid(DateTime.UtcNow);

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
