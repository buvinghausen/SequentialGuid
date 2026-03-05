using MongoDB.Bson.Serialization;

namespace SequentialGuid.MongoDB;

/// <summary>
/// Implements <see cref="IIdGenerator"/> to generate sequential <see cref="Guid"/> values
/// for use as MongoDB document identifiers.
/// </summary>
public sealed class MongoSequentialGuidGenerator : IIdGenerator
{
	/// <summary>
	/// Gets the singleton instance of the generator.
	/// </summary>
	public static MongoSequentialGuidGenerator Instance { get; } = new();

	/// <summary>
	/// Generates a new sequential <see cref="Guid"/> as the document identifier.
	/// </summary>
	/// <param name="container">The container of the document being assigned an id.</param>
	/// <param name="document">The document being assigned an id.</param>
	/// <returns>A new sequential <see cref="Guid"/>.</returns>
	public object GenerateId(object container, object document) =>
		SequentialGuidGenerator.Instance.NewGuid();

	/// <summary>
	/// Determines whether the specified id is considered empty.
	/// </summary>
	/// <param name="id">The id value to test.</param>
	/// <returns><see langword="true"/> if <paramref name="id"/> is not a <see cref="Guid"/> or equals <see cref="Guid.Empty"/>; otherwise <see langword="false"/>.</returns>
	public bool IsEmpty(object id) =>
		id is not Guid guid || guid == Guid.Empty;
	// Pattern matching is life
	// Anything that isn't a guid is empty
	// Guid is considered not empty as long as it's not all 0s
}
