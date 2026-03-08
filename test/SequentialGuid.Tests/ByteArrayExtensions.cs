namespace SequentialGuid.Tests;

internal static class ByteArrayExtensions
{
	extension(byte[] b)
	{
		// For SQL byte order the variant is the 7th byte
		internal bool SqlVariantIsRfc9562() =>
			(b[6] & 0xC0) == 0x80;

		// In sql byte order the 11th byte was always 8
		internal bool IsSqlLegacy() =>
			b[10] == 8 && !b.SqlVariantIsRfc9562();
	}
}
