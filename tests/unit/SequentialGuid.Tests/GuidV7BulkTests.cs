#if NET6_0_OR_GREATER
using System.Data.SqlTypes;
using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class GuidV7BulkTests
{
	// RFC 9562 Appendix A.6 test vector: 1645557742000 = 0x017F22E279B0
	const long RfcTestVectorMs = 1645557742000L;

	[Fact]
	void FillProducesValidVersionVariantAndTimestamp()
	{
		// Arrange
		var ids = new Guid[100];
		// Act
		GuidV7.Fill(ids, RfcTestVectorMs);
		// Assert
		foreach (var id in ids)
		{
			var bytes = id.ToByteArray();
			bytes.IsRfc9562Version(7).ShouldBeTrue();
			bytes.VariantIsRfc9562().ShouldBeTrue();
			id.ToUnixMs().ShouldBe(RfcTestVectorMs);
		}
	}

	[Fact]
	void FillIsMonotonicallyOrdered()
	{
		// Arrange - same exposure to the 2^26 wrap boundary as the single-call
		// monotonic test in GuidV7Tests; identical precedent, identical odds.
		var ids = new Guid[1_000];
		// Act
		GuidV7.Fill(ids, RfcTestVectorMs);
		// Assert - the reserved counter block orders the batch
		Guid[] sorted = [.. ids.OrderBy(x => x)];
		sorted.ShouldBe(ids, ignoreOrder: false);
	}

	[Fact]
	void FillEmptyDestinationIsNoOp() =>
		GuidV7.Fill([]); // must not throw

	[Fact]
	void FillNegativeTimestampThrows()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
		{
			var ids = new Guid[1];
			GuidV7.Fill(ids, -1L);
		});
	}

	[Fact]
	void FillOverflowTimestampThrows()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
		{
			var ids = new Guid[1];
			GuidV7.Fill(ids, 0x0001_0000_0000_0000L);
		});
	}

	[Fact]
	void FillSqlProducesSqlOrderedV7()
	{
		// Arrange
		var ids = new Guid[100];
		// Act
		GuidV7.FillSql(ids, RfcTestVectorMs);
		// Assert
		foreach (var id in ids)
		{
			id.ToByteArray().IsSqlRfc9562Version(7).ShouldBeTrue();
			id.FromSqlGuid().ToUnixMs().ShouldBe(RfcTestVectorMs);
		}
	}

	[Fact]
	void FillSqlSortsInSqlServerOrder()
	{
		// Arrange
		var ids = new Guid[1_000];
		// Act
		GuidV7.FillSql(ids, RfcTestVectorMs);
		// Assert - SqlGuid comparison implements SQL Server uniqueidentifier ordering
		Guid[] sorted = [.. ids.OrderBy(g => new SqlGuid(g))];
		sorted.ShouldBe(ids, ignoreOrder: false);
	}

	[Fact]
	void NewGuidsMatchesFillSemantics()
	{
		// Act
		var ids = GuidV7.NewGuids(50, RfcTestVectorMs);
		// Assert
		ids.Length.ShouldBe(50);
		foreach (var id in ids)
		{
			id.ToByteArray().IsRfc9562Version(7).ShouldBeTrue();
			id.ToUnixMs().ShouldBe(RfcTestVectorMs);
		}
	}

	[Fact]
	void NewGuidsZeroCountReturnsEmpty() =>
		GuidV7.NewGuids(0).ShouldBeEmpty();

	[Fact]
	void NewGuidsNegativeCountThrows() =>
		Should.Throw<ArgumentOutOfRangeException>(() => GuidV7.NewGuids(-1));

	[Fact]
	void NewGuidsOversizedCountThrows() =>
		// 2^26 + 1 exceeds the RFC 9562 §6.2 Method 1 counter space; throws before allocating
		Should.Throw<ArgumentOutOfRangeException>(() => GuidV7.NewGuids(0x400_0001));

	[Fact]
	void NewSqlGuidsOversizedCountThrows() =>
		Should.Throw<ArgumentOutOfRangeException>(() => GuidV7.NewSqlGuids(0x400_0001));

	[Fact]
	void NewSqlGuidsProducesSqlOrderedV7()
	{
		// Act
		var ids = GuidV7.NewSqlGuids(50, RfcTestVectorMs);
		// Assert
		ids.Length.ShouldBe(50);
		foreach (var id in ids)
			id.ToByteArray().IsSqlRfc9562Version(7).ShouldBeTrue();
	}

	[Fact]
	void NoArgOverloadsEmbedCurrentTime()
	{
		// Arrange
		var before = DateTime.UtcNow.TruncateToMs();
		// Act
		var ids = GuidV7.NewGuids(10);
		var after = DateTime.UtcNow.TruncateToMs();
		// Assert - one timestamp capture for the whole batch
		var stamps = ids.Select(i => i.ToDateTime().GetValueOrDefault()).Distinct().ToArray();
		stamps.Length.ShouldBe(1);
		stamps[0].ShouldBeGreaterThanOrEqualTo(before);
		stamps[0].ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	void ConcurrentFillProducesNoDuplicates()
	{
		// Arrange
		const int Threads = 8;
		const int PerThread = 10_000;
		var batches = new Guid[Threads][];
		// Act - concurrent block reservations must never overlap
		Parallel.For(0, Threads, t => batches[t] = GuidV7.NewGuids(PerThread, RfcTestVectorMs));
		// Assert
		batches.SelectMany(b => b).Distinct().Count().ShouldBe(Threads * PerThread);
	}

	[Fact]
	void BulkAndSingleCallInterleaveStayUnique()
	{
		// Single-first then bulk is the order that exposed the off-by-one block
		// reservation (bulk[0] reusing the prior single call's counter slot) — keep
		// singles on both sides of the bulk call.
		var before = Enumerable.Range(0, 100).Select(_ => GuidV7.NewGuid(RfcTestVectorMs)).ToArray();
		var bulk = GuidV7.NewGuids(100, RfcTestVectorMs);
		var after = Enumerable.Range(0, 100).Select(_ => GuidV7.NewGuid(RfcTestVectorMs)).ToArray();
		before.Concat(bulk).Concat(after).Distinct().Count().ShouldBe(300);
	}
}
#endif
