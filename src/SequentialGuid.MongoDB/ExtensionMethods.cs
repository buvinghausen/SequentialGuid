using MongoDB.Bson.Serialization;

namespace SequentialGuid.MongoDB;

/// <summary>
/// Provides extension methods for integrating SequentialGuid functionality with MongoDB.
/// </summary>
public static class ExtensionMethods
{
	/// <summary>
	/// Registers the <see cref="MongoSequentialGuidGenerator"/> as the ID generator for <see cref="Guid"/> types in MongoDB.
	/// </summary>
	/// <param name="generator">
	/// The instance of <see cref="MongoSequentialGuidGenerator"/> to be registered as the ID generator.
	/// </param>
	/// <remarks>
	/// This method integrates the <see cref="MongoSequentialGuidGenerator"/> with MongoDB's BSON serialization framework,
	/// enabling the generation of sequential <see cref="Guid"/> values for MongoDB documents.
	/// Sequential GUIDs can improve database indexing performance by reducing fragmentation.
	/// </remarks>
	public static void RegisterMongoIdGenerator(this MongoSequentialGuidGenerator generator) =>
		BsonSerializer.RegisterIdGenerator(typeof(Guid), generator);
}
