using System;
using MongoDB.Bson.Serialization;

namespace SequentialGuid.MongoDB
{
	public class SequentialGuidGenerator : IIdGenerator
	{
		/// <summary>
		/// Gets an instance of SequentialGuidGenerator.
		/// </summary>
		public static SequentialGuidGenerator Instance { get; } = new SequentialGuidGenerator();

		public object GenerateId(object container, object document) =>
			SequentialGuid.SequentialGuidGenerator.Instance.NewGuid();

		public bool IsEmpty(object id) =>
			id == null || (Guid)id == Guid.Empty;
	}
}
