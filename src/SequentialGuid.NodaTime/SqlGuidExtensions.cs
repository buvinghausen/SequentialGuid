using NodaTime;
using SequentialGuid;
using SequentialGuid.NodaTime;

#pragma warning disable IDE0130 // Namespace does not match folder structure
// ReSharper disable once CheckNamespace
namespace System.Data.SqlTypes;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for extracting NodaTime <see cref="Instant"/> timestamps from sequential <see cref="SqlGuid"/> values.
/// </summary>
public static class SqlGuidExtensions
{
	extension(SqlGuid sqlGuid)
	{
		/// <summary>
		/// Extracts the embedded UTC timestamp from a sequential <see cref="SqlGuid"/> as a NodaTime <see cref="Instant"/>.
		/// </summary>
		/// <returns>The embedded <see cref="Instant"/>, or <see langword="null"/> if no timestamp is present.</returns>
		public Instant? ToInstant() =>
			sqlGuid.ToDateTime().ToInstant();
	}
}
