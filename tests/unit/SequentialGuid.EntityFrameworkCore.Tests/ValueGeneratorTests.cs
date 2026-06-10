namespace SequentialGuid.EntityFrameworkCore.Tests;

public sealed class ValueGeneratorTests
{
	[Fact]
	void SequentialGuidValueGeneratorProducesV7()
	{
		SequentialGuidValueGenerator generator = new();
		generator.GeneratesTemporaryValues.ShouldBeFalse();
		var id = generator.Next(null!);
		id.ShouldNotBe(Guid.Empty);
		GuidVersion.Standard(id).ShouldBe(7);
		id.IsSequentialGuid().ShouldBeTrue();
	}

	[Fact]
	void SequentialSqlGuidValueGeneratorProducesSqlOrderedV7()
	{
		SequentialSqlGuidValueGenerator generator = new();
		generator.GeneratesTemporaryValues.ShouldBeFalse();
		var id = generator.Next(null!);
		id.ShouldNotBe(Guid.Empty);
		GuidVersion.Sql(id).ShouldBe(7);
		id.IsSequentialGuid().ShouldBeTrue();
	}

	[Fact]
	void SequentialGuidStructValueGeneratorProducesValidStruct()
	{
		SequentialGuidStructValueGenerator generator = new();
		generator.GeneratesTemporaryValues.ShouldBeFalse();
		var value = generator.Next(null!);
		value.Value.ShouldNotBe(Guid.Empty);
		value.Timestamp.ShouldBeGreaterThan(DateTime.MinValue);
		GuidVersion.Standard(value.Value).ShouldBe(7);
	}

	[Fact]
	void SequentialSqlGuidStructValueGeneratorProducesValidStruct()
	{
		SequentialSqlGuidStructValueGenerator generator = new();
		generator.GeneratesTemporaryValues.ShouldBeFalse();
		var value = generator.Next(null!);
		value.Value.ShouldNotBe(Guid.Empty);
		value.Timestamp.ShouldBeGreaterThan(DateTime.MinValue);
		GuidVersion.Sql(value.Value).ShouldBe(7);
	}
}
