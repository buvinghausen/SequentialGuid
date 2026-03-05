using MongoDB.Bson.Serialization;

namespace SequentialGuid.MongoDB;

/// <summary>
/// Provides extension methods for registering <see cref="MongoSequentialGuidGenerator"/> with the MongoDB BSON serializer.
/// </summary>
public static class MongoSequentialGuidGeneratorExtensions
{
	extension(MongoSequentialGuidGenerator generator)
	{
		/// <summary>
		/// Registers this generator as the MongoDB BSON id generator for <see cref="Guid"/> values.
		/// </summary>
		public void RegisterMongoIdGenerator() =>
			BsonSerializer.RegisterIdGenerator(typeof(Guid), generator);
	}
}
