# SequentialGuid vNext — Design

**Status:** Approved
**Author:** Buvy
**Date:** 2026-05-12

## Goal

Ship a major version of SequentialGuid that closes the "zero-allocation on modern .NET"
gap the README already claims, removes long-deprecated API, and is verified
Native-AOT compatible. Three independently reviewable PRs against a `vNext` branch.

## Non-goals

- New UUID versions (e.g. v6).
- Changes to the implicit `string`/`Guid` operators on the struct wrappers.
- Changes to the `SequentialGuidType` enum.
- AOT compatibility for the MongoDB companion package (driver-side concern).

## Delivery plan

Three sequential PRs against branch `vNext`. Each PR is reviewable and revertable
on its own.

1. **PR1 — perf.** Internal-only changes. No public API breakage. Benchmarks
   prove the win.
2. **PR2 — cleanup.** Drops obsolete API and the public `Timestamp` static
   properties. This is the actual breaking change.
3. **PR3 — AOT hardening.** csproj flags, analyzers, AOT smoke test, README
   update.

After PR3 merges, tag `vN.0.0` and publish.

---

## PR1 — Perf: zero-allocation generation paths

### Problem

`GuidV4.NewGuid`, `GuidV7.NewGuid(long)`, and `GuidV8Time.NewGuid(long)` each
allocate `new byte[16]` per call. On NET6+ this can be `stackalloc byte[16]`
flowing through `RandomNumberGenerator.Fill(Span<byte>)` and
`new Guid(ReadOnlySpan<byte>, bigEndian: true)`. Legacy targets keep the heap
allocation.

The struct wrappers also do redundant work: `SequentialGuid(SequentialGuidType)`
reads `GuidV7.Timestamp` (which truncates `DateTimeOffset.UtcNow` to milliseconds)
and passes it to `GuidV7.NewGuid(...)`. The constructor then sets
`Timestamp = ...` from the static property and `Value` from the result. The
companion ctor `SequentialGuid(Guid)` calls `Value.ToDateTime()` separately —
a second decode pass through the byte layout.

### Changes

**`Extensions/ByteArrayExtensions.cs`** — add `Span<byte>` overloads of
`SetRfc9562Version(byte)` and `SetRfc9562Variant()`. Currently both exist only
on the `byte[]` extension block.

**`GuidV4.NewGuid`** — on NET6+, replace `new byte[16]` with `stackalloc byte[16]`;
fill via `RandomNumberGenerator.Fill(Span<byte>)`; return
`new Guid(span, bigEndian: true)`. Legacy path unchanged.

**`GuidV7.NewGuid(long)`** — same pattern. The 6-byte random tail is filled with
`RandomNumberGenerator.Fill(span[10..])`. Timestamp and counter writes operate
on the span directly. Legacy path unchanged.

**`GuidV8Time.NewGuid(long)`** — same pattern. The 5-byte `MachinePid` array is
copied into the span via `MachinePid.AsSpan().CopyTo(span[11..])`. Legacy path
unchanged.

**`GuidV8Time` static constructor** — switch the `Environment.MachineName` hash
from SHA-512 to SHA-1. We use 3 of the digest bytes as a machine fingerprint;
this is not a security property. SHA-1 is faster on cold start and reduces the
included algorithm surface.

**`GuidNameBased.Create`** on NET6+ — request a fixed-size digest into a
`Span<byte>`:

```csharp
Span<byte> digest = stackalloc byte[32]; // SHA-256 max; SHA-1 fills first 20
hash.TryGetHashAndReset(digest, out _);
```

Operate on `digest[..16]` for the version/variant write and the final
`new Guid(...)`. **Important TFM gate:** `IncrementalHash.TryGetHashAndReset(Span<byte>, out int)`
is .NET 5+ / .NET Standard 2.1+ only. The Span path applies to **NET6+ only**.
netstandard2.0 keeps `GetHashAndReset()` and the heap byte array. The
`#if NETFRAMEWORK` branch that uses `HashAlgorithm.Create(algorithmName.Name)`
remains unchanged.

**Struct wrappers** — `SequentialGuid(SequentialGuidType type)` and
`SequentialSqlGuid(SequentialGuidType type)`:

```csharp
public SequentialGuid(SequentialGuidType type = SequentialGuidType.Rfc9562V7)
{
    Value = type switch
    {
        SequentialGuidType.Rfc9562V7      => GuidV7.NewGuid(),
        SequentialGuidType.Rfc9562V8Custom => GuidV8Time.NewGuid(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
    Timestamp = Value.ToDateTime().GetValueOrDefault();
}
```

Decode timestamp from `Value` once. Drops the parallel-read of
`GuidV{7,8}.Timestamp`. Same for `SequentialSqlGuid` with `NewSqlGuid()`.

### Verification

- Existing xUnit suite passes unchanged.
- `util/Benchmarks/GenerationBenchmarks` shows `Allocated: 0 B` for V4/V7/V8Time
  on net8.0/net9.0/net10.0.
- `util/Benchmarks/NameBenchmarks` shows allocation drop on the digest side
  (caller-supplied UTF-8 input bytes still allocate; that's caller-controlled).
- `util/Benchmarks/ConversionBenchmarks` unchanged (already zero-alloc).
- PR description includes a before/after benchmark table on .NET 10.

---

## PR2 — Cleanup: drop obsolete API and consolidate constructors

### Breaking deletions

- `src/SequentialGuid/Obsolete/` folder, in full:
  - `SequentialGuidGenerator.cs`
  - `SequentialGuidGeneratorBase.cs`
  - `SequentialSqlGuidGenerator.cs`
- Public static properties:
  - `GuidV7.Timestamp`
  - `GuidV8Time.Timestamp`
- `README.md` — delete the "Upgrade Guide" section (it documented the obsolete
  classes' replacement; once they're gone, the section is dead weight).

### Non-breaking consolidation

**New internal helper.** Add `src/SequentialGuid/Extensions/SequentialGuidByteOrder.cs`:

```csharp
internal static class SequentialGuidByteOrder
{
    // Returns true if `value` is a recognised sequential GUID in either
    // standard or SQL Server byte order. `wasSqlOrder` indicates which.
    // Encapsulates the V7/V8/legacy detection, the SQL-V8 false-positive
    // guard, and the byte-order disambiguation.
    internal static bool TryDetect(Guid value, out bool wasSqlOrder);
}
```

Implementation lifts the existing branching from `SequentialGuid(Guid)` and
`SequentialSqlGuid(Guid)` verbatim — including the SQL-V8 false-positive guard
that disambiguates by requiring a valid timestamp when both standard and SQL
detection fire on the same bytes.

**`SequentialGuid(Guid)`** collapses to:

```csharp
public SequentialGuid(Guid value)
{
    if (!SequentialGuidByteOrder.TryDetect(value, out var wasSqlOrder))
        throw new ArgumentException(
            "Guid must be a version 7, version 8, or legacy sequential guid in standard or SQL Server byte order.",
            nameof(value));
    Value = wasSqlOrder ? value.FromSqlGuid() : value;
    Timestamp = Value.ToDateTime().GetValueOrDefault();
}
```

**`SequentialSqlGuid(Guid)`** is the mirror: `Value = wasSqlOrder ? value : value.ToSqlGuid()`.

**`TicksExtensions.IsDateTime`** — loosen upper bound:

```csharp
internal bool IsDateTime =>
    value >= UnixEpochTicks &&
    value <= DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond; // 1s forward slack for clock skew
```

Prevents legitimate near-now IDs from failing validation when the system clock
moves backward during round-tripping. Lower bound unchanged.

### Tests

Existing suite passes unchanged. Add `test/SequentialGuid.Tests/TicksExtensionsTests.cs`
covering: now (passes), now + 500 ms (passes), now + 2 s (fails), UNIX epoch
(passes), UNIX epoch - 1 tick (fails).

Existing `SequentialSqlGuidStructTests` already covers the SQL-V8 false-positive
case; verify the consolidation in `TryDetect` preserves that behavior.

---

## PR3 — AOT hardening

### csproj flags

Add to all four production projects (`SequentialGuid`,
`SequentialGuid.EntityFrameworkCore`, `SequentialGuid.MongoDB`,
`SequentialGuid.NodaTime`):

```xml
<PropertyGroup Condition="'$(TargetFramework)' == 'net8.0' or '$(TargetFramework)' == 'net9.0' or '$(TargetFramework)' == 'net10.0'">
  <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

`IsAotCompatible=true` in .NET 8+ implies `IsTrimmable`, `EnableTrimAnalyzer`,
and `EnableAotAnalyzer`. Single flag is preferred over explicit list.

### Expected analyzer interactions

- **`SequentialGuid` (core)** — pure byte manipulation. Clean.
- **`SequentialGuid.EntityFrameworkCore`** — value converters use static
  abstract `T.Create(v)` from `ISequentialGuid<T>`. AOT-clean.
- **`SequentialGuid.NodaTime`** — extension methods on `Instant`/`Guid`,
  no reflection. Clean.
- **`SequentialGuid.MongoDB`** — uses `BsonSerializer.LookupSerializer<Guid>()`.
  MongoDB driver has its own AOT story. If analyzer warnings surface from the
  driver, document the limitation in the MongoDB package README; do not attempt
  to fix driver-internal issues.

If the AOT analyzer fires inside the core package, fix the offending code path
(not expected, but possible — e.g. if `IncrementalHash` has trim attributes that
surface through our usage).

### AOT smoke test

New project `test/SequentialGuid.AotSmokeTest/`. Console app, .NET 10 only,
exercises every public entry point:

- `GuidV4.NewGuid()`
- `GuidV5.Create(Guid, string)` + `(Guid, byte[])`
- `GuidV7.NewGuid()`, `NewGuid(DateTimeOffset)`, `NewGuid(DateTime)`, `NewGuid(long)`
- `GuidV7.NewSqlGuid()` (all overloads)
- `GuidV8Time.NewGuid()` (all overloads)
- `GuidV8Time.NewSqlGuid()` (all overloads)
- `GuidV8Name.Create(Guid, string)` + `(Guid, byte[])`
- `guid.ToSqlGuid()`, `guid.FromSqlGuid()`, `guid.ToDateTime()`
- `new SequentialGuid()`, `new SequentialGuid(SequentialGuidType.Rfc9562V8Custom)`,
  `new SequentialGuid(guid)`, `new SequentialGuid(stringValue)`
- Same matrix for `SequentialSqlGuid`
- `JsonSerializer.Serialize/Deserialize` round-trip with `AddSequentialGuidConverters()`

Each call asserts a basic invariant (non-default guid, parseable, timestamp
within expected range). Exit code 0 on success, non-zero on any failure.

### CI

Add a job to `.github/workflows/*.yml` that, on the .NET 10 matrix entry only:

```yaml
- name: AOT smoke test
  run: |
    dotnet publish test/SequentialGuid.AotSmokeTest -c Release -r linux-x64 -o ./aot-out
    ./aot-out/SequentialGuid.AotSmokeTest
```

Single platform is enough — same source code, same result. Reduces CI minutes.

### README updates

- **Highlights** section: add a bullet "**Native AOT compatible** — verified
  end-to-end with a published AOT smoke test in CI."
- Update the existing **Blazor WebAssembly** mention to read "explicit `browser`
  platform support and Native AOT compatibility for Blazor WebAssembly."

---

## Open questions

None — all scope decisions are locked in.

## References

- RFC 9562 — https://www.rfc-editor.org/rfc/rfc9562.html
- §5.4 (UUIDv4), §5.5 (UUIDv5), §5.7 (UUIDv7), §6.2 Method 1 (counter), Appendix B.1 (UUIDv8 time), Appendix B.2 (UUIDv8 name)
- Existing benchmarks: `util/Benchmarks/GenerationBenchmarks.cs`,
  `ConversionBenchmarks.cs`, `NameBenchmarks.cs`
- SQL Server GUID sort order: https://www.sqlbi.com/blog/alberto/2007/08/31/how-are-guids-sorted-by-sql-server/
