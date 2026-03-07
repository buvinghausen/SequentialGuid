namespace SequentialGuid;

/// <summary>
/// Generates sequential <see cref="Guid"/> values that are ordered by their embedded timestamp,
/// suitable for use as database primary keys to minimize index fragmentation.
/// </summary>
[Obsolete("Use GuidV8Time static methods directly instead.")]
public sealed class SequentialGuidGenerator : SequentialGuidGeneratorBase<SequentialGuidGenerator>
{
	private SequentialGuidGenerator() { }
}
