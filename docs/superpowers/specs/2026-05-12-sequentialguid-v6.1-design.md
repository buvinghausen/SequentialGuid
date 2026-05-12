# SequentialGuid v6.1 — Design

**Status:** Approved
**Author:** Buvy
**Date:** 2026-05-12

## Goal

Close the last allocation gap in the deterministic generators (`GuidV5.Create`,
`GuidV8Name.Create`), pick up the free perf wins left on the table after v6.0,
and add four small ergonomic extension members to round out the public surface.

Single PR against `master`, SemVer minor (v6.1.0).

## Non-goals

- UUIDv6 support (deferred to a future release for RFC 9562 completeness).
- `TimeProvider` overloads, bulk-generation API, Newtonsoft.Json companion,
  DI helpers, companion-package AOT smoke tests (deferred — out of scope for
  this minor).
- RFC 9562 §6.2 counter-overflow handling (practical impact nil; deferred).
- `MachinePid` bulk-write micro-optimization (insignificant; deferred).

## Section 1 — Deterministic generator zero-allocation

### Problem

`GuidV5.Create(byte[])` and `GuidV8Name.Create(byte[])` allocate ~176 B per call
on .NET 6+. Sources:

- `IncrementalHash.CreateHash(algorithmName)` — ~120 B for the hash instance + state
- `namespaceId.ToByteArray(true)` — 16-byte heap array (~40 B with header)

### Fix

Rewrite `GuidNameBased.Create` on the NET6+ path to use one-shot static methods
`SHA1.HashData(ReadOnlySpan<byte>, Span<byte>)` and
`SHA256.HashData(ReadOnlySpan<byte>, Span<byte>)` (both .NET 5+, zero-alloc),
with a stack-allocated concat buffer for namespace+name. Names larger than a
256-byte threshold fall back to `ArrayPool<byte>.Shared.Rent`/`Return` — zero
net allocation after the pool warms up at process startup.

```csharp
internal static Guid Create(Guid namespaceId, byte[] name, HashAlgorithmName algorithmName, byte version)
{
#if NETFRAMEWORK
    // unchanged
#elif NET6_0_OR_GREATER
    const int StackThreshold = 256;
    var totalLen = 16 + name.Length;

    Span<byte> stackBuf = stackalloc byte[StackThreshold];
    byte[]? rented = null;
    var input = totalLen <= StackThreshold
        ? stackBuf[..totalLen]
        : (rented = ArrayPool<byte>.Shared.Rent(totalLen)).AsSpan(0, totalLen);
    try
    {
#if NET8_0_OR_GREATER
        namespaceId.TryWriteBytes(input[..16], bigEndian: true, out _);
#else
        // NET6/7: TryWriteBytes(span, bigEndian) doesn't exist; write native order then swap in-place
        namespaceId.TryWriteBytes(input[..16]);
        input[..16].SwapGuidBytesInPlace();
#endif
        name.AsSpan().CopyTo(input.Slice(16, name.Length));

        Span<byte> digest = stackalloc byte[32]; // SHA-256 max; SHA-1 fills first 20
        if (algorithmName == HashAlgorithmName.SHA1)
            SHA1.HashData(input, digest);
        else
            SHA256.HashData(input, digest);

        var head = digest[..16];
        head.SetRfc9562Version(version);
        head.SetRfc9562Variant();
        return new(head, bigEndian: true);
    }
    finally
    {
        if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
    }
#else // netstandard2.0
    // existing IncrementalHash + GetHashAndReset() path unchanged
    // (TryGetHashAndReset(Span<byte>, out int) is .NET 5+ / netstandard 2.1+)
#endif
}
```

**Tier breakdown:**

- **NETFRAMEWORK:** unchanged (uses `HashAlgorithm.Create(algorithmName.Name)`).
- **netstandard2.0:** unchanged (keeps `IncrementalHash` + `GetHashAndReset()`;
  the Span overload of `TryGetHashAndReset` is netstandard2.1+).
- **NET6+:** new zero-allocation path described above.
- **NET8+:** uses `Guid.TryWriteBytes(span, bigEndian: true, out _)` to write
  the namespace directly into the buffer in network order. NET6/7 falls back to
  `TryWriteBytes(span)` followed by an in-place byte-order swap.

### New helper

Add `SwapGuidBytesInPlace(this Span<byte>)` to `ByteArrayExtensions.cs` inside
the `extension(Span<byte> b)` block (added in v3.0). Mirrors the existing
`byte[].SwapByteOrder()` but operates on a 16-byte `Span<byte>` in place:

```csharp
internal void SwapGuidBytesInPlace()
{
    // Reverse Data1 (4 bytes), Data2 (2 bytes), Data3 (2 bytes); Data4 unchanged.
    (b[0], b[3]) = (b[3], b[0]);
    (b[1], b[2]) = (b[2], b[1]);
    (b[4], b[5]) = (b[5], b[4]);
    (b[6], b[7]) = (b[7], b[6]);
}
```

### Expected benchmark deltas

`util/Benchmarks/NameBenchmarks.cs`:

| Benchmark | Before (v6.0) | After (v6.1) |
|---|---|---|
| `GuidV5.Create(byte[])` 20-char name | 176 B | 0 B |
| `GuidV5.Create(byte[])` 71-char name | 176 B | 0 B |
| `GuidV8Name.Create(byte[])` 20-char name | 176 B | 0 B |
| `GuidV8Name.Create(byte[])` 71-char name | 176 B | 0 B |
| `GuidV5.Create(string)` | (unchanged — UTF-8 buffer is caller-controlled) | unchanged |

## Section 2 — Free perf wins

### `[SkipLocalsInit]` on generator hot paths

Apply to:
- `GuidV4.NewGuid()`
- `GuidV7.NewGuid(long)`
- `GuidV8Time.NewGuid(long)` (the internal core overload)
- `GuidNameBased.Create(...)`

Each method's `stackalloc byte[16]` (or `byte[32]` for the digest) buffer is
fully written before use — every byte index is explicitly assigned or filled
by `RandomNumberGenerator.Fill`. The JIT's automatic zero-init is redundant
work. The struct wrappers already have the attribute; extending to generators
is consistent.

The polyfill shim in `src/SequentialGuid/SkipLocalsInitPolyfill.cs` already
covers legacy TFMs.

### `GuidV7.NewGuid()` no-arg cleanup

```csharp
// Before
public static Guid NewGuid() =>
    NewGuid(DateTimeOffset.UtcNow);

public static Guid NewGuid(DateTimeOffset timestamp) =>
    NewGuid(timestamp.ToUnixTimeMilliseconds());

// After
public static Guid NewGuid() =>
    NewGuid(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public static Guid NewGuid(DateTimeOffset timestamp) =>
    NewGuid(timestamp.ToUnixTimeMilliseconds());
```

Saves one `DateTimeOffset` struct copy on the no-arg path.

### Flatten `#if` nest in `GuidNameBased`

After the Section 1 rewrite the file has three tiers (NETFRAMEWORK,
netstandard2.0, NET6+). Restructure as a single flat `#if/#elif/#else` chain
rather than nested conditionals. Pure readability — identical compile output.

## Section 3 — Ergonomic features

All four members are purely additive and live in
`src/SequentialGuid/Extensions/GuidExtensions.cs`. They join the existing
C# 14 `extension(Guid id) { ... }` block. The `MaxValue` member sits in a new
`extension(Guid) { ... }` (static) block.

```csharp
[SkipLocalsInit]
public static class GuidExtensions
{
    private static readonly Guid s_maxValue = new("ffffffff-ffff-ffff-ffff-ffffffffffff");

    extension(Guid)
    {
        /// <summary>Gets the RFC 9562 §5.10 max UUID — all bits set to 1.</summary>
        public static Guid MaxValue => s_maxValue;
    }

    extension(Guid id)
    {
        // (existing ToDateTime, ToSqlGuid, FromSqlGuid stay)

        /// <summary>Try-pattern variant of <see cref="ToDateTime"/>.</summary>
        public bool TryToDateTime(out DateTime timestamp)
        {
            if (id.ToDateTime() is { } dt)
            {
                timestamp = dt;
                return true;
            }
            timestamp = default;
            return false;
        }

        /// <summary>Extracts the embedded UTC timestamp as a <see cref="DateTimeOffset"/>.</summary>
        public DateTimeOffset? ToDateTimeOffset() =>
            id.ToDateTime() is { } dt ? new DateTimeOffset(dt, TimeSpan.Zero) : null;

        /// <summary>Try-pattern variant of <see cref="ToDateTimeOffset"/>.</summary>
        public bool TryToDateTimeOffset(out DateTimeOffset timestamp)
        {
            if (id.ToDateTime() is { } dt)
            {
                timestamp = new DateTimeOffset(dt, TimeSpan.Zero);
                return true;
            }
            timestamp = default;
            return false;
        }

        /// <summary>Returns true if <paramref name="id"/> is a recognised sequential GUID
        /// (V7, V8, or legacy SequentialGuid format) in either standard or SQL Server byte order.</summary>
        public bool IsSequentialGuid() =>
            SequentialGuidByteOrder.TryDetect(id, out _);
    }
}
```

**Notes:**

- `Guid.MaxValue` syntax via the static extension block. Cached in
  `s_maxValue` so the literal isn't reparsed. (Per RFC 9562 §5.10 / Max UUID.
  Naming follows the BCL convention `int.MaxValue` / `DateTime.MaxValue`.)
- `IsSequentialGuid()` delegates to the existing internal
  `SequentialGuidByteOrder.TryDetect` — no new logic.
- `ToDateTimeOffset()` uses `new DateTimeOffset(dt, TimeSpan.Zero)` because
  `ToDateTime()` already returns a `DateTime` with `Kind == Utc`.

## Section 4 — Testing & verification

### New test file

`test/SequentialGuid.Tests/GuidExtensionsTests.cs` (new):

- `MaxValueIsAllBitsSet` — equals `new Guid("ffffffff-...-ffffffffffff")`.
- `MaxValueIsNotEmpty` — not equal to `Guid.Empty`.
- `TryToDateTimeReturnsTrueForSequentialGuid` — V7 input → true + valid DateTime.
- `TryToDateTimeReturnsFalseForRandomGuid` — RFC §A.4 v4 vector → false + default.
- `TryToDateTimeReturnsFalseForEmpty` — false + default.
- `ToDateTimeOffsetReturnsUtcOffset` — non-null + `Offset == TimeSpan.Zero`.
- `ToDateTimeOffsetReturnsNullForRandomGuid`.
- `TryToDateTimeOffsetReturnsTrueForSequentialGuid` — true + non-default value.
- `TryToDateTimeOffsetReturnsFalseForRandomGuid`.
- `IsSequentialGuidTrueForV7` — V7 returns true.
- `IsSequentialGuidTrueForV8Time` — V8 returns true.
- `IsSequentialGuidTrueForLegacy` — known legacy guid string returns true.
- `IsSequentialGuidTrueForSqlOrderedV7` — V7 piped through `.ToSqlGuid()` returns true.
- `IsSequentialGuidFalseForRandomV4` — RFC §A.4 v4 vector returns false.
- `IsSequentialGuidFalseForEmpty` — `Guid.Empty` returns false.
- `IsSequentialGuidFalseForMaxValue` — `Guid.MaxValue` returns false.

Existing RFC test vectors in `GuidV5Tests.cs` and `GuidV8NameTests.cs` cover
deterministic-generator correctness — no new tests needed there. They are the
regression guard for the `IncrementalHash` → `HashData` swap.

### Benchmarks

`util/Benchmarks/NameBenchmarks.cs` — no code change. Re-run and confirm 0 B
allocated for both `(byte[])` overloads at both name lengths.

### AOT smoke test additions

`test/SequentialGuid.AotSmokeTest/Program.cs` — add `Check` lines:

```csharp
Check("Guid.MaxValue non-empty", Guid.MaxValue != Guid.Empty);

Check("v7 TryToDateTime true", v7.TryToDateTime(out var v7Dt) && v7Dt > DateTime.MinValue);
Check("v4 TryToDateTime false", !v4.TryToDateTime(out _));

Check("v7 ToDateTimeOffset Utc", v7.ToDateTimeOffset() is { Offset.Ticks: 0 });
Check("v4 ToDateTimeOffset null", v4.ToDateTimeOffset() is null);

Check("v7 IsSequentialGuid true", v7.IsSequentialGuid());
Check("v4 IsSequentialGuid false", !v4.IsSequentialGuid());
```

### Build + CI

No CI changes. Existing pipeline (Windows runner, .NET 8/9/10 SDK, AOT publish
on net10) covers the new code. Build expectations:

- `dotnet build` — 0 warnings on all 5 production TFMs.
- `dotnet test` — total grows by ~16 (one per assertion in
  `GuidExtensionsTests`), all pass.
- AOT smoke test still exits 0.

## Section 5 — Release shape

**SemVer:** minor — v6.1.0.

**PR shape:** single PR against `master` from a feature branch named
`v6.1/perf-and-ergonomics` (or maintainer's preferred convention).

**Suggested commit decomposition (one PR, multiple commits for revertability):**

1. `perf: stack-allocate concat buffer in GuidNameBased`
2. `perf: apply [SkipLocalsInit] to generator hot paths`
3. `perf: GuidV7.NewGuid() takes one struct copy fewer`
4. `refactor: flatten #if nest in GuidNameBased`
5. `feat: add Guid.MaxValue static extension property`
6. `feat: add TryToDateTime + ToDateTimeOffset extensions`
7. `feat: add public IsSequentialGuid predicate`
8. `test: cover new ergonomic extensions`
9. `test: extend AOT smoke test to new public surface`
10. `docs: update README — zero-alloc on all generators + new helpers`

### README updates

- Bump the Highlights bullet: "**Zero allocations on modern .NET** —
  `stackalloc`, `Span<T>`, and `[SkipLocalsInit]` eliminate heap allocations
  on **every** generation path (.NET 6+), including the deterministic v5/v8
  name-based generators."
- Add a brief example in the "Round-trip timestamp extraction" section showing
  `TryToDateTime` / `ToDateTimeOffset` / `IsSequentialGuid` usage.

## Open questions

None — scope is locked.

## References

- v6.0 spec: `docs/superpowers/specs/2026-05-12-sequentialguid-vnext-design.md`
- v6.0 plan: `docs/superpowers/plans/2026-05-12-sequentialguid-vnext.md`
- RFC 9562: https://www.rfc-editor.org/rfc/rfc9562.html
  - §5.10 (Max UUID), §6.6 (well-known namespaces)
- `IncrementalHash.TryGetHashAndReset(Span<byte>, out int)`: .NET 5+ / netstandard 2.1+
- `Guid.TryWriteBytes(Span<byte>, bool bigEndian, out int)`: .NET 8+
- `ArrayPool<byte>.Shared`: process-wide pool, allocation-free after warmup
