using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace SequentialGuid.EntityFrameworkCore;

/// <summary>
/// Generates RFC 9562 version 7 <see cref="Guid"/> key values in SQL Server byte order,
/// suitable for <c>uniqueidentifier</c> clustered primary keys.
/// </summary>
public sealed class SequentialSqlGuidValueGenerator : ValueGenerator<Guid>
{
	/// <summary>Always <see langword="false"/> — generated keys are real, client-generated values.</summary>
	public override bool GeneratesTemporaryValues => false;

	/// <summary>Creates a new time-ordered version 7 <see cref="Guid"/> in SQL Server sort order.</summary>
	/// <param name="entry">The change-tracking entry for the entity being assigned a key. Not used.</param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/> with bytes in SQL Server sort order.</returns>
	public override Guid Next(EntityEntry entry) =>
		GuidV7.NewSqlGuid();
}
