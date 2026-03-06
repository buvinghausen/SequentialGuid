using System.Data.SqlTypes;

namespace SequentialGuid.Tests;

public sealed class SequentialGuidTests
{
	private const long EpochTicks = 621355968000000000;

	/// <summary>
	///     Properly sequenced Guid array
	/// </summary>
	private IList<Guid> SortedGuidList { get; } =
		[
			new("00000000-0000-0000-0000-000000000001"),
			new("00000000-0000-0000-0000-000000000100"),
			new("00000000-0000-0000-0000-000000010000"),
			new("00000000-0000-0000-0000-000001000000"),
			new("00000000-0000-0000-0000-000100000000"),
			new("00000000-0000-0000-0000-010000000000"),
			new("00000000-0000-0000-0001-000000000000"),
			new("00000000-0000-0000-0100-000000000000"),
			new("00000000-0000-0001-0000-000000000000"),
			new("00000000-0000-0100-0000-000000000000"),
			new("00000000-0001-0000-0000-000000000000"),
			new("00000000-0100-0000-0000-000000000000"),
			new("00000001-0000-0000-0000-000000000000"),
			new("00000100-0000-0000-0000-000000000000"),
			new("00010000-0000-0000-0000-000000000000"),
			new("01000000-0000-0000-0000-000000000000")
		];

	/// <summary>
	///     Properly sequenced SqlGuid array
	/// </summary>
	/// See: https://www.sqlbi.com/blog/alberto/2007/08/31/how-are-guids-sorted-by-sql-server/
	private IList<SqlGuid> SortedSqlGuidList { get; } =
		[
			new("01000000-0000-0000-0000-000000000000"),
			new("00010000-0000-0000-0000-000000000000"),
			new("00000100-0000-0000-0000-000000000000"),
			new("00000001-0000-0000-0000-000000000000"),
			new("00000000-0100-0000-0000-000000000000"),
			new("00000000-0001-0000-0000-000000000000"),
			new("00000000-0000-0100-0000-000000000000"),
			new("00000000-0000-0001-0000-000000000000"),
			new("00000000-0000-0000-0001-000000000000"),
			new("00000000-0000-0000-0100-000000000000"),
			new("00000000-0000-0000-0000-000000000001"),
			new("00000000-0000-0000-0000-000000000100"),
			new("00000000-0000-0000-0000-000000010000"),
			new("00000000-0000-0000-0000-000001000000"),
			new("00000000-0000-0000-0000-000100000000"),
			new("00000000-0000-0000-0000-010000000000")
		];

	[Fact]
	void TestGuidSorting()
	{
		//Act
		IList<Guid> sortedList = [.. SortedGuidList.OrderBy(x => x)];
		//Assert
		sortedList.ShouldBe(SortedGuidList);
	}

	[Fact]
	void TestSqlGuidSorting()
	{
		//Act
		IList<SqlGuid> sortedList = [.. SortedSqlGuidList.OrderBy(x => x)];
		//Assert
		sortedList.ShouldBe(SortedSqlGuidList);
	}

	[Fact]
	void TestSequentialGuidNewGuid()
	{
		//Arrange
		var generator = SequentialGuidGenerator.Instance;
		var items = Enumerable.Range(0, 25).Select(i => new { Id = generator.NewGuid(), Sort = i }).ToArray();
		//Act
		var sortedItems = items.OrderBy(x => x.Id).ToArray();
		//Assert
		for (var i = 0; i < sortedItems.Length; i++)
		{
			sortedItems[i].Id.ShouldBe(items[i].Id);
			sortedItems[i].Sort.ShouldBe(items[i].Sort);
		}
	}

	[Fact]
	void TestVersion8Bits()
	{
		// Act
		var id = SequentialGuidGenerator.Instance.NewGuid();
		var bytes = id.ToByteArray();
		// Assert - version is in the high nibble of bytes[7] (Data3 high byte, little-endian)
		(bytes[7] >> 4).ShouldBe(8);
#if NET9_0_OR_GREATER
		id.Version.ShouldBe(8);
#endif
	}

	[Fact]
	void TestVariantBits()
	{
		// Act
		var id = SequentialGuidGenerator.Instance.NewGuid();
		var bytes = id.ToByteArray();
		// Assert - RFC 9562 variant: bits 7-6 of bytes[8] (Data4[0]) must be 10
		(bytes[8] & 0xC0).ShouldBe(0x80);
#if NET9_0_OR_GREATER
		id.Variant.ShouldBeInRange(8, 11);
#endif
	}

	[Fact]
	void TestSequentialGuidNewSqlGuid()
	{
		//Arrange
		var generator = SequentialSqlGuidGenerator.Instance;
		var items = Enumerable.Range(0, 25).Select(i => new { Id = generator.NewSqlGuid(), Sort = i }).ToArray();
		//Act
		var sortedItems = items.OrderBy(x => x.Id).ToArray();
		//Assert
		for (var i = 0; i < sortedItems.Length; i++)
		{
			sortedItems[i].Id.ShouldBe(items[i].Id);
			sortedItems[i].Sort.ShouldBe(items[i].Sort);
		}
	}

	[Fact]
	void TestLocalDateIsUtcInGuid()
	{
		var localNow = DateTime.Now;
		TestLocalDateIsUtcInGuidImpl(localNow,
			SequentialGuidGenerator.Instance.NewGuid(localNow));
	}

	[Fact]
	void TestLocalDateIsUtcInSqlGuid()
	{
		var localNow = DateTime.Now;
		TestLocalDateIsUtcInGuidImpl(localNow,
			SequentialSqlGuidGenerator.Instance.NewGuid(localNow));
	}

	// ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
	private static void TestLocalDateIsUtcInGuidImpl(DateTime localNow,
		Guid id)
	{
		// Act
		var utcDate = id.ToDateTime().GetValueOrDefault();
		// Assert
		utcDate.Kind.ShouldBe(DateTimeKind.Utc);
		utcDate.ToLocalTime().ShouldBe(localNow);
	}

	[Fact]
	void TestSqlGuidToGuid()
	{
		// Act
		var sqlList = SortedSqlGuidList.Select(g => g.ToGuid());
		// Assert
		sqlList.ShouldBe(SortedGuidList);
	}

	[Fact]
	void TestGuidToSqlGuid()
	{
		// Act
		var guidList = SortedGuidList.Select(g => g.ToSqlGuid());
		// Assert
		guidList.ShouldBe(SortedSqlGuidList);
	}

	[Fact]
	void TestGuidToDateTimeIsUtc()
	{
		//Arrange
		var expectedDateTime = DateTime.UtcNow;
		//Act
		var dateTime = SequentialGuidGenerator.Instance
			.NewGuid(expectedDateTime)
			.ToDateTime()
			.GetValueOrDefault();
		//Assert
		dateTime.Ticks.ShouldBe(expectedDateTime.Ticks);
		dateTime.Kind.ShouldBe(expectedDateTime.Kind);
	}

	[Fact]
	void TestGuidLocalDateTime()
	{
		//Arrange
		var expectedDateTime = DateTime.Now;
		//Act
		var dateTime = SequentialGuidGenerator.Instance
			.NewGuid(expectedDateTime)
			.ToDateTime()
			.GetValueOrDefault();
		//Assert
		dateTime.Ticks.ShouldBe(expectedDateTime.ToUniversalTime().Ticks);
		dateTime.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	void TestGuidToDateTimeForNonSequentialGuidReturnsNull()
	{
		//Arrange
		var guid = Guid.NewGuid();
		//Act
		var actual = guid.ToDateTime();
		//Assert
		actual.ShouldBeNull();
	}

	[Fact]
	void TestUtcNowDoesNotThrowException() =>
		SequentialGuidGenerator.Instance.NewGuid(DateTime.UtcNow);

	[Fact]
	void TestLocalNowDoesNotThrowException() =>
		SequentialGuidGenerator.Instance.NewGuid(DateTime.Now);

	[Fact]
	void TestUnixEpochDoesNotThrowException() =>
		SequentialGuidGenerator.Instance.NewGuid(EpochTicks);

	[Fact]
	void TestBetweenUnixEpochAndNowDoesNotThrowException() =>
		SequentialGuidGenerator.Instance.NewGuid(
			new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));

	[Fact]
	void TestDateTimeKindUnspecifiedThrowsArgumentException() =>
		TestThrowsArgumentException(new DateTime(2000, 1, 1));

	[Fact]
	void TestAfterNowThrowsArgumentException() =>
		TestThrowsArgumentException(DateTime.UtcNow.AddSeconds(1));

	[Fact]
	void TestAfterNowReturnsNullDateTime() =>
		TestReturnsNullDateTime(DateTime.UtcNow.AddSeconds(1).Ticks);

	[Fact]
	void TestBeforeUnixEpochThrowsArgumentException() =>
		TestThrowsArgumentException(new DateTime(EpochTicks - 1));

	[Fact]
	void TestBeforeUnixEpochReturnsNullDateTime() =>
		TestReturnsNullDateTime(EpochTicks - 1);

	// Test the internal mechanism that bypasses date validation
	private static void TestReturnsNullDateTime(long ticks)
	{
		//Arrange
		var guid = SequentialGuidGenerator.Instance.NewGuid(ticks);
		var sqlGuid = SequentialSqlGuidGenerator.Instance.NewGuid(ticks);
		//Act & Assert
		guid.ToDateTime().ShouldBeNull();
		sqlGuid.ToDateTime().ShouldBeNull();
	}

	private static void TestThrowsArgumentException(DateTime timestamp) =>
		Should.Throw<ArgumentException>(() =>
			SequentialGuidGenerator.Instance.NewGuid(timestamp));

	[Fact]
	void TestSqlGuidToDateTime()
	{
		//Arrange
		var expectedDateTime = DateTime.UtcNow;
		//Act
		var dateTime = SequentialSqlGuidGenerator.Instance
			.NewSqlGuid(expectedDateTime)
			.ToDateTime()
			.GetValueOrDefault();
		//Assert
		dateTime.Ticks.ShouldBe(expectedDateTime.Ticks);
		dateTime.Kind.ShouldBe(expectedDateTime.Kind);
	}

	[Fact]
	void TestSqlGuidLocalDateTime()
	{
		//Arrange
		var expectedDateTime = DateTime.Now;
		//Act
		var dateTime = SequentialSqlGuidGenerator.Instance
			.NewSqlGuid(expectedDateTime)
			.ToDateTime()
			.GetValueOrDefault();
		//Assert
		dateTime.Ticks.ShouldBe(expectedDateTime.ToUniversalTime().Ticks);
		dateTime.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	void TestGuidBigDateRange()
	{
		//Arrange
		var generator = SequentialGuidGenerator.Instance;
		IList<Guid> items = [];
		//Act
		for (var i = 1970; i < DateTime.Today.Year; i++)
		{
			items.Add(generator.NewGuid(new DateTime(i, 1, 1, 0, 0, 0,
				DateTimeKind.Local)));
		}

		IList<Guid> sortedItems = [.. items.OrderBy(x => x)];
		//Assert
		sortedItems.ShouldBe(items);
	}

	[Fact]
	void TestSqlGuidBigDateRange()
	{
		//Arrange
		var generator = SequentialSqlGuidGenerator.Instance;
		IList<SqlGuid> items = [];
		//Act
		for (var i = 1970; i < DateTime.Today.Year; i++)
		{
			items.Add(generator.NewSqlGuid(new DateTime(i, 1, 1, 0, 0, 0,
				DateTimeKind.Utc)));
		}
		IList<SqlGuid> sortedItems = [.. items.OrderBy(x => x)];
		//Assert
		sortedItems.ShouldBe(items);
	}

	[Fact]
	void TestGuidConversions()
	{
		// Arrange
		var id = SequentialGuidGenerator.Instance.NewGuid();
		// Act
		var converted = id.ToSqlGuid().ToGuid();
		// Assert
		converted.ShouldBe(id);
	}

	[Fact]
	void TestSqlGuidConversions()
	{
		// Arrange
		var id = SequentialSqlGuidGenerator.Instance.NewSqlGuid();
		// Act
		var converted = id.ToGuid().ToSqlGuid();
		// Assert
		converted.ShouldBe(id);
	}

	[Theory]
	[InlineData("08c12202-47e4-4000-f24c-569ed8035ff3", 2000)]
	[InlineData("08c2419c-eb14-c000-f24c-569ed8035ff4", 2001)]
	[InlineData("08c3606e-63db-8000-f24c-569ed8035ff5", 2002)]
	[InlineData("08c47f3f-dca2-4000-f24c-569ed8035ff6", 2003)]
	[InlineData("08c59e11-5569-0000-f24c-569ed8035ff7", 2004)]
	[InlineData("08c6bdab-f899-8000-f24c-569ed8035ff8", 2005)]
	[InlineData("08c7dc7d-7160-4000-f24c-569ed8035ff9", 2006)]
	[InlineData("08c8fb4e-ea27-0000-f24c-569ed8035ffa", 2007)]
	[InlineData("08ca1a20-62ed-c000-f24c-569ed8035ffb", 2008)]
	[InlineData("08cb39bb-061e-4000-f24c-569ed8035ffc", 2009)]
	[InlineData("08cc588c-7ee5-0000-f24c-569ed8035ffd", 2010)]
	[InlineData("08cd775d-f7ab-c000-f24c-569ed8035ffe", 2011)]
	[InlineData("08ce962f-7072-8000-f24c-569ed8035fff", 2012)]
	[InlineData("08cfb5ca-13a3-0000-f24c-569ed8036000", 2013)]
	[InlineData("08d0d49b-8c69-c000-f24c-569ed8036001", 2014)]
	[InlineData("08d1f36d-0530-8000-f24c-569ed8036002", 2015)]
	[InlineData("08d3123e-7df7-4000-f24c-569ed8036003", 2016)]
	[InlineData("08d431d9-2127-c000-f24c-569ed8036004", 2017)]
	[InlineData("08d550aa-99ee-8000-f24c-569ed8036005", 2018)]
	[InlineData("08d66f7c-12b5-4000-f24c-569ed8036006", 2019)]
	[InlineData("08d78e4d-8b7c-0000-f24c-569ed8036007", 2020)]
	[InlineData("08d8ade8-2eac-8000-f24c-569ed8036008", 2021)]
	[InlineData("08d9ccb9-a773-4000-f24c-569ed8036009", 2022)]
	[InlineData("08daeb8b-203a-0000-f24c-569ed803600a", 2023)]
	[InlineData("08dc0a5c-9900-c000-f24c-569ed803600b", 2024)]
	[InlineData("08dd29f7-3c31-4000-f24c-569ed803600c", 2025)]
	[InlineData("08de48c8-b4f8-0000-f24c-569ed803600d", 2026)]
	void LegacyToDateTimeTests(string input, ushort year)
	{
		// Arrange
		Guid id = new(input);
		DateTime expected = new(year,1,1,0,0,0, DateTimeKind.Utc);

		// Act
		var actual = id.ToDateTime();

		// Assert
		actual.ShouldBe(expected);
	}

	[Theory]
	[InlineData("de0a0260-5856-4cf2-4000-08c1220247e4", 2000)]
	[InlineData("df0a0260-5856-4cf2-c000-08c2419ceb14", 2001)]
	[InlineData("e00a0260-5856-4cf2-8000-08c3606e63db", 2002)]
	[InlineData("e10a0260-5856-4cf2-4000-08c47f3fdca2", 2003)]
	[InlineData("e20a0260-5856-4cf2-0000-08c59e115569", 2004)]
	[InlineData("e30a0260-5856-4cf2-8000-08c6bdabf899", 2005)]
	[InlineData("e40a0260-5856-4cf2-4000-08c7dc7d7160", 2006)]
	[InlineData("e50a0260-5856-4cf2-0000-08c8fb4eea27", 2007)]
	[InlineData("e60a0260-5856-4cf2-c000-08ca1a2062ed", 2008)]
	[InlineData("e70a0260-5856-4cf2-4000-08cb39bb061e", 2009)]
	[InlineData("e80a0260-5856-4cf2-0000-08cc588c7ee5", 2010)]
	[InlineData("e90a0260-5856-4cf2-c000-08cd775df7ab", 2011)]
	[InlineData("ea0a0260-5856-4cf2-8000-08ce962f7072", 2012)]
	[InlineData("eb0a0260-5856-4cf2-0000-08cfb5ca13a3", 2013)]
	[InlineData("ec0a0260-5856-4cf2-c000-08d0d49b8c69", 2014)]
	[InlineData("ed0a0260-5856-4cf2-8000-08d1f36d0530", 2015)]
	[InlineData("ee0a0260-5856-4cf2-4000-08d3123e7df7", 2016)]
	[InlineData("ef0a0260-5856-4cf2-c000-08d431d92127", 2017)]
	[InlineData("f00a0260-5856-4cf2-8000-08d550aa99ee", 2018)]
	[InlineData("f10a0260-5856-4cf2-4000-08d66f7c12b5", 2019)]
	[InlineData("f20a0260-5856-4cf2-0000-08d78e4d8b7c", 2020)]
	[InlineData("f30a0260-5856-4cf2-8000-08d8ade82eac", 2021)]
	[InlineData("f40a0260-5856-4cf2-4000-08d9ccb9a773", 2022)]
	[InlineData("f50a0260-5856-4cf2-0000-08daeb8b203a", 2023)]
	[InlineData("f60a0260-5856-4cf2-c000-08dc0a5c9900", 2024)]
	[InlineData("f70a0260-5856-4cf2-4000-08dd29f73c31", 2025)]
	[InlineData("f80a0260-5856-4cf2-0000-08de48c8b4f8", 2026)]
	void LegacySqlToDateTimeTests(string input, ushort year)
	{
		// Arrange
		SqlGuid id = new(input);
		DateTime expected = new(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		// Act
		var actual = id.ToDateTime();

		// Assert
		actual.ShouldBe(expected);
	}
}
