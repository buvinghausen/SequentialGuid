using System;
using MongoDB.Bson.Serialization;

namespace SequentialGuid.MongoDB
{
	/// <summary>
	///     Implementation of the <see cref="IIdGenerator" /> interface so it can be used by the Mongo driver
	/// </summary>
	public class SequentialGuidGenerator : IIdGenerator
	{
		/// <summary>
		///     Gets an instance of SequentialGuidGenerator helpful for calling the registration method
		/// </summary>
		public static SequentialGuidGenerator Instance { get; } = new();

		/// <summary>
		/// </summary>
		/// <param name="container"></param>
		/// <param name="document"></param>
		/// <returns></returns>
		public object GenerateId(object container, object document)
		{
			return SequentialGuid.SequentialGuidGenerator.Instance.NewGuid();
		}

		/// <summary>
		///     Checks to see if existing value is empty and needs to be replaced
		/// </summary>
		/// <param name="id">Current value from the document</param>
		/// <returns>True or false on if GenerateId needs to be invoked</returns>
		public bool IsEmpty(object id)
		{
			return id == null || (Guid)id == Guid.Empty;
		}
	}
}
