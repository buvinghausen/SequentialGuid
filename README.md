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

Then define your implementing class which can be transient since the singleton is implemented by the framework

```csharp
public class SequentialIdGenerator : IIdGenerator
{
	public Guid NewId() => SequentialGuidGenerator.Instance.NewGuid();
}
```

Then wire it up in the ConfigureServices method during application startup

```csharp
public void ConfigureServices(IServiceCollection services)
{
	services.AddTransient<IIdGenerator, SequentialIdGenerator>();
}
```
