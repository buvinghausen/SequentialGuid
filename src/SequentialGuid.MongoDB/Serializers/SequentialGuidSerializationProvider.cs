using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace SequentialGuid.MongoDB.Serializers;

sealed class SequentialGuidSerializationProvider : IBsonSerializationProvider
{
	private static readonly IBsonSerializer<SequentialGuid?> NullableSequentialGuidSerializer =
		new NullableSerializer<SequentialGuid>(SequentialGuidSerializer.Instance);

	private static readonly IBsonSerializer<SequentialSqlGuid?> NullableSequentialSqlGuidSerializer =
		new NullableSerializer<SequentialSqlGuid>(SequentialSqlGuidSerializer.Instance);

	public IBsonSerializer? GetSerializer(Type type)
	{
		if (type == typeof(SequentialGuid)) return SequentialGuidSerializer.Instance;
		if (type == typeof(SequentialGuid?)) return NullableSequentialGuidSerializer;
		if (type == typeof(SequentialSqlGuid)) return SequentialSqlGuidSerializer.Instance;
		return type == typeof(SequentialSqlGuid?) ? NullableSequentialSqlGuidSerializer : null; // fall through to default
	}
}
