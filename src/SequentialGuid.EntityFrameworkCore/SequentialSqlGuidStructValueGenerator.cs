using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using SeqSqlGuid = SequentialGuid.SequentialSqlGuid;

namespace SequentialGuid.EntityFrameworkCore;

/// <summary>
/// Generates <see cref="SeqSqlGuid"/> key values backed by
/// a version 7 GUID in SQL Server byte order.
/// </summary>
public sealed class SequentialSqlGuidStructValueGenerator : ValueGenerator<SeqSqlGuid>
{
	/// <summary>Always <see langword="false"/> — generated keys are real, client-generated values.</summary>
	public override bool GeneratesTemporaryValues => false;

	/// <summary>Creates a new <see cref="SeqSqlGuid"/> wrapping a SQL-ordered version 7 GUID.</summary>
	/// <param name="entry">The change-tracking entry for the entity being assigned a key. Not used.</param>
	/// <returns>A new <see cref="SeqSqlGuid"/>.</returns>
	public override SeqSqlGuid Next(EntityEntry entry) =>
		new();
}
