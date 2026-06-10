#if NET6_0_OR_GREATER
using System.Data.SqlTypes;
using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class GuidV8TimeBulkTests
{
	// RFC 9562 Appendix A.6 moment expressed as a UTC DateTime
	static readonly DateTime _fixedTimestamp = new(2022, 2, 22, 19, 22, 22, DateTimeKind.Utc);

	[Fact]
	void FillProducesValidVersionVariantAndTimestamp()
	{
		// Arrange
		var ids = new Guid[100];
		// Act
		GuidV8Time.Fill(ids, _fixedTimestamp);
		// Assert
		foreach (var id in ids)
		{
			var bytes = id.ToByteArray();
			bytes.IsRfc9562Version(8).ShouldBeTrue();
			bytes.VariantIsRfc9562().ShouldBeTrue();
			// V8 time embeds full 100 ns tick precision
			id.ToDateTime().ShouldBe(_fixedTimestamp);
		}
	}

	[Fact]
	void FillIsMonotonicallyOrdered()
	{
		// Arrange - same wrap-boundary exposure precedent as the V7 tests
		var ids = new Guid[1_000];
		// Act
		GuidV8Time.Fill(ids, _fixedTimestamp);
		// Assert
		Guid[] sorted = [.. ids.OrderBy(x => x)];
		sorted.ShouldBe(ids, ignoreOrder: false);
	}

	[Fact]
	void FillEmptyDestinationIsNoOp() =>
		GuidV8Time.Fill([]); // must not throw

	[Fact]
	void FillUnspecifiedKindThrows()
	{
		Should.Throw<ArgumentException>(() =>
		{
			var ids = new Guid[1];
			GuidV8Time.Fill(ids, new DateTime(2022, 2, 22, 19, 22, 22));
		});
	}

	[Fact]
	void FillPreEpochTimestampThrows()
	{
		Should.Throw<ArgumentException>(() =>
		{
			var ids = new Guid[1];
			GuidV8Time.Fill(ids, new DateTime(1969, 12, 31, 0, 0, 0, DateTimeKind.Utc));
		});
	}

	[Fact]
	void FillSqlSortsInSqlServerOrder()
	{
		// Arrange
		var ids = new Guid[1_000];
		// Act
		GuidV8Time.FillSql(ids, _fixedTimestamp);
		// Assert
		Guid[] sorted = [.. ids.OrderBy(g => new SqlGuid(g))];
		sorted.ShouldBe(ids, ignoreOrder: false);
		foreach (var id in ids)
			id.ToByteArray().IsSqlRfc9562Version(8).ShouldBeTrue();
	}

	[Fact]
	void NewGuidsMatchesFillSemantics()
	{
		// Act
		var ids = GuidV8Time.NewGuids(50, _fixedTimestamp);
		// Assert
		ids.Length.ShouldBe(50);
		foreach (var id in ids)
		{
			id.ToByteArray().IsRfc9562Version(8).ShouldBeTrue();
			id.ToDateTime().ShouldBe(_fixedTimestamp);
		}
	}

	[Fact]
	void NewGuidsZeroCountReturnsEmpty() =>
		GuidV8Time.NewGuids(0).ShouldBeEmpty();

	[Fact]
	void NewGuidsNegativeCountThrows() =>
		Should.Throw<ArgumentOutOfRangeException>(() => GuidV8Time.NewGuids(-1));

	[Fact]
	void NewGuidsOversizedCountThrows() =>
		// 2^22 + 1 exceeds the 22-bit counter space; throws before allocating
		Should.Throw<ArgumentOutOfRangeException>(() => GuidV8Time.NewGuids(0x40_0001));

	[Fact]
	void NewSqlGuidsProducesSqlOrderedV8()
	{
		// Act
		var ids = GuidV8Time.NewSqlGuids(50, _fixedTimestamp);
		// Assert
		ids.Length.ShouldBe(50);
		foreach (var id in ids)
			id.ToByteArray().IsSqlRfc9562Version(8).ShouldBeTrue();
	}

	[Fact]
	void ConcurrentFillProducesNoDuplicates()
	{
		// Arrange - 22-bit counter space; keep total well under 2^22
		const int Threads = 8;
		const int PerThread = 10_000;
		var batches = new Guid[Threads][];
		// Act
		Parallel.For(0, Threads, t => batches[t] = GuidV8Time.NewGuids(PerThread, _fixedTimestamp));
		// Assert
		batches.SelectMany(b => b).Distinct().Count().ShouldBe(Threads * PerThread);
	}

	[Fact]
	void BulkAndSingleCallInterleaveStayUnique()
	{
		// Single-first then bulk is the order that exposed the off-by-one block
		// reservation — with no per-item entropy, a reused slot is a byte-for-byte
		// duplicate GUID on V8Time. Keep singles on both sides of the bulk call.
		var before = Enumerable.Range(0, 100).Select(_ => GuidV8Time.NewGuid(_fixedTimestamp)).ToArray();
		var bulk = GuidV8Time.NewGuids(100, _fixedTimestamp);
		var after = Enumerable.Range(0, 100).Select(_ => GuidV8Time.NewGuid(_fixedTimestamp)).ToArray();
		before.Concat(bulk).Concat(after).Distinct().Count().ShouldBe(300);
	}
}
#endif
