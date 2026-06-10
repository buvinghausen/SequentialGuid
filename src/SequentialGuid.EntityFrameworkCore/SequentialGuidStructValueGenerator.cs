using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using SeqGuid = SequentialGuid.SequentialGuid;

namespace SequentialGuid.EntityFrameworkCore;

/// <summary>
/// Generates <see cref="SeqGuid"/> key values backed by RFC 9562 version 7 GUIDs.
/// </summary>
public sealed class SequentialGuidStructValueGenerator : ValueGenerator<SeqGuid>
{
	/// <summary>Always <see langword="false"/> — generated keys are real, client-generated values.</summary>
	public override bool GeneratesTemporaryValues => false;

	/// <summary>Creates a new <see cref="SeqGuid"/> wrapping a time-ordered version 7 GUID.</summary>
	/// <param name="entry">The change-tracking entry for the entity being assigned a key. Not used.</param>
	/// <returns>A new <see cref="SeqGuid"/> wrapping a time-ordered version 7 GUID.</returns>
	public override SeqGuid Next(EntityEntry entry) =>
		new();
}
