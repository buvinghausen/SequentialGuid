SequentialGuid
==============

Will generate Sequential Guids based on MongoDB's ObjectId specification. Date &amp; time are encoded into the value so you do not need to store them separately in your database

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
Finally define a base entity for your application which will contain an id and a timestamp as soon as you initialize it

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = SequentialGuidGenerator.Instance.NewGuid();
    public DateTime? Timestamp => Id.ToDateTime();
}
```
