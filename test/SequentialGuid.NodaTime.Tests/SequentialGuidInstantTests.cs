using NodaTime;

namespace SequentialGuid.NodaTime.Tests;

public sealed class SequentialGuidInstantTests
{
	[Fact]
	void TestInstantToGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var id = SequentialGuidGenerator.Instance.NewGuid(now);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(now);
	}

	[Fact]
	void TestInstantToGuidRoundTripSqlSorting()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var id = SequentialSqlGuidGenerator.Instance.NewGuid(now);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(now);
	}

	[Fact]
	void TestInstantToSqlGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var id = SequentialSqlGuidGenerator.Instance.NewSqlGuid(now);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(now);
	}
}
