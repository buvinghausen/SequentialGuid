namespace SequentialGuid.MongoDB.Tests;

public sealed class SequentialGuidMongoTests
{
	[Fact]
	void VerifyGenerateId()
	{
		// Mongo must be able to publicly construct the generator
		var generator = new MongoSequentialGuidGenerator();
		var objId = generator.GenerateId(null!, null!);
		if (objId is Guid id)
		{
			id.ToDateTime().HasValue.ShouldBeTrue();
		}
		else
		{
			Assert.Fail("Invalid data type");
		}
	}

	[Fact]
	void VerifyIsEmpty()
	{
		// Mongo must be able to publicly construct the generator
		var generator = new MongoSequentialGuidGenerator();
		// Make sure null returns empty
		generator.IsEmpty(null!).ShouldBeTrue();
		// Make sure a new guid returns empty
		generator.IsEmpty(Guid.Empty).ShouldBeTrue();
		// Make sure a nullable guid is empty
		generator.IsEmpty(null!).ShouldBeTrue();
		Guid? nullableEmpty = Guid.Empty;
		// Make sure an empty nullable guid returns not empty
		generator.IsEmpty(nullableEmpty).ShouldBeTrue();
		// Make sure injecting non-guid types comes back as empty
		generator.IsEmpty(5000).ShouldBeTrue();
		// Make sure a hydrated guid returns not empty
		generator.IsEmpty(Guid.NewGuid()).ShouldBeFalse();
		Guid? nullableWithValue = Guid.NewGuid();
		// Make sure a hydrated nullable guid returns not empty
		generator.IsEmpty(nullableWithValue).ShouldBeFalse();
	}
}
