#if NET7_0_OR_GREATER
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SequentialGuid.Json;

static class JsonConverters
{
	// Ordered: more-specific converters first so STJ picks them correctly.
	internal static IReadOnlyList<JsonConverter> All { get; } =
	[
		new SequentialGuidJsonConverter<SequentialGuid>(),
		new SequentialGuidJsonConverter<SequentialSqlGuid>()
	];
}
#endif
