namespace SequentialGuid;

/// <summary>
/// Provides internal extension methods for working with <see cref="long"/> tick values,
/// including conversions to <see cref="DateTime"/> and timestamp validation.
/// </summary>
static class TicksExtensions
{
	private const long UnixEpochTicks = 621355968000000000L;

	extension(long ticks)
	{
		internal DateTime ToDateTime() =>
			new(ticks, DateTimeKind.Utc);

		internal bool IsDateTime() =>
			ticks >= UnixEpochTicks &&
			ticks <= DateTime.UtcNow.Ticks;
	}
}
