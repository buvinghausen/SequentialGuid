using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

static class GuidExtensions
{
	extension(Guid id)
	{
		internal long ToUnixMs() =>
			id.ToByteArray().Rfc9562V7UnixMs();
	}

	extension(DateTime dt)
	{
		internal DateTime TruncateToMs() =>
			new(dt.Ticks - dt.Ticks % TimeSpan.TicksPerMillisecond, dt.Kind);
	}
}
