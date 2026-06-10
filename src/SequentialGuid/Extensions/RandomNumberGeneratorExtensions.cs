#if !NET6_0_OR_GREATER
using System.Security.Cryptography;

namespace SequentialGuid.Extensions;

static class RandomNumberGeneratorExtensions
{
	// Create matching signature for old RNG class
	extension(RandomNumberGenerator generator)
	{
		internal int GetInt32(int toExclusive)
		{
			// Unbiased mask-and-reject sampling: mask to the smallest power-of-two
			// range covering toExclusive, retry on overshoot. Expected iterations < 2.
			var mask = toExclusive - 1;
			mask |= mask >> 1;
			mask |= mask >> 2;
			mask |= mask >> 4;
			mask |= mask >> 8;
			mask |= mask >> 16;

			var bytes = new byte[sizeof(int)]; // 4 bytes
			int result;
			do
			{
				generator.GetBytes(bytes);
				result = BitConverter.ToInt32(bytes, 0) & mask;
			} while (result >= toExclusive);
			return result;
		}
	}
}
#endif
