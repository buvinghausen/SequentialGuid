using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using SequentialGuid.MongoDB.Serializers;

namespace SequentialGuid.MongoDB.Tests;

public sealed class BsonSerializerExtensionsTests : IClassFixture<BsonSerializerRegistrationFixture>
{
	[Fact]
	void SequentialGuidSerializerIsRegistered() =>
		BsonSerializer.LookupSerializer<SequentialGuid>().ShouldBeOfType<SequentialGuidSerializer>();

	[Fact]
	void NullableSequentialGuidSerializerIsRegistered() =>
		BsonSerializer.LookupSerializer<SequentialGuid?>().ShouldBeOfType<NullableSerializer<SequentialGuid>>();

	[Fact]
	void SequentialSqlGuidSerializerIsRegistered() =>
		BsonSerializer.LookupSerializer<SequentialSqlGuid>().ShouldBeOfType<SequentialSqlGuidSerializer>();

	[Fact]
	void NullableSequentialSqlGuidSerializerIsRegistered() =>
		BsonSerializer.LookupSerializer<SequentialSqlGuid?>().ShouldBeOfType<NullableSerializer<SequentialSqlGuid>>();
}

sealed class BsonSerializerRegistrationFixture
{
	public BsonSerializerRegistrationFixture() =>
		BsonSerializer.RegisterSequentialGuidSerializers();
}
