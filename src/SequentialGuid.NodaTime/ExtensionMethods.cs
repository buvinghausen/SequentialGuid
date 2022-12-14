using System.Data.SqlTypes;
using SequentialGuid;

// ReSharper disable once CheckNamespace
namespace NodaTime;

/// <summary>
///     Helper functions that utilize NodaTime's Instant struct rather than DateTime
/// </summary>
public static class ExtensionMethods
{
	/// <summary>
	/// Takes an instance of NodaTime's Instant struct and returns back a sequential guid
	/// </summary>
	/// <param name="generator">Extension parameter the singleton instance of the generator</param>
	/// <param name="timestamp">Time value in UTC between the Unix epoch and now</param>
	/// <returns>sequential guid</returns>
	public static Guid NewGuid(this SequentialGuidGenerator generator, Instant timestamp) =>
		generator.NewGuid(timestamp.ToDateTimeUtc());

	/// <summary>
	/// Takes an instance of NodaTime's Instant struct and returns back a sequential guid sorted for SQL Server
	/// </summary>
	/// <param name="generator">Extension parameter the singleton instance of the generator</param>
	/// <param name="timestamp">Time value in UTC between the Unix epoch and now</param>
	/// <returns>sequential guid for SQL Server</returns>
	public static Guid NewGuid(this SequentialSqlGuidGenerator generator, Instant timestamp) =>
		generator.NewGuid(timestamp.ToDateTimeUtc());

	/// <summary>
	/// Takes an instance of NodaTime's Instant struct and returns back a sequential SQL guid
	/// </summary>
	/// <param name="generator">Extension parameter the singleton instance of the generator</param>
	/// <param name="timestamp">Time value in UTC between the Unix epoch and now</param>
	/// <returns>sequential SQL guid</returns>
	public static SqlGuid NewSqlGuid(this SequentialSqlGuidGenerator generator, Instant timestamp) =>
		generator.NewSqlGuid(timestamp.ToDateTimeUtc());

	/// <summary>
	///     Will return the value of SystemClock.Instance.GetCurrentInstant() at the time of the generation of the Guid will
	///     keep you from needing to store separate audit fields
	/// </summary>
	/// <param name="id">A sequential Guid with the first 8 bytes containing the system ticks at time of generation</param>
	/// <returns>Instant?</returns>
	public static Instant? ToInstant(this Guid id) =>
		id.ToDateTime().ToInstant();

	/// <summary>
	///     Will return the value of SystemClock.Instance.GetCurrentInstant() at the time of the generation of the Guid will
	///     keep you from needing to store separate audit fields
	/// </summary>
	/// <param name="sqlGuid">
	///     A sequential SqlGuid with the first sorted 8 bytes containing the system ticks at time of
	///     generation
	/// </param>
	/// <returns>Instant?</returns>
	public static Instant? ToInstant(this SqlGuid sqlGuid) =>
		sqlGuid.ToDateTime().ToInstant();

	// Helper function to prevent code duplication all it does is conditionally convert a nullable datetime to a nullable instant
	private static Instant? ToInstant(this DateTime? value) =>
		value.HasValue ? Instant.FromDateTimeUtc(value.Value) : default(Instant?);
}
