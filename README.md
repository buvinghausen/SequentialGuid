
SequentialGuid
==============
![Continuous Integration](https://github.com/buvinghausen/SequentialGuid/workflows/Continuous%20Integration/badge.svg)[![NuGet](https://img.shields.io/nuget/v/SequentialGuid.svg)](https://www.nuget.org/packages/SequentialGuid/)[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/buvinghausen/SequentialGuid/blob/master/LICENSE.txt)

Will generate Sequential Guids based on [MongoDB's ObjectId specification](https://docs.mongodb.com/manual/reference/method/ObjectId/) sorting algorithm. Date &amp; time are encoded into the value so you do not need to store them separately in your database

Author's Note: The entire purpose of this library is for you to have a dependency free way of generating unique uuid/Guid values that contain the time of creation which will typically result in lower clustered index fragmentation on the back end once they get persisted but it will allow you to generate the keys all the way up in WebAssembly or MAUI and pass it through your API and store it in the database this can help you with itempotency not requiring a trip to the database to generate the ID.  Please DO NOT open an issue telling me it doesn't use the Unix timestamp or it isn't an ObjectId those are both true I had to find 32 additional bits to fill a Guid vs ObjectId and so I opted to use the Ticks count to do so while retaining the remainder of the Mongo algorithm. An added bonus to this is this library is not subject to break on 19 January 2038, at 03:14:07 UTC when the Unix timestamp overflows a 32 bit integer.

Returns a new [Guid](https://learn.microsoft.com/en-us/dotnet/api/system.guid) or [SqlGuid](https://learn.microsoft.com/en-us/dotnet/api/system.data.sqltypes.sqlguid). The 16-byte [Guid](https://learn.microsoft.com/en-us/dotnet/api/system.guid) or [SqlGuid](https://learn.microsoft.com/en-us/dotnet/api/system.data.sqltypes.sqlguid) consists of:

* A 8-byte timestamp, representing the Guid's creation, measured in system [ticks](https://learn.microsoft.com/en-us/dotnet/api/system.datetime.ticks).
* A 5-byte random value generated once per process. This random value is unique to the machine and process.
* A 3-byte incrementing counter, initialized to a random value.

If you use [SQL Server](https://www.microsoft.com/en-us/sql-server/) then I highly recommend reading the following two articles to get a basic understanding of how [SQL Server](https://www.microsoft.com/en-us/sql-server/) sorts [uniqueidentifier](https://learn.microsoft.com/en-us/sql/t-sql/data-types/uniqueidentifier-transact-sql) values
* [Comparing GUID and uniqueidentifier Values](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/comparing-guid-and-uniqueidentifier-values)
* [How are GUIDs sorted by SQL Server?](https://www.sqlbi.com/blog/alberto/2007/08/31/how-are-guids-sorted-by-sql-server/)

Define an interface to the signature you like
```csharp
public interface IIdGenerator
{
    Guid NewId();
}
```

Define your implementing class which can be transient since the singleton is implemented by the framework

```csharp
public class SequentialIdGenerator : IIdGenerator
{
    public Guid NewId() => SequentialGuidGenerator.Instance.NewGuid();
}
```

Wire it up to .NET Core dependency injection in the ConfigureServices method during application startup

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddTransient<IIdGenerator, SequentialIdGenerator>();
}
```

Finally define a base entity for your application which will contain an id and a timestamp as soon as you initialize it. Note I do not advocate setting a default Id getter this way just illustrating it can be done

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = SequentialGuidGenerator.Instance.NewGuid();
    public DateTime? Timestamp => Id.ToDateTime();
    // If you really must have non-UTC time
    public DateTime? LocalTime => Id.ToDateTime()?.ToLocalTime();
}
```

You can convert between a standard Guid and a SqlGuid using the available helper functions
```csharp
var guid = SequentialGuidGenerator.Instance.NewGuid();
var sqlGuid = guid.ToSqlGuid();
```
OR
```csharp
var sqlGuid = SequentialSqlGuidGenerator.Instance.NewSqlGuid();
var guid = sqlGuid.ToGuid();
```
