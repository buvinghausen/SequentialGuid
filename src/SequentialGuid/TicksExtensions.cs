namespace SequentialGuid;

/// <summary>
/// Provides internal extension methods for working with <see cref="long"/> tick values,
/// including conversions to <see cref="DateTime"/> and timestamp validation.
/// </summary>
static class TicksExtensions
{
	private const long UnixEpochTicks = 621355968000000000L;

	extension(long value)
	{
		internal DateTime ToDateTime() =>
			new(value, DateTimeKind.Utc);

		internal bool IsDateTime =>
			value >= UnixEpochTicks &&
			value <= DateTime.UtcNow.Ticks;

		internal long Rfc9562V7Ticks =>
			UnixEpochTicks + value * TimeSpan.TicksPerMillisecond;
	}
}
