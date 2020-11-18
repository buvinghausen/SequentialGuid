using System;
using System.Data.SqlTypes;
using NodaTime;

namespace SequentialGuid.NodaTime
{
	/// <summary>
	///     Helper functions that utilize NodaTime's Instant struct rather than DateTime
	/// </summary>
	public static class ExtensionMethods
	{
		/// <summary>
		///     Will return the value of SystemClock.Instance.GetCurrentInstant() at the time of the generation of the Guid will
		///     keep you from needing to store separate audit fields
		/// </summary>
		/// <param name="guid">A sequential Guid with the first 8 bytes containing the system ticks at time of generation</param>
		/// <returns>Instant?</returns>
		public static Instant? ToInstant(this Guid guid)
		{
			return guid.ToDateTime().ToInstant();
		}

		/// <summary>
		///     Will return the value of SystemClock.Instance.GetCurrentInstant() at the time of the generation of the Guid will
		///     keep you from needing to store separate audit fields
		/// </summary>
		/// <param name="sqlGuid">
		///     A sequential SqlGuid with the first sorted 8 bytes containing the system ticks at time of
		///     generation
		/// </param>
		/// <returns>Instant?</returns>
		public static Instant? ToInstant(this SqlGuid sqlGuid)
		{
			return sqlGuid.ToDateTime().ToInstant();
		}

		private static Instant? ToInstant(this DateTime? value)
		{
			return value.HasValue ? Instant.FromDateTimeUtc(value.Value) : default(Instant?);
		}
	}
}
