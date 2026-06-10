using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace SequentialGuid.EntityFrameworkCore;

/// <summary>
/// Generates RFC 9562 version 7 <see cref="Guid"/> key values in standard byte order.
/// </summary>
public sealed class SequentialGuidValueGenerator : ValueGenerator<Guid>
{
	/// <summary>Always <see langword="false"/> — generated keys are real, client-generated values.</summary>
	public override bool GeneratesTemporaryValues => false;

	/// <summary>Creates a new time-ordered version 7 <see cref="Guid"/>.</summary>
	/// <param name="entry">The change-tracking entry for the entity being assigned a key. Not used.</param>
	/// <returns>A new time-ordered version 7 <see cref="Guid"/>.</returns>
	public override Guid Next(EntityEntry entry) =>
		GuidV7.NewGuid();
}
