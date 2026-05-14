#if NET7_0_OR_GREATER
using System.Text.Json;
using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class JsonConverterTests
{
	static JsonSerializerOptions CreateOptions() =>
		new JsonSerializerOptions().AddSequentialGuidConverters();

	[Fact]
	void AddSequentialGuidConvertersRegistersSequentialGuidConverter()
	{
		// Act
		var options = CreateOptions();
		// Assert
		options.Converters.Any(c => c.CanConvert(typeof(SequentialGuid))).ShouldBeTrue();
	}

	[Fact]
	void AddSequentialGuidConvertersRegistersSequentialSqlGuidConverter()
	{
		// Act
		var options = CreateOptions();
		// Assert
		options.Converters.Any(c => c.CanConvert(typeof(SequentialSqlGuid))).ShouldBeTrue();
	}

	[Fact]
	void AddSequentialGuidConvertersIsIdempotent()
	{
		// Arrange
		var options = new JsonSerializerOptions();
		// Act
		options.AddSequentialGuidConverters();
		options.AddSequentialGuidConverters();
		// Assert
		options.Converters.Count.ShouldBe(2);
	}

	[Fact]
	void AddSequentialGuidConvertersReturnsOptionsForChaining()
	{
		// Arrange
		var options = new JsonSerializerOptions();
		// Act
		var returned = options.AddSequentialGuidConverters();
		// Assert
		returned.ShouldBeSameAs(options);
	}

	[Fact]
	void SequentialGuidRoundTrips()
	{
		// Arrange
		var options = CreateOptions();
		SequentialGuid original = new();
		// Act
		var json = JsonSerializer.Serialize(original, options);
		var deserialized = JsonSerializer.Deserialize<SequentialGuid>(json, options);
		// Assert
		deserialized.Value.ShouldBe(original.Value);
		deserialized.Timestamp.ShouldBe(original.Timestamp);
	}

	[Fact]
	void NullableSequentialGuidWithValueRoundTrips()
	{
		// Arrange
		var options = CreateOptions();
		SequentialGuid? original = new();
		// Act
		var json = JsonSerializer.Serialize(original, options);
		var deserialized = JsonSerializer.Deserialize<SequentialGuid?>(json, options);
		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Value.Value.ShouldBe(original.Value.Value);
		deserialized.Value.Timestamp.ShouldBe(original.Value.Timestamp);
	}

	[Fact]
	void NullableSequentialGuidNullRoundTrips()
	{
		// Arrange
		var options = CreateOptions();
		SequentialGuid? original = null;
		// Act
		var json = JsonSerializer.Serialize(original, options);
		var deserialized = JsonSerializer.Deserialize<SequentialGuid?>(json, options);
		// Assert
		deserialized.ShouldBeNull();
	}

	[Fact]
	void SequentialSqlGuidRoundTrips()
	{
		// Arrange
		var options = CreateOptions();
		SequentialSqlGuid original = new();
		// Act
		var json = JsonSerializer.Serialize(original, options);
		var deserialized = JsonSerializer.Deserialize<SequentialSqlGuid>(json, options);
		// Assert
		deserialized.Value.ShouldBe(original.Value);
		deserialized.Timestamp.ShouldBe(original.Timestamp);
	}

	[Fact]
	void NullableSequentialSqlGuidWithValueRoundTrips()
	{
		// Arrange
		var options = CreateOptions();
		SequentialSqlGuid? original = new();
		// Act
		var json = JsonSerializer.Serialize(original, options);
		var deserialized = JsonSerializer.Deserialize<SequentialSqlGuid?>(json, options);
		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Value.Value.ShouldBe(original.Value.Value);
		deserialized.Value.Timestamp.ShouldBe(original.Value.Timestamp);
	}

	[Fact]
	void NullableSequentialSqlGuidNullRoundTrips()
	{
		// Arrange
		var options = CreateOptions();
		SequentialSqlGuid? original = null;
		// Act
		var json = JsonSerializer.Serialize(original, options);
		var deserialized = JsonSerializer.Deserialize<SequentialSqlGuid?>(json, options);
		// Assert
		deserialized.ShouldBeNull();
	}
}
#endif
