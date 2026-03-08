namespace SequentialGuid.Tests;

internal static class GuidExtensions
{
	extension(Guid id)
	{
		internal long ToUnixMs()
		{
#if NET6_0_OR_GREATER
			Span<byte> bytes = stackalloc byte[16];
			id.TryWriteBytes(bytes);
			return bytes.Rfc9562V7UnixMs();
#else
			return id.ToByteArray().Rfc9562V7UnixMs();
#endif
		}
	}
}
