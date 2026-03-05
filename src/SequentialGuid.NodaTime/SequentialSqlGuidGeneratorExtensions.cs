using System.Data.SqlTypes;
using SequentialGuid;

#pragma warning disable IDE0130 // Namespace does not match folder structure
// ReSharper disable once CheckNamespace
namespace NodaTime;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for <see cref="SequentialSqlGuidGenerator"/> that accept NodaTime <see cref="Instant"/> timestamps.
/// </summary>
public static class SequentialSqlGuidGeneratorExtensions
{
	extension(SequentialSqlGuidGenerator generator)
	{
		/// <summary>
		/// Generates a new sequential <see cref="Guid"/> using the specified NodaTime <see cref="Instant"/> as the timestamp.
		/// </summary>
		/// <param name="timestamp">The <see cref="Instant"/> to embed as the timestamp.</param>
		/// <returns>A new sequential <see cref="Guid"/> with the given timestamp embedded.</returns>
		public Guid NewGuid(Instant timestamp) =>
			generator.NewGuid(timestamp.ToDateTimeUtc());

		/// <summary>
		/// Generates a new sequential <see cref="SqlGuid"/> using the specified NodaTime <see cref="Instant"/> as the timestamp.
		/// </summary>
		/// <param name="timestamp">The <see cref="Instant"/> to embed as the timestamp.</param>
		/// <returns>A new sequential <see cref="SqlGuid"/> with the given timestamp embedded.</returns>
		public SqlGuid NewSqlGuid(Instant timestamp) =>
			generator.NewSqlGuid(timestamp.ToDateTimeUtc());
	}
}
