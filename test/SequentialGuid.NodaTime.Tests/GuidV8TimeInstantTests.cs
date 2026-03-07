using NodaTime;

namespace SequentialGuid.NodaTime.Tests;

public sealed class GuidV8TimeInstantTests
{
	[Fact]
	void TestInstantToGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var id = GuidV8Time.NewGuid(now);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(now);
	}

	[Fact]
	void TestInstantToSqlGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var id = GuidV8Time.NewSqlGuid(now);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(now);
	}
}
