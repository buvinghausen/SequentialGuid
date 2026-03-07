
SequentialGuid
==============
![Continuous Integration](https://github.com/buvinghausen/SequentialGuid/workflows/Continuous%20Integration/badge.svg)[![NuGet](https://img.shields.io/nuget/v/SequentialGuid.svg)](https://www.nuget.org/packages/SequentialGuid/)[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/buvinghausen/SequentialGuid/blob/master/LICENSE.txt)

A dependency-free library for generating [RFC 9562](https://www.rfc-editor.org/rfc/rfc9562.html) compliant UUIDs in .NET. All five structs — `GuidV4`, `GuidV5`, `GuidV7`, `GuidV8Time`, and `GuidV8Name` — conform to the RFC 9562 specification with correct version nibble and variant bits. Time-based UUIDs embed the creation timestamp into the value, which typically results in lower clustered index fragmentation when used as database primary keys. You can generate IDs all the way up in WebAssembly or MAUI, pass them through your API, and store them in the database — helping with idempotency without requiring a trip to the database to generate the key.

## UUID Versions at a Glance

| Struct | RFC 9562 Section | Purpose |
|---|---|---|
| `GuidV4` | §5.4 | Cryptographically random UUID — drop-in replacement for `Guid.NewGuid()` with guaranteed RFC 9562 version/variant bits |
| `GuidV5` | §5.5 | Deterministic, namespace + name UUID using **SHA-1** hashing |
| `GuidV7` | §5.7 | Time-ordered UUID using a **48-bit Unix millisecond** timestamp with a monotonic counter (RFC 9562 §6.2 Method 1) |
| `GuidV8Time` | Appendix B.1 | Time-ordered UUID using **60-bit .NET Ticks** for sub-millisecond precision, plus a machine/process identifier and counter |
| `GuidV8Name` | Appendix B.2 | Deterministic, namespace + name UUID using **SHA-256** hashing |

### GuidV7 vs GuidV8Time — Which Should I Use?

Both generate time-ordered, sortable UUIDs. The key difference is **timestamp precision**:

* **`GuidV7`** — Uses a standard 48-bit Unix Epoch **millisecond** timestamp as defined by RFC 9562 §5.7. This is the most interoperable choice and is recommended if you are exchanging UUIDs with non-.NET systems or millisecond precision is sufficient.
* **`GuidV8Time`** — Uses 60 bits of .NET `DateTime.Ticks` (100-nanosecond intervals) for **sub-millisecond precision**. Use this when you need/want precision beyond the millisecond level or want to retain the machine/process identifier from the original algorithm.

### GuidV5 vs GuidV8Name

Both produce deterministic UUIDs from a namespace + name pair. The **only** difference is the hash algorithm:

* **`GuidV5`** — Uses **SHA-1** as required by RFC 9562 §5.5.
* **`GuidV8Name`** — Uses **SHA-256** as described in RFC 9562 Appendix B.2, providing a stronger hash at the cost of not being interoperable with standard UUIDv5 implementations in other languages.

## Quick Start

### Generate a time-ordered UUID

```csharp
// Millisecond precision (RFC 9562 UUIDv7)
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
var id = GuidV4.NewGuid();
```

### Generate a deterministic UUID from a namespace + name

```csharp
// SHA-1 (UUIDv5)
var id = GuidV5.Create(GuidV5.Namespaces.Url, "https://example.com");

// SHA-256 (UUIDv8 name-based)
var id = GuidV8Name.Create(GuidV8Name.Namespaces.Url, "https://example.com");
```

### Extract the timestamp from any time-based UUID

```csharp
DateTime? timestamp = id.ToDateTime();
```

### Convert between Guid and SqlGuid

```csharp
var guid = GuidV7.NewGuid();
var sqlGuid = guid.ToSqlGuid();
```
```csharp
var sqlGuid = GuidV7.NewSqlGuid();
var guid = sqlGuid.ToGuid();
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
    public Guid Id { get; set; } = GuidV7.NewGuid();
    public DateTime? Timestamp => Id.ToDateTime();
    // If you really must have non-UTC time
    public DateTime? LocalTime => Id.ToDateTime()?.ToLocalTime();
}
```

## SQL Server Sorting

If you use [SQL Server](https://www.microsoft.com/en-us/sql-server/) I highly recommend reading the following two articles to understand how SQL Server sorts [uniqueidentifier](https://learn.microsoft.com/en-us/sql/t-sql/data-types/uniqueidentifier-transact-sql) values:

* [Comparing GUID and uniqueidentifier Values](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/comparing-guid-and-uniqueidentifier-values)
* [How are GUIDs sorted by SQL Server?](https://www.sqlbi.com/blog/alberto/2007/08/31/how-are-guids-sorted-by-sql-server/)

Use the `NewSqlGuid()` methods (or the `.ToSqlGuid()` extension) to produce UUIDs whose byte order aligns with SQL Server's comparison logic.

## Companion Packages

| Package | Purpose |
|---|---|
| **SequentialGuid.NodaTime** | Extension methods for `Instant` and other NodaTime types |
| **SequentialGuid.MongoDB** | `IIdGenerator` integration for the MongoDB C# driver |

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
