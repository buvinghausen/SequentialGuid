# SequentialGuid v6.2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the v6.2 "generation ergonomics" release: bulk generation, `TimeProvider` overloads, EF Core value generation, BCL comparison benchmarks, plus three riders (Mongo type option, legacy `GetInt32` bias fix, satellite AOT smoke tests).

**Architecture:** All changes are additive (SemVer minor). Bulk generation amortizes the three per-call costs (timestamp capture, counter increment, RNG fill) across a batch. EF gets four sealed `ValueGenerator` classes plus an opt-in `IModelFinalizingConvention`. Everything follows the repo's existing `#if` TFM-gating and zero-allocation patterns.

**Tech Stack:** .NET multi-target (net11.0/net10.0/net9.0/net8.0/net462/netstandard2.0 core; net10/9/8 EF), xUnit v3 + Shouldly on Microsoft.Testing.Platform, BenchmarkDotNet, EF Core 8/9/10, MongoDB driver.

**Spec:** `docs/superpowers/specs/2026-06-09-sequentialguid-v6.2-design.md` (approved). Branch `v6.2/generation-ergonomics` already exists with the spec committed — all work happens on it.

**Workflow constraint — NO GIT COMMITS:** The maintainer reviews and commits everything himself via GitHub Desktop. Do **not** run `git commit`, `git add`, or `git push` at any point. Each task ends at a checkpoint with changes left in the working tree; the suggested commit message is provided for the maintainer's use only.

**Conventions that gate every task (build fails otherwise):**
- Tabs for indentation. `TreatWarningsAsErrors` is on; **XML doc comments are required on every public member** (CS1591).
- Test methods are private `void` with `[Fact]`/`[Theory]` (IDE0051 suppressed in test projects). Shouldly assertions.
- Cite RFC 9562 sections in comments when touching UUID generation logic.
- Run targeted tests with `dotnet test <project> --framework net10.0`; the full multi-TFM suite runs in the final task.

---

### Task 1: GuidV7 bulk generation

**Files:**
- Modify: `src/SequentialGuid/GuidV7.cs`
- Test: `tests/unit/SequentialGuid.Tests/GuidV7BulkTests.cs` (create)

- [ ] **Step 1: Write the failing tests**

Create `tests/unit/SequentialGuid.Tests/GuidV7BulkTests.cs`:

```csharp
#if NET6_0_OR_GREATER
using System.Data.SqlTypes;
using System.Threading.Tasks;
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
		// Act - bulk and single-call generation share the same process-global counter
		var bulk = GuidV7.NewGuids(100, RfcTestVectorMs);
		var single = Enumerable.Range(0, 100).Select(_ => GuidV7.NewGuid(RfcTestVectorMs)).ToArray();
		// Assert
		bulk.Concat(single).Distinct().Count().ShouldBe(200);
	}
}
#endif
```

Note: the oversized-`destination` guard on `Fill` itself is exercised through `NewGuids`/`NewSqlGuids` (same code path); a direct `Fill` test would need a 1 GB array.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/unit/SequentialGuid.Tests --framework net10.0`
Expected: compile errors — `GuidV7.Fill`, `FillSql`, `NewGuids`, `NewSqlGuids` do not exist.

- [ ] **Step 3: Implement bulk generation in GuidV7**

In `src/SequentialGuid/GuidV7.cs`:

3a. Add to the top of the file (after the existing `using System.Runtime.CompilerServices;` line):

```csharp
#if NET6_0_OR_GREATER
using System.Buffers;
#endif
```

3b. Add the following block immediately before the closing brace of the `GuidV7` class (after the `NewGuid(long)` method):

```csharp
#if NET6_0_OR_GREATER
	// Scratch buffer threshold for the batch random region (6 bytes per item),
	// mirroring the GuidNameBased stackalloc/ArrayPool pattern.
	const int StackThreshold = 256;

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values sharing a single
	/// current-UTC-time capture, ordered by a contiguous block of monotonic counter slots.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	public static void Fill(Span<Guid> destination) =>
		Fill(destination, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values that all embed
	/// <paramref name="unixMilliseconds"/>, ordered by a contiguous block of monotonic counter slots.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <param name="unixMilliseconds">
	/// The number of milliseconds since 1970-01-01T00:00:00Z. Must be non-negative and
	/// fit in 48 bits (maximum value 281474976710655, valid until the year 10889 AD).
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="unixMilliseconds"/> is negative or exceeds the 48-bit maximum,
	/// or when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	[SkipLocalsInit]
	public static void Fill(Span<Guid> destination, long unixMilliseconds)
	{
		if (unixMilliseconds is < 0 or > 0x0000_FFFF_FFFF_FFFF)
			throw new ArgumentOutOfRangeException(nameof(unixMilliseconds),
				"Unix millisecond timestamp must be non-negative and fit within 48 bits.");
		if (destination.Length > 0x400_0000)
			throw new ArgumentOutOfRangeException(nameof(destination),
				"Batch size must not exceed the 26-bit counter space (67,108,864).");
		if (destination.IsEmpty)
			return;

		var count = destination.Length;
		// RFC 9562 §6.2 Method 1: reserve a contiguous block of counter slots so the
		// whole batch is ordered and concurrent callers can never collide.
		var start = Interlocked.Add(ref _counter, count) - count;

		// One RNG call covers every item's 6-byte random tail.
		var randLen = count * 6;
		Span<byte> stackBuf = stackalloc byte[StackThreshold];
		byte[]? rented = null;
		var rand = randLen <= StackThreshold
			? stackBuf[..randLen]
			: (rented = ArrayPool<byte>.Shared.Rent(randLen)).AsSpan(0, randLen);
		try
		{
			RandomNumberGenerator.Fill(rand);

			Span<byte> bytes = stackalloc byte[16];
			// unix_ts_ms: 48-bit big-endian millisecond timestamp (octets 0-5),
			// identical for every item — written once.
			bytes[0] = (byte)(unixMilliseconds >> 40);
			bytes[1] = (byte)(unixMilliseconds >> 32);
			bytes[2] = (byte)(unixMilliseconds >> 24);
			bytes[3] = (byte)(unixMilliseconds >> 16);
			bytes[4] = (byte)(unixMilliseconds >> 8);
			bytes[5] = (byte)unixMilliseconds;

			for (var i = 0; i < count; i++)
			{
				var counter = (start + i) & 0x3FFFFFF;

				// rand_a: upper 12 bits of 26-bit counter (octets 6-7)
				bytes[6] = (byte)(counter >> 22);
				bytes[7] = (byte)((counter >> 14) & 0xFF);

				// rand_b extension: lower 14 bits of counter (octets 8-9)
				bytes[8] = (byte)((counter >> 8) & 0x3F);
				bytes[9] = (byte)(counter & 0xFF);

				rand.Slice(i * 6, 6).CopyTo(bytes[10..]);

				bytes.SetRfc9562Version(7);
				bytes.SetRfc9562Variant();

				destination[i] = new(bytes, bigEndian: true);
			}
		}
		finally
		{
			if (rented is not null)
				ArrayPool<byte>.Shared.Return(rented);
		}
	}

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values in SQL Server byte
	/// order, sharing a single current-UTC-time capture.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination) =>
		FillSql(destination, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values in SQL Server byte
	/// order that all embed <paramref name="unixMilliseconds"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <param name="unixMilliseconds">
	/// The number of milliseconds since 1970-01-01T00:00:00Z. Must be non-negative and
	/// fit in 48 bits (maximum value 281474976710655, valid until the year 10889 AD).
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="unixMilliseconds"/> is negative or exceeds the 48-bit maximum,
	/// or when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination, long unixMilliseconds)
	{
		Fill(destination, unixMilliseconds);
		for (var i = 0; i < destination.Length; i++)
			destination[i] = destination[i].ToSqlGuid();
	}

	/// <summary>
	/// Creates an array of new UUID version 7 values sharing a single current-UTC-time capture.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 7 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space.
	/// </exception>
	public static Guid[] NewGuids(int count) =>
		NewGuids(count, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

	/// <summary>
	/// Creates an array of new UUID version 7 values that all embed <paramref name="unixMilliseconds"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <param name="unixMilliseconds">
	/// The number of milliseconds since 1970-01-01T00:00:00Z. Must be non-negative and
	/// fit in 48 bits (maximum value 281474976710655, valid until the year 10889 AD).
	/// </param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 7 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space,
	/// or when <paramref name="unixMilliseconds"/> is out of range.
	/// </exception>
	public static Guid[] NewGuids(int count, long unixMilliseconds)
	{
		if (count is < 0 or > 0x400_0000)
			throw new ArgumentOutOfRangeException(nameof(count),
				"Count must be between 0 and the 26-bit counter space (67,108,864).");
		if (count == 0)
			return [];
		var result = new Guid[count];
		Fill(result, unixMilliseconds);
		return result;
	}

	/// <summary>
	/// Creates an array of new UUID version 7 values in SQL Server byte order, sharing a
	/// single current-UTC-time capture.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <returns>An array of <paramref name="count"/> version 7 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space.
	/// </exception>
	public static Guid[] NewSqlGuids(int count) =>
		NewSqlGuids(count, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

	/// <summary>
	/// Creates an array of new UUID version 7 values in SQL Server byte order that all embed
	/// <paramref name="unixMilliseconds"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <param name="unixMilliseconds">
	/// The number of milliseconds since 1970-01-01T00:00:00Z. Must be non-negative and
	/// fit in 48 bits (maximum value 281474976710655, valid until the year 10889 AD).
	/// </param>
	/// <returns>An array of <paramref name="count"/> version 7 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space,
	/// or when <paramref name="unixMilliseconds"/> is out of range.
	/// </exception>
	public static Guid[] NewSqlGuids(int count, long unixMilliseconds)
	{
		var result = NewGuids(count, unixMilliseconds);
		for (var i = 0; i < result.Length; i++)
			result[i] = result[i].ToSqlGuid();
		return result;
	}
#endif
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/unit/SequentialGuid.Tests --framework net10.0`
Expected: PASS (all `GuidV7BulkTests` plus the existing suite).

- [ ] **Step 5: Build all TFMs to verify gating compiles**

Run: `dotnet build src/SequentialGuid/SequentialGuid.csproj`
Expected: 0 warnings, 0 errors on every TFM including net462/netstandard2.0 (which must not see the new members).

- [ ] **Step 6: Checkpoint — no commit**

Leave the changes in the working tree (maintainer commits via GitHub Desktop). Suggested message: `feat: add GuidV7 bulk generation (Fill/FillSql/NewGuids/NewSqlGuids)`

---

### Task 2: GuidV8Time bulk generation

**Files:**
- Modify: `src/SequentialGuid/GuidV8Time.cs`
- Test: `tests/unit/SequentialGuid.Tests/GuidV8TimeBulkTests.cs` (create)

- [ ] **Step 1: Write the failing tests**

Create `tests/unit/SequentialGuid.Tests/GuidV8TimeBulkTests.cs`:

```csharp
#if NET6_0_OR_GREATER
using System.Data.SqlTypes;
using System.Threading.Tasks;
using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class GuidV8TimeBulkTests
{
	// RFC 9562 Appendix A.6 moment expressed as a UTC DateTime
	static readonly DateTime FixedTimestamp = new(2022, 2, 22, 19, 22, 22, DateTimeKind.Utc);

	[Fact]
	void FillProducesValidVersionVariantAndTimestamp()
	{
		// Arrange
		var ids = new Guid[100];
		// Act
		GuidV8Time.Fill(ids, FixedTimestamp);
		// Assert
		foreach (var id in ids)
		{
			var bytes = id.ToByteArray();
			bytes.IsRfc9562Version(8).ShouldBeTrue();
			bytes.VariantIsRfc9562().ShouldBeTrue();
			// V8 time embeds full 100 ns tick precision
			id.ToDateTime().ShouldBe(FixedTimestamp);
		}
	}

	[Fact]
	void FillIsMonotonicallyOrdered()
	{
		// Arrange - same wrap-boundary exposure precedent as the V7 tests
		var ids = new Guid[1_000];
		// Act
		GuidV8Time.Fill(ids, FixedTimestamp);
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
		GuidV8Time.FillSql(ids, FixedTimestamp);
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
		var ids = GuidV8Time.NewGuids(50, FixedTimestamp);
		// Assert
		ids.Length.ShouldBe(50);
		foreach (var id in ids)
		{
			id.ToByteArray().IsRfc9562Version(8).ShouldBeTrue();
			id.ToDateTime().ShouldBe(FixedTimestamp);
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
		var ids = GuidV8Time.NewSqlGuids(50, FixedTimestamp);
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
		Parallel.For(0, Threads, t => batches[t] = GuidV8Time.NewGuids(PerThread, FixedTimestamp));
		// Assert
		batches.SelectMany(b => b).Distinct().Count().ShouldBe(Threads * PerThread);
	}

	[Fact]
	void BulkAndSingleCallInterleaveStayUnique()
	{
		// Act
		var bulk = GuidV8Time.NewGuids(100, FixedTimestamp);
		var single = Enumerable.Range(0, 100).Select(_ => GuidV8Time.NewGuid(FixedTimestamp)).ToArray();
		// Assert
		bulk.Concat(single).Distinct().Count().ShouldBe(200);
	}
}
#endif
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/unit/SequentialGuid.Tests --framework net10.0`
Expected: compile errors — `GuidV8Time.Fill`, `FillSql`, `NewGuids`, `NewSqlGuids` do not exist.

- [ ] **Step 3: Implement bulk generation in GuidV8Time**

Add the following block immediately before the closing brace of the `GuidV8Time` class in `src/SequentialGuid/GuidV8Time.cs` (after the internal `NewGuid(long)` method):

```csharp
#if NET6_0_OR_GREATER
	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values sharing a single
	/// current-UTC-time capture, ordered by a contiguous block of monotonic counter slots.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void Fill(Span<Guid> destination) =>
		FillCore(destination, DateTime.UtcNow.Ticks);

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values that all embed
	/// <paramref name="timestamp"/>, ordered by a contiguous block of monotonic counter slots.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <param name="timestamp">
	/// The timestamp to embed in every UUID. Must have <see cref="DateTimeKind.Utc"/> or
	/// <see cref="DateTimeKind.Local"/> kind, with a value between January 1st, 1970 UTC and now.
	/// </param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void Fill(Span<Guid> destination, DateTime timestamp)
	{
		var ticks = timestamp.Kind switch
		{
			DateTimeKind.Utc => timestamp.Ticks, // use ticks as is
			DateTimeKind.Local => timestamp.ToUniversalTime().Ticks, // convert to UTC
			_ => throw new ArgumentException("DateTimeKind.Unspecified not supported", nameof(timestamp))
		};
		if (!ticks.IsDateTime)
			throw new ArgumentException("Timestamp must be between January 1st, 1970 UTC and now",
				nameof(timestamp));
		FillCore(destination, ticks);
	}

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values in SQL Server byte
	/// order, sharing a single current-UTC-time capture.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination) =>
		FillSqlCore(destination, DateTime.UtcNow.Ticks);

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values in SQL Server byte
	/// order that all embed <paramref name="timestamp"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <param name="timestamp">
	/// The timestamp to embed in every UUID. Must have <see cref="DateTimeKind.Utc"/> or
	/// <see cref="DateTimeKind.Local"/> kind, with a value between January 1st, 1970 UTC and now.
	/// </param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination, DateTime timestamp)
	{
		Fill(destination, timestamp);
		for (var i = 0; i < destination.Length; i++)
			destination[i] = destination[i].ToSqlGuid();
	}

	/// <summary>
	/// Creates an array of new UUID version 8 values sharing a single current-UTC-time capture.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 8 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewGuids(int count)
	{
		ValidateCount(count);
		if (count == 0)
			return [];
		var result = new Guid[count];
		FillCore(result, DateTime.UtcNow.Ticks);
		return result;
	}

	/// <summary>
	/// Creates an array of new UUID version 8 values that all embed <paramref name="timestamp"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <param name="timestamp">
	/// The timestamp to embed in every UUID. Must have <see cref="DateTimeKind.Utc"/> or
	/// <see cref="DateTimeKind.Local"/> kind, with a value between January 1st, 1970 UTC and now.
	/// </param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 8 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewGuids(int count, DateTime timestamp)
	{
		ValidateCount(count);
		if (count == 0)
			return [];
		var result = new Guid[count];
		Fill(result, timestamp);
		return result;
	}

	/// <summary>
	/// Creates an array of new UUID version 8 values in SQL Server byte order, sharing a
	/// single current-UTC-time capture.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <returns>An array of <paramref name="count"/> version 8 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewSqlGuids(int count)
	{
		var result = NewGuids(count);
		for (var i = 0; i < result.Length; i++)
			result[i] = result[i].ToSqlGuid();
		return result;
	}

	/// <summary>
	/// Creates an array of new UUID version 8 values in SQL Server byte order that all embed
	/// <paramref name="timestamp"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <param name="timestamp">
	/// The timestamp to embed in every UUID. Must have <see cref="DateTimeKind.Utc"/> or
	/// <see cref="DateTimeKind.Local"/> kind, with a value between January 1st, 1970 UTC and now.
	/// </param>
	/// <returns>An array of <paramref name="count"/> version 8 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timestamp"/> has <see cref="DateTimeKind.Unspecified"/> kind,
	/// or when its value is outside the valid range.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewSqlGuids(int count, DateTime timestamp)
	{
		var result = NewGuids(count, timestamp);
		for (var i = 0; i < result.Length; i++)
			result[i] = result[i].ToSqlGuid();
		return result;
	}

	static void ValidateCount(int count)
	{
		if (count is < 0 or > 0x40_0000)
			throw new ArgumentOutOfRangeException(nameof(count),
				"Count must be between 0 and the 22-bit counter space (4,194,304).");
	}

	static void FillSqlCore(Span<Guid> destination, long timestamp)
	{
		FillCore(destination, timestamp);
		for (var i = 0; i < destination.Length; i++)
			destination[i] = destination[i].ToSqlGuid();
	}

	[SkipLocalsInit]
	static void FillCore(Span<Guid> destination, long timestamp)
	{
		if (destination.Length > 0x40_0000)
			throw new ArgumentOutOfRangeException(nameof(destination),
				"Batch size must not exceed the 22-bit counter space (4,194,304).");
		if (destination.IsEmpty)
			return;

		var count = destination.Length;
		// RFC 9562 §6.2 Method 1: reserve a contiguous block of counter slots so the
		// whole batch is ordered and concurrent callers can never collide.
		var start = Interlocked.Add(ref _increment, count) - count;

		Span<byte> bytes = stackalloc byte[16];
		// custom_a: timestamp bits [59:12] → octets 0-5; custom_b: bits [11:0] → octets 6-7.
		// Identical for every item — written once, as is the machine/pid fingerprint.
		bytes[0] = (byte)(timestamp >> 52);
		bytes[1] = (byte)(timestamp >> 44);
		bytes[2] = (byte)(timestamp >> 36);
		bytes[3] = (byte)(timestamp >> 28);
		bytes[4] = (byte)(timestamp >> 20);
		bytes[5] = (byte)(timestamp >> 12);
		bytes[6] = (byte)((timestamp >> 8) & 0x0F);
		bytes[7] = (byte)timestamp;
		bytes[11] = _machinePid[0];
		bytes[12] = _machinePid[1];
		bytes[13] = _machinePid[2];
		bytes[14] = _machinePid[3];
		bytes[15] = _machinePid[4];
		bytes.SetRfc9562Version(8); // octet 6 is per-batch; version set once

		for (var i = 0; i < count; i++)
		{
			var increment = (start + i) & 0x003fffff;

			// custom_c: increment[21:0] → octets 8-10 (variant takes upper 2 bits of octet 8)
			bytes[8] = (byte)((increment >> 16) & 0x3F);
			bytes[9] = (byte)(increment >> 8);
			bytes[10] = (byte)increment;
			bytes.SetRfc9562Variant(); // octet 8 is rewritten per item

			destination[i] = new(bytes, bigEndian: true);
		}
	}
#endif
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/unit/SequentialGuid.Tests --framework net10.0`
Expected: PASS.

- [ ] **Step 5: Build all TFMs**

Run: `dotnet build src/SequentialGuid/SequentialGuid.csproj`
Expected: 0 warnings, 0 errors.

- [ ] **Step 6: Checkpoint — no commit**

Leave the changes in the working tree (maintainer commits via GitHub Desktop). Suggested message: `feat: add GuidV8Time bulk generation (Fill/FillSql/NewGuids/NewSqlGuids)`

---

### Task 3: TimeProvider overloads (single-call + bulk, both generators)

**Files:**
- Modify: `src/SequentialGuid/GuidV7.cs`
- Modify: `src/SequentialGuid/GuidV8Time.cs`
- Test: `tests/unit/SequentialGuid.Tests/TimeProviderTests.cs` (create)

- [ ] **Step 1: Write the failing tests**

Create `tests/unit/SequentialGuid.Tests/TimeProviderTests.cs`:

```csharp
#if NET8_0_OR_GREATER
using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class TimeProviderTests
{
	// RFC 9562 Appendix A.6 moment
	static readonly DateTimeOffset FixedNow = new(2022, 2, 22, 19, 22, 22, TimeSpan.Zero);
	const long RfcTestVectorMs = 1645557742000L;

	sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}

	static readonly TimeProvider Fixed = new FixedTimeProvider(FixedNow);

	[Fact]
	void GuidV7NewGuidEmbedsProviderTime() =>
		GuidV7.NewGuid(Fixed).ToUnixMs().ShouldBe(RfcTestVectorMs);

	[Fact]
	void GuidV7NewSqlGuidEmbedsProviderTime() =>
		GuidV7.NewSqlGuid(Fixed).FromSqlGuid().ToUnixMs().ShouldBe(RfcTestVectorMs);

	[Fact]
	void GuidV7NullProviderThrows() =>
		Should.Throw<ArgumentNullException>(() => GuidV7.NewGuid(null!));

	[Fact]
	void GuidV8TimeNewGuidEmbedsProviderTime() =>
		GuidV8Time.NewGuid(Fixed).ToDateTime().ShouldBe(FixedNow.UtcDateTime);

	[Fact]
	void GuidV8TimeNewSqlGuidEmbedsProviderTime() =>
		GuidV8Time.NewSqlGuid(Fixed).FromSqlGuid().ToDateTime().ShouldBe(FixedNow.UtcDateTime);

	[Fact]
	void GuidV8TimeNullProviderThrows() =>
		Should.Throw<ArgumentNullException>(() => GuidV8Time.NewGuid(null!));

	[Fact]
	void GuidV7FillEmbedsProviderTime()
	{
		var ids = new Guid[10];
		GuidV7.Fill(ids, Fixed);
		foreach (var id in ids)
			id.ToUnixMs().ShouldBe(RfcTestVectorMs);
	}

	[Fact]
	void GuidV7FillSqlEmbedsProviderTime()
	{
		var ids = new Guid[10];
		GuidV7.FillSql(ids, Fixed);
		foreach (var id in ids)
			id.FromSqlGuid().ToUnixMs().ShouldBe(RfcTestVectorMs);
	}

	[Fact]
	void GuidV7NewGuidsEmbedsProviderTime()
	{
		foreach (var id in GuidV7.NewGuids(10, Fixed))
			id.ToUnixMs().ShouldBe(RfcTestVectorMs);
	}

	[Fact]
	void GuidV7NewSqlGuidsEmbedsProviderTime()
	{
		foreach (var id in GuidV7.NewSqlGuids(10, Fixed))
			id.FromSqlGuid().ToUnixMs().ShouldBe(RfcTestVectorMs);
	}

	[Fact]
	void GuidV7FillNullProviderThrows()
	{
		Should.Throw<ArgumentNullException>(() =>
		{
			var ids = new Guid[1];
			GuidV7.Fill(ids, (TimeProvider)null!);
		});
	}

	[Fact]
	void GuidV8TimeFillEmbedsProviderTime()
	{
		var ids = new Guid[10];
		GuidV8Time.Fill(ids, Fixed);
		foreach (var id in ids)
			id.ToDateTime().ShouldBe(FixedNow.UtcDateTime);
	}

	[Fact]
	void GuidV8TimeFillSqlEmbedsProviderTime()
	{
		var ids = new Guid[10];
		GuidV8Time.FillSql(ids, Fixed);
		foreach (var id in ids)
			id.FromSqlGuid().ToDateTime().ShouldBe(FixedNow.UtcDateTime);
	}

	[Fact]
	void GuidV8TimeNewGuidsEmbedsProviderTime()
	{
		foreach (var id in GuidV8Time.NewGuids(10, Fixed))
			id.ToDateTime().ShouldBe(FixedNow.UtcDateTime);
	}

	[Fact]
	void GuidV8TimeNewSqlGuidsEmbedsProviderTime()
	{
		foreach (var id in GuidV8Time.NewSqlGuids(10, Fixed))
			id.FromSqlGuid().ToDateTime().ShouldBe(FixedNow.UtcDateTime);
	}

	[Fact]
	void GuidV8TimeFillNullProviderThrows()
	{
		Should.Throw<ArgumentNullException>(() =>
		{
			var ids = new Guid[1];
			GuidV8Time.Fill(ids, (TimeProvider)null!);
		});
	}
}
#endif
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/unit/SequentialGuid.Tests --framework net10.0`
Expected: compile errors — no `TimeProvider` overloads exist.

- [ ] **Step 3: Implement the GuidV7 overloads**

Add inside the `#if NET6_0_OR_GREATER` block added in Task 1 (just before its `#endif`) in `src/SequentialGuid/GuidV7.cs`:

```csharp
#if NET8_0_OR_GREATER
	/// <summary>
	/// Creates a new UUID version 7 using the current time of the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="provider">The clock supplying the timestamp.</param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	public static Guid NewGuid(TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewGuid(provider.GetUtcNow().ToUnixTimeMilliseconds());
	}

	/// <summary>
	/// Creates a new UUID version 7 using the current time of the supplied <see cref="TimeProvider"/>,
	/// with byte ordering suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <param name="provider">The clock supplying the timestamp.</param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	public static Guid NewSqlGuid(TimeProvider provider) =>
		NewGuid(provider).ToSqlGuid();

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values using a single
	/// timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	public static void Fill(Span<Guid> destination, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		Fill(destination, provider.GetUtcNow().ToUnixTimeMilliseconds());
	}

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 7 values in SQL Server byte
	/// order using a single timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 67,108,864 (2^26) elements.</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 26-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		FillSql(destination, provider.GetUtcNow().ToUnixTimeMilliseconds());
	}

	/// <summary>
	/// Creates an array of new UUID version 7 values using a single timestamp capture from the
	/// supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 7 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space.
	/// </exception>
	public static Guid[] NewGuids(int count, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewGuids(count, provider.GetUtcNow().ToUnixTimeMilliseconds());
	}

	/// <summary>
	/// Creates an array of new UUID version 7 values in SQL Server byte order using a single
	/// timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 67,108,864 (2^26).</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <returns>An array of <paramref name="count"/> version 7 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 26-bit counter space.
	/// </exception>
	public static Guid[] NewSqlGuids(int count, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewSqlGuids(count, provider.GetUtcNow().ToUnixTimeMilliseconds());
	}
#endif
```

- [ ] **Step 4: Implement the GuidV8Time overloads**

Add inside the `#if NET6_0_OR_GREATER` block added in Task 2 (just before its `#endif`) in `src/SequentialGuid/GuidV8Time.cs`:

```csharp
#if NET8_0_OR_GREATER
	/// <summary>
	/// Creates a new UUID version 8 using the current time of the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="provider">The clock supplying the timestamp.</param>
	/// <returns>A new time-ordered version 8 <see cref="Guid"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	public static Guid NewGuid(TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewGuid(provider.GetUtcNow().UtcDateTime);
	}

	/// <summary>
	/// Creates a new UUID version 8 using the current time of the supplied <see cref="TimeProvider"/>,
	/// with byte ordering suitable for storage in a SQL Server <c>uniqueidentifier</c> column.
	/// </summary>
	/// <param name="provider">The clock supplying the timestamp.</param>
	/// <returns>A new time-ordered version 8 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	public static Guid NewSqlGuid(TimeProvider provider) =>
		NewGuid(provider).ToSqlGuid();

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values using a single
	/// timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void Fill(Span<Guid> destination, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		Fill(destination, provider.GetUtcNow().UtcDateTime);
	}

	/// <summary>
	/// Fills <paramref name="destination"/> with new UUID version 8 values in SQL Server byte
	/// order using a single timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="destination">The span to fill. Must not exceed 4,194,304 (2^22) elements.</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> exceeds the 22-bit counter space.
	/// </exception>
	public static void FillSql(Span<Guid> destination, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		FillSql(destination, provider.GetUtcNow().UtcDateTime);
	}

	/// <summary>
	/// Creates an array of new UUID version 8 values using a single timestamp capture from the
	/// supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <returns>An array of <paramref name="count"/> time-ordered version 8 <see cref="Guid"/> values.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewGuids(int count, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewGuids(count, provider.GetUtcNow().UtcDateTime);
	}

	/// <summary>
	/// Creates an array of new UUID version 8 values in SQL Server byte order using a single
	/// timestamp capture from the supplied <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="count">The number of UUIDs to create. Must be between 0 and 4,194,304 (2^22).</param>
	/// <param name="provider">The clock supplying the shared timestamp.</param>
	/// <returns>An array of <paramref name="count"/> version 8 <see cref="Guid"/> values in SQL Server sort order.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative or exceeds the 22-bit counter space.
	/// </exception>
	public static Guid[] NewSqlGuids(int count, TimeProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		return NewSqlGuids(count, provider.GetUtcNow().UtcDateTime);
	}
#endif
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/unit/SequentialGuid.Tests --framework net10.0`
Expected: PASS.

- [ ] **Step 6: Build all TFMs**

Run: `dotnet build src/SequentialGuid/SequentialGuid.csproj`
Expected: 0 warnings, 0 errors (net462/netstandard2.0 must not see `TimeProvider`).

- [ ] **Step 7: Checkpoint — no commit**

Leave the changes in the working tree (maintainer commits via GitHub Desktop). Suggested message: `feat: add TimeProvider overloads to GuidV7 and GuidV8Time`

---

### Task 4: EF Core value generators

**Files:**
- Create: `src/SequentialGuid.EntityFrameworkCore/SequentialGuidValueGenerator.cs`
- Create: `src/SequentialGuid.EntityFrameworkCore/SequentialSqlGuidValueGenerator.cs`
- Create: `src/SequentialGuid.EntityFrameworkCore/SequentialGuidStructValueGenerator.cs`
- Create: `src/SequentialGuid.EntityFrameworkCore/SequentialSqlGuidStructValueGenerator.cs`
- Test: `tests/unit/SequentialGuid.EntityFrameworkCore.Tests/ValueGeneratorTests.cs` (create)

- [ ] **Step 1: Write the failing tests**

Create `tests/unit/SequentialGuid.EntityFrameworkCore.Tests/ValueGeneratorTests.cs`:

```csharp
namespace SequentialGuid.EntityFrameworkCore.Tests;

public sealed class ValueGeneratorTests
{
	// .NET ToByteArray() is mixed-endian: the RFC version nibble lands in byte[7]
	// for standard byte order and byte[8] for SQL Server byte order.
	static int StandardVersion(Guid id) => id.ToByteArray()[7] >> 4;
	static int SqlVersion(Guid id) => id.ToByteArray()[8] >> 4;

	[Fact]
	void SequentialGuidValueGeneratorProducesV7()
	{
		SequentialGuidValueGenerator generator = new();
		generator.GeneratesTemporaryValues.ShouldBeFalse();
		var id = generator.Next(null!);
		id.ShouldNotBe(Guid.Empty);
		StandardVersion(id).ShouldBe(7);
		id.IsSequentialGuid().ShouldBeTrue();
	}

	[Fact]
	void SequentialSqlGuidValueGeneratorProducesSqlOrderedV7()
	{
		SequentialSqlGuidValueGenerator generator = new();
		generator.GeneratesTemporaryValues.ShouldBeFalse();
		var id = generator.Next(null!);
		id.ShouldNotBe(Guid.Empty);
		SqlVersion(id).ShouldBe(7);
		id.IsSequentialGuid().ShouldBeTrue();
	}

	[Fact]
	void SequentialGuidStructValueGeneratorProducesValidStruct()
	{
		SequentialGuidStructValueGenerator generator = new();
		generator.GeneratesTemporaryValues.ShouldBeFalse();
		var value = generator.Next(null!);
		value.Value.ShouldNotBe(Guid.Empty);
		value.Timestamp.ShouldBeGreaterThan(DateTime.MinValue);
		StandardVersion(value.Value).ShouldBe(7);
	}

	[Fact]
	void SequentialSqlGuidStructValueGeneratorProducesValidStruct()
	{
		SequentialSqlGuidStructValueGenerator generator = new();
		generator.GeneratesTemporaryValues.ShouldBeFalse();
		var value = generator.Next(null!);
		value.Value.ShouldNotBe(Guid.Empty);
		value.Timestamp.ShouldBeGreaterThan(DateTime.MinValue);
		SqlVersion(value.Value).ShouldBe(7);
	}
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/unit/SequentialGuid.EntityFrameworkCore.Tests --framework net10.0`
Expected: compile errors — generator types do not exist.

- [ ] **Step 3: Implement the four generators**

Create `src/SequentialGuid.EntityFrameworkCore/SequentialGuidValueGenerator.cs`:

```csharp
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace SequentialGuid.EntityFrameworkCore;

/// <summary>
/// Generates RFC 9562 version 7 <see cref="Guid"/> key values in standard byte order.
/// </summary>
public sealed class SequentialGuidValueGenerator : ValueGenerator<Guid>
{
	/// <summary>Always <see langword="false"/> — generated keys are real, client-generated values.</summary>
	public override bool GeneratesTemporaryValues => false;

	/// <summary>Creates a new time-ordered version 7 <see cref="Guid"/>.</summary>
	/// <param name="entry">The change-tracking entry for the entity being assigned a key. Not used.</param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/>.</returns>
	public override Guid Next(EntityEntry entry) =>
		GuidV7.NewGuid();
}
```

Create `src/SequentialGuid.EntityFrameworkCore/SequentialSqlGuidValueGenerator.cs`:

```csharp
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace SequentialGuid.EntityFrameworkCore;

/// <summary>
/// Generates RFC 9562 version 7 <see cref="Guid"/> key values in SQL Server byte order,
/// suitable for <c>uniqueidentifier</c> clustered primary keys.
/// </summary>
public sealed class SequentialSqlGuidValueGenerator : ValueGenerator<Guid>
{
	/// <summary>Always <see langword="false"/> — generated keys are real, client-generated values.</summary>
	public override bool GeneratesTemporaryValues => false;

	/// <summary>Creates a new time-ordered version 7 <see cref="Guid"/> in SQL Server sort order.</summary>
	/// <param name="entry">The change-tracking entry for the entity being assigned a key. Not used.</param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	public override Guid Next(EntityEntry entry) =>
		GuidV7.NewSqlGuid();
}
```

Create `src/SequentialGuid.EntityFrameworkCore/SequentialGuidStructValueGenerator.cs`:

```csharp
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using SeqGuid = SequentialGuid.SequentialGuid;

namespace SequentialGuid.EntityFrameworkCore;

/// <summary>
/// Generates <see cref="SeqGuid"/> key values backed by RFC 9562 version 7 GUIDs.
/// </summary>
public sealed class SequentialGuidStructValueGenerator : ValueGenerator<SeqGuid>
{
	/// <summary>Always <see langword="false"/> — generated keys are real, client-generated values.</summary>
	public override bool GeneratesTemporaryValues => false;

	/// <summary>Creates a new <see cref="SeqGuid"/> wrapping a time-ordered version 7 GUID.</summary>
	/// <param name="entry">The change-tracking entry for the entity being assigned a key. Not used.</param>
	/// <returns>A new <see cref="SeqGuid"/>.</returns>
	public override SeqGuid Next(EntityEntry entry) =>
		new();
}
```

Create `src/SequentialGuid.EntityFrameworkCore/SequentialSqlGuidStructValueGenerator.cs`:

```csharp
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using SeqSqlGuid = SequentialGuid.SequentialSqlGuid;

namespace SequentialGuid.EntityFrameworkCore;

/// <summary>
/// Generates <see cref="SeqSqlGuid"/> key values backed by RFC 9562 version 7 GUIDs
/// in SQL Server byte order.
/// </summary>
public sealed class SequentialSqlGuidStructValueGenerator : ValueGenerator<SeqSqlGuid>
{
	/// <summary>Always <see langword="false"/> — generated keys are real, client-generated values.</summary>
	public override bool GeneratesTemporaryValues => false;

	/// <summary>Creates a new <see cref="SeqSqlGuid"/> wrapping a SQL-ordered version 7 GUID.</summary>
	/// <param name="entry">The change-tracking entry for the entity being assigned a key. Not used.</param>
	/// <returns>A new <see cref="SeqSqlGuid"/>.</returns>
	public override SeqSqlGuid Next(EntityEntry entry) =>
		new();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/unit/SequentialGuid.EntityFrameworkCore.Tests --framework net10.0`
Expected: PASS.

- [ ] **Step 5: Checkpoint — no commit**

Leave the changes in the working tree (maintainer commits via GitHub Desktop). Suggested message: `feat: add RFC 9562 v7 EF Core value generators`

---

### Task 5: EF Core value generation convention + registration extension

**Files:**
- Create: `src/SequentialGuid.EntityFrameworkCore/SequentialGuidValueGenerationConvention.cs`
- Modify: `src/SequentialGuid.EntityFrameworkCore/ModelConfigurationBuilderExtensions.cs`
- Test: `tests/unit/SequentialGuid.EntityFrameworkCore.Tests/ValueGenerationConventionTests.cs` (create)

- [ ] **Step 1: Write the failing tests**

Create `tests/unit/SequentialGuid.EntityFrameworkCore.Tests/ValueGenerationConventionTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using SeqGuid = SequentialGuid.SequentialGuid;
using SeqSqlGuid = SequentialGuid.SequentialSqlGuid;

namespace SequentialGuid.EntityFrameworkCore.Tests;

sealed class GuidKeyEntity
{
	public Guid Id { get; set; }
	public Guid Payload { get; set; }
}

sealed class StructKeyEntity
{
	public SeqGuid Id { get; set; }
}

sealed class SqlStructKeyEntity
{
	public SeqSqlGuid Id { get; set; }
}

sealed class ExplicitGeneratorEntity
{
	public Guid Id { get; set; }
}

sealed class CompositeKeyEntity
{
	public string Code { get; set; } = null!;
	public Guid Id { get; set; }
}

sealed class FixedGuidValueGenerator : ValueGenerator<Guid>
{
	internal static readonly Guid Fixed = new("11111111-1111-7111-8111-111111111111");

	public override bool GeneratesTemporaryValues => false;

	public override Guid Next(EntityEntry entry) =>
		Fixed;
}

sealed class ConventionDbContext(DbContextOptions<ConventionDbContext> options) : DbContext(options)
{
	public DbSet<GuidKeyEntity> GuidKeyEntities => Set<GuidKeyEntity>();
	public DbSet<StructKeyEntity> StructKeyEntities => Set<StructKeyEntity>();
	public DbSet<SqlStructKeyEntity> SqlStructKeyEntities => Set<SqlStructKeyEntity>();
	public DbSet<ExplicitGeneratorEntity> ExplicitGeneratorEntities => Set<ExplicitGeneratorEntity>();
	public DbSet<CompositeKeyEntity> CompositeKeyEntities => Set<CompositeKeyEntity>();

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		configurationBuilder.AddSequentialGuidValueConverters();
		configurationBuilder.UseSequentialGuidValueGeneration();
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ExplicitGeneratorEntity>()
			.Property(e => e.Id)
			.HasValueGenerator<FixedGuidValueGenerator>();
		modelBuilder.Entity<CompositeKeyEntity>()
			.HasKey(e => new { e.Code, e.Id });
	}
}

sealed class SqlConventionDbContext(DbContextOptions<SqlConventionDbContext> options) : DbContext(options)
{
	public DbSet<GuidKeyEntity> GuidKeyEntities => Set<GuidKeyEntity>();

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
		configurationBuilder.UseSequentialGuidValueGeneration(sqlServerByteOrder: true);
}

public sealed class ValueGenerationConventionTests
{
	static DbContextOptions<T> CreateOptions<T>(string dbName) where T : DbContext =>
		new DbContextOptionsBuilder<T>()
			.UseInMemoryDatabase(dbName)
			.Options;

	[Fact]
	void GuidPrimaryKeyIsGeneratedAsV7OnAdd()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(GuidPrimaryKeyIsGeneratedAsV7OnAdd)));
		GuidKeyEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Id.ShouldNotBe(Guid.Empty);
		(entity.Id.ToByteArray()[7] >> 4).ShouldBe(7);
		entity.Id.IsSequentialGuid().ShouldBeTrue();
	}

	[Fact]
	void NonKeyGuidPropertyIsNotGenerated()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(NonKeyGuidPropertyIsNotGenerated)));
		GuidKeyEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Payload.ShouldBe(Guid.Empty);
	}

	[Fact]
	void StructKeyIsGeneratedOnAdd()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(StructKeyIsGeneratedOnAdd)));
		StructKeyEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Id.Value.ShouldNotBe(Guid.Empty);
		(entity.Id.Value.ToByteArray()[7] >> 4).ShouldBe(7);
	}

	[Fact]
	void SqlStructKeyIsGeneratedOnAdd()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(SqlStructKeyIsGeneratedOnAdd)));
		SqlStructKeyEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Id.Value.ShouldNotBe(Guid.Empty);
		(entity.Id.Value.ToByteArray()[8] >> 4).ShouldBe(7);
	}

	[Fact]
	void ExplicitGeneratorWins()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(ExplicitGeneratorWins)));
		ExplicitGeneratorEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Id.ShouldBe(FixedGuidValueGenerator.Fixed);
	}

	[Fact]
	void CompositeKeyGuidMemberIsGenerated()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(CompositeKeyGuidMemberIsGenerated)));
		CompositeKeyEntity entity = new() { Code = "A" };
		db.Add(entity);
		db.SaveChanges();
		entity.Id.ShouldNotBe(Guid.Empty);
		(entity.Id.ToByteArray()[7] >> 4).ShouldBe(7);
	}

	[Fact]
	void SqlByteOrderFlagProducesSqlOrderedV7()
	{
		using var db = new SqlConventionDbContext(CreateOptions<SqlConventionDbContext>(nameof(SqlByteOrderFlagProducesSqlOrderedV7)));
		GuidKeyEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Id.ShouldNotBe(Guid.Empty);
		(entity.Id.ToByteArray()[8] >> 4).ShouldBe(7);
		entity.Id.IsSequentialGuid().ShouldBeTrue();
	}
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/unit/SequentialGuid.EntityFrameworkCore.Tests --framework net10.0`
Expected: compile error — `UseSequentialGuidValueGeneration` does not exist.

- [ ] **Step 3: Implement the convention**

Create `src/SequentialGuid.EntityFrameworkCore/SequentialGuidValueGenerationConvention.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using SeqGuid = SequentialGuid.SequentialGuid;
using SeqSqlGuid = SequentialGuid.SequentialSqlGuid;

namespace SequentialGuid.EntityFrameworkCore;

/// <summary>
/// Model-finalizing convention that assigns RFC 9562 v7 sequential value generators to every
/// <see cref="Guid"/>, <see cref="SeqGuid"/>, and <see cref="SeqSqlGuid"/> primary-key property
/// that has no explicitly configured generator.
/// </summary>
sealed class SequentialGuidValueGenerationConvention(bool sqlServerByteOrder) : IModelFinalizingConvention
{
	// Factories are stateless; share one instance per generator type across the model.
	static readonly Func<IProperty, ITypeBase, ValueGenerator> _guid =
		static (_, _) => new SequentialGuidValueGenerator();
	static readonly Func<IProperty, ITypeBase, ValueGenerator> _sqlGuid =
		static (_, _) => new SequentialSqlGuidValueGenerator();
	static readonly Func<IProperty, ITypeBase, ValueGenerator> _struct =
		static (_, _) => new SequentialGuidStructValueGenerator();
	static readonly Func<IProperty, ITypeBase, ValueGenerator> _sqlStruct =
		static (_, _) => new SequentialSqlGuidStructValueGenerator();

	public void ProcessModelFinalizing(
		IConventionModelBuilder modelBuilder,
		IConventionContext<IConventionModelBuilder> context)
	{
		foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
		{
			var key = entityType.FindPrimaryKey();
			if (key is null)
				continue;
			foreach (var property in key.Properties)
			{
				var factory = property.ClrType switch
				{
					var t when t == typeof(Guid) => sqlServerByteOrder ? _sqlGuid : _guid,
					var t when t == typeof(SeqGuid) => _struct,
					var t when t == typeof(SeqSqlGuid) => _sqlStruct,
					_ => null
				};
				// Skip non-matching types and anything already explicitly configured;
				// convention-source precedence means explicit config wins regardless.
				if (factory is null || property.GetValueGeneratorFactory() is not null)
					continue;
				property.Builder.HasValueGenerator(factory);
				property.Builder.ValueGenerated(ValueGenerated.OnAdd);
			}
		}
	}
}
```

- [ ] **Step 4: Add the registration extension**

In `src/SequentialGuid.EntityFrameworkCore/ModelConfigurationBuilderExtensions.cs`, add the following method inside the existing `extension(ModelConfigurationBuilder configurationBuilder)` block, after `AddSequentialGuidValueConverters()`:

```csharp
		/// <summary>
		/// Registers a model-finalizing convention that assigns RFC 9562 v7 sequential value
		/// generators to every <see cref="Guid"/>, <see cref="SeqGuid"/>, and <see cref="SeqSqlGuid"/>
		/// primary-key property, so keys are generated client-side on Add.
		/// Explicit per-property configuration always takes precedence.
		/// </summary>
		/// <param name="sqlServerByteOrder">
		/// When <see langword="true"/>, plain <see cref="Guid"/> keys are generated in SQL Server
		/// byte order (<c>uniqueidentifier</c> sort order). The struct types carry their byte
		/// order in the type itself and are unaffected.
		/// </param>
		public void UseSequentialGuidValueGeneration(bool sqlServerByteOrder = false) =>
			configurationBuilder.Conventions.Add(
				_ => new SequentialGuidValueGenerationConvention(sqlServerByteOrder));
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/unit/SequentialGuid.EntityFrameworkCore.Tests --framework net10.0`
Expected: PASS.

Note: if `IConventionPropertyBuilder.HasValueGenerator`'s delegate signature differs on the net8.0 build (EF Core 8.0.10), the build for that TFM will say so — verify with `dotnet build src/SequentialGuid.EntityFrameworkCore/SequentialGuid.EntityFrameworkCore.csproj` (all three TFMs must compile; `Func<IProperty, ITypeBase, ValueGenerator>` is the EF 8+ signature).

- [ ] **Step 6: Build and test all EF TFMs**

Run: `dotnet test tests/unit/SequentialGuid.EntityFrameworkCore.Tests`
Expected: PASS on net8.0, net9.0, and net10.0 test targets (the test project's net11.0/net472 targets do not reference the EF package — if the test project multi-targets beyond the package's TFMs, the project file already handles it; otherwise expect PASS on the three EF TFMs).

- [ ] **Step 7: Checkpoint — no commit**

Leave the changes in the working tree (maintainer commits via GitHub Desktop). Suggested message: `feat: add UseSequentialGuidValueGeneration model-finalizing convention`

---

### Task 6: Mongo configurable generator type

**Files:**
- Modify: `src/SequentialGuid.MongoDB/MongoSequentialGuidGenerator.cs`
- Test: `tests/unit/SequentialGuid.MongoDB.Tests/SequentialGuidMongoTests.cs` (modify — append tests)

**Spec note:** the spec mentions an optional `SequentialGuidType` parameter on `RegisterMongoIdGenerator()`. That extension is instance-based — the generator instance already carries its type, so a second type parameter would contradict it. Implement the constructor only; v7 registration reads `new MongoSequentialGuidGenerator(SequentialGuidType.Rfc9562V7).RegisterMongoIdGenerator()`. Flag this simplification in the PR description.

- [ ] **Step 1: Write the failing tests**

Append to `tests/unit/SequentialGuid.MongoDB.Tests/SequentialGuidMongoTests.cs` (inside the existing test class — open the file first and match its namespace/class):

```csharp
	[Fact]
	void DefaultGeneratorEmitsV8()
	{
		// Back-compat pin: Instance must keep emitting v8 time-based GUIDs.
		var id = (Guid)MongoSequentialGuidGenerator.Instance.GenerateId(null!, null!);
		// Mixed-endian ToByteArray(): RFC version nibble is the high nibble of byte[7]
		(id.ToByteArray()[7] >> 4).ShouldBe(8);
	}

	[Fact]
	void V7ConstructedGeneratorEmitsV7()
	{
		MongoSequentialGuidGenerator generator = new(SequentialGuidType.Rfc9562V7);
		var id = (Guid)generator.GenerateId(null!, null!);
		(id.ToByteArray()[7] >> 4).ShouldBe(7);
	}

	[Fact]
	void V8ConstructedGeneratorEmitsV8()
	{
		MongoSequentialGuidGenerator generator = new(SequentialGuidType.Rfc9562V8Custom);
		var id = (Guid)generator.GenerateId(null!, null!);
		(id.ToByteArray()[7] >> 4).ShouldBe(8);
	}

	[Fact]
	void UndefinedTypeThrows() =>
		Should.Throw<ArgumentOutOfRangeException>(() => new MongoSequentialGuidGenerator((SequentialGuidType)99));
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/unit/SequentialGuid.MongoDB.Tests --framework net10.0`
Expected: compile error — no constructor takes `SequentialGuidType`.

- [ ] **Step 3: Implement the configurable type**

Replace the class body of `MongoSequentialGuidGenerator` in `src/SequentialGuid.MongoDB/MongoSequentialGuidGenerator.cs` so the file reads:

```csharp
using MongoDB.Bson.Serialization;

namespace SequentialGuid.MongoDB;

/// <summary>
/// Implements <see cref="IIdGenerator"/> to generate sequential <see cref="Guid"/> values
/// for use as MongoDB document identifiers.
/// </summary>
public sealed class MongoSequentialGuidGenerator : IIdGenerator
{
	readonly SequentialGuidType _type;

	/// <summary>
	/// Initializes a generator emitting <see cref="SequentialGuidType.Rfc9562V8Custom"/> GUIDs —
	/// the historical default, preserving 100 ns tick precision.
	/// </summary>
	public MongoSequentialGuidGenerator() : this(SequentialGuidType.Rfc9562V8Custom) { }

	/// <summary>
	/// Initializes a generator emitting the specified sequential GUID type.
	/// </summary>
	/// <param name="type">The algorithm to use when generating document ids.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="type"/> is not a recognised <see cref="SequentialGuidType"/> value.</exception>
	public MongoSequentialGuidGenerator(SequentialGuidType type) =>
		_type = type switch
		{
			SequentialGuidType.Rfc9562V7 or SequentialGuidType.Rfc9562V8Custom => type,
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};

	/// <summary>
	/// Gets the singleton instance of the generator, emitting
	/// <see cref="SequentialGuidType.Rfc9562V8Custom"/> GUIDs.
	/// </summary>
	public static MongoSequentialGuidGenerator Instance { get; } = new();

	/// <summary>
	/// Generates a new sequential <see cref="Guid"/> as the document identifier.
	/// </summary>
	/// <param name="container">The container of the document being assigned an id.</param>
	/// <param name="document">The document being assigned an id.</param>
	/// <returns>A new sequential <see cref="Guid"/>.</returns>
	public object GenerateId(object container, object document) =>
		_type == SequentialGuidType.Rfc9562V7 ? GuidV7.NewGuid() : GuidV8Time.NewGuid();

	/// <summary>
	/// Determines whether the specified id is considered empty.
	/// </summary>
	/// <param name="id">The id value to test.</param>
	/// <returns><see langword="true"/> if <paramref name="id"/> is not a <see cref="Guid"/> or equals <see cref="Guid.Empty"/>; otherwise <see langword="false"/>.</returns>
	public bool IsEmpty(object id) =>
		id is not Guid guid || guid == Guid.Empty;
	// Pattern matching is life
	// Anything that isn't a guid is empty
	// Guid is considered not empty as long as it's not all 0s
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/unit/SequentialGuid.MongoDB.Tests --framework net10.0`
Expected: PASS.

- [ ] **Step 5: Checkpoint — no commit**

Leave the changes in the working tree (maintainer commits via GitHub Desktop). Suggested message: `feat: make MongoSequentialGuidGenerator type configurable (v8 default preserved)`

---

### Task 7: Legacy GetInt32 bias fix

**Files:**
- Modify: `src/SequentialGuid/Extensions/RandomNumberGeneratorExtensions.cs`
- Test: `tests/unit/SequentialGuid.Tests/RandomNumberGeneratorExtensionsTests.cs` (create)

- [ ] **Step 1: Write the test (legacy TFM only — `InternalsVisibleTo` covers SequentialGuid.Tests)**

Create `tests/unit/SequentialGuid.Tests/RandomNumberGeneratorExtensionsTests.cs`:

```csharp
#if NETFRAMEWORK
using System.Security.Cryptography;
using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class RandomNumberGeneratorExtensionsTests
{
	[Fact]
	void GetInt32StaysInRange()
	{
		using var rng = RandomNumberGenerator.Create();
		for (var i = 0; i < 10_000; i++)
		{
			var value = rng.GetInt32(500000);
			value.ShouldBeGreaterThanOrEqualTo(0);
			value.ShouldBeLessThan(500000);
		}
	}

	[Fact]
	void GetInt32OfOneReturnsZero()
	{
		using var rng = RandomNumberGenerator.Create();
		rng.GetInt32(1).ShouldBe(0);
	}

	[Fact]
	void GetInt32CanProduceZero()
	{
		// GetNonZeroBytes could never yield 0 from small ranges; the unbiased
		// implementation must cover the full [0, toExclusive) range.
		using var rng = RandomNumberGenerator.Create();
		var sawZero = false;
		for (var i = 0; i < 10_000 && !sawZero; i++)
			sawZero = rng.GetInt32(2) == 0;
		sawZero.ShouldBeTrue();
	}
}
#endif
```

- [ ] **Step 2: Run the test on the legacy target to capture current behavior**

Run: `dotnet test tests/unit/SequentialGuid.Tests --framework net472`
Expected: `GetInt32CanProduceZero` FAILS against the current implementation when `toExclusive` is small (`GetNonZeroBytes` never produces zero bytes, so `GetInt32(2)` can yield 0 only via the modulo fold — verify the failure; if it happens to pass due to the fold, the range/bias fix below is still correct and the test documents the contract).

- [ ] **Step 3: Implement the unbiased version**

Replace the `GetInt32` method body in `src/SequentialGuid/Extensions/RandomNumberGeneratorExtensions.cs` so the file reads:

```csharp
#if !NET6_0_OR_GREATER
using System.Security.Cryptography;

namespace SequentialGuid.Extensions;

static class RandomNumberGeneratorExtensions
{
	// Create matching signature for old RNG class
	extension(RandomNumberGenerator generator)
	{
		internal int GetInt32(int toExclusive)
		{
			// Unbiased mask-and-reject sampling: mask to the smallest power-of-two
			// range covering toExclusive, retry on overshoot. Expected iterations < 2.
			var mask = toExclusive - 1;
			mask |= mask >> 1;
			mask |= mask >> 2;
			mask |= mask >> 4;
			mask |= mask >> 8;
			mask |= mask >> 16;

			var bytes = new byte[sizeof(int)]; // 4 bytes
			int result;
			do
			{
				generator.GetBytes(bytes);
				result = BitConverter.ToInt32(bytes, 0) & mask;
			} while (result >= toExclusive);
			return result;
		}
	}
}
#endif
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/unit/SequentialGuid.Tests --framework net472`
Expected: PASS (all three new tests plus the existing net472 suite).

- [ ] **Step 5: Checkpoint — no commit**

Leave the changes in the working tree (maintainer commits via GitHub Desktop). Suggested message: `fix: remove sampling bias from legacy GetInt32 counter seeding`

---

### Task 8: BCL comparison + bulk benchmarks

**Files:**
- Create: `benches/Benchmarks/BclComparisonBenchmarks.cs`

No TDD cycle — benchmarks are measurement code, validated by running them.

- [ ] **Step 1: Create the benchmark class**

Create `benches/Benchmarks/BclComparisonBenchmarks.cs`:

```csharp
using BenchmarkDotNet.Attributes;

namespace SequentialGuid.Benchmarks;

/// <summary>
/// Compares GuidV7 generation against the BCL's <see cref="Guid.CreateVersion7()"/> and
/// <see cref="Guid.NewGuid()"/>, single-call and bulk.
/// Run with: dotnet run -c Release -- --filter *BclComparison*
/// </summary>
[MemoryDiagnoser]
public class BclComparisonBenchmarks
{
	readonly Guid[] _buffer = new Guid[1000];

	[Benchmark(Baseline = true, Description = "Guid.NewGuid")]
	public Guid SystemGuidNewGuid() =>
		Guid.NewGuid();

	[Benchmark(Description = "Guid.CreateVersion7")]
	public Guid SystemCreateVersion7() =>
		Guid.CreateVersion7();

	[Benchmark(Description = "GuidV7.NewGuid")]
	public Guid GuidV7NewGuid() =>
		GuidV7.NewGuid();

	[Benchmark(Description = "Guid.CreateVersion7 ×1000 loop")]
	public Guid[] BclLoop()
	{
		for (var i = 0; i < _buffer.Length; i++)
			_buffer[i] = Guid.CreateVersion7();
		return _buffer;
	}

	[Benchmark(Description = "GuidV7.NewGuid ×1000 loop")]
	public Guid[] SingleCallLoop()
	{
		for (var i = 0; i < _buffer.Length; i++)
			_buffer[i] = GuidV7.NewGuid();
		return _buffer;
	}

	[Benchmark(Description = "GuidV7.Fill ×1000 bulk")]
	public Guid[] BulkFill()
	{
		GuidV7.Fill(_buffer);
		return _buffer;
	}
}
```

- [ ] **Step 2: Run the benchmarks and save the output**

Run: `dotnet run -c Release --project benches/Benchmarks -- --filter *BclComparison*`
Expected: completes with a results table; `GuidV7.Fill ×1000 bulk` should beat both loops, and all GuidV7 rows should show `Allocated: 0 B` (or `-`) except the loop/bulk rows' shared pre-allocated buffer. **Save the results table — Task 11 pastes it into the README.**

- [ ] **Step 3: Checkpoint — no commit**

Leave the changes in the working tree (maintainer commits via GitHub Desktop). Suggested message: `feat: add BCL CreateVersion7 comparison benchmarks`

---

### Task 9: AOT smoke test — core additions + NodaTime

**Files:**
- Modify: `tests/smoke/SequentialGuid.AotSmokeTest/SequentialGuid.AotSmokeTest.csproj`
- Modify: `tests/smoke/SequentialGuid.AotSmokeTest/Program.cs`

- [ ] **Step 1: Add the NodaTime package reference**

In `tests/smoke/SequentialGuid.AotSmokeTest/SequentialGuid.AotSmokeTest.csproj`, replace the `<ItemGroup>` with:

```xml
	<ItemGroup>
		<ProjectReference Include="..\..\..\src\SequentialGuid\SequentialGuid.csproj" />
		<ProjectReference Include="..\..\..\src\SequentialGuid.NodaTime\SequentialGuid.NodaTime.csproj" />
	</ItemGroup>
```

- [ ] **Step 2: Add smoke checks**

In `tests/smoke/SequentialGuid.AotSmokeTest/Program.cs`, insert the following block after the v6.1 ergonomic extension checks (after the `Check("v4 IsSequentialGuid false", ...)` line) and before the `if (failures.Count == 0)` block:

```csharp
// v6.2 bulk generation
var bulk = GuidV7.NewGuids(8);
Check("v7 bulk count", bulk.Length == 8);
Check("v7 bulk sorted", bulk.SequenceEqual(bulk.OrderBy(g => g)));
Check("v7 bulk all valid", bulk.All(g => g.IsSequentialGuid()));

var bulkSql = GuidV7.NewSqlGuids(8);
Check("v7 bulk sql all valid", bulkSql.All(g => g.IsSequentialGuid()));

var bulkV8 = GuidV8Time.NewGuids(8);
Check("v8 bulk count", bulkV8.Length == 8);
Check("v8 bulk sorted", bulkV8.SequenceEqual(bulkV8.OrderBy(g => g)));

Span<Guid> fillSpan = stackalloc Guid[4];
GuidV7.Fill(fillSpan);
Check("v7 Fill span non-default", fillSpan[3] != Guid.Empty);

// v6.2 TimeProvider overloads
Check("v7 TimeProvider non-default", GuidV7.NewGuid(TimeProvider.System) != Guid.Empty);
Check("v8 TimeProvider non-default", GuidV8Time.NewGuid(TimeProvider.System) != Guid.Empty);

// NodaTime companion
var instant = NodaTime.SystemClock.Instance.GetCurrentInstant();
var v7FromInstant = GuidV7.NewGuid(instant);
Check("v7 from Instant non-default", v7FromInstant != Guid.Empty);
Check("v7 ToInstant roundtrip", NodaTime.GuidExtensions.ToInstant(v7FromInstant) is not null);
```

Note: `ToInstant` is an extension on `Guid` in the `NodaTime` namespace; the explicit static call avoids adding a file-level `using NodaTime;` that could conflict. If the C# 14 extension resolves cleanly with a `using NodaTime;`, prefer `v7FromInstant.ToInstant() is not null` instead.

- [ ] **Step 3: Publish and run the AOT smoke test**

Run: `dotnet publish tests/smoke/SequentialGuid.AotSmokeTest -c Release -r win-x64 -o aot-out; .\aot-out\SequentialGuid.AotSmokeTest.exe`
Expected: build with 0 trim/AOT warnings, output `AOT smoke test: PASS`, exit code 0.

- [ ] **Step 4: Checkpoint — no commit**

Leave the changes in the working tree (maintainer commits via GitHub Desktop). Suggested message: `test: extend AOT smoke test to bulk, TimeProvider, and NodaTime surface`

---

### Task 10: AOT smoke test — EF Core project + CI wiring

**Files:**
- Create: `tests/smoke/SequentialGuid.EntityFrameworkCore.AotSmokeTest/SequentialGuid.EntityFrameworkCore.AotSmokeTest.csproj`
- Create: `tests/smoke/SequentialGuid.EntityFrameworkCore.AotSmokeTest/Program.cs`
- Modify: `.github/workflows/ci.yml`
- Modify: `SequentialGuid.slnx` (add the new project — match how the existing smoke project is listed)

**Spec note:** the value converters are `internal`; the smoke test exercises the four **public** value generators (the new surface). The converters are covered by the build-time trim/AOT analyzers already on via `IsAotCompatible`.

- [ ] **Step 1: Create the project file**

Create `tests/smoke/SequentialGuid.EntityFrameworkCore.AotSmokeTest/SequentialGuid.EntityFrameworkCore.AotSmokeTest.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<OutputType>Exe</OutputType>
		<TargetFramework>net8.0</TargetFramework>
		<PublishAot>true</PublishAot>
		<!-- Enable trim/AOT analyzers at build time too, so IL2xxx/IL3xxx warnings surface in
		     ordinary builds (and in CI publish on platforms that lack the native linker). -->
		<IsAotCompatible>true</IsAotCompatible>
		<InvariantGlobalization>true</InvariantGlobalization>
		<IsPackable>false</IsPackable>
	</PropertyGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\..\src\SequentialGuid.EntityFrameworkCore\SequentialGuid.EntityFrameworkCore.csproj" />
	</ItemGroup>

</Project>
```

- [ ] **Step 2: Create the program**

Create `tests/smoke/SequentialGuid.EntityFrameworkCore.AotSmokeTest/Program.cs`:

```csharp
using SequentialGuid.EntityFrameworkCore;
// Disambiguate the SequentialGuid type from the SequentialGuid namespace.
using SgStruct = SequentialGuid.SequentialGuid;
using SsgStruct = SequentialGuid.SequentialSqlGuid;

IList<string> failures = [];

// The four public value generators are the new v6.2 surface; exercising them under ILC
// verifies our code paths trim cleanly. No DbContext — EF Core's full NativeAOT story is
// experimental and out of scope.
SequentialGuidValueGenerator guidGen = new();
var v7 = guidGen.Next(null!);
Check("Guid generator emits v7", v7 != Guid.Empty && v7.ToByteArray()[7] >> 4 == 7);
Check("Guid generator not temporary", !guidGen.GeneratesTemporaryValues);

SequentialSqlGuidValueGenerator sqlGen = new();
var sqlV7 = sqlGen.Next(null!);
Check("SQL generator emits SQL-ordered v7", sqlV7 != Guid.Empty && sqlV7.ToByteArray()[8] >> 4 == 7);

SequentialGuidStructValueGenerator structGen = new();
SgStruct sg = structGen.Next(null!);
Check("struct generator non-default", sg.Value != Guid.Empty);
Check("struct generator timestamp populated", sg.Timestamp > DateTime.MinValue);

SequentialSqlGuidStructValueGenerator sqlStructGen = new();
SsgStruct ssg = sqlStructGen.Next(null!);
Check("SQL struct generator non-default", ssg.Value != Guid.Empty);

if (failures.Count == 0)
{
	Console.WriteLine("EF Core AOT smoke test: PASS");
	return 0;
}

Console.WriteLine($"EF Core AOT smoke test: FAIL ({failures.Count} failures)");
foreach (var f in failures)
	Console.WriteLine($"  - {f}");
return 1;

void Check(string name, bool condition)
{
	if (!condition)
		failures.Add(name);
}
```

- [ ] **Step 3: Add the project to the solution**

Open `SequentialGuid.slnx`, find the entry for `tests/smoke/SequentialGuid.AotSmokeTest`, and add a sibling entry for `tests/smoke/SequentialGuid.EntityFrameworkCore.AotSmokeTest/SequentialGuid.EntityFrameworkCore.AotSmokeTest.csproj` in the same folder node, matching the existing XML shape exactly.

- [ ] **Step 4: Publish and run — contingency gate**

Run: `dotnet publish tests/smoke/SequentialGuid.EntityFrameworkCore.AotSmokeTest -c Release -r win-x64 -o aot-ef-out; .\aot-ef-out\SequentialGuid.EntityFrameworkCore.AotSmokeTest.exe`
Expected: publish with 0 IL2xxx/IL3xxx warnings, output `EF Core AOT smoke test: PASS`, exit code 0.

**Contingency (from the spec):** if ILC emits trim warnings originating in EF Core internals (not our code), delete this project, remove its slnx entry, skip the CI wiring for it in Step 5, and instead add this line to `src/SequentialGuid.EntityFrameworkCore/README.md` under a `## Native AOT` heading: "The package builds clean under the .NET trim/AOT analyzers (`IsAotCompatible`). End-to-end Native AOT publishing depends on EF Core's own (currently experimental) NativeAOT support." Then note the contingency was taken in the commit message and PR description.

- [ ] **Step 5: Extend the CI AOT step**

In `.github/workflows/ci.yml`, replace the `AOT smoke test` step with:

```yaml
    - name: AOT smoke test
      run: |
        dotnet publish tests/smoke/SequentialGuid.AotSmokeTest -c Release -r win-x64 -o aot-out
        .\aot-out\SequentialGuid.AotSmokeTest.exe
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        dotnet publish tests/smoke/SequentialGuid.EntityFrameworkCore.AotSmokeTest -c Release -r win-x64 -o aot-ef-out
        .\aot-ef-out\SequentialGuid.EntityFrameworkCore.AotSmokeTest.exe
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
      shell: pwsh
```

- [ ] **Step 6: Checkpoint — no commit**

Leave the changes in the working tree (maintainer commits via GitHub Desktop). Suggested message: `test: add EF Core AOT smoke test and wire into CI`

---

### Task 11: README updates (root + EF + Mongo)

**Files:**
- Modify: `README.md`
- Modify: `src/SequentialGuid.EntityFrameworkCore/README.md`
- Modify: `src/SequentialGuid.MongoDB/README.md`

- [ ] **Step 1: Root README — Key Features bullets**

In `README.md`, in the `## Key Features` list, add after the "**Monotonically increasing**" bullet:

```markdown
- **Bulk generation** — `GuidV7.Fill(Span<Guid>)` / `NewGuids(count)` amortize timestamp capture, counter reservation, and RNG across the batch (.NET 6+)
- **EF Core value generation** — one-line convention assigns RFC 9562 v7 generators to every Guid primary key
- **Testable clocks** — `TimeProvider` overloads on every generation path (.NET 8+)
```

- [ ] **Step 2: Root README — "Why not Guid.CreateVersion7?" section**

In `README.md`, insert before `## Contributing`:

```markdown
## Why not `Guid.CreateVersion7`?

.NET 9 added [`Guid.CreateVersion7`](https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7) — so why this library?

1. **No monotonic counter.** BCL v7 GUIDs generated within the same millisecond sort randomly relative to each other (RFC 9562 §6.2 Method 1 is not implemented). Under insert load that is exactly the index-fragmentation problem v7 adoption is meant to solve. SequentialGuid's process-global counter guarantees strict creation order, even across threads.
2. **No SQL Server byte-order story.** A BCL v7 GUID stored in a `uniqueidentifier` clustered index still fragments — SQL Server sorts GUIDs by its own byte order. `NewSqlGuid()`, `.ToSqlGuid()`, and `.FromSqlGuid()` handle that here.
3. **No round-trip tooling.** No timestamp extraction, no sequential-GUID detection, no strongly-typed wrappers.
4. **Reach.** `Guid.CreateVersion7` is .NET 9+; this library covers .NET Framework 4.6.2 and .NET Standard 2.0.

Benchmarks (.NET 10, Release — run `dotnet run -c Release --project benches/Benchmarks -- --filter *BclComparison*` on your own hardware):

<!-- Paste the BclComparisonBenchmarks results table from the Task 8 run here. -->
```

Then **replace the HTML comment with the actual results table saved in Task 8** (the committed README must contain real numbers, with the machine's CPU line from the BenchmarkDotNet header noted above the table).

- [ ] **Step 3: Root README — EF package blurb**

In the `### [SequentialGuid.EntityFrameworkCore]` section, replace the sentence "Value converters and JSON serialization support for the `SequentialGuid` and `SequentialSqlGuid` struct types." with:

```markdown
Value converters, value generation, and JSON serialization support for the `SequentialGuid` and `SequentialSqlGuid` struct types — plus a one-line convention that generates RFC 9562 v7 keys for every `Guid` primary key on Add.
```

and replace the code sample in that section with:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.AddSequentialGuidValueConverters();
    configurationBuilder.UseSequentialGuidValueGeneration();           // v7 keys on Add
    // configurationBuilder.UseSequentialGuidValueGeneration(sqlServerByteOrder: true);
}
```

(Root README code samples use spaces — match the file's existing samples.)

- [ ] **Step 4: EF package README**

Open `src/SequentialGuid.EntityFrameworkCore/README.md` and add a `## Value generation` section after the existing converter documentation:

```markdown
## Value generation

`UseSequentialGuidValueGeneration()` registers a model-finalizing convention that assigns
RFC 9562 v7 sequential value generators to every `Guid`, `SequentialGuid`, and
`SequentialSqlGuid` primary-key property. Keys are generated client-side on `Add` —
no database round-trip, retry-safe.

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.AddSequentialGuidValueConverters();
    configurationBuilder.UseSequentialGuidValueGeneration();
}
```

- Plain `Guid` keys get standard byte order; pass `sqlServerByteOrder: true` for
  `uniqueidentifier` clustered-index-friendly SQL Server byte order.
- `SequentialGuid` / `SequentialSqlGuid` keys carry their byte order in the type and
  ignore the flag.
- Explicit `HasValueGenerator<T>()` configuration always wins — the convention never
  overwrites it.
- The four generators (`SequentialGuidValueGenerator`, `SequentialSqlGuidValueGenerator`,
  `SequentialGuidStructValueGenerator`, `SequentialSqlGuidStructValueGenerator`) are
  public for explicit per-property wiring.
```

(Adjust the nested code fence to match the file's existing fence style if it already uses backticks inside sections.)

- [ ] **Step 5: Mongo package README**

Open `src/SequentialGuid.MongoDB/README.md` and add after the existing registration documentation:

```markdown
## Choosing the GUID version

`MongoSequentialGuidGenerator.Instance` emits RFC 9562 **v8 time-based** GUIDs — the
historical default, preserving full 100 ns tick precision in the document id. To emit
**v7** ids instead (the general recommendation elsewhere in this library):

```csharp
new MongoSequentialGuidGenerator(SequentialGuidType.Rfc9562V7).RegisterMongoIdGenerator();
```

The default stays v8 so existing applications keep their id semantics across upgrades.
```

- [ ] **Step 6: Verify markdown renders and commit**

Visually inspect the three files (fences balanced, tables intact). Leave the changes in the working tree (maintainer commits via GitHub Desktop). Suggested message: `docs: v6.2 README updates - bulk, EF generation, CreateVersion7 comparison`

---

### Task 12: Final verification + PR prep

**Files:** none new.

- [ ] **Step 1: Full clean build — all projects, all TFMs**

Run: `dotnet build`
Expected: 0 warnings, 0 errors (warnings are errors repo-wide).

- [ ] **Step 2: Full test suite — all TFMs**

Run: `dotnet test`
Expected: all tests pass on every target (net11.0/net10.0/net9.0/net8.0/net472 for core tests; net10/9/8 for EF; check the summary line shows zero failures).

- [ ] **Step 3: Both AOT smoke tests**

Run:
```powershell
dotnet publish tests/smoke/SequentialGuid.AotSmokeTest -c Release -r win-x64 -o aot-out; .\aot-out\SequentialGuid.AotSmokeTest.exe
dotnet publish tests/smoke/SequentialGuid.EntityFrameworkCore.AotSmokeTest -c Release -r win-x64 -o aot-ef-out; .\aot-ef-out\SequentialGuid.EntityFrameworkCore.AotSmokeTest.exe
```
Expected: both print `... PASS` and exit 0. (Skip the second if the Task 10 contingency was taken.)

- [ ] **Step 4: Verify zero-allocation claim on bulk Fill**

Run: `dotnet run -c Release --project benches/Benchmarks -- --filter *BclComparison*`
Expected: `GuidV7.Fill ×1000 bulk` allocates 0 B beyond the shared buffer and is faster than both loops. If the README table from Task 11 came from a dirty tree, re-paste from this clean run.

- [ ] **Step 5: Hand off to the maintainer — no push**

Do not push. The maintainer reviews the working tree in GitHub Desktop, commits (suggested messages are at each task's checkpoint), and pushes `v6.2/generation-ergonomics` himself.

- [ ] **Step 6: Output the PR title and body for manual creation (no `gh` CLI on this machine)**

Output exactly this for the maintainer to paste once he has pushed:

**Title:** `v6.2: bulk generation, TimeProvider overloads, EF Core value generation`

**Body:**

```markdown
## Summary

SemVer **minor** (v6.2.0) — all changes additive. Implements the approved spec at
`docs/superpowers/specs/2026-06-09-sequentialguid-v6.2-design.md`.

- **Bulk generation** (`Fill`/`FillSql`/`NewGuids`/`NewSqlGuids` on `GuidV7` and `GuidV8Time`, .NET 6+):
  one timestamp capture, one counter-block reservation, one RNG fill per batch. Loud
  `ArgumentOutOfRangeException` when a batch would exceed the counter space (2^26 v7 / 2^22 v8).
- **`TimeProvider` overloads** (.NET 8+) on every generation path, single-call and bulk.
- **EF Core value generation**: four public `ValueGenerator` classes plus
  `UseSequentialGuidValueGeneration(bool sqlServerByteOrder = false)` — a model-finalizing
  convention covering `Guid`, `SequentialGuid`, and `SequentialSqlGuid` primary keys.
  Explicit configuration always wins.
- **`MongoSequentialGuidGenerator`** gains a `SequentialGuidType` constructor; `Instance`
  default stays v8 (no silent behavior change). Note: the spec's optional type parameter on
  `RegisterMongoIdGenerator()` was dropped — the instance already carries its type.
- **Legacy `GetInt32` bias fix**: mask-and-reject sampling replaces `GetNonZeroBytes` + double-modulo.
- **AOT smoke tests** extended to bulk/TimeProvider/NodaTime; new EF Core AOT smoke project wired into CI.
- **README**: "Why not `Guid.CreateVersion7`?" section with benchmark table; new feature bullets.

## Test plan

- [ ] `dotnet build` — 0 warnings across all TFMs
- [ ] `dotnet test` — full multi-TFM suite green (new: bulk, TimeProvider, EF generator/convention, Mongo type, legacy GetInt32 tests)
- [ ] Both AOT smoke tests publish and exit 0 (CI-enforced)
- [ ] `BclComparisonBenchmarks` run on Release; README table reflects actual numbers

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

---

## Self-review notes (already applied)

- **Spec coverage:** §1 → Tasks 4–5; §2 → Tasks 1–2; §3 → Task 3; §4 → Tasks 8, 11; §5 riders → Tasks 6, 7, 9, 10; §6 testing → embedded per task; §7 release → Task 12.
- **Known deviations from spec (both flagged in PR body):** (1) `RegisterMongoIdGenerator()` keeps its parameterless signature — the instance carries the type; (2) the EF AOT smoke test exercises the public generators only — the converters are `internal` and remain covered by build-time analyzers.
- **Type consistency:** `Fill`/`FillSql`/`NewGuids`/`NewSqlGuids` names identical across `GuidV7`/`GuidV8Time`; generator class names identical between Task 4 (definitions), Task 5 (convention), and Task 10 (smoke test); `0x400_0000`/`0x40_0000` limits consistent between implementations and tests.
