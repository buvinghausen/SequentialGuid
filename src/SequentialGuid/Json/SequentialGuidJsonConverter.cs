#if NET7_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SequentialGuid.Json;

sealed class SequentialGuidJsonConverter<T> : JsonConverter<T> where T : struct, ISequentialGuid<T>
{
	public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		T.Create(reader.GetGuid());

	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
		writer.WriteStringValue(value.Value);
}
#endif
