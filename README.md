SequentialGuid
==============

Will generate Sequential Guids based on [MongoDB's ObjectId specification](https://docs.mongodb.com/manual/reference/method/ObjectId/). Date &amp; time are encoded into the value so you do not need to store them separately in your database

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

[![NuGet](https://img.shields.io/nuget/v/SequentialGuid.svg)](https://www.nuget.org/packages/SequentialGuid/)
