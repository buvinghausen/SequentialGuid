using System;

namespace SequentialGuid
{
	public abstract class BaseSequentialGuidGenerator<T> where T : BaseSequentialGuidGenerator<T>
	{
		private static readonly Lazy<T> Lazy =
			new Lazy<T>(() => Activator.CreateInstance(typeof(T), true) as T);

		/// <summary>
		/// Singleton instance of the generator
		/// </summary>
		public static T Instance =>
			Lazy.Value;

		/// <summary>
		/// </summary>
		/// <returns>Guid</returns>
		public Guid NewGuid() =>
			NewGuid(DateTime.UtcNow.Ticks);

		/// <summary>
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		public Guid NewGuid(DateTime timestamp)
		{
			long ticks;
			switch (timestamp.Kind)
			{
				case DateTimeKind.Utc: // use ticks as is
					ticks = timestamp.Ticks;
					break;
				case DateTimeKind.Local: // convert to UTC
					ticks = timestamp.ToUniversalTime().Ticks;
					break;
				default:
					// unspecified time throw exception
					throw new ArgumentException("DateTimeKind.Unspecified not supported", nameof(timestamp));
			}

			// run validation after tick conversion
			if (!ticks.IsDateTime())
				throw new ArgumentException("Timestamp must be between January 1st, 1970 UTC and now",
					nameof(timestamp));

			// perform computation on abstract method in child class
			return NewGuid(ticks);
		}

		internal abstract Guid NewGuid(long timestamp);
	}
}
