using NodaTime;

namespace SequentialGuid.NodaTime.Tests;

public sealed class GuidExtensionsTests
{
	// UUIDs that carry no embedded timestamp, so ToDateTime() (and therefore ToInstant()) yields null.
	[Theory]
	[InlineData("00000000-0000-0000-0000-000000000000")] // RFC 9562 §5.9 Nil UUID
	[InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")] // RFC 9562 §5.10 Max UUID
	[InlineData("919108f7-52d1-4320-9bac-f847db4148a8")] // RFC 9562 Appendix A.3 v4 example
	void TestToInstantReturnsNullWhenNoTimestamp(string value) =>
		Guid.Parse(value).ToInstant().ShouldBeNull();
}
