using MongoDB.Bson.Serialization;

namespace SequentialGuid.MongoDB;

/// <summary>
/// Provides a mechanism for generating sequential <see cref="Guid"/> values specifically for use with MongoDB.
/// </summary>
/// <remarks>
/// The <see cref="MongoSequentialGuidGenerator"/> class is designed to integrate seamlessly with MongoDB's BSON serialization framework.
/// It generates sequential <see cref="Guid"/> values to improve database indexing performance by reducing fragmentation.
/// This class implements the <see cref="IIdGenerator"/> interface, making it compatible with MongoDB's ID generation system.
/// </remarks>
public sealed class MongoSequentialGuidGenerator : IIdGenerator
{
	/// <summary>
	/// Gets the singleton instance of the <see cref="SequentialGuidGenerator"/> class.
	/// </summary>
	/// <value>
	/// The singleton instance of <see cref="SequentialGuidGenerator"/>.
	/// </value>
	/// <remarks>
	/// This property provides a globally accessible instance of the <see cref="MongoSequentialGuidGenerator"/>.
	/// It is designed to integrate seamlessly with MongoDB's BSON serialization framework, enabling the generation
	/// of sequential <see cref="Guid"/> values for improved database indexing performance.
	/// </remarks>
	public static MongoSequentialGuidGenerator Instance { get; } = new();
	
	/// <summary>
	/// Generates a new sequential <see cref="Guid"/> to be used as an identifier for a MongoDB document.
	/// </summary>
	/// <param name="container">The container object that holds the document. This parameter is not used in the implementation.</param>
	/// <param name="document">The document for which the ID is being generated. This parameter is not used in the implementation.</param>
	/// <returns>A new sequential <see cref="Guid"/>.</returns>
	/// <remarks>
	/// This method utilizes the <see cref="SequentialGuidGenerator"/> to generate a sequential <see cref="Guid"/>.
	/// Sequential GUIDs are designed to improve performance in scenarios such as database indexing by reducing fragmentation.
	/// </remarks>
	public object GenerateId(object container, object document) =>
		SequentialGuidGenerator.Instance.NewGuid();
	
	/// <summary>
	/// Determines whether the specified identifier is considered empty.
	/// </summary>
	/// <param name="id">The identifier to evaluate. This can be of any type.</param>
	/// <returns>
	/// <c>true</c> if the <paramref name="id"/> is either not a <see cref="Guid"/> or is an empty <see cref="Guid"/>; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// This method checks if the provided <paramref name="id"/> is either not a <see cref="Guid"/> or represents an empty <see cref="Guid"/> value.
	/// It is useful for validating identifiers in scenarios where <see cref="Guid"/> values are expected.
	/// </remarks>
	public bool IsEmpty(object id) =>
		id is not Guid guid || guid == Guid.Empty;
	// Pattern matching is life
	// Anything that isn't a guid is empty
	// Guid is considered not empty as long as it's not all 0s
}
