using Xunit;

namespace SequentialGuid.MongoDB.Tests;

public class SequentialGuidMongoTests
{
	[Fact]
	private void VerifyGenerateId()
	{
		// Mongo must be able to publicly construct the generator
		var generator = new MongoSequentialGuidGenerator();
		var objId = generator.GenerateId(null!, null!);
		if (objId is Guid id)
		{
			Assert.True(id.ToDateTime().HasValue);
		}
		else
		{
			Assert.Fail("Invalid data type");
		}
	}

	[Fact]
	private void VerifyIsEmpty()
	{
		// Mongo must be able to publicly construct the generator
		var generator = new MongoSequentialGuidGenerator();
		// Make sure null returns empty
		Assert.True(generator.IsEmpty(null!));
		// Make sure a new guid returns empty
		Assert.True(generator.IsEmpty(Guid.Empty));
		// Make sure a nullable guid is empty
		Assert.True(generator.IsEmpty(null!));
		Guid? nullableEmpty = Guid.Empty;
		// Make sure an empty nullable guid returns not empty
		Assert.True(generator.IsEmpty(nullableEmpty));
		// Make sure injecting non-guid types comes back as empty
		Assert.True(generator.IsEmpty(5000));
		// Make sure a hydrated guid returns not empty
		Assert.False(generator.IsEmpty(Guid.NewGuid()));
		Guid? nullableWithValue = Guid.NewGuid();
		// Make sure a hydrated nullable guid returns not empty
		Assert.False(generator.IsEmpty(nullableWithValue));
	}
}
