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
		/// </summary>
		/// <param name="generator"></param>
		public static void Register(this SequentialGuidGenerator generator)
		{
			BsonSerializer.RegisterIdGenerator(typeof(Guid), generator);
		}
	}
}
