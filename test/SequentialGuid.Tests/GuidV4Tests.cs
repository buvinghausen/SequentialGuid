namespace SequentialGuid.Tests;

public sealed class GuidV4Tests
{
	[Fact]
	void TestVersion4Bits()
	{
		// Act
		var id = GuidV4.NewGuid();
		var bytes = id.ToByteArray();
#if NET9_0_OR_GREATER
		id.Version.ShouldBe(4);
#endif
		bytes.IsRfc9562Version(4).ShouldBeTrue();
	}

	[Fact]
	void TestVariantBits()
	{
		// Act
		var id = GuidV4.NewGuid();
		var bytes = id.ToByteArray();
#if NET9_0_OR_GREATER
		id.Variant.ShouldBeInRange(8, 11);
#endif
		bytes.VariantIsRfc9562().ShouldBeTrue();
	}

	[Fact]
	void TestUniqueness()
	{
		// Act
		var first = GuidV4.NewGuid();
		var second = GuidV4.NewGuid();
		// Assert
		first.ShouldNotBe(second);
	}

	[Fact]
	void TestNonDeterministic()
	{
		// Two calls with no input should never produce the same value
		var results = Enumerable.Range(0, 100).Select(_ => GuidV4.NewGuid()).ToHashSet();
		// Assert
		results.Count.ShouldBe(100);
	}
}
