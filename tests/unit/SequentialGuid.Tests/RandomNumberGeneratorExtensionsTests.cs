#if NETFRAMEWORK
using System.Security.Cryptography;
using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class RandomNumberGeneratorExtensionsTests
{
	[Fact]
	void GetInt32StaysInRange()
	{
		using var rng = RandomNumberGenerator.Create();
		for (var i = 0; i < 10_000; i++)
		{
			var value = rng.GetInt32(500000);
			value.ShouldBeGreaterThanOrEqualTo(0);
			value.ShouldBeLessThan(500000);
		}
	}

	[Fact]
	void GetInt32OfOneReturnsZero()
	{
		using var rng = RandomNumberGenerator.Create();
		rng.GetInt32(1).ShouldBe(0);
	}

	[Fact]
	void GetInt32CanProduceZero()
	{
		// GetNonZeroBytes could never yield 0 from small ranges; the unbiased
		// implementation must cover the full [0, toExclusive) range.
		using var rng = RandomNumberGenerator.Create();
		var sawZero = false;
		for (var i = 0; i < 10_000 && !sawZero; i++)
			sawZero = rng.GetInt32(2) == 0;
		sawZero.ShouldBeTrue();
	}
}
#endif
