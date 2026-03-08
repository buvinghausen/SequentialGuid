using SequentialGuid;

#pragma warning disable IDE0130 // Namespace does not match folder structure
// ReSharper disable once CheckNamespace
namespace NodaTime;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for <see cref="GuidV8Time"/> that accept NodaTime <see cref="Instant"/> timestamps.
/// </summary>
public static class GuidV8TimeExtensions
{
	extension(GuidV8Time)
	{
		/// <summary>
		/// Creates a new UUID version 8 from a NodaTime <see cref="Instant"/> timestamp.
		/// </summary>
		/// <param name="timestamp">The <see cref="Instant"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 8 <see cref="Guid"/>.</returns>
		public static Guid NewGuid(Instant timestamp) =>
			GuidV8Time.NewGuid(timestamp.ToDateTimeUtc());

		/// <summary>
		/// Creates a new UUID version 8 from a NodaTime <see cref="Instant"/> timestamp, with byte ordering
		/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
		/// </summary>
		/// <param name="timestamp">The <see cref="Instant"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 8 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
		public static Guid NewSqlGuid(Instant timestamp) =>
			GuidV8Time.NewSqlGuid(timestamp.ToDateTimeUtc());

		/// <summary>
		/// Creates a new UUID version 8 from a NodaTime <see cref="OffsetDateTime"/> timestamp.
		/// </summary>
		/// <param name="timestamp">The <see cref="OffsetDateTime"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 8 <see cref="Guid"/>.</returns>
		public static Guid NewGuid(OffsetDateTime timestamp) =>
			GuidV8Time.NewGuid(timestamp.ToInstant());

		/// <summary>
		/// Creates a new UUID version 8 from a NodaTime <see cref="OffsetDateTime"/> timestamp, with byte ordering
		/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
		/// </summary>
		/// <param name="timestamp">The <see cref="OffsetDateTime"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 8 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
		public static Guid NewSqlGuid(OffsetDateTime timestamp) =>
			GuidV8Time.NewSqlGuid(timestamp.ToInstant());

		/// <summary>
		/// Creates a new UUID version 8 from a NodaTime <see cref="ZonedDateTime"/> timestamp.
		/// </summary>
		/// <param name="timestamp">The <see cref="ZonedDateTime"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 8 <see cref="Guid"/>.</returns>
		public static Guid NewGuid(ZonedDateTime timestamp) =>
			GuidV8Time.NewGuid(timestamp.ToDateTimeUtc());

		/// <summary>
		/// Creates a new UUID version 8 from a NodaTime <see cref="ZonedDateTime"/> timestamp, with byte ordering
		/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
		/// </summary>
		/// <param name="timestamp">The <see cref="ZonedDateTime"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 8 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
		public static Guid NewSqlGuid(ZonedDateTime timestamp) =>
			GuidV8Time.NewSqlGuid(timestamp.ToDateTimeUtc());
	}
}
