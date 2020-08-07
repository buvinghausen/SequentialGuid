using System;
using MongoDB.Bson.Serialization;

namespace SequentialGuid.MongoDB
{
	public static class ExtensionMethods
	{
		public static void RegisterGlobally(this SequentialGuidGenerator generator) =>
			BsonSerializer.RegisterIdGenerator(typeof(Guid), generator);
	}
}
