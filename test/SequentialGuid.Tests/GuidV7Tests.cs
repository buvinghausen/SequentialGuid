namespace SequentialGuid.Tests;

public sealed class GuidV7Tests
{
	// RFC 9562 Appendix A.6 test vector: Tuesday, February 22, 2022 2:22:22.00 PM GMT-05:00
	// Unix Epoch milliseconds: 1645557742000 = 0x017F22E279B0
	private const long RfcTestVectorMs = 1645557742000L;

	[Fact]
	void TestVersion7Bits()
	{
		// Act
		var id = GuidV7.NewGuid();
		var bytes = id.ToByteArray();
#if NET9_0_OR_GREATER
		id.Version.ShouldBe(7);
#endif
		// At present the compiler can't access static instance-like properties across assemblies
		bytes.IsRfc9562Version(7).ShouldBeTrue();
	}

	[Fact]
	void TestVariantBits()
	{
		// Act
		var id = GuidV7.NewGuid();
		var bytes = id.ToByteArray();
		var sqlBytes = bytes.ToSqlByteOrder();
#if NET9_0_OR_GREATER
		id.Variant.ShouldBeInRange(8, 11);
#endif
		bytes.VariantIsRfc9562().ShouldBeTrue();
		sqlBytes.SqlVariantIsRfc9562().ShouldBeTrue();
	}

	[Fact]
	void TestRfcTestVectorTimestamp()
	{
		// Arrange - use the RFC 9562 Appendix A.6 timestamp
		// Act
		var ms = GuidV7.NewGuid(RfcTestVectorMs).ToUnixMs();
		// Assert
		ms.ShouldBe(RfcTestVectorMs);
	}

	[Fact]
	void TestDateTimeOffsetOverload()
	{
		// Arrange - RFC 9562 Appendix A.6 timestamp expressed as DateTimeOffset (UTC)
		DateTimeOffset timestamp = new(2022, 2, 22, 19, 22, 22, TimeSpan.Zero);
		// Act
		var ms = GuidV7.NewGuid(timestamp).ToUnixMs();
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
		var ms = guid.ToUnixMs();
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
		bytes.IsRfc9562Version(7).ShouldBeTrue();
		bytes.VariantIsRfc9562().ShouldBeTrue();
		guid.ToUnixMs().ShouldBe(0L);
	}

	[Fact]
	void TestMaxValidTimestampSucceeds()
	{
		// Arrange / Act
		const long maxMs = 0x0000_FFFF_FFFF_FFFF;
		var guid = GuidV7.NewGuid(maxMs);
		var bytes = guid.ToByteArray();
		// Assert
		bytes.IsRfc9562Version(7).ShouldBeTrue();
		bytes.VariantIsRfc9562().ShouldBeTrue();
		guid.ToUnixMs().ShouldBe(maxMs);
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

	[Fact]
	void TestGuidToDateTime()
	{
		// Arrange
		var utcNow = DateTimeOffset.UtcNow;
		// Guid V7 only keeps time to the millisecond so strip off additional precision
		var expected = utcNow.DateTime.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMillisecond));

		// Act
		var actual = GuidV7.NewGuid(utcNow).ToDateTime();

		// Assert
		actual.ShouldBe(expected);
	}

	[Fact]
	void TestSqlGuidToDateTime()
	{
		// Arrange
		var utcNow = DateTimeOffset.UtcNow;
		// Guid V7 only keeps time to the millisecond so strip off additional precision
		var expected = utcNow.DateTime.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMillisecond));

		// Act
		var actual = GuidV7.NewSqlGuid(utcNow).ToDateTime();

		// Assert
		actual.ShouldBe(expected);
	}
}
