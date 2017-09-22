using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;

namespace Buvinghausen.SequentialGuid.Tests
{
	[TestClass]
	public class SequentialGuidTests
	{
		//Arrange
		/// <summary>
		/// Properly sequenced Guid array
		/// </summary>
		private readonly IList<Guid> _guids = new List<Guid>
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
		private readonly IList<SqlGuid> _sqlGuids = new List<SqlGuid>
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

		[TestMethod]
		public void TestGuidSorting()
		{
			//Act
			var sortedList = _guids.OrderBy(x => x).ToList();
			//Assert
			Assert.AreEqual(16, _guids.Count);
			for (var i = 0; i < _guids.Count; i++) Assert.AreEqual(_guids[i], sortedList[i]);
		}

		[TestMethod]
		public void TestSqlGuidSorting()
		{
			//Act
			var sortedList = _sqlGuids.OrderBy(x => x).ToList();
			//Assert
			Assert.AreEqual(16, _sqlGuids.Count);
			for (var i = 0; i < _sqlGuids.Count; i++) Assert.AreEqual(_sqlGuids[i], sortedList[i]);
		}

		[TestMethod]
		public void TestSequentialGuidNewGuid()
		{
			//Arrange
			var generator = SequentialGuidGenerator.Instance;
			var items = Enumerable.Range(0, 25).Select(i => new { Id = generator.NewGuid(), Sort = i });
			//Act
			var sortedItems = items.OrderBy(x => x.Id).ToList();
			//Assert
			for (var i = 0; i < sortedItems.Count; i++) Assert.AreEqual(i, sortedItems[i].Sort);
		}

		/// <summary>
		/// This test is needed to test the ToSqlMap
		/// </summary>
		[TestMethod]
		public void TestSequentialGuidNewSqlGuid()
		{
			//Arrange
			var generator = SequentialSqlGuidGenerator.Instance;
			var items = Enumerable.Range(0, 25).Select(i => new { Id = new SqlGuid(generator.NewGuid()), Sort = i });
			//Act
			var sortedItems = items.OrderBy(x => x.Id).ToList();
			//Assert
			for (var i = 0; i < sortedItems.Count; i++) Assert.AreEqual(i, sortedItems[i].Sort);
		}


		[TestMethod]
		public void TestSqlGuidToGuid()
		{
			//Act & Assert
			for (var i = 0; i < _sqlGuids.Count; i++) Assert.AreEqual(_guids[i], _sqlGuids[i].ToGuid());
		}

		[TestMethod]
		public void TestGuidToSqlGuid()
		{
			//Act & Assert
			for (var i = 0; i < _guids.Count; i++) Assert.AreEqual(_sqlGuids[i], _guids[i].ToSqlGuid());
		}

		/// <summary>
		/// This test ensures we get the exact ticks value out of Guid.ToDateTime method & the date time is in UTC
		/// </summary>
		[TestMethod]
		public void TestGuidToDateTime()
		{
			//Arrange
			var generator = SequentialGuidGenerator.Instance;
			var expectedTicks = DateTime.UtcNow.Ticks;
			//Act
			var dateTime = generator.NewGuid(expectedTicks).ToDateTime();
			//Assert
			Assert.AreEqual(expectedTicks, dateTime.Ticks);
			Assert.AreEqual(DateTimeKind.Utc, dateTime.Kind);
		}

		[TestMethod]
		public void TestGuidToDateTimeForNonSequentialGuidReturnsMinUnixDateTime()
		{
			//Arrange
			var guid = Guid.NewGuid();
			//Act
			var actual = guid.ToDateTime();
			//Assert
			Assert.AreEqual(actual, ExtensionMethods.UnixEpoch);
		}

		[TestMethod]
		public void TestGuidIsSequentialGuidFailsForNormalGuid()
		{
			//Arrange
			var guid = Guid.NewGuid();
			//Act
			var actual = guid.IsSequentialGuid();
			//Assert
			Assert.IsFalse(actual);
		}


		[TestMethod] public void TestNowIsSequentialGuid() { TestIsSequentialGuid(DateTime.UtcNow, true); }
		[TestMethod] public void TestUnixEpochIsSequentialGuid() { TestIsSequentialGuid(ExtensionMethods.UnixEpoch, true); }
		[TestMethod] public void TestBetweenUnixEpochAndNowIsSequentialGuid() { TestIsSequentialGuid(new DateTime(2000, 01, 01), true); }
		[TestMethod] public void TestAfterNowIsNotSequentialGuid() { TestIsSequentialGuid(DateTime.Now.AddDays(3), false); }
		[TestMethod] public void TestBeforeUnixEpochIsSequentialGuid() { TestIsSequentialGuid(ExtensionMethods.UnixEpoch.AddDays(-3), false); }

		private static void TestIsSequentialGuid(DateTime d, bool expected)
		{
			var generator = SequentialGuidGenerator.Instance;
			var actual = generator.NewGuid(d).IsSequentialGuid();
			Assert.AreEqual(actual, expected);
		}


		/// <summary>
		/// This test ensures we get the exact ticks value out of SqlGuid.ToDateTime method
		/// </summary>
		[TestMethod]
		public void TestSqlGuidToDateTime()
		{
			//Arrange
			var generator = SequentialSqlGuidGenerator.Instance;
			var expectedTicks = DateTime.UtcNow.Ticks;
			//Act
			var actualTicks = generator.NewGuid(expectedTicks).ToDateTime().Ticks;
			//Assert
			Assert.AreEqual(expectedTicks, actualTicks);
		}

		[TestMethod]
		public void TestGuidBigDateRange()
		{
			//Arrange
			var generator = SequentialGuidGenerator.Instance;
			var items = new List<Guid>();
			//Act
			for (var i = 1970; i < 2015; i++) items.Add(generator.NewGuid(DateTime.Parse($"{i}-01-01")));
			var sortedItems = items.OrderBy(x => x).ToList();
			//Assert
			for (var i = 0; i < sortedItems.Count; i++) Assert.AreEqual(items[i], sortedItems[i]);
		}

		[TestMethod]
		public void TestSqlGuidBigDateRange()
		{
			//Arrange
			var generator = SequentialSqlGuidGenerator.Instance;
			var items = new List<SqlGuid>();
			//Act
			for (var i = 1970; i < 2015; i++) items.Add(generator.NewGuid(DateTime.Parse($"{i}-01-01")));
			var sortedItems = items.OrderBy(x => x).ToList();
			//Assert
			for (var i = 0; i < sortedItems.Count; i++) Assert.AreEqual(items[i], sortedItems[i]);
		}
	}
}