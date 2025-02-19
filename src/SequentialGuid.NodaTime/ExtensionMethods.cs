using System.Data.SqlTypes;
using SequentialGuid;

// ReSharper disable once CheckNamespace
namespace NodaTime;

/// <summary>
/// Provides extension methods for working with NodaTime's <see cref="Instant"/> and sequential GUID generators.
/// </summary>
/// <remarks>
/// This static class contains helper methods to facilitate the creation of sequential GUIDs and 
/// conversions between <see cref="Instant"/> and GUID-related types.
/// </remarks>
public static class ExtensionMethods
{
	/// <summary>
	/// Generates a new sequential <see cref="Guid"/> based on the specified <see cref="Instant"/> timestamp.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="SequentialGuidGenerator"/> instance used to generate the sequential <see cref="Guid"/>.
	/// </param>
	/// <param name="timestamp">
	/// The <see cref="Instant"/> representing the point in time to base the GUID generation on.
	/// </param>
	/// <returns>
	/// A new sequential <see cref="Guid"/> generated using the provided <paramref name="timestamp"/>.
	/// </returns>
	/// <remarks>
	/// This method converts the provided <see cref="Instant"/> to a UTC <see cref="DateTime"/> and uses it
	/// to generate a sequential <see cref="Guid"/>. Sequential GUIDs are particularly useful for database
	/// indexing and reducing fragmentation.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown if the <paramref name="timestamp"/> is outside the valid range for GUID generation.
	/// </exception>
	public static Guid NewGuid(this SequentialGuidGenerator generator, Instant timestamp) =>
		generator.NewGuid(timestamp.ToDateTimeUtc());
	
	/// <summary>
	/// Generates a new sequential <see cref="Guid"/> based on the provided <see cref="Instant"/> timestamp.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="SequentialSqlGuidGenerator"/> instance used to generate the sequential GUID.
	/// </param>
	/// <param name="timestamp">
	/// The <see cref="Instant"/> representing the timestamp to base the GUID generation on.
	/// </param>
	/// <returns>
	/// A new sequential <see cref="Guid"/> generated using the specified timestamp.
	/// </returns>
	/// <remarks>
	/// This method converts the provided <see cref="Instant"/> to a UTC <see cref="DateTime"/> and uses it
	/// to generate a sequential GUID. Sequential GUIDs are particularly useful in scenarios where maintaining
	/// index performance in databases is critical.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown if the <paramref name="timestamp"/> is outside the valid range for GUID generation.
	/// </exception>
	public static Guid NewGuid(this SequentialSqlGuidGenerator generator, Instant timestamp) =>
		generator.NewGuid(timestamp.ToDateTimeUtc());
	
	/// <summary>
	/// Generates a new sequential <see cref="SqlGuid"/> based on the specified <see cref="Instant"/> timestamp.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="SequentialSqlGuidGenerator"/> instance used to create the sequential <see cref="SqlGuid"/>.
	/// </param>
	/// <param name="timestamp">
	/// The <see cref="Instant"/> representing the point in time to base the sequential <see cref="SqlGuid"/> on.
	/// </param>
	/// <returns>
	/// A new sequential <see cref="SqlGuid"/> corresponding to the provided <see cref="Instant"/>.
	/// </returns>
	/// <remarks>
	/// This method facilitates the creation of sequential <see cref="SqlGuid"/> values, which are particularly
	/// useful in database scenarios where maintaining index performance is critical. The <paramref name="timestamp"/>
	/// is converted to a <see cref="DateTime"/> in UTC before generating the <see cref="SqlGuid"/>.
	/// </remarks>
	public static SqlGuid NewSqlGuid(this SequentialSqlGuidGenerator generator, Instant timestamp) =>
		generator.NewSqlGuid(timestamp.ToDateTimeUtc());

	/// <summary>
	/// Converts a <see cref="Guid"/> to a <see cref="NodaTime.Instant"/> if the <see cref="Guid"/> contains a valid timestamp.
	/// </summary>
	/// <param name="id">The <see cref="Guid"/> to extract the timestamp from.</param>
	/// <returns>
	/// An <see cref="NodaTime.Instant"/> representing the timestamp embedded in the <see cref="Guid"/>, 
	/// or <c>null</c> if the <see cref="Guid"/> does not contain a valid timestamp.
	/// </returns>
	public static Instant? ToInstant(this Guid id) =>
		id.ToDateTime().ToInstant();

	/// <summary>
	/// Converts a <see cref="SqlGuid"/> to a nullable <see cref="NodaTime.Instant"/>.
	/// </summary>
	/// <param name="sqlGuid">The <see cref="SqlGuid"/> to convert.</param>
	/// <returns>
	/// A nullable <see cref="NodaTime.Instant"/> representing the timestamp embedded in the <see cref="SqlGuid"/>, 
	/// or <c>null</c> if the <see cref="SqlGuid"/> does not contain a valid timestamp.
	/// </returns>
	/// <remarks>
	/// This method extracts the timestamp from the provided <see cref="SqlGuid"/> and converts it to a 
	/// <see cref="NodaTime.Instant"/>. If the <see cref="SqlGuid"/> does not contain a valid timestamp, 
	/// the method returns <c>null</c>.
	/// </remarks>
	public static Instant? ToInstant(this SqlGuid sqlGuid) =>
		sqlGuid.ToDateTime().ToInstant();

	// Helper function to prevent code duplication all it does is conditionally convert a nullable datetime to a nullable instant
	private static Instant? ToInstant(this DateTime? value) =>
		value.HasValue ? Instant.FromDateTimeUtc(value.Value) : null;
}
