using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using SequentialGuid.MongoDB.Serializers;

namespace SequentialGuid.MongoDB.Tests;

public sealed class SequentialGuidMongoTests
{
	[Fact]
	void VerifyGenerateId()
	{
		// Mongo must be able to publicly construct the generator
		MongoSequentialGuidGenerator generator = new();
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
		MongoSequentialGuidGenerator generator = new();
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

	[Fact]
	void SequentialGuidBsonSerializerRoundTrip()
	{
		SequentialGuid seqGuid = new();
		var result = Roundtrip(SequentialGuidSerializer.Instance, seqGuid);
		result.Value.ShouldBe(seqGuid.Value);
		result.Timestamp.ShouldBe(seqGuid.Timestamp);
	}

	[Fact]
	void SequentialSqlGuidBsonSerializerRoundTrip()
	{
		SequentialSqlGuid seqSqlGuid = new();
		var result = Roundtrip(SequentialSqlGuidSerializer.Instance, seqSqlGuid);
		result.Value.ShouldBe(seqSqlGuid.Value);
		result.Timestamp.ShouldBe(seqSqlGuid.Timestamp);
	}

	static T Roundtrip<T>(IBsonSerializer<T> serializer, T value)
	{
		BsonDocument document = [];
		BsonDocumentWriter writer = new(document);
		var writeContext = BsonSerializationContext.CreateRoot(writer);
		writer.WriteStartDocument();
		writer.WriteName("v");
		serializer.Serialize(writeContext, default, value);
		writer.WriteEndDocument();

		BsonDocumentReader reader = new(document);
		var readContext = BsonDeserializationContext.CreateRoot(reader);
		reader.ReadStartDocument();
		reader.ReadName();
		var result = serializer.Deserialize(readContext, default);
		reader.ReadEndDocument();
		return result;
	}
}
