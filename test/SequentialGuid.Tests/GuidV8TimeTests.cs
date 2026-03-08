namespace SequentialGuid.Tests;

public sealed class GuidV8TimeTests
{
	private const long EpochTicks = 621355968000000000L;

	[Fact]
	void TestVersion8Bits()
	{
		// Act
		var id = GuidV8Time.NewGuid();
		var bytes = id.ToByteArray();
#if NET9_0_OR_GREATER
		id.Version.ShouldBe(8);
#endif
		// At present the compiler can't access static instance-like properties across assemblies
		bytes.IsRfc9562Version(8).ShouldBeTrue();
	}

	[Fact]
	void TestVariantBits()
	{
		// Act
		var id = GuidV8Time.NewGuid();
		var sqlId = id.ToSqlGuid();
#if NET9_0_OR_GREATER
		id.Variant.ShouldBeInRange(8, 11);
#endif
		id.ToByteArray().VariantIsRfc9562().ShouldBeTrue();
		sqlId.ToByteArray()!.SqlVariantIsRfc9562().ShouldBeTrue();
	}

	[Fact]
	void TestCurrentTimestampIsEmbedded()
	{
		// Arrange
		var before = DateTime.UtcNow;
		// Act
		var id = GuidV8Time.NewGuid();
		var after = DateTime.UtcNow;
		var dateTime = id.ToDateTime().GetValueOrDefault();
		// Assert
		dateTime.ShouldBeGreaterThanOrEqualTo(before);
		dateTime.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	void TestUtcDateTimeOverload()
	{
		// Arrange
		var expected = DateTime.UtcNow;
		// Act
		var dateTime = GuidV8Time.NewGuid(expected).ToDateTime().GetValueOrDefault();
		// Assert
		dateTime.Ticks.ShouldBe(expected.Ticks);
		dateTime.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	void TestLocalDateTimeIsStoredAsUtc()
	{
		// Arrange
		var localNow = DateTime.Now;
		// Act
		var dateTime = GuidV8Time.NewGuid(localNow).ToDateTime().GetValueOrDefault();
		// Assert
		dateTime.Kind.ShouldBe(DateTimeKind.Utc);
		dateTime.ToLocalTime().ShouldBe(localNow);
	}

	[Fact]
	void TestSqlLocalDateTimeIsStoredAsUtc()
	{
		// Arrange
		var localNow = DateTime.Now;
		// Act
		var dateTime = GuidV8Time.NewSqlGuid(localNow).ToDateTime().GetValueOrDefault();
		// Assert
		dateTime.Kind.ShouldBe(DateTimeKind.Utc);
		dateTime.ToLocalTime().ShouldBe(localNow);
	}

	[Fact]
	void TestSequentialTimestampsProduceOrderedGuids()
	{
		// Arrange - generate UUIDs with strictly increasing tick timestamps
		const long baseTicks = 639084490271870091L;
		var guids = Enumerable.Range(0, 10).Select(i => GuidV8Time.NewGuid(baseTicks + i)).ToArray();
		// Act
		Guid[] sorted = [.. guids.OrderBy(x => x)];
		// Assert - different timestamp tick values always sort in creation order
		sorted.ShouldBe(guids);
	}

	[Fact]
	void TestUnixEpochDoesNotThrowException() =>
		GuidV8Time.NewGuid(new DateTime(EpochTicks, DateTimeKind.Utc));

	[Fact]
	void TestUtcNowDoesNotThrowException() =>
		GuidV8Time.NewGuid(DateTime.UtcNow);

	[Fact]
	void TestLocalNowDoesNotThrowException() =>
		GuidV8Time.NewGuid(DateTime.Now);

	[Fact]
	void TestUnspecifiedDateTimeKindThrowsArgumentException() =>
		Should.Throw<ArgumentException>(() => GuidV8Time.NewGuid(new DateTime(2000, 1, 1)));

	[Fact]
	void TestAfterNowThrowsArgumentException() =>
		Should.Throw<ArgumentException>(() => GuidV8Time.NewGuid(DateTime.UtcNow.AddSeconds(1)));

	[Fact]
	void TestBeforeUnixEpochThrowsArgumentException() =>
		Should.Throw<ArgumentException>(() =>
			GuidV8Time.NewGuid(new DateTime(EpochTicks - 1, DateTimeKind.Utc)));

	[Fact]
	void TestDateTimeOffsetOverload()
	{
		// Arrange
		var expected = DateTimeOffset.UtcNow;
		// Act
		var dateTime = GuidV8Time.NewGuid(expected).ToDateTime().GetValueOrDefault();
		// Assert - ticks are stored as UTC
		dateTime.Ticks.ShouldBe(expected.UtcTicks);
		dateTime.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	void TestDateTimeOffsetWithOffsetIsStoredAsUtc()
	{
		var dto = DateTimeOffset.Now;
		// Act
		var dateTime = GuidV8Time.NewGuid(dto).ToDateTime().GetValueOrDefault();
		// Assert - stored value equals the UTC representation
		dateTime.Kind.ShouldBe(DateTimeKind.Utc);
		dateTime.Ticks.ShouldBe(dto.UtcTicks);
	}

	[Fact]
	void TestSqlGuidDateTimeOffsetOverload()
	{
		// Arrange
		var expected = DateTimeOffset.UtcNow;
		// Act
		var dateTime = GuidV8Time.NewSqlGuid(expected).ToDateTime().GetValueOrDefault();
		// Assert
		dateTime.Kind.ShouldBe(DateTimeKind.Utc);
		dateTime.Ticks.ShouldBe(expected.UtcTicks);
	}

	[Fact]
	void TestDateTimeOffsetAfterNowThrowsArgumentException() =>
		Should.Throw<ArgumentException>(() =>
			GuidV8Time.NewGuid(DateTimeOffset.UtcNow.AddSeconds(1)));

	[Fact]
	void TestDateTimeOffsetBeforeUnixEpochThrowsArgumentException() =>
		Should.Throw<ArgumentException>(() =>
			GuidV8Time.NewGuid(new DateTimeOffset(new DateTime(EpochTicks - 1, DateTimeKind.Utc))));
}
