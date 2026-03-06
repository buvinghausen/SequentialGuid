namespace SequentialGuid;

internal static class ByteArrayExtensions
{
	extension(byte[] b)
	{
		// Swaps between .NET mixed-endian and RFC 9562 network (big-endian) byte order.
		// Reverses Data1 (4 bytes), Data2 (2 bytes), and Data3 (2 bytes); Data4 is unchanged.
		// This mapping is self-inverse: applying it twice returns the original bytes.
		internal byte[] SwapByteOrder() =>
			[b[3], b[2], b[1], b[0], b[5], b[4], b[7], b[6], b[8], b[9], b[10], b[11], b[12], b[13], b[14], b[15]];
	}
}
