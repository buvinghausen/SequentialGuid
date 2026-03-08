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

		// RFC 9562 variant: bits 7-6 of bytes[8] (Data4[0]) must be 10
		internal bool VariantIsRfc9562() =>
			(b[8] & 0xC0) == 0x80;

		// Sets the RFC 9562 version nibble (bits 48-51) in bytes[6]
		internal void SetRfc9562Version(byte version) =>
			b[6] = (byte)((b[6] & 0x0F) | (version << 4));

		// Sets the RFC 9562 variant bits (10xxxxxx) on bytes[8]
		internal void SetRfc9562Variant() =>
			b[8] = (byte)((b[8] & 0x3F) | 0x80);

		// For SQL byte order the variant is the 7th byte
		internal bool SqlVariantIsRfc9562() =>
			(b[6] & 0xC0) == 0x80;

		// The big endian lead byte was always 8 in the legacy calculation
		internal bool IsLegacy() =>
			b[3] == 8 && !b.VariantIsRfc9562();

		// In sql byte order the 11th byte was always 8
		internal bool IsSqlLegacy() =>
			b[10] == 8 && !b.SqlVariantIsRfc9562();

		// version is in the high nibble of bytes[7] (Data3 high byte, little-endian)
		internal bool IsRfc9562Version(byte version) =>
			b[7] >> 4 == version && b.VariantIsRfc9562();

		internal long Rfc9562V7UnixMs =>
			((long)b[3] << 40) |
			((long)b[2] << 32) |
			((long)b[1] << 24) |
			((long)b[0] << 16) |
			((long)b[5] << 8) |
			b[4];

		private long Rfc9562V8Ticks =>
			((long)b[3] << 52) +
			((long)b[2] << 44) +
			((long)b[1] << 36) +
			((long)b[0] << 28) +
			((long)b[5] << 20) +
			((long)b[4] << 12) +
			(((long)b[7] & 0x0F) << 8) +
			b[6];

		private long LegacyTicks =>
			((long)b[3] << 56) +
			((long)b[2] << 48) +
			((long)b[1] << 40) +
			((long)b[0] << 32) +
			((long)b[5] << 24) +
			(b[4] << 16) +
			(b[7] << 8) +
			b[6];

		internal long? ToTicks() =>
			b.IsRfc9562Version(8) ? b.Rfc9562V8Ticks :
			b.IsRfc9562Version(7) ? b.Rfc9562V7UnixMs.Rfc9562V7Ticks :
			b.IsLegacy() ? b.LegacyTicks :
			null;
	}

#if !NETFRAMEWORK && !NETSTANDARD
	extension(ReadOnlySpan<byte> b)
	{
		internal bool VariantIsRfc9562() =>
			(b[8] & 0xC0) == 0x80;

		internal bool IsLegacy() =>
			b[3] == 8 && !b.VariantIsRfc9562();

		internal bool IsRfc9562Version(byte version) =>
			b[7] >> 4 == version && b.VariantIsRfc9562();

		internal long Rfc9562V7UnixMs =>
			((long)b[3] << 40) |
			((long)b[2] << 32) |
			((long)b[1] << 24) |
			((long)b[0] << 16) |
			((long)b[5] << 8) |
			b[4];

		private long Rfc9562V8Ticks =>
			((long)b[3] << 52) +
			((long)b[2] << 44) +
			((long)b[1] << 36) +
			((long)b[0] << 28) +
			((long)b[5] << 20) +
			((long)b[4] << 12) +
			(((long)b[7] & 0x0F) << 8) +
			b[6];

		private long LegacyTicks =>
			((long)b[3] << 56) +
			((long)b[2] << 48) +
			((long)b[1] << 40) +
			((long)b[0] << 32) +
			((long)b[5] << 24) +
			(b[4] << 16) +
			(b[7] << 8) +
			b[6];

		internal long? ToTicks() =>
			b.IsRfc9562Version(8) ? b.Rfc9562V8Ticks :
			b.IsRfc9562Version(7) ? b.Rfc9562V7UnixMs.Rfc9562V7Ticks :
			b.IsLegacy() ? b.LegacyTicks :
			null;

		internal void WriteToSqlByteOrder(Span<byte> dest)
		{
			dest[0] = b[12];
			dest[1] = b[13];
			dest[2] = b[14];
			dest[3] = b[15];
			dest[4] = b[10];
			dest[5] = b[11];
			dest[6] = b[8];
			dest[7] = b[9];
			dest[8] = b[7];
			dest[9] = b[6];
			dest[10] = b[3];
			dest[11] = b[2];
			dest[12] = b[1];
			dest[13] = b[0];
			dest[14] = b[5];
			dest[15] = b[4];
		}

		internal void WriteFromSqlByteOrder(Span<byte> dest)
		{
			dest[0] = b[13];
			dest[1] = b[12];
			dest[2] = b[11];
			dest[3] = b[10];
			dest[4] = b[15];
			dest[5] = b[14];
			dest[6] = b[9];
			dest[7] = b[8];
			dest[8] = b[6];
			dest[9] = b[7];
			dest[10] = b[4];
			dest[11] = b[5];
			dest[12] = b[0];
			dest[13] = b[1];
			dest[14] = b[2];
			dest[15] = b[3];
		}
	}
#endif
}
