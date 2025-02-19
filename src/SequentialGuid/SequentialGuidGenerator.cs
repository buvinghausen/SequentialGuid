namespace SequentialGuid;

/// <summary>
/// Represents a generator for creating sequential <see cref="Guid"/> values.
/// </summary>
/// <remarks>
/// This sealed class inherits from <see cref="SequentialGuidGeneratorBase{T}"/> and provides
/// an implementation for generating sequential GUIDs. Sequential GUIDs are particularly useful
/// in scenarios where GUIDs are used as primary keys in databases, as they can improve indexing
/// performance by reducing fragmentation.
/// </remarks>
public sealed class SequentialGuidGenerator : SequentialGuidGeneratorBase<SequentialGuidGenerator>
{
	private SequentialGuidGenerator() { }
}
