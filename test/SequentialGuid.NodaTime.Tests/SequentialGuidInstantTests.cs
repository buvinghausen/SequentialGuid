using NodaTime;
using Xunit;

namespace SequentialGuid.NodaTime.Tests;

public class SequentialGuidInstantTests
{
	[Fact]
	private void TestInstantToGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var id = SequentialGuidGenerator.Instance.NewGuid(now);
		var instant = id.ToInstant();
		Assert.True(instant.HasValue);
		Assert.Equal(now, instant);
	}

	[Fact]
	private void TestInstantToGuidRoundTripSqlSorting()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var id = SequentialSqlGuidGenerator.Instance.NewGuid(now);
		var instant = id.ToInstant();
		Assert.True(instant.HasValue);
		Assert.Equal(now, instant);
	}

	[Fact]
	private void TestInstantToSqlGuidRoundTrip()
	{
		var now = SystemClock.Instance.GetCurrentInstant();
		var id = SequentialSqlGuidGenerator.Instance.NewSqlGuid(now);
		var instant = id.ToInstant();
		Assert.True(instant.HasValue);
		Assert.Equal(now, instant);
	}
}
