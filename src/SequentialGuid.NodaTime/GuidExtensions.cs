using SequentialGuid.NodaTime;

#pragma warning disable IDE0130 // Namespace does not match folder structure
// ReSharper disable once CheckNamespace
namespace NodaTime;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for extracting NodaTime <see cref="Instant"/> timestamps from sequential <see cref="Guid"/> values.
/// </summary>
public static class GuidExtensions
{
	extension(Guid id)
	{
		/// <summary>
		/// Extracts the embedded UTC timestamp from a sequential <see cref="Guid"/> as a NodaTime <see cref="Instant"/>.
		/// </summary>
		/// <returns>The embedded <see cref="Instant"/>, or <see langword="null"/> if no timestamp is present.</returns>
		public Instant? ToInstant() =>
			id.ToDateTime().ToInstant();
	}
}
