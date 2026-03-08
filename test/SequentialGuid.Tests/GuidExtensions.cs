namespace SequentialGuid.Tests;

internal static class GuidExtensions
{
	extension(Guid id)
	{
		internal long ToUnixMs() =>
			id.ToByteArray().Rfc9562V7UnixMs();
	}
}
