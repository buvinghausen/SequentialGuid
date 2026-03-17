#if NET7_0_OR_GREATER
using System.Text.Json;
using SequentialGuid.Json;

namespace SequentialGuid.Extensions;

/// <summary>
/// Provides extension methods for <see cref="JsonSerializerOptions" /> to register SequentialGuid JSON converters.
/// </summary>
public static class SequentialGuidJsonOptionsExtensions
{
	extension(JsonSerializerOptions options)
	{
		/// <summary>
		/// Registers all SequentialGuid JSON converters with the specified <see cref="JsonSerializerOptions" />.
		/// If the converters have already been registered, this method returns without making any changes.
		/// </summary>
		/// <returns>
		/// The same <see cref="JsonSerializerOptions" /> instance so that additional configuration calls can be chained.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="options" /> is <see langword="null" />.</exception>
		public JsonSerializerOptions AddSequentialGuidConverters()
		{
			ArgumentNullException.ThrowIfNull(options);

			// Guard: don't double-register if the caller calls this more than once.
			bool alreadyRegistered = options.Converters
				.Any(c => c is SequentialGuidJsonConverter<SequentialGuid>); // sentinel check

			if (alreadyRegistered)
			{
				return options; // fluent — callers can chain further configuration
			}

			foreach (var converter in JsonConverters.All)
				options.Converters.Add(converter);

			return options; // fluent — callers can chain further configuration
		}
	}
}
#endif
