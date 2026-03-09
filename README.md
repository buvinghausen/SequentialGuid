
# SequentialGuid

[![Continuous Integration](https://github.com/buvinghausen/SequentialGuid/workflows/Continuous%20Integration/badge.svg)](https://github.com/buvinghausen/SequentialGuid/actions)
[![NuGet](https://img.shields.io/nuget/v/SequentialGuid.svg)](https://www.nuget.org/packages/SequentialGuid/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SequentialGuid.svg)](https://www.nuget.org/packages/SequentialGuid/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/buvinghausen/SequentialGuid/blob/master/LICENSE.txt)

**Generate database-friendly, time-ordered UUIDs anywhere in your stack — no database round-trip required.**

SequentialGuid is a zero-dependency .NET library that produces [RFC 9562](https://www.rfc-editor.org/rfc/rfc9562.html) compliant UUIDs. Generate IDs in Blazor WebAssembly, MAUI, a background worker, or a REST controller — then pass them through your API and into the database. Because the timestamp is embedded in the value itself, you get **natural sort order** (dramatically reducing clustered index fragmentation), **built-in timestamp extraction** (no extra `CreatedAt` column needed), and **client-side idempotency** (retry-safe inserts without a server round-trip to generate a key).

### Why not just use `Guid.NewGuid()`?

`Guid.NewGuid()` produces random version 4 UUIDs. They work, but they **fragment clustered indexes** in SQL Server, PostgreSQL, and other B-tree based stores because every insert lands at a random page. SequentialGuid solves this by putting the timestamp first — new rows always append near the end of the index, just like an auto-increment integer, while still giving you the global uniqueness and merge-safety of a UUID.

## UUID Versions at a Glance

| Class | RFC 9562 Section | Purpose |
|---|---|---|
| `GuidV4` | §5.4 | Cryptographically random UUID — drop-in replacement for `Guid.NewGuid()` with guaranteed RFC 9562 version & variant bits |
| `GuidV5` | §5.5 | Deterministic, namespace + name UUID using **SHA-1** hashing |
| `GuidV7` | §5.7 | Time-ordered UUID — **48-bit Unix millisecond** timestamp + **26-bit monotonic counter** ([§6.2 Method 1](https://www.rfc-editor.org/rfc/rfc9562.html#section-6.2)) + 36 bits of cryptographic randomness |
| `GuidV8Time` | Appendix B.1 | Time-ordered UUID — **60-bit .NET Ticks** (100 ns precision) + machine/process fingerprint + 22-bit monotonic counter |
| `GuidV8Name` | Appendix B.2 | Deterministic, namespace + name UUID using **SHA-256** hashing |

## Highlights

- **RFC 9562 compliant** — correct version nibble and variant bits on every UUID, every time
- **Monotonically increasing** — `GuidV7` and `GuidV8Time` both use a process-global `Interlocked.Increment` counter so IDs generated on the same timestamp are still strictly ordered, even under heavy concurrency
- **Zero dependencies** — the core package references nothing outside the BCL
- **Zero allocations on modern .NET** — `stackalloc`, `Span<T>`, and `[SkipLocalsInit]` eliminate heap allocations on the hot path (.NET 8+)
- **Broad platform support** — targets **.NET 10 / 9 / 8**, **.NET Framework 4.6.2**, and **.NET Standard 2.0**, with explicit `browser` platform support for Blazor WebAssembly
- **Round-trip timestamp extraction** — call `.ToDateTime()` on any `Guid` (V7, V8, or legacy) to recover the embedded UTC timestamp — works on `SqlGuid` too
- **SQL Server sort-order aware** — `NewSqlGuid()` and `.ToSqlGuid()` / `.FromSqlGuid()` handle the byte-order shuffle so your UUIDs sort chronologically in `uniqueidentifier` columns — the only uses of `System.Data.SqlTypes.SqlGuid` are in the obsolete legacy classes and test suite
- **Built-in benchmarks** — a BenchmarkDotNet project is included so you can measure generation and conversion performance on your own hardware

### GuidV7 vs GuidV8Time — Which Should I Use?

Both generate time-ordered, sortable UUIDs. The difference is **timestamp resolution** and **payload**:

| | `GuidV7` | `GuidV8Time` |
|---|---|---|
| Timestamp precision | **1 ms** (Unix Epoch millis) | **100 ns** (.NET Ticks) |
| Counter bits | 26-bit monotonic | 22-bit monotonic |
| Random / identity bits | 36 bits of crypto-random data | 40-bit machine + process fingerprint |
| Interoperability | ✅ Standard UUIDv7 — understood by any RFC 9562 implementation | .NET-specific custom layout |
| Best for | Cross-platform / polyglot systems, general-purpose use | .NET-only systems that need sub-millisecond ordering or machine traceability |

**Rule of thumb:** Start with `GuidV7`. Reach for `GuidV8Time` only when you need tick-level precision or the machine/process fingerprint.

### GuidV5 vs GuidV8Name

Both produce deterministic UUIDs from a namespace + name pair. The **only** difference is the hash algorithm:

* **`GuidV5`** — Uses **SHA-1** as required by RFC 9562 §5.5. Choose this when you need interoperability with UUIDv5 implementations in other languages.
* **`GuidV8Name`** — Uses **SHA-256** as described in RFC 9562 Appendix B.2, providing a stronger hash for .NET-only scenarios.

## Quick Start

### Install

```shell
dotnet add package SequentialGuid
```

### Generate a time-ordered UUID

```csharp
using SequentialGuid;

// Millisecond precision (RFC 9562 UUIDv7) — recommended for most applications
var id = GuidV7.NewGuid();

// Sub-millisecond / tick precision (RFC 9562 UUIDv8)
var id = GuidV8Time.NewGuid();
```

### Generate a SQL Server-friendly UUID

Both time-based generators provide a `NewSqlGuid()` method that rearranges the byte order to match SQL Server's `uniqueidentifier` sorting rules:

```csharp
var sqlId = GuidV7.NewSqlGuid();
// or
var sqlId = GuidV8Time.NewSqlGuid();
```

### Generate a random UUID (V4)

```csharp
// Guaranteed RFC 9562 version & variant bits (unlike Guid.NewGuid() on some runtimes)
var id = GuidV4.NewGuid();
```

### Generate a deterministic UUID from a namespace + name

```csharp
// SHA-1 (UUIDv5) — interoperable with other languages
var id = GuidV5.Create(GuidV5.Namespaces.Url, "https://example.com");

// SHA-256 (UUIDv8 name-based) — stronger hash, .NET only
var id = GuidV8Name.Create(GuidV8Name.Namespaces.Url, "https://example.com");
```

### Extract the timestamp from any time-based UUID

```csharp
DateTime? created = id.ToDateTime();
// Works on GuidV7, GuidV8Time, legacy SequentialGuid, and even SqlGuid values
```

### Convert between Guid and SqlGuid byte order

```csharp
var guid = GuidV7.NewGuid();
var sqlGuid = guid.ToSqlGuid();    // reorder bytes for SQL Server
var back = sqlGuid.FromSqlGuid();  // restore standard byte order
```

### Stamp a timestamp from an existing DateTime / DateTimeOffset

```csharp
var id = GuidV7.NewGuid(DateTimeOffset.UtcNow);
var id = GuidV8Time.NewGuid(DateTime.UtcNow);
```

### Dependency injection example

```csharp
public interface IIdGenerator
{
    Guid NewId();
}

public class SequentialIdGenerator : IIdGenerator
{
    public Guid NewId() => GuidV7.NewGuid();
}

// In your startup / Program.cs
services.AddTransient<IIdGenerator, SequentialIdGenerator>();
```

### Base entity example

```csharp
public abstract class BaseEntity
{
    // ID is assigned at construction — no database round-trip needed
    public Guid Id { get; set; } = GuidV7.NewGuid();

    // Timestamp is always available — no extra column required
    public DateTime? CreatedAt => Id.ToDateTime();
}
```

## SQL Server Sorting

SQL Server sorts `uniqueidentifier` values in a [non-obvious byte order](https://www.sqlbi.com/blog/alberto/2007/08/31/how-are-guids-sorted-by-sql-server/). If you use SQL Server, read these two articles to understand the implications:

* [Comparing GUID and uniqueidentifier Values](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/comparing-guid-and-uniqueidentifier-values)
* [How are GUIDs sorted by SQL Server?](https://www.sqlbi.com/blog/alberto/2007/08/31/how-are-guids-sorted-by-sql-server/)

Use the `NewSqlGuid()` methods (or the `.ToSqlGuid()` extension) to produce UUIDs whose byte order aligns with SQL Server's comparison logic. The `.FromSqlGuid()` extension on `Guid` reverses the transformation — the name clearly conveys the intent of converting *from* SQL Server byte order back to the standard layout.

## Companion Packages

| Package | Purpose |
|---|---|
| [**SequentialGuid.NodaTime**](https://www.nuget.org/packages/SequentialGuid.NodaTime/) | Extension methods for `Instant`, `OffsetDateTime`, and `ZonedDateTime` — generate and extract timestamps using NodaTime types |
| [**SequentialGuid.MongoDB**](https://www.nuget.org/packages/SequentialGuid.MongoDB/) | Drop-in `IIdGenerator` for the MongoDB C# driver — register once and every inserted document gets a sequential `Guid` ID |

## Performance

On modern .NET the hot paths are **zero-allocation** — byte buffers use `stackalloc` and `Span<T>`, and conversion methods are annotated with `[SkipLocalsInit]`. A BenchmarkDotNet project is included under `util/Benchmarks` so you can verify on your own hardware:

```shell
cd util/Benchmarks
dotnet run -c Release -- --filter *Generation*
```

## Upgrade Guide

Upgrading from the legacy `SequentialGuidGenerator` / `SequentialSqlGuidGenerator` API is straightforward — replace the obsolete singleton calls with the new static methods:

| Before (legacy) | After |
|---|---|
| `SequentialGuidGenerator.Instance.NewGuid()` | `GuidV8Time.NewGuid()` |
| `SequentialSqlGuidGenerator.Instance.NewSqlGuid()` | `GuidV8Time.NewSqlGuid()` |

The legacy classes are still available (marked `[Obsolete]`) so your code will continue to compile, but you should migrate at your convenience.

## Backwards Compatibility

The new RFC 9562 algorithm is **fully backwards compatible** with previously generated Guids:

* **Sort order is preserved** — All UUIDs generated with the legacy algorithm will sort *before* any UUIDs generated with the new algorithm in the database, so existing data ordering is unaffected.
* **Timestamp extraction still works** — The `ToDateTime()` extension method can extract timestamps from both legacy and new UUIDs, so you do not need to migrate existing data.
