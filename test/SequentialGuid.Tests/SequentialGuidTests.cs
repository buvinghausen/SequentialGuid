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
	private void TestGuidSorting()
	{
		//Act
		IList<Guid> sortedList = [.. SortedGuidList.OrderBy(x => x)];
		//Assert
		sortedList.ShouldBe(SortedGuidList);
	}

	[Fact]
	private void TestSqlGuidSorting()
	{
		//Act
		IList<SqlGuid> sortedList = [.. SortedSqlGuidList.OrderBy(x => x)];
		//Assert
		sortedList.ShouldBe(SortedSqlGuidList);
	}

	[Fact]
	private void TestSequentialGuidNewGuid()
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
	private void TestSequentialGuidNewSqlGuid()
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
	private void TestLocalDateIsUtcInGuid()
	{
		var localNow = DateTime.Now;
		TestLocalDateIsUtcInGuidImpl(localNow,
			SequentialGuidGenerator.Instance.NewGuid(localNow));
	}

	[Fact]
	private void TestLocalDateIsUtcInSqlGuid()
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
	private void TestSqlGuidToGuid()
	{
		// Act
		var sqlList = SortedSqlGuidList.Select(g => g.ToGuid());
		// Assert
		sqlList.ShouldBe(SortedGuidList);
	}

	[Fact]
	private void TestGuidToSqlGuid()
	{
		// Act
		var guidList = SortedGuidList.Select(g => g.ToSqlGuid());
		// Assert
		guidList.ShouldBe(SortedSqlGuidList);
	}

	[Fact]
	private void TestGuidToDateTimeIsUtc()
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
	private void TestGuidLocalDateTime()
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
	private void TestGuidToDateTimeForNonSequentialGuidReturnsNull()
	{
		//Arrange
		var guid = Guid.NewGuid();
		//Act
		var actual = guid.ToDateTime();
		//Assert
		actual.ShouldBeNull();
	}

	[Fact]
	private void TestUtcNowDoesNotThrowException() =>
		SequentialGuidGenerator.Instance.NewGuid(DateTime.UtcNow);

	[Fact]
	private void TestLocalNowDoesNotThrowException() =>
		SequentialGuidGenerator.Instance.NewGuid(DateTime.Now);

	[Fact]
	private void TestUnixEpochDoesNotThrowException() =>
		SequentialGuidGenerator.Instance.NewGuid(EpochTicks);

	[Fact]
	private void TestBetweenUnixEpochAndNowDoesNotThrowException() =>
		SequentialGuidGenerator.Instance.NewGuid(
			new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));

	[Fact]
	private void TestDateTimeKindUnspecifiedThrowsArgumentException() =>
		TestThrowsArgumentException(new DateTime(2000, 1, 1));

	[Fact]
	private void TestAfterNowThrowsArgumentException() =>
		TestThrowsArgumentException(DateTime.UtcNow.AddSeconds(1));

	[Fact]
	private void TestAfterNowReturnsNullDateTime() =>
		TestReturnsNullDateTime(DateTime.UtcNow.AddSeconds(1).Ticks);

	[Fact]
	private void TestBeforeUnixEpochThrowsArgumentException() =>
		TestThrowsArgumentException(new DateTime(EpochTicks - 1));

	[Fact]
	private void TestBeforeUnixEpochReturnsNullDateTime() =>
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
	private void TestSqlGuidToDateTime()
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
	private void TestSqlGuidLocalDateTime()
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
	private void TestGuidBigDateRange()
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
	private void TestSqlGuidBigDateRange()
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
	private void TestGuidConversions()
	{
		// Arrange
		var id = SequentialGuidGenerator.Instance.NewGuid();
		// Act
		var converted = id.ToSqlGuid().ToGuid();
		// Assert
		converted.ShouldBe(id);
	}

	[Fact]
	private void TestSqlGuidConversions()
	{
		// Arrange
		var id = SequentialSqlGuidGenerator.Instance.NewSqlGuid();
		// Act
		var converted = id.ToGuid().ToSqlGuid();
		// Assert
		converted.ShouldBe(id);
	}
}
