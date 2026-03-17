using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SequentialGuid.EntityFrameworkCore;

sealed class SequentialGuidValueConverter<T>() : ValueConverter<T, Guid>(
	static v => v.Value,
	static v => FromDb(v)
) where T : struct, ISequentialGuid<T>
{
	static T FromDb(Guid v) =>
		T.Create(v);
}
