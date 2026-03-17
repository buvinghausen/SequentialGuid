using MongoDB.Bson.Serialization;

namespace SequentialGuid.MongoDB.Serializers;

/// <summary>
/// Provides extension methods for registering sequential GUID serializers with the MongoDB BSON serializer.
/// </summary>
public static class BsonSerializerExtensions
{
	extension(BsonSerializer)
	{
		/// <summary>
		/// Registers the <see cref="SequentialGuidSerializationProvider"/> with the MongoDB BSON serializer
		/// to enable serialization of sequential GUID types.
		/// </summary>
		public static void RegisterSequentialGuidSerializers() =>
			BsonSerializer.RegisterSerializationProvider(new SequentialGuidSerializationProvider());
	}
}
