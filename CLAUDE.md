# CLAUDE.md

## Project Overview

**SequentialGuid** is a .NET library that generates database-friendly, time-ordered UUIDs
compliant with [RFC 9562](https://www.rfc-editor.org/rfc/rfc9562.html). It eliminates clustered
index fragmentation in SQL Server and other databases, embeds timestamps directly in the UUID,
and supports client-side ID generation without a server round-trip.

### Packages

| Package | Description |
|---|---|
| `SequentialGuid` | Core library — zero dependencies |
| `SequentialGuid.EntityFrameworkCore` | EF Core value converters and conventions |
| `SequentialGuid.MongoDB` | MongoDB BSON serializers |
| `SequentialGuid.NodaTime` | NodaTime `Instant`/`ZonedDateTime` overloads |

## Repository Structure

    SequentialGuid/
    ├── src/
    │   ├── SequentialGuid/                     # Core library
    │   │   ├── GuidV4.cs                       # Cryptographically random UUID (v4)
    │   │   ├── GuidV5.cs                       # Deterministic UUID using SHA-1 (v5)
    │   │   ├── GuidV7.cs                       # Time-ordered UUID, 48-bit Unix ms + 26-bit counter (v7)
    │   │   ├── GuidV8Name.cs                   # Deterministic UUID using SHA-256 (v8)
    │   │   ├── GuidV8Time.cs                   # Time-ordered UUID, 60-bit .NET Ticks (v8)
    │   │   ├── SequentialGuid.cs               # Strongly-typed struct wrapper (ISequentialGuid<T>)
    │   │   ├── SequentialSqlGuid.cs            # SQL Server byte-order struct wrapper
    │   │   └── Extensions/
    │   │       ├── ByteArrayExtensions.cs      # Internal: byte order swaps, RFC bit helpers
    │   │       └── GuidExtensions.cs           # Public: ToDateTime(), ToSqlGuid(), etc.
    │   ├── SequentialGuid.EntityFrameworkCore/
    │   ├── SequentialGuid.MongoDB/
    │   └── SequentialGuid.NodaTime/
    ├── test/
    │   └── SequentialGuid.Tests/               # xUnit test project
    └── util/
        └── Benchmarks/                         # BenchmarkDotNet benchmarks

## Target Frameworks

All projects multi-target:

- `.NET 10`, `.NET 9`, `.NET 8` (modern .NET)
- `.NET Framework 4.7.2`, `.NET Framework 4.6.2`
- `.NET Standard 2.0`

Use `#if NET6_0_OR_GREATER` (or the appropriate TFM guard) to separate modern and legacy code
paths. Always provide both paths — do **not** drop legacy support.

## Key Design Patterns

### RFC 9562 Byte Layout

UUIDs are built in **network (big-endian) byte order** per RFC 9562, then converted to .NET's
mixed-endian `Guid` format using `new Guid(bytes, bigEndian: true)` on NET6+ or
`bytes.SwapByteOrder()` on legacy targets.

### SQL Server Byte Ordering

`ToSqlGuid()` / `NewSqlGuid()` reorder bytes so SQL Server's `uniqueidentifier` comparison sorts
correctly. The mapping is defined in `ByteArrayExtensions` and follows the documented SQL Server
sort rules.

### Monotonic Counter (`GuidV7` and `GuidV8Time`)

- A process-global `static int s_counter` is advanced with `Interlocked.Increment`.
- Seeded at startup with a small random value from `RandomNumberGenerator` to leave headroom
  before wrap.
- **No CAS loop, no timestamp tracking** — the counter is unconditional and race-free.
- `GuidV7`: 26-bit counter (upper 12 bits → `rand_a`, lower 14 bits → start of `rand_b`).

### Multi-Target Crypto Helpers

Preferred (NET6+):

    RandomNumberGenerator.Fill(span);
    var n = RandomNumberGenerator.GetInt32(max);

Legacy (< NET6):

    using var rng = RandomNumberGenerator.Create();
    rng.GetBytes(buffer, offset, count);

### C# 14 Extension Members

`ByteArrayExtensions.cs` uses the C# 14 `extension(byte[] b) { ... }` block syntax instead of
traditional static extension methods. Follow this pattern when adding internal byte-array helpers.

## Coding Standards

- **Null safety**: nullable reference types enabled everywhere; no `!` suppressions without a comment.
- **No allocations on hot paths**: prefer `Span<T>` / `stackalloc` on NET6+ targets.
- **`[SkipLocalsInit]`** applied to performance-sensitive structs (e.g., `SequentialGuid`).
- XML doc comments (`<summary>`, `<param>`, `<returns>`, `<exception>`, `<remarks>`) are
  **required** on all public members.
- `sealed` on all concrete classes and test classes.
- `readonly record struct` for value-type wrappers.
- Internal helpers live in `Extensions/` as `internal static` classes.

## Testing

Framework: **xUnit**

Run all tests:

    dotnet test

Test file naming convention: `{ClassName}Tests.cs` in `test/SequentialGuid.Tests/`.

### Test Conventions

- RFC test vectors (e.g., Appendix A.6 for v7) **must** be included and clearly labeled with a
  comment citing the spec section.
- Sort-order tests use pre-built `static IReadOnlyList<SequentialGuid>` /
  `IReadOnlyList<SequentialSqlGuid>` fixtures defined at the top of the test class.
- Test class fields are `static readonly` or `const` where possible.
- No test should depend on wall-clock time unless testing the no-arg `NewGuid()` overload;
  prefer the `long unixMilliseconds` overload for deterministic tests.

## Benchmarks

Framework: **BenchmarkDotNet** (in `util/Benchmarks/`)

    dotnet run -c Release --project util/Benchmarks -- --filter *<Pattern>*

Use `[MemoryDiagnoser]` on all benchmark classes.

## RFC 9562 References

- §5.7 — UUID Version 7 layout
- §6.2 Method 1 — Fixed Bit-Length Dedicated Counter (implemented in `GuidV7`)
- Appendix A.6 — v7 test vector (`unix_ts_ms = 0x017F22E279B0`)

When modifying UUID generation logic always cite the relevant RFC section in a comment.
