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
		/// Function to generate value for the _id property when it was empty
		/// </summary>
		/// <param name="container">Not used</param>
		/// <param name="document">Not used</param>
		/// <returns>sequential guid</returns>
		public object GenerateId(object container, object document) =>
			SequentialGuid.SequentialGuidGenerator.Instance.NewGuid();

		/// <summary>
		///     Checks to see if existing value is empty and needs to be replaced
		/// </summary>
		/// <param name="id">Current value from the document</param>
		/// <returns>True or false on if GenerateId needs to be invoked</returns>
		public bool IsEmpty(object id) => id is not Guid guid || guid == Guid.Empty;
		// Pattern matching is life
		// Anything that isn't a guid is empty
		// Guid is considered not empty as long as it's not all 0s
	}
}
