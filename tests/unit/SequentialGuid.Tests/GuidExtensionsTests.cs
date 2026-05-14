namespace SequentialGuid.Tests;

public sealed class GuidExtensionsTests
{
	[Fact]
	void MaxValueIsAllBitsSet() =>
		Guid.MaxValue.ShouldBe(new("ffffffff-ffff-ffff-ffff-ffffffffffff"));

	[Fact]
	void MaxValueIsNotEmpty() =>
		Guid.MaxValue.ShouldNotBe(Guid.Empty);

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
		timestamp.ShouldBe(default);
	}

	[Fact]
	void TryToDateTimeReturnsFalseForEmpty()
	{
		var success = Guid.Empty.TryToDateTime(out var timestamp);
		success.ShouldBeFalse();
		timestamp.ShouldBe(default);
	}

	[Fact]
	void ToDateTimeOffsetReturnsUtcOffsetForSequentialGuid()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		// Act
		var dto = v7.ToDateTimeOffset();
		// Assert
		dto.ShouldNotBeNull();
		dto.Value.Offset.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	void ToDateTimeOffsetReturnsNullForRandomV4()
	{
		var v4 = new Guid("919108f7-52d1-4320-9bac-f847db4148a8");
		v4.ToDateTimeOffset().ShouldBeNull();
	}

	[Fact]
	void TryToDateTimeOffsetReturnsTrueForSequentialGuid()
	{
		var v7 = GuidV7.NewGuid();
		var success = v7.TryToDateTimeOffset(out var dto);
		success.ShouldBeTrue();
		dto.Offset.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	void TryToDateTimeOffsetReturnsFalseForRandomV4()
	{
		var v4 = new Guid("919108f7-52d1-4320-9bac-f847db4148a8");
		var success = v4.TryToDateTimeOffset(out var dto);
		success.ShouldBeFalse();
		dto.ShouldBe(default);
	}

	[Fact]
	void IsSequentialGuidTrueForV7() =>
		GuidV7.NewGuid().IsSequentialGuid().ShouldBeTrue();

	[Fact]
	void IsSequentialGuidTrueForV8Time() =>
		GuidV8Time.NewGuid().IsSequentialGuid().ShouldBeTrue();

	[Fact]
	void IsSequentialGuidTrueForLegacy() =>
		new Guid("08de7bf5-381d-cc8b-f24c-56e3580439dd").IsSequentialGuid().ShouldBeTrue();

	[Fact]
	void IsSequentialGuidTrueForSqlOrderedV7() =>
		GuidV7.NewSqlGuid().IsSequentialGuid().ShouldBeTrue();

	// RFC 9562 §A.4 v4 vector
	[Fact]
	void IsSequentialGuidFalseForRandomV4() =>
		new Guid("919108f7-52d1-4320-9bac-f847db4148a8").IsSequentialGuid().ShouldBeFalse();

	[Fact]
	void IsSequentialGuidFalseForEmpty() =>
		Guid.Empty.IsSequentialGuid().ShouldBeFalse();

	[Fact]
	void IsSequentialGuidFalseForMaxValue() =>
		Guid.MaxValue.IsSequentialGuid().ShouldBeFalse();
}
