# SequentialGuid.NodaTime

[![NuGet](https://img.shields.io/nuget/v/SequentialGuid.NodaTime.svg)](https://www.nuget.org/packages/SequentialGuid.NodaTime/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SequentialGuid.NodaTime.svg)](https://www.nuget.org/packages/SequentialGuid.NodaTime/)

[NodaTime](https://nodatime.org/) integration for the [SequentialGuid](https://www.nuget.org/packages/SequentialGuid/) library. Generate sequential UUIDs from NodaTime `Instant`, `OffsetDateTime`, and `ZonedDateTime` values, and extract embedded timestamps as `Instant`.

## Install

```shell
dotnet add package SequentialGuid.NodaTime
```

> Requires `NodaTime` 2.4.0 or later.

## Features

| Feature | Description |
|---|---|
| `GuidV7.NewGuid(instant)` | Generate a UUIDv7 from an `Instant`, `OffsetDateTime`, or `ZonedDateTime` |
| `GuidV7.NewSqlGuid(instant)` | Same as above but with SQL Server byte ordering |
| `GuidV8Time.NewGuid(instant)` | Generate a UUIDv8 from an `Instant`, `OffsetDateTime`, or `ZonedDateTime` |
| `GuidV8Time.NewSqlGuid(instant)` | Same as above but with SQL Server byte ordering |
| `guid.ToInstant()` | Extract the embedded timestamp as a NodaTime `Instant` |

## Quick Start

### Generate UUIDs from NodaTime types

```csharp
using NodaTime;
using SequentialGuid;

var now = SystemClock.Instance.GetCurrentInstant();

// UUIDv7 (millisecond precision) - recommended for most applications
var id = GuidV7.NewGuid(now);

// UUIDv8 (tick precision)
var id = GuidV8Time.NewGuid(now);

// SQL Server byte order variants
var sqlId = GuidV7.NewSqlGuid(now);
var sqlId = GuidV8Time.NewSqlGuid(now);
```

### Generate from OffsetDateTime or ZonedDateTime

```csharp
var offsetDt = new OffsetDateTime(
    new LocalDateTime(2025, 7, 1, 12, 0, 0),
    Offset.FromHours(-5));
var id = GuidV7.NewGuid(offsetDt);

var zonedDt = offsetDt.InZone(DateTimeZoneProviders.Tzdb["America/New_York"]);
var id = GuidV7.NewGuid(zonedDt);
```

### Extract a timestamp as Instant

```csharp
using NodaTime;

Guid id = GuidV7.NewGuid();
Instant? created = id.ToInstant();
// Returns the embedded UTC timestamp as an Instant, or null if not a sequential GUID
```

## Supported Timestamp Types

All generation methods accept these NodaTime types:

| Type | Notes |
|---|---|
| `Instant` | Directly converted; most common usage |
| `OffsetDateTime` | Converted to `Instant` (for V8) or `DateTimeOffset` (for V7) |
| `ZonedDateTime` | Converted to `Instant` (for V8) or `DateTimeOffset` (for V7) |

## Further Reading

See the [main SequentialGuid README](https://github.com/buvinghausen/SequentialGuid/blob/master/README.md) for full documentation on UUID generation, timestamp extraction, and SQL Server byte-order handling.
