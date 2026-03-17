using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace SequentialGuid.MongoDB.Serializers;

sealed class SequentialGuidSerializationProvider : IBsonSerializationProvider
{
	public IBsonSerializer? GetSerializer(Type type)
	{
		if (type == typeof(SequentialGuid)) return SequentialGuidSerializer.Instance;
		if (type == typeof(SequentialGuid?)) return new NullableSerializer<SequentialGuid>(SequentialGuidSerializer.Instance);
		if (type == typeof(SequentialSqlGuid)) return SequentialSqlGuidSerializer.Instance;
		return type == typeof(SequentialSqlGuid?) ? new NullableSerializer<SequentialSqlGuid>(SequentialSqlGuidSerializer.Instance) : null; // fall through to default
	}
}
