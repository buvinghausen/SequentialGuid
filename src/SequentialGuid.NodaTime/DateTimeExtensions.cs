using NodaTime;

namespace SequentialGuid.NodaTime;

/// <summary>
/// Provides internal extension methods for converting nullable <see cref="DateTime"/> values to NodaTime <see cref="Instant"/> values.
/// </summary>
static class DateTimeExtensions
{
	extension(DateTime? value)
	{
		/// <summary>
		/// Converts a nullable UTC <see cref="DateTime"/> to a nullable NodaTime <see cref="Instant"/>.
		/// </summary>
		/// <returns>An <see cref="Instant"/> equivalent to the UTC <see cref="DateTime"/>, or <see langword="null"/> if the value is <see langword="null"/>.</returns>
		internal Instant? ToInstant() =>
			value.HasValue ? Instant.FromDateTimeUtc(value.Value) : null;
	}
}
