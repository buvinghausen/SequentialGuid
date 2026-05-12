namespace SequentialGuid.Tests;

public sealed class GuidExtensionsTests
{
	[Fact]
	void MaxValueIsAllBitsSet()
	{
		Guid.MaxValue.ShouldBe(new("ffffffff-ffff-ffff-ffff-ffffffffffff"));
	}

	[Fact]
	void MaxValueIsNotEmpty()
	{
		Guid.MaxValue.ShouldNotBe(Guid.Empty);
	}
}
