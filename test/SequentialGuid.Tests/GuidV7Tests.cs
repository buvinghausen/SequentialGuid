namespace SequentialGuid.Tests;

public sealed class GuidV7Tests
{
	// RFC 9562 Appendix A.6 test vector: Tuesday, February 22, 2022 2:22:22.00 PM GMT-05:00
	// Unix Epoch milliseconds: 1645557742000 = 0x017F22E279B0
	private const long RfcTestVectorMs = 1645557742000L;

	// Extracts the embedded 48-bit Unix millisecond timestamp from a UUIDv7.
	// In .NET's mixed-endian Guid layout the timestamp bytes are stored as:
	//   bytes[3..0] = ms bits 47..16 (Data1 little-endian)
	//   bytes[5..4] = ms bits 15..0  (Data2 little-endian)
	private static long ExtractUnixMs(Guid guid)
	{
		var b = guid.ToByteArray();
		return ((long)b[3] << 40) | ((long)b[2] << 32) | ((long)b[1] << 24) |
		       ((long)b[0] << 16) | ((long)b[5] << 8)  | b[4];
	}

	[Fact]
	void TestVersion7Bits()
	{
		// Act
		var id = GuidV7.NewGuid();
		var bytes = id.ToByteArray();
		// Assert - version is in the high nibble of bytes[7] (Data3 high byte, little-endian)
		(bytes[7] >> 4).ShouldBe(7);
#if NET9_0_OR_GREATER
		id.Version.ShouldBe(7);
#endif
		// At present the compiler can't access static instance-like properties across assemblies
		bytes.AreRfc9562(7).ShouldBeTrue();
	}

	[Fact]
	void TestVariantBits()
	{
		// Act
		var id = GuidV7.NewGuid();
		var bytes = id.ToByteArray();
		// Assert - RFC 9562 variant: bits 7-6 of bytes[8] (Data4[0]) must be 10
		(bytes[8] & 0xC0).ShouldBe(0x80);
#if NET9_0_OR_GREATER
		id.Variant.ShouldBeInRange(8, 11);
#endif
		// At present the compiler can't access static instance-like properties across assemblies
		bytes.AreRfc9562(7).ShouldBeTrue();
	}

	[Fact]
	void TestRfcTestVectorTimestamp()
	{
		// Arrange - use the RFC 9562 Appendix A.6 timestamp
		// Act
		var ms = ExtractUnixMs(GuidV7.NewGuid(RfcTestVectorMs));
		// Assert
		ms.ShouldBe(RfcTestVectorMs);
	}

	[Fact]
	void TestDateTimeOffsetOverload()
	{
		// Arrange - RFC 9562 Appendix A.6 timestamp expressed as DateTimeOffset (UTC)
		DateTimeOffset timestamp = new(2022, 2, 22, 19, 22, 22, TimeSpan.Zero);
		// Act
		var ms = ExtractUnixMs(GuidV7.NewGuid(timestamp));
		// Assert
		ms.ShouldBe(RfcTestVectorMs);
	}

	[Fact]
	void TestCurrentTimestampIsEmbedded()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		// Act
		var guid = GuidV7.NewGuid();
		var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var ms = ExtractUnixMs(guid);
		// Assert
		ms.ShouldBeGreaterThanOrEqualTo(before);
		ms.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	void TestSequentialTimestampsProduceOrderedGuids()
	{
		// Arrange - generate UUIDs with strictly increasing 1 ms timestamps
		const long baseMs = 1_000_000L;
		var guids = Enumerable.Range(0, 10).Select(i => GuidV7.NewGuid(baseMs + i)).ToArray();
		// Act
		Guid[] sorted = [.. guids.OrderBy(x => x)];
		// Assert - different timestamp ms values always sort in creation order
		sorted.ShouldBe(guids);
	}

	[Fact]
	void TestZeroTimestampSucceeds()
	{
		// Arrange / Act
		var guid = GuidV7.NewGuid(0L);
		var bytes = guid.ToByteArray();
		// Assert
		(bytes[7] >> 4).ShouldBe(7);
		(bytes[8] & 0xC0).ShouldBe(0x80);
		ExtractUnixMs(guid).ShouldBe(0L);
	}

	[Fact]
	void TestMaxValidTimestampSucceeds()
	{
		// Arrange / Act
		const long maxMs = 0x0000_FFFF_FFFF_FFFF;
		var guid = GuidV7.NewGuid(maxMs);
		var bytes = guid.ToByteArray();
		// Assert
		(bytes[7] >> 4).ShouldBe(7);
		(bytes[8] & 0xC0).ShouldBe(0x80);
		ExtractUnixMs(guid).ShouldBe(maxMs);
	}

	[Fact]
	void TestNegativeTimestampThrows()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => GuidV7.NewGuid(-1L));
	}

	[Fact]
	void TestOverflowTimestampThrows()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => GuidV7.NewGuid(0x0001_0000_0000_0000L));
	}

	[Fact]
	void TestTwoGuidsWithSameTimestampAreDistinct()
	{
		// Act
		var first = GuidV7.NewGuid(RfcTestVectorMs);
		var second = GuidV7.NewGuid(RfcTestVectorMs);
		// Assert - random bits should make them extremely unlikely to collide
		first.ShouldNotBe(second);
	}

	[Fact]
	void TestSameTimestampBatchIsMonotonicallyOrdered()
	{
		// Arrange - use the current time so the counter path is exercised
		// (RFC 9562 §6.2 Method 1: fixed bit-length dedicated counter in rand_a)
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		// Act - generate 100 UUIDs all sharing the same millisecond timestamp
		Guid[] guids = [.. Enumerable.Range(0, 100).Select(_ => GuidV7.NewGuid(timestamp))];
		// Assert - the counter in rand_a ensures they are already in creation order
		Guid[] sorted = [.. guids.OrderBy(x => x)];
		sorted.ShouldBe(guids);
	}
}
