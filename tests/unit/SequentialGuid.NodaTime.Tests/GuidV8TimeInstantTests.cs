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

	[Fact]
	void TestOffsetDateTimeToGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var offsetDateTime = now.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault()).ToOffsetDateTime();
		var id = GuidV8Time.NewGuid(offsetDateTime);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(now);
	}

	[Fact]
	void TestOffsetDateTimeToSqlGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var offsetDateTime = now.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault()).ToOffsetDateTime();
		var id = GuidV8Time.NewSqlGuid(offsetDateTime);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(now);
	}

	[Fact]
	void TestZonedDateTimeToGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var zonedDateTime = now.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());
		var id = GuidV8Time.NewGuid(zonedDateTime);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(now);
	}

	[Fact]
	void TestZonedDateTimeToSqlGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var zonedDateTime = now.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());
		var id = GuidV8Time.NewSqlGuid(zonedDateTime);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(now);
	}
}
