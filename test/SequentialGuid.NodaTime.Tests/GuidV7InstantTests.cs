using NodaTime;

namespace SequentialGuid.NodaTime.Tests;

public sealed class GuidV7InstantTests
{
	// GuidV7 stores only millisecond precision, so we truncate before comparing.
	static Instant Truncate(Instant instant) =>
		Instant.FromUnixTimeMilliseconds(instant.ToUnixTimeMilliseconds());

	[Fact]
	void TestInstantToGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var id = GuidV7.NewGuid(now);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(Truncate(now));
	}

	[Fact]
	void TestInstantToSqlGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var id = GuidV7.NewSqlGuid(now);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(Truncate(now));
	}

	[Fact]
	void TestOffsetDateTimeToGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var offsetDateTime = now.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault()).ToOffsetDateTime();
		var id = GuidV7.NewGuid(offsetDateTime);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(Truncate(now));
	}

	[Fact]
	void TestOffsetDateTimeToSqlGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var offsetDateTime = now.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault()).ToOffsetDateTime();
		var id = GuidV7.NewSqlGuid(offsetDateTime);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(Truncate(now));
	}

	[Fact]
	void TestZonedDateTimeToGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var zonedDateTime = now.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());
		var id = GuidV7.NewGuid(zonedDateTime);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(Truncate(now));
	}

	[Fact]
	void TestZonedDateTimeToSqlGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var zonedDateTime = now.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault());
		var id = GuidV7.NewSqlGuid(zonedDateTime);
		var instant = id.ToInstant();
		instant.HasValue.ShouldBeTrue();
		instant.ShouldBe(Truncate(now));
	}
}
