using SequentialGuid;

#pragma warning disable IDE0130 // Namespace does not match folder structure
// ReSharper disable once CheckNamespace
namespace NodaTime;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for <see cref="SequentialGuidGenerator"/> that accept NodaTime <see cref="Instant"/> timestamps.
/// </summary>
[Obsolete("Use GuidV8Time.NewGuid(instant) directly instead.")]
public static class SequentialGuidGeneratorExtensions
{
	extension(SequentialGuidGenerator generator)
	{
		/// <summary>
		/// Generates a new sequential <see cref="Guid"/> using the specified NodaTime <see cref="Instant"/> as the timestamp.
		/// </summary>
		/// <param name="timestamp">The <see cref="Instant"/> to embed as the timestamp.</param>
		/// <returns>A new sequential <see cref="Guid"/> with the given timestamp embedded.</returns>
		public Guid NewGuid(Instant timestamp) =>
			generator.NewGuid(timestamp.ToDateTimeUtc());
	}
}
