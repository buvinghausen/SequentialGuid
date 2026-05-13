namespace SequentialGuid.Extensions;

static class SequentialGuidByteOrder
{
	/// <summary>
	/// Detects whether <paramref name="value"/> is a recognised sequential GUID,
	/// and in which byte order. Encapsulates the V7/V8/legacy detection and the
	/// SQL-V8 false-positive guard that disambiguates by requiring a valid timestamp
	/// when both standard and SQL detection fire on the same bytes.
	/// </summary>
	/// <param name="value">The candidate GUID.</param>
	/// <param name="wasSqlOrder">
	/// On return, <see langword="true"/> if the bytes were in SQL Server byte order;
	/// <see langword="false"/> if in standard RFC byte order.
	/// Undefined when this method returns <see langword="false"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="value"/> is a V7, V8, or legacy
	/// sequential GUID in either byte order; otherwise <see langword="false"/>.
	/// </returns>
	internal static bool TryDetect(Guid value, out bool wasSqlOrder)
	{
#if NET6_0_OR_GREATER
		Span<byte> bytes = stackalloc byte[16];
		value.TryWriteBytes(bytes);
#else
		var bytes = value.ToByteArray();
#endif
		// Standard byte order detection. Guard against SQL-ordered V8 GUIDs whose
		// counter byte (mapped to position [7]) accidentally has high nibble 7 or 8,
		// which makes IsRfc9562Version fire as a false positive. Disambiguate by
		// requiring a valid timestamp when SQL detection also fires.
		if ((bytes.IsRfc9562Version(7) || bytes.IsRfc9562Version(8) || bytes.IsLegacy()) &&
			(bytes.ToTicks() is { IsDateTime: true } ||
			 !bytes.IsSqlRfc9562Version(7) && !bytes.IsSqlRfc9562Version(8) && !bytes.IsSqlLegacy()))
		{
			wasSqlOrder = false;
			return true;
		}

		if (bytes.IsSqlRfc9562Version(7) || bytes.IsSqlRfc9562Version(8) || bytes.IsSqlLegacy())
		{
			wasSqlOrder = true;
			return true;
		}

		wasSqlOrder = false;
		return false;
	}
}
