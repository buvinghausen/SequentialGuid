using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using Xunit;

namespace SequentialGuid.Tests
{
	public class SequentialGuidTests
	{
		/// <summary>
		/// Properly sequenced Guid array
		/// </summary>
		private IReadOnlyList<Guid> SortedGuids { get; } = new List<Guid>
		{
			new Guid("00000000-0000-0000-0000-000000000001"),
			new Guid("00000000-0000-0000-0000-000000000100"),
			new Guid("00000000-0000-0000-0000-000000010000"),
			new Guid("00000000-0000-0000-0000-000001000000"),
			new Guid("00000000-0000-0000-0000-000100000000"),
			new Guid("00000000-0000-0000-0000-010000000000"),
			new Guid("00000000-0000-0000-0001-000000000000"),
			new Guid("00000000-0000-0000-0100-000000000000"),
			new Guid("00000000-0000-0001-0000-000000000000"),
			new Guid("00000000-0000-0100-0000-000000000000"),
			new Guid("00000000-0001-0000-0000-000000000000"),
			new Guid("00000000-0100-0000-0000-000000000000"),
			new Guid("00000001-0000-0000-0000-000000000000"),
			new Guid("00000100-0000-0000-0000-000000000000"),
			new Guid("00010000-0000-0000-0000-000000000000"),
			new Guid("01000000-0000-0000-0000-000000000000")
		};

		/// <summary>
		/// Properly sequenced SqlGuid array
		/// </summary>
		private IReadOnlyList<SqlGuid> SortedSqlGuids { get; } = new List<SqlGuid>
		{
			new SqlGuid("01000000-0000-0000-0000-000000000000"),
			new SqlGuid("00010000-0000-0000-0000-000000000000"),
			new SqlGuid("00000100-0000-0000-0000-000000000000"),
			new SqlGuid("00000001-0000-0000-0000-000000000000"),
			new SqlGuid("00000000-0100-0000-0000-000000000000"),
			new SqlGuid("00000000-0001-0000-0000-000000000000"),
			new SqlGuid("00000000-0000-0100-0000-000000000000"),
			new SqlGuid("00000000-0000-0001-0000-000000000000"),
			new SqlGuid("00000000-0000-0000-0001-000000000000"),
			new SqlGuid("00000000-0000-0000-0100-000000000000"),
			new SqlGuid("00000000-0000-0000-0000-000000000001"),
			new SqlGuid("00000000-0000-0000-0000-000000000100"),
			new SqlGuid("00000000-0000-0000-0000-000000010000"),
			new SqlGuid("00000000-0000-0000-0000-000001000000"),
			new SqlGuid("00000000-0000-0000-0000-000100000000"),
			new SqlGuid("00000000-0000-0000-0000-010000000000")
		};

		[Fact]
		public void TestGuidSorting()
		{
			//Act
			var sortedList = SortedGuids.OrderBy(x => x).ToList();
			//Assert
			Assert.Equal(16, SortedGuids.Count);
			for (var i = 0; i < SortedGuids.Count; i++)
				Assert.Equal(SortedGuids[i], sortedList[i]);
		}

		[Fact]
		public void TestSqlGuidSorting()
		{
			//Act
			var sortedList = SortedSqlGuids.OrderBy(x => x).ToList();
			//Assert
			Assert.Equal(16, SortedSqlGuids.Count);
			for (var i = 0; i < SortedSqlGuids.Count; i++)
				Assert.Equal(SortedSqlGuids[i], sortedList[i]);
		}

		[Fact]
		public void TestSequentialGuidNewGuid()
		{
			//Arrange
			var generator = SequentialGuidGenerator.Instance;
			var items = Enumerable.Range(0, 25).Select(i => new
			{
				Id = generator.NewGuid(),
				Sort = i
			});
			//Act
			var sortedItems = items.OrderBy(x => x.Id).ToList();
			//Assert
			for (var i = 0; i < sortedItems.Count; i++)
				Assert.Equal(i, sortedItems[i].Sort);
		}

		/// <summary>
		/// This test is needed to test the ToSqlMap
		/// </summary>
		[Fact]
		public void TestSequentialGuidNewSqlGuid()
		{
			//Arrange
			var generator = SequentialSqlGuidGenerator.Instance;
			var items = Enumerable.Range(0, 25).Select(i => new
			{
				Id = new SqlGuid(generator.NewGuid()),
				Sort = i
			});
			//Act
			var sortedItems = items.OrderBy(x => x.Id).ToList();
			//Assert
			for (var i = 0; i < sortedItems.Count; i++)
				Assert.Equal(i, sortedItems[i].Sort);
		}

		[Fact]
		public void TestLocalDateIsUtcInGuid()
		{
			var localNow = DateTime.Now;
			TestLocalDateIsUtcInGuid(localNow,
				SequentialGuidGenerator.Instance.NewGuid(localNow));
		}
			

		[Fact]
		public void TestLocalDateIsUtcInSqlGuid()
		{
			var localNow = DateTime.Now;
			TestLocalDateIsUtcInGuid(localNow,
				SequentialSqlGuidGenerator.Instance.NewGuid(localNow));
		}

		// ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
		private static void TestLocalDateIsUtcInGuid(DateTime localNow, Guid id)
		{
			//Act
			var utcDate = id.ToDateTime().GetValueOrDefault();
			//Assert
			Assert.Equal(DateTimeKind.Utc, utcDate.Kind);
			Assert.Equal(localNow, utcDate.ToLocalTime());
		}


		[Fact]
		public void TestSqlGuidToGuid()
		{
			//Act & Assert
			for (var i = 0; i < 16; i++)
				Assert.Equal(SortedGuids[i], SortedSqlGuids[i].ToGuid());
		}

		[Fact]
		public void TestGuidToSqlGuid()
		{
			//Act & Assert
			for (var i = 0; i < 16; i++)
				Assert.Equal(SortedSqlGuids[i], SortedGuids[i].ToSqlGuid());
		}

		/// <summary>
		/// This test ensures we get the exact ticks value out of Guid.ToDateTime method & the date time is in UTC
		/// </summary>
		[Fact]
		public void TestGuidToDateTime()
		{
			//Arrange
			var generator = SequentialGuidGenerator.Instance;
			var expectedDateTime = DateTime.UtcNow;
			//Act
			var dateTime = generator.NewGuid(expectedDateTime).ToDateTime()
				.GetValueOrDefault();
			//Assert
			Assert.Equal(expectedDateTime.Ticks, dateTime.Ticks);
			Assert.Equal(DateTimeKind.Utc, dateTime.Kind);
		}

		[Fact]
		public void TestGuidToDateTimeForNonSequentialGuidReturnsNull()
		{
			//Arrange
			var guid = Guid.NewGuid();
			//Act
			var actual = guid.ToDateTime();
			//Assert
			Assert.Null(actual);
		}

		[Fact]
		public void TestGuidIsSequentialGuidFailsForNormalGuid()
		{
			//Arrange
			var guid = Guid.NewGuid();
			//Act
			var actual = guid.IsSequentialGuid();
			//Assert
			Assert.False(actual);
		}

		[Fact]
		public void TestNowIsSequentialGuid() =>
			TestIsSequentialGuid(true, DateTime.UtcNow);

		[Fact]
		public void TestUnixEpochIsSequentialGuid() =>
			TestIsSequentialGuid(true, SequentialGuid.UnixEpoch);

		[Fact]
		public void TestBetweenUnixEpochAndNowIsSequentialGuid() =>
			TestIsSequentialGuid(true, new DateTime(2000, 01, 01));

		[Fact]
		public void TestAfterNowIsNotSequentialGuid() =>
			TestIsSequentialGuid(false, DateTime.UtcNow.AddMilliseconds(1));

		[Fact]
		public void TestBeforeUnixEpochIsSequentialGuid() =>
			TestIsSequentialGuid(false, SequentialGuid.UnixEpoch.AddMilliseconds(-1));

		// ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
		private static void TestIsSequentialGuid(bool expected, DateTime d)
		{
			var generator = SequentialGuidGenerator.Instance;
			var actual = generator.NewGuid(d).IsSequentialGuid();
			Assert.Equal(expected, actual);
		}

		/// <summary>
		/// This test ensures we get the exact ticks value out of SqlGuid.ToDateTime method
		/// </summary>
		[Fact]
		public void TestSqlGuidToDateTime()
		{
			//Arrange
			var generator = SequentialSqlGuidGenerator.Instance;
			var expectedDateTime = DateTime.UtcNow;
			//Act
			var dateTime = generator.NewGuid(expectedDateTime).ToDateTime()
				.GetValueOrDefault();
			//Assert
			Assert.Equal(expectedDateTime.Ticks, dateTime.Ticks);
		}

		[Fact]
		public void TestGuidBigDateRange()
		{
			//Arrange
			var generator = SequentialGuidGenerator.Instance;
			var items = new List<Guid>();
			//Act
			for (var i = 1970; i < DateTime.Today.Year; i++)
				items.Add(generator.NewGuid(DateTime.Parse($"{i}-01-01")));
			var sortedItems = items.OrderBy(x => x).ToList();
			//Assert
			for (var i = 0; i < sortedItems.Count; i++)
				Assert.Equal(items[i], sortedItems[i]);
		}

		[Fact]
		public void TestSqlGuidBigDateRange()
		{
			//Arrange
			var generator = SequentialSqlGuidGenerator.Instance;
			var items = new List<SqlGuid>();
			//Act
			for (var i = 1970; i < DateTime.Today.Year; i++)
				items.Add(generator.NewGuid(DateTime.Parse($"{i}-01-01")));
			var sortedItems = items.OrderBy(x => x).ToList();
			//Assert
			for (var i = 0; i < sortedItems.Count; i++)
				Assert.Equal(items[i], sortedItems[i]);
		}
	}
}
