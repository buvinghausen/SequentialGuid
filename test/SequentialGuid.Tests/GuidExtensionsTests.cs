using SequentialGuid;

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

	[Fact]
	void TryToDateTimeReturnsTrueForSequentialGuid()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		// Act
		var success = v7.TryToDateTime(out var timestamp);
		// Assert
		success.ShouldBeTrue();
		timestamp.ShouldBe(v7.ToDateTime()!.Value);
	}

	[Fact]
	void TryToDateTimeReturnsFalseForRandomV4()
	{
		// RFC 9562 §A.4 v4 vector — known not a sequential guid
		var v4 = new Guid("919108f7-52d1-4320-9bac-f847db4148a8");
		var success = v4.TryToDateTime(out var timestamp);
		success.ShouldBeFalse();
		timestamp.ShouldBe(default(DateTime));
	}

	[Fact]
	void TryToDateTimeReturnsFalseForEmpty()
	{
		var success = Guid.Empty.TryToDateTime(out var timestamp);
		success.ShouldBeFalse();
		timestamp.ShouldBe(default(DateTime));
	}
}
