using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace SequentialGuid.MongoDB.Serializers;

sealed class SequentialGuidSerializationProvider : IBsonSerializationProvider
{
	static readonly IBsonSerializer<SequentialGuid?> _nullableSequentialGuidSerializer =
		new NullableSerializer<SequentialGuid>(SequentialGuidSerializer.Instance);

	static readonly IBsonSerializer<SequentialSqlGuid?> _nullableSequentialSqlGuidSerializer =
		new NullableSerializer<SequentialSqlGuid>(SequentialSqlGuidSerializer.Instance);

	public IBsonSerializer? GetSerializer(Type type)
	{
		if (type == typeof(SequentialGuid))
			return SequentialGuidSerializer.Instance;
		if (type == typeof(SequentialGuid?))
			return _nullableSequentialGuidSerializer;
		if (type == typeof(SequentialSqlGuid))
			return SequentialSqlGuidSerializer.Instance;
		return type == typeof(SequentialSqlGuid?) ? _nullableSequentialSqlGuidSerializer : null; // fall through to default
	}
}
