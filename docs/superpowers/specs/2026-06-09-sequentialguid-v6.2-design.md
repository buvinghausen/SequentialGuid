# SequentialGuid v6.2 — Design

**Status:** Approved
**Author:** Buvy
**Date:** 2026-06-09

## Goal

Ship a "generation ergonomics" minor release: EF Core value generation, bulk
generation, `TimeProvider` overloads, and a head-to-head benchmark answer to
"why not just use `Guid.CreateVersion7`?" — plus three small riders (Mongo
generator type option, legacy `GetInt32` bias fix, satellite AOT smoke tests).

Everything is additive. SemVer minor — v6.2.0. Single PR against `master`.

## Non-goals

- Newtonsoft.Json companion package — permanently out. STJ is in the BCL;
  anyone still on Newtonsoft owns that decision themselves. No dependency drift.
- UUIDv6 (still deferred).
- Buffered/thread-local CSPRNG pool for single-call generation (benchmark-driven
  decision for a future release; bulk generation covers the high-throughput case).
- `TimeProvider` parameters on the `SequentialGuid`/`SequentialSqlGuid` struct
  constructors — deterministic struct construction already exists via
  `new SequentialGuid(guid)`.
- Generated values for non-key `Guid` properties in the EF convention — explicit
  per-property `HasValueGenerator<T>()` covers that.
- DI helpers (still deferred).
- `Microsoft.Bcl.TimeProvider` dependency for legacy TFMs — zero-dep stays zero-dep.

---

## Section 1 — EF Core value generation

The EF package converts today but never generates. EF Core's built-in
`SequentialGuidValueGenerator` produces a non-RFC, SQL-Server-specific pattern;
this section gives EF users real RFC 9562 v7 keys with zero ceremony.

The package targets net8.0/net9.0/net10.0 with EF Core 8/9/10 — the convention
API (`ModelConfigurationBuilder.Conventions`) is EF 7+, so no TFM gating is
needed anywhere in this section.

### New generators

Four sealed, non-generic classes in `SequentialGuid.EntityFrameworkCore`. No
generics: the generic alternative (`new T()` / static abstract dispatch) buys
nothing at this arity and costs AOT/readability clarity.

| File | Class | Base | Produces |
|---|---|---|---|
| `SequentialGuidValueGenerator.cs` | `SequentialGuidValueGenerator` | `ValueGenerator<Guid>` | `GuidV7.NewGuid()` |
| `SequentialSqlGuidValueGenerator.cs` | `SequentialSqlGuidValueGenerator` | `ValueGenerator<Guid>` | `GuidV7.NewSqlGuid()` |
| `SequentialGuidStructValueGenerator.cs` | `SequentialGuidStructValueGenerator` | `ValueGenerator<SequentialGuid>` | `new SequentialGuid()` |
| `SequentialSqlGuidStructValueGenerator.cs` | `SequentialSqlGuidStructValueGenerator` | `ValueGenerator<SequentialSqlGuid>` | `new SequentialSqlGuid()` |

All four: `public sealed`, `GeneratesTemporaryValues => false` (real,
client-generated, retry-safe keys), XML docs on every public member.

### New convention

`SequentialGuidValueGenerationConvention.cs` — `internal sealed`, implements
`IModelFinalizingConvention`. Constructor takes `bool sqlServerByteOrder`.

`ProcessModelFinalizing` walks every entity type and, for each **primary-key**
property:

- CLR type `Guid` → assign `SequentialGuidValueGenerator`, or
  `SequentialSqlGuidValueGenerator` when `sqlServerByteOrder` is `true`.
- CLR type `SequentialGuid` → assign `SequentialGuidStructValueGenerator` and
  set `ValueGenerated.OnAdd` (EF only defaults that for raw `Guid` keys).
- CLR type `SequentialSqlGuid` → assign `SequentialSqlGuidStructValueGenerator`
  and set `ValueGenerated.OnAdd`.

The struct types carry their byte order in the type itself, so the
`sqlServerByteOrder` flag only affects plain `Guid` keys.

All assignments go through the convention builder API (`IConventionPropertyBuilder`)
so explicit user configuration wins automatically — the convention never
overwrites an explicitly configured generator or `ValueGenerated` setting.
A property that already has a value generator factory is skipped.

### Registration extension

`ModelConfigurationBuilderExtensions.cs` (existing file) gains:

```csharp
extension(ModelConfigurationBuilder configurationBuilder)
{
	/// <summary>
	/// Registers a model-finalizing convention that assigns RFC 9562 v7 sequential
	/// value generators to every Guid, SequentialGuid, and SequentialSqlGuid primary key.
	/// </summary>
	public void UseSequentialGuidValueGeneration(bool sqlServerByteOrder = false) =>
		configurationBuilder.Conventions.Add(
			_ => new SequentialGuidValueGenerationConvention(sqlServerByteOrder));
}
```

Usage:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
	configurationBuilder.AddSequentialGuidValueConverters();
	configurationBuilder.UseSequentialGuidValueGeneration();
	// or: UseSequentialGuidValueGeneration(sqlServerByteOrder: true);
}
```

---

## Section 2 — Bulk generation

Both time-based generators (`GuidV7`, `GuidV8Time`) gain a zero-alloc span
primitive and an array convenience, **NET6+ only**. Legacy TFMs
(net462/netstandard2.0) keep looping `NewGuid()` — `Span<Guid>` there would
require System.Memory, which breaks the zero-dependency story.

### Surface

Four methods per class, each in exactly three timestamp forms:

| Method | Returns | Byte order |
|---|---|---|
| `Fill(Span<Guid> destination, ...)` | `void` | standard |
| `FillSql(Span<Guid> destination, ...)` | `void` | SQL Server |
| `NewGuids(int count, ...)` | `Guid[]` | standard |
| `NewSqlGuids(int count, ...)` | `Guid[]` | SQL Server |

Timestamp forms (the overload rule — deliberately not the full mirror of the
single-call matrix, to keep 24 methods from becoming 60):

1. No-arg — current UTC time, captured **once** for the whole batch.
2. The deterministic primitive — `long unixMilliseconds` for `GuidV7`
   (48-bit validation as today), `DateTime timestamp` for `GuidV8Time`
   (Kind/range validation as today).
3. `TimeProvider provider` — `#if NET8_0_OR_GREATER` (see Section 3).

`NewGuids`/`NewSqlGuids` allocate only the result array and delegate to
`Fill`/`FillSql`.

### Algorithm

Per call, regardless of batch size:

1. **One timestamp capture.** Every UUID in the batch shares it; the counter
   provides intra-batch ordering.
2. **One counter-block reservation.**
   `var end = Interlocked.Add(ref _counter, count); var start = end - count;`
   — item *i* uses slot `(start + i) & mask` (mask `0x3FFFFFF` for V7,
   `0x3FFFFF` for V8Time). Concurrent bulk and single-call generation interleave
   safely because they draw from the same process-global counter.
3. **One `RandomNumberGenerator.Fill`** over the batch's entire random region
   (V7 only — 6 bytes per item; V8Time has no per-item random bytes, its tail
   is the machine/process fingerprint). Scratch buffer is `stackalloc` up to a
   256-byte threshold (mirroring the `GuidNameBased` pattern), `ArrayPool<byte>.Shared`
   above it — `Fill` is 0 B steady-state.
4. **Per-slot assembly** identical to the single-call path: big-endian layout,
   `SetRfc9562Version` / `SetRfc9562Variant`, `new Guid(bytes, bigEndian: true)`,
   with the SQL variants applying `WriteToSqlByteOrder` before construction.

Cite RFC 9562 §6.2 Method 1 in the counter-reservation comment, per repo
convention.

### Validation — fail loudly

- `count < 0` → `ArgumentOutOfRangeException`.
- `count` / `destination.Length` greater than the counter space (67,108,864 for
  V7; 4,194,304 for V8Time) → `ArgumentOutOfRangeException`. A batch that would
  wrap its own counter within a single timestamp is a caller bug; we do not
  silently emit out-of-order IDs.
- Zero-length destination / `count == 0` → no-op / `Array.Empty<Guid>()`.
- Timestamp validation identical to the existing single-call overloads.

---

## Section 3 — `TimeProvider` overloads

All gated `#if NET8_0_OR_GREATER` (`TimeProvider` enters the BCL in .NET 8; no
new dependencies for older TFMs).

Single-call:

- `GuidV7.NewGuid(TimeProvider provider)` / `GuidV7.NewSqlGuid(TimeProvider provider)`
  → `NewGuid(provider.GetUtcNow())`.
- `GuidV8Time.NewGuid(TimeProvider provider)` / `GuidV8Time.NewSqlGuid(TimeProvider provider)`
  → `NewGuid(provider.GetUtcNow().UtcDateTime)`.

Bulk: the third timestamp form on every Section 2 method.

`ArgumentNullException.ThrowIfNull(provider)` before any other work. The
existing `long unixMilliseconds` overloads remain the recommended path for
fully deterministic tests; `TimeProvider` is for production code already built
around an injected clock (and for `FakeTimeProvider` in tests).

---

## Section 4 — BCL comparison benchmarks + README

### Benchmarks

New `benches/Benchmarks/BclComparisonBenchmarks.cs`, `[MemoryDiagnoser]` like
the rest:

- Single: `GuidV7.NewGuid()` vs `Guid.CreateVersion7()` vs `Guid.NewGuid()`.
- Bulk: `GuidV7.NewGuids(1000)` vs a 1000-iteration `Guid.CreateVersion7()` loop
  vs a 1000-iteration `GuidV7.NewGuid()` loop (isolates the amortization win).

The benchmark project targets net10.0, so `Guid.CreateVersion7` (net9+) needs
no guard.

### README

New section "Why not `Guid.CreateVersion7`?" in the root README, with the
benchmark table (real numbers from a release run, hardware noted) and the
substantive differences:

1. **No monotonic counter** — BCL v7 IDs generated within the same millisecond
   sort randomly relative to each other; under insert load that is exactly the
   index-fragmentation problem v7 adoption is meant to solve. RFC 9562 §6.2
   Method 1 is implemented here, not there.
2. **No SQL Server byte-order story** — no `NewSqlGuid()`/`ToSqlGuid()`
   equivalent; a BCL v7 in a `uniqueidentifier` clustered index still fragments.
3. **No round-trip tooling** — no timestamp extraction, no sequential-GUID
   detection, no struct wrappers.
4. **Reach** — `Guid.CreateVersion7` is .NET 9+; this library covers net462
   and netstandard2.0.

Also: add bullets for bulk generation and EF value generation to Key Features,
and update the EF package blurb + package README for the new generator surface.

---

## Section 5 — Riders

### Mongo configurable generator type

`MongoSequentialGuidGenerator` gains a public constructor taking
`SequentialGuidType` (existing enum; `Rfc9562V7` or `Rfc9562V8Custom`), wired
through `GenerateId`. `Instance` keeps emitting `GuidV8Time` — changing the
default in a minor release is a silent behavior change, which this library does
not do. `RegisterMongoIdGenerator()` gains an optional
`SequentialGuidType type = SequentialGuidType.Rfc9562V8Custom` parameter.
The Mongo package README documents the v7 option and why the default stays v8
(100 ns tick precision; historical compatibility).

### Legacy `GetInt32` bias fix

`RandomNumberGeneratorExtensions.GetInt32` (compiled for legacy TFMs only) is
biased twice over: `GetNonZeroBytes` excludes zero bytes entirely, and
double-modulo folds unevenly. Replace with `GetBytes` + mask-and-reject:

```csharp
internal int GetInt32(int toExclusive)
{
	// Smallest power-of-two-minus-one mask covering toExclusive - 1
	var mask = toExclusive - 1;
	mask |= mask >> 1;
	mask |= mask >> 2;
	mask |= mask >> 4;
	mask |= mask >> 8;
	mask |= mask >> 16;

	var bytes = new byte[sizeof(int)];
	int result;
	do
	{
		generator.GetBytes(bytes);
		result = BitConverter.ToInt32(bytes, 0) & mask;
	} while (result >= toExclusive);
	return result;
}
```

Unbiased; expected iterations < 2. Only used for counter seeding at type
initialization, so no measurable cost. No API change.

### Satellite AOT smoke tests

- **NodaTime:** add the `SequentialGuid.NodaTime` project reference to the
  existing `tests/smoke/SequentialGuid.AotSmokeTest` app plus `Check` lines:
  `GuidV7.NewGuid(Instant)` round-trips through `ToInstant()`, SQL variant
  detection still works.
- **EF Core:** new minimal console project
  `tests/smoke/SequentialGuid.EntityFrameworkCore.AotSmokeTest`. It instantiates
  the two value converters and four value generators directly and exercises
  their delegates/`Next` — **no `DbContext`**, because EF Core's full NativeAOT
  support is still experimental and the goal is verifying *our* code under ILC,
  not the framework's. **Contingency:** if ILC warnings from EF internals
  surface even at this scope, drop the EF smoke project, rely on the build-time
  trim/AOT analyzers already enabled via `IsAotCompatible`, and document the
  limitation in the EF package README.
- **Mongo:** excluded — driver-side concern, unchanged from the vNext decision.
- **CI:** the existing AOT publish/run step extends to the EF smoke app
  (single platform, same as today).

---

## Section 6 — Testing

### EF Core (`tests/unit/SequentialGuid.EntityFrameworkCore.Tests`)

- `ValueGeneratorTests.cs` — each generator's output: correct version nibble
  and variant bits, correct byte order (SQL variants validate via
  `FromSqlGuid().ToDateTime()`), `GeneratesTemporaryValues == false`,
  struct generators produce non-default values with valid timestamps.
- `ValueGenerationConventionTests.cs` — using an in-memory model build:
  - `Guid` PK gets `SequentialGuidValueGenerator`; with
    `sqlServerByteOrder: true` gets the SQL variant.
  - `SequentialGuid` / `SequentialSqlGuid` PKs get their struct generators and
    `ValueGenerated.OnAdd`.
  - Explicit `HasValueGenerator<T>()` is not overwritten.
  - Non-key `Guid` properties are untouched.
  - Composite keys containing a `Guid` property are covered (assigned).

### Core (`tests/unit/SequentialGuid.Tests`)

- `GuidV7BulkTests.cs` / `GuidV8TimeBulkTests.cs`:
  - Strict ascending sort order within a batch (standard and SQL order,
    validated with the existing fixture style).
  - Deterministic-timestamp batches embed exactly the supplied timestamp.
  - Concurrent `Fill` calls across threads produce zero duplicates.
  - Interleaved single-call + bulk generation stays globally unique.
  - `count < 0`, oversized batch → `ArgumentOutOfRangeException`; empty → no-op.
  - `NewGuids(n)` equals a `Fill` over an `n`-array semantically (length,
    validity).
- `TimeProvider` tests (net8+ TFMs, folded into `GuidV7Tests`/`GuidV8TimeTests`):
  a fixed `FakeTimeProvider`-style stub produces the exact embedded timestamp;
  null provider throws. No wall-clock dependence per repo test conventions.
- Legacy `GetInt32`: range test on the net472 test target (all results in
  `[0, toExclusive)` over many draws).

### Mongo (`tests/unit/SequentialGuid.MongoDB.Tests`)

- Generator constructed with `Rfc9562V7` emits v7; default/`Instance` still
  emits v8 (back-compat pin).

### Benchmarks / smoke

- `BclComparisonBenchmarks` runs clean; README table produced from a Release run.
- Both AOT smoke apps publish and exit 0 in CI.

---

## Section 7 — Release shape

- **SemVer:** minor — v6.2.0. All changes additive; no breaking surface.
- **PR shape:** single PR against `master` from branch `v6.2/generation-ergonomics`,
  multiple commits for revertability (perf/feat/test/docs split as in v6.1).
- **Workflow note:** no `gh` CLI on the dev machine — push the branch and output
  PR title + raw markdown body for manual paste.

## Open questions

None — scope locked 2026-06-09.

## References

- v6.1 spec: `docs/superpowers/specs/2026-05-12-sequentialguid-v6.1-design.md`
  (deferred-items list that seeded this release)
- RFC 9562: https://www.rfc-editor.org/rfc/rfc9562.html — §5.7 (UUIDv7),
  §6.2 Method 1 (fixed bit-length dedicated counter), Appendix B.1 (UUIDv8 time)
- EF Core value generation: https://learn.microsoft.com/en-us/ef/core/modeling/generated-properties
- `IModelFinalizingConvention`: https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.metadata.conventions.imodelfinalizingconvention
- `TimeProvider`: https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider
- `Guid.CreateVersion7`: https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7
