using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace SequentialGuid.MongoDB.Serializers;

abstract class SequentialGuidSerializerBase<T> : SerializerBase<T> where T : struct
{
	// ReSharper disable once StaticMemberInGenericType
	static readonly IBsonSerializer<Guid> Serializer = BsonSerializer.LookupSerializer<Guid>();

	public override T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args) =>
		FromGuid(Serializer.Deserialize(context, new() { NominalType = typeof(Guid) }));

	public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value) =>
		Serializer.Serialize(context, new() { NominalType = typeof(Guid) }, ToGuid(value));

	protected abstract T FromGuid(Guid value);

	protected abstract Guid ToGuid(T value);
}

sealed class SequentialGuidSerializer : SequentialGuidSerializerBase<SequentialGuid>
{
	private SequentialGuidSerializer() { }

	public static SequentialGuidSerializer Instance { get; } = new();

	protected override SequentialGuid FromGuid(Guid value) =>
		new(value);

	protected override Guid ToGuid(SequentialGuid value) =>
		value.Value;
}

sealed class SequentialSqlGuidSerializer : SequentialGuidSerializerBase<SequentialSqlGuid>
{
	private SequentialSqlGuidSerializer() { }

	public static SequentialSqlGuidSerializer Instance { get; } = new();

	protected override SequentialSqlGuid FromGuid(Guid value) =>
		new(value);

	protected override Guid ToGuid(SequentialSqlGuid value) =>
		value.Value;
}
