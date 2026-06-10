# SequentialGuid

[![Continuous Integration](https://github.com/buvinghausen/SequentialGuid/workflows/Continuous%20Integration/badge.svg)](https://github.com/buvinghausen/SequentialGuid/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/buvinghausen/SequentialGuid/blob/master/LICENSE.txt)

| Package | NuGet | Downloads |
|---|---|---|
| **SequentialGuid** | [![NuGet](https://img.shields.io/nuget/v/SequentialGuid.svg)](https://www.nuget.org/packages/SequentialGuid/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/SequentialGuid.svg)](https://www.nuget.org/packages/SequentialGuid/) |
| **SequentialGuid.EntityFrameworkCore** | [![NuGet](https://img.shields.io/nuget/v/SequentialGuid.EntityFrameworkCore.svg)](https://www.nuget.org/packages/SequentialGuid.EntityFrameworkCore/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/SequentialGuid.EntityFrameworkCore.svg)](https://www.nuget.org/packages/SequentialGuid.EntityFrameworkCore/) |
| **SequentialGuid.MongoDB** | [![NuGet](https://img.shields.io/nuget/v/SequentialGuid.MongoDB.svg)](https://www.nuget.org/packages/SequentialGuid.MongoDB/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/SequentialGuid.MongoDB.svg)](https://www.nuget.org/packages/SequentialGuid.MongoDB/) |
| **SequentialGuid.NodaTime** | [![NuGet](https://img.shields.io/nuget/v/SequentialGuid.NodaTime.svg)](https://www.nuget.org/packages/SequentialGuid.NodaTime/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/SequentialGuid.NodaTime.svg)](https://www.nuget.org/packages/SequentialGuid.NodaTime/) |

**Generate database-friendly, time-ordered UUIDs anywhere in your .NET stack — no database round-trip required.**

SequentialGuid produces [RFC 9562](https://www.rfc-editor.org/rfc/rfc9562.html) compliant UUIDs with embedded timestamps. Generate IDs client-side — in Blazor WebAssembly, MAUI, a background worker, or a REST controller — and pass them straight through to the database. You get **natural sort order** (dramatically reducing clustered index fragmentation), **built-in timestamp extraction** (no extra `CreatedAt` column needed), and **client-side idempotency** (retry-safe inserts without a server round-trip to generate a key).

## Quick Start

```shell
dotnet add package SequentialGuid
```

```csharp
using SequentialGuid;

// RFC 9562 UUIDv7 — recommended for most applications
var id = GuidV7.NewGuid();

// Extract the embedded timestamp
DateTime? created = id.ToDateTime();

// SQL Server byte order variant
var sqlId = GuidV7.NewSqlGuid();
```

## Packages

### [SequentialGuid](src/SequentialGuid/README.md) — Core Library

The zero-dependency core package. Provides UUID generation, timestamp extraction, SQL Server byte-order conversion, and strongly-typed struct wrappers.

**UUID versions included:**

| Class | Purpose |
|---|---|
| `GuidV4` | Cryptographically random UUID with guaranteed RFC 9562 version & variant bits |
| `GuidV5` | Deterministic namespace + name UUID using SHA-1 (interoperable with other languages) |
| `GuidV7` | Time-ordered UUID — 48-bit Unix ms timestamp + 26-bit monotonic counter + crypto-random bits |
| `GuidV8Time` | Time-ordered UUID — 60-bit .NET Ticks (100 ns precision) + machine/process fingerprint |
| `GuidV8Name` | Deterministic namespace + name UUID using SHA-256 |

**Use when:** You need sequential GUID generation in any .NET application — API, console, library, Blazor, MAUI, or background service.

---

### [SequentialGuid.EntityFrameworkCore](src/SequentialGuid.EntityFrameworkCore/README.md) — EF Core Integration

Value converters, value generation, and JSON serialization support for the `SequentialGuid` and `SequentialSqlGuid` struct types — plus a one-line convention that generates RFC 9562 v7 keys for every `Guid` primary key on Add.

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.AddSequentialGuidValueConverters();
    configurationBuilder.UseSequentialGuidValueGeneration();           // v7 keys on Add
    // configurationBuilder.UseSequentialGuidValueGeneration(sqlServerByteOrder: true);
}
```

**Use when:** You want to use the strongly-typed `SequentialGuid` / `SequentialSqlGuid` structs as entity properties and have EF Core transparently convert them to and from `Guid` database columns.

---

### [SequentialGuid.MongoDB](src/SequentialGuid.MongoDB/README.md) — MongoDB Integration

Drop-in `IIdGenerator` for automatic sequential `Guid` document IDs, plus BSON serializers for the struct types.

```csharp
MongoSequentialGuidGenerator.Instance.RegisterMongoIdGenerator();
BsonSerializer.RegisterSequentialGuidSerializers();
```

**Use when:** You use MongoDB and want sequential, time-ordered GUIDs as document IDs — either raw `Guid` with automatic generation or the strongly-typed structs.

---

### [SequentialGuid.NodaTime](src/SequentialGuid.NodaTime/README.md) — NodaTime Integration

Generate sequential UUIDs from NodaTime `Instant`, `OffsetDateTime`, and `ZonedDateTime` values, and extract embedded timestamps as `Instant`.

```csharp
var now = SystemClock.Instance.GetCurrentInstant();
var id = GuidV7.NewGuid(now);
Instant? created = id.ToInstant();
```

**Use when:** Your application uses NodaTime for date/time handling and you want to generate or extract timestamps without converting to `DateTime` / `DateTimeOffset`.

## Key Features

- **RFC 9562 compliant** — correct version nibble and variant bits on every UUID
- **Monotonically increasing** — process-global `Interlocked.Increment` counter ensures strict ordering under concurrency
- **Bulk generation** — `GuidV7.Fill(Span<Guid>)` / `NewGuids(count)` amortize timestamp capture, counter reservation, and RNG across the batch (.NET 8+)
- **EF Core value generation** — one-line convention assigns RFC 9562 v7 generators to every Guid primary key
- **Testable clocks** — `TimeProvider` overloads on every generation path (.NET 8+)
- **Zero dependencies** — the core package references nothing outside the BCL
- **Zero allocations on modern .NET** — `stackalloc`, `Span<T>`, and `[SkipLocalsInit]` on .NET 8+
- **Broad platform support** — .NET 10 / 9 / 8, .NET Framework 4.6.2, and .NET Standard 2.0 (including Blazor WebAssembly)
- **Round-trip timestamp extraction** — `.ToDateTime()` on any `Guid` (V7, V8, or legacy)
- **SQL Server sort-order aware** — `NewSqlGuid()`, `.ToSqlGuid()`, `.FromSqlGuid()` handle byte-order conversion
- **Strongly-typed struct wrappers** — `SequentialGuid` and `SequentialSqlGuid` validate at construction and implement `IComparable`, `IFormattable`, `ISpanParsable<T>`, and more
- **Built-in benchmarks** — BenchmarkDotNet project included for measuring performance on your hardware

## Why not `Guid.CreateVersion7`?

.NET 9 added [`Guid.CreateVersion7`](https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7) — so why this library?

1. **No monotonic counter.** BCL v7 GUIDs generated within the same millisecond sort randomly relative to each other (RFC 9562 §6.2 Method 1 is not implemented). Under insert load that is exactly the index-fragmentation problem v7 adoption is meant to solve. SequentialGuid's process-global counter guarantees strict creation order, even across threads.
2. **No SQL Server byte-order story.** A BCL v7 GUID stored in a `uniqueidentifier` clustered index still fragments — SQL Server sorts GUIDs by its own byte order. `NewSqlGuid()`, `.ToSqlGuid()`, and `.FromSqlGuid()` handle that here.
3. **No bulk generation.** `GuidV7.Fill(Span<Guid>)` reserves one counter block and makes one RNG call for the whole batch — an order of magnitude faster than calling `Guid.CreateVersion7` in a loop.
4. **No round-trip tooling.** No timestamp extraction, no sequential-GUID detection, no strongly-typed wrappers.
5. **Reach.** `Guid.CreateVersion7` is .NET 9+; this library covers .NET Framework 4.6.2 and .NET Standard 2.0.

The trade-off is honest: the monotonic counter and RFC-compliant layout cost a few nanoseconds per single call versus the BCL. Where throughput matters, the bulk API more than repays it.

Measured with BenchmarkDotNet on Intel Core Ultra 9 185H 2.30GHz, .NET 10.0.9 — regenerate with `dotnet run -c Release --project benches/Benchmarks -- --filter *BclComparison*`:

| Method                           | Mean         | Error        | StdDev       | Ratio    | RatioSD | Allocated | Alloc Ratio |
|--------------------------------- |-------------:|-------------:|-------------:|---------:|--------:|----------:|------------:|
| Guid.NewGuid                     |     44.46 ns |     0.812 ns |     0.634 ns |     1.00 |    0.02 |         - |          NA |
| Guid.CreateVersion7              |     65.09 ns |     1.025 ns |     1.437 ns |     1.46 |    0.04 |         - |          NA |
| GuidV7.NewGuid                   |     82.59 ns |     1.501 ns |     2.055 ns |     1.86 |    0.05 |         - |          NA |
| 'Guid.CreateVersion7 ×1000 loop' | 67,284.17 ns | 1,299.937 ns | 1,152.361 ns | 1,513.81 |   32.54 |         - |          NA |
| 'GuidV7.NewGuid ×1000 loop'      | 88,345.82 ns | 1,731.024 ns | 2,892.152 ns | 1,987.67 |   69.76 |         - |          NA |
| 'GuidV7.Fill ×1000 bulk'         |  6,210.75 ns |    56.053 ns |    49.690 ns |   139.73 |    2.20 |         - |          NA |

## Contributing

Pull requests and issues are welcome. The repository uses GitHub Actions for CI — all PRs must pass the build and test suite before merging.

## License

This project is licensed under the [MIT License](LICENSE.txt).
