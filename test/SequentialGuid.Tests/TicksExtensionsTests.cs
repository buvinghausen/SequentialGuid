using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class TicksExtensionsTests
{
	[Fact]
	void NowTicksAreValid()
	{
		DateTime.UtcNow.Ticks.IsDateTime.ShouldBeTrue();
	}

	[Fact]
	void UnixEpochTicksAreValid()
	{
		new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks.IsDateTime.ShouldBeTrue();
	}

	[Fact]
	void OneTickBeforeUnixEpochIsInvalid()
	{
		(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks - 1L).IsDateTime.ShouldBeFalse();
	}

	[Fact]
	void HalfSecondInFutureIsValid()
	{
		// New slack window allows up to 1 s in the future
		(DateTime.UtcNow.Ticks + TimeSpan.TicksPerMillisecond * 500).IsDateTime.ShouldBeTrue();
	}

	[Fact]
	void TwoSecondsInFutureIsInvalid()
	{
		(DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond * 2).IsDateTime.ShouldBeFalse();
	}
}
