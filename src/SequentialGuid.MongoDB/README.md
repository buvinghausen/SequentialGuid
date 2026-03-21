# SequentialGuid.MongoDB

[![NuGet](https://img.shields.io/nuget/v/SequentialGuid.MongoDB.svg)](https://www.nuget.org/packages/SequentialGuid.MongoDB/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SequentialGuid.MongoDB.svg)](https://www.nuget.org/packages/SequentialGuid.MongoDB/)

MongoDB integration for the [SequentialGuid](https://www.nuget.org/packages/SequentialGuid/) library. Provides a drop-in `IIdGenerator` for automatic sequential `Guid` document IDs and BSON serializers for the `SequentialGuid` / `SequentialSqlGuid` struct types.

## Install

```shell
dotnet add package SequentialGuid.MongoDB
```

> Requires `MongoDB.Bson` 2.29.0 or later.

## Features

| Feature | Description |
|---|---|
| `MongoSequentialGuidGenerator` | `IIdGenerator` implementation - generates sequential `Guid` IDs for documents automatically |
| BSON serializers | Transparent serialization of `SequentialGuid` and `SequentialSqlGuid` (including nullable variants) |

## Quick Start

### 1. Register the ID generator

Register the singleton generator so MongoDB uses sequential GUIDs for all `Guid` id properties:

```csharp
using SequentialGuid.MongoDB;

// Register as the default Guid id generator
MongoSequentialGuidGenerator.Instance.RegisterMongoIdGenerator();
```

### 2. Register the BSON serializers

If your documents use `SequentialGuid` or `SequentialSqlGuid` as property types (not just raw `Guid`), register the serialization provider:

```csharp
using MongoDB.Bson.Serialization;
using SequentialGuid.MongoDB.Serializers;

BsonSerializer.RegisterSequentialGuidSerializers();
```

This registers serializers for `SequentialGuid`, `SequentialGuid?`, `SequentialSqlGuid`, and `SequentialSqlGuid?`.

### 3. Use in your document models

**Option A** - Raw `Guid` with automatic generation:

```csharp
public class Order
{
    public Guid Id { get; set; }

    public string Description { get; set; } = string.Empty;
}

// Insert - Id is generated automatically by MongoSequentialGuidGenerator
await collection.InsertOneAsync(new Order { Description = "Widget" });
```

**Option B** - Strongly-typed struct properties:

```csharp
using SequentialGuid;

public class Order
{
    public SequentialGuid Id { get; set; } = new();

    // Timestamp is always available from the ID itself
    public DateTime? CreatedAt => Id.Timestamp;

    public string Description { get; set; } = string.Empty;
}
```

### Complete setup example

```csharp
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using SequentialGuid.MongoDB;
using SequentialGuid.MongoDB.Serializers;

// One-time setup (typically in Program.cs or a startup class)
MongoSequentialGuidGenerator.Instance.RegisterMongoIdGenerator();
BsonSerializer.RegisterSequentialGuidSerializers();

// Use MongoDB as normal
var client = new MongoClient("mongodb://localhost:27017");
var db = client.GetDatabase("myapp");
var orders = db.GetCollection<Order>("orders");

await orders.InsertOneAsync(new Order { Description = "Widget" });
```

## How It Works

- **`MongoSequentialGuidGenerator`** implements `IIdGenerator` and calls `GuidV8Time.NewGuid()` to generate each ID. It considers any `Guid` value that is not `Guid.Empty` as non-empty.
- **`SequentialGuidSerializer<T>`** delegates to the default `Guid` BSON serializer for the wire format, then wraps/unwraps the struct type on read/write. This means documents stored with raw `Guid` IDs remain compatible.

## Further Reading

See the [main SequentialGuid README](https://github.com/buvinghausen/SequentialGuid/blob/master/README.md) for full documentation on UUID generation, timestamp extraction, and SQL Server byte-order handling.
