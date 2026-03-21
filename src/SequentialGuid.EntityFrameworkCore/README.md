# SequentialGuid.EntityFrameworkCore

[![NuGet](https://img.shields.io/nuget/v/SequentialGuid.EntityFrameworkCore.svg)](https://www.nuget.org/packages/SequentialGuid.EntityFrameworkCore/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SequentialGuid.EntityFrameworkCore.svg)](https://www.nuget.org/packages/SequentialGuid.EntityFrameworkCore/)

EF Core value-converter support for the [SequentialGuid](https://www.nuget.org/packages/SequentialGuid/) library. Register once and Entity Framework Core can automatically persist `SequentialGuid` and `SequentialSqlGuid` properties as standard `Guid` database columns.

## Install

```shell
dotnet add package SequentialGuid.EntityFrameworkCore
```

## Supported Frameworks

| Target | EF Core Version |
|---|---|
| .NET 10 | 10.0.0 |
| .NET 9 | 9.0.0 |
| .NET 8 | 8.0.10+ |

## Setup

Register the value converters in your `DbContext` by overriding `ConfigureConventions`:

```csharp
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Registers converters for both SequentialGuid and SequentialSqlGuid
        configurationBuilder.AddSequentialGuidValueConverters();
    }
}
```

This single call registers value converters for both `SequentialGuid` and `SequentialSqlGuid` so that any entity property of either type is transparently converted to and from `Guid` when reading/writing to the database.

## Entity Model Example

```csharp
using SequentialGuid;

public class Order
{
    // Assigned at construction - no database round-trip needed
    public SequentialGuid Id { get; set; } = new();

    // Timestamp is always available from the ID itself
    public DateTime? CreatedAt => Id.Timestamp;

    public string Description { get; set; } = string.Empty;
}
```

If you are targeting SQL Server and want IDs that sort correctly in `uniqueidentifier` columns, use `SequentialSqlGuid` instead:

```csharp
public class Order
{
    public SequentialSqlGuid Id { get; set; } = new();
}
```

## How It Works

Under the hood, `SequentialGuidValueConverter<T>` (where `T : struct, ISequentialGuid<T>`) converts:

- **To database**: extracts the underlying `Guid` via `value.Value`
- **From database**: reconstructs the struct via `T.Create(guid)`, which validates the GUID is a recognized sequential format

This means the database column type remains a standard `Guid` / `uniqueidentifier` - no schema changes are needed.

## JSON Serialization

If your API returns entities containing `SequentialGuid` / `SequentialSqlGuid` properties, register the built-in JSON converters in your `Program.cs`:

```csharp
using SequentialGuid.Extensions;

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.AddSequentialGuidConverters());
```

## Further Reading

See the [main SequentialGuid README](https://github.com/buvinghausen/SequentialGuid/blob/master/README.md) for full documentation on UUID generation, timestamp extraction, and SQL Server byte-order handling.
