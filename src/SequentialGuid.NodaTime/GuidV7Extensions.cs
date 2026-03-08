using SequentialGuid;

#pragma warning disable IDE0130 // Namespace does not match folder structure
// ReSharper disable once CheckNamespace
namespace NodaTime;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for <see cref="GuidV7"/> that accept NodaTime <see cref="Instant"/> timestamps.
/// </summary>
public static class GuidV7TimeExtensions
{
	extension(GuidV7)
	{
		/// <summary>
		/// Creates a new UUID version 7 from a NodaTime <see cref="Instant"/> timestamp.
		/// </summary>
		/// <param name="timestamp">The <see cref="Instant"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 7 <see cref="Guid"/>.</returns>
		public static Guid NewGuid(Instant timestamp) =>
			GuidV7.NewGuid(timestamp.ToDateTimeOffset());

		/// <summary>
		/// Creates a new UUID version 7 from a NodaTime <see cref="Instant"/> timestamp, with byte ordering
		/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
		/// </summary>
		/// <param name="timestamp">The <see cref="Instant"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
		public static Guid NewSqlGuid(Instant timestamp) =>
			GuidV7.NewSqlGuid(timestamp.ToDateTimeOffset());

		/// <summary>
		/// Creates a new UUID version 7 from a NodaTime <see cref="OffsetDateTime"/> timestamp.
		/// </summary>
		/// <param name="timestamp">The <see cref="OffsetDateTime"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 7 <see cref="Guid"/>.</returns>
		public static Guid NewGuid(OffsetDateTime timestamp) =>
			GuidV7.NewGuid(timestamp.ToDateTimeOffset());

		/// <summary>
		/// Creates a new UUID version 7 from a NodaTime <see cref="OffsetDateTime"/> timestamp, with byte ordering
		/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
		/// </summary>
		/// <param name="timestamp">The <see cref="OffsetDateTime"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
		public static Guid NewSqlGuid(OffsetDateTime timestamp) =>
			GuidV7.NewSqlGuid(timestamp.ToDateTimeOffset());

		/// <summary>
		/// Creates a new UUID version 7 from a NodaTime <see cref="ZonedDateTime"/> timestamp.
		/// </summary>
		/// <param name="timestamp">The <see cref="ZonedDateTime"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 7 <see cref="Guid"/>.</returns>
		public static Guid NewGuid(ZonedDateTime timestamp) =>
			GuidV7.NewGuid(timestamp.ToDateTimeOffset());

		/// <summary>
		/// Creates a new UUID version 7 from a NodaTime <see cref="ZonedDateTime"/> timestamp, with byte ordering
		/// suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
		/// </summary>
		/// <param name="timestamp">The <see cref="ZonedDateTime"/> to embed as the timestamp.</param>
		/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
		public static Guid NewSqlGuid(ZonedDateTime timestamp) =>
			GuidV7.NewSqlGuid(timestamp.ToDateTimeOffset());
	}
}
