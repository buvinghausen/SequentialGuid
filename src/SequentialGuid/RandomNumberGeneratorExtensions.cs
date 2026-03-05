#if NETFRAMEWORK || NETSTANDARD2_0
using System.Security.Cryptography;

namespace SequentialGuid;

static class RandomNumberGeneratorExtensions
{
	// Create matching signature for old RNG class
	extension(RandomNumberGenerator generator)
	{
		internal int GetInt32(int toExclusive)
		{
			// where max is exclusive
			var bytes = new byte[sizeof(int)]; // 4 bytes
			generator.GetNonZeroBytes(bytes);
			return (BitConverter.ToInt32(bytes, 0) % toExclusive + toExclusive) % toExclusive;
		}
	}
}
#endif
