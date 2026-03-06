namespace SequentialGuid;

internal static class ByteArrayExtensions
{
	extension(byte[] b)
	{
#if NETFRAMEWORK || NETSTANDARD
		// Swaps between .NET mixed-endian and RFC 9562 network (big-endian) byte order.
		// Reverses Data1 (4 bytes), Data2 (2 bytes), and Data3 (2 bytes); Data4 is unchanged.
		// This mapping is self-inverse: applying it twice returns the original bytes.
		internal byte[] SwapByteOrder() =>
			[b[3], b[2], b[1], b[0], b[5], b[4], b[7], b[6], b[8], b[9], b[10], b[11], b[12], b[13], b[14], b[15]];
#endif
		//See: https://www.sqlbi.com/blog/alberto/2007/08/31/how-are-guids-sorted-by-sql-server/
		//See: https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/comparing-guid-and-uniqueidentifier-values
		internal byte[] ToSqlByteOrder() =>
			[b[12], b[13], b[14], b[15], b[10], b[11], b[8], b[9], b[7], b[6], b[3], b[2], b[1], b[0], b[5], b[4]];

		internal byte[] FromSqlByteOrder() =>
			[b[13], b[12], b[11], b[10], b[15], b[14], b[9], b[8], b[6], b[7], b[4], b[5], b[0], b[1], b[2], b[3]];

		// bytes[7] is the high byte of Data3 (c) in .NET's little-endian layout;
		// its high nibble is the RFC 9562 version field.
		// UUIDv8 (version 8): a=ts[59:28], b=ts[27:12], c_low12=ts[11:0] — 4-bit shift vs legacy.
		// Legacy: a=ts[63:32], b=ts[31:16], c=ts[15:0] — full 64-bit timestamp, no version nibble.
		// Require BOTH version=8 AND RFC 9562 variant (10xxxxxx in bytes[8]) to avoid
		// false-positives on legacy GUIDs whose timestamp bits accidentally produce a
		// version nibble of 8; the combined probability drops to 1/64 for random data.
		internal bool IsRfc9562V8 =>
			b[7] >> 4 == 8 && (b[8] & 0xC0) == 0x80;

		internal long Rfc9562V8Ticks =>
			((long)b[3] << 52) +
			((long)b[2] << 44) +
			((long)b[1] << 36) +
			((long)b[0] << 28) +
			((long)b[5] << 20) +
			((long)b[4] << 12) +
			(((long)b[7] & 0x0F) << 8) +
			b[6];

		internal long LegacyTicks =>
			((long)b[3] << 56) +
			((long)b[2] << 48) +
			((long)b[1] << 40) +
			((long)b[0] << 32) +
			((long)b[5] << 24) +
			(b[4] << 16) +
			(b[7] << 8) +
			b[6];
	}
}
