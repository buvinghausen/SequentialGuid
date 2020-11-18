using System;
using MongoDB.Bson.Serialization;

namespace SequentialGuid.MongoDB
{
	/// <summary>
	///     Helper method to make registering the generator easier
	/// </summary>
	public static class ExtensionMethods
	{
		/// <summary>
		/// Registers SequentialGuidGenerator with the Mongo BsonSerializer for all Guid types
		/// </summary>
		/// <param name="generator"></param>
		public static void RegisterMongoIdGenerator(this SequentialGuidGenerator generator)
		{
			BsonSerializer.RegisterIdGenerator(typeof(Guid), generator);
		}
	}
}
