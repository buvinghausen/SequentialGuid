#if NET10_0_OR_GREATER
using System.Text;
#endif
using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class SequentialGuidStructTests
{
	// Known guids for each supported category
	static readonly Guid V7Guid = GuidV7.NewGuid();
	static readonly Guid V8Guid = GuidV8Time.NewGuid();
	static readonly Guid LegacyGuid = new("08de7bf5-381d-cc8b-f24c-56e3580439dd");

	[Fact]
	void DefaultConstructorCreatesVersion7()
	{
		// Act
		SequentialGuid id = new();
		var bytes = id.Value.ToByteArray();
		// Assert
		bytes.IsRfc9562Version(7).ShouldBeTrue();
	}

	[Fact]
	void DefaultConstructorProducesUniqueValues()
	{
		// Act
		SequentialGuid
			first = new(),
			second = new();
		// Assert
		first.ShouldNotBe(second);
	}

	[Fact]
	void GuidConstructorAcceptsVersion7()
	{
		// Act
		SequentialGuid id = new(V7Guid);
		// Assert
		id.Value.ShouldBe(V7Guid);
	}

	[Fact]
	void GuidConstructorAcceptsVersion8()
	{
		// Act
		SequentialGuid id = new(V8Guid);
		// Assert
		id.Value.ShouldBe(V8Guid);
	}

	[Fact]
	void GuidConstructorAcceptsLegacy()
	{
		// Act
		SequentialGuid id = new(LegacyGuid);
		// Assert
		id.Value.ShouldBe(LegacyGuid);
	}

	[Fact]
	void GuidConstructorThrowsForRandomGuid()
	{
		// Use the RFC 9562 §A.4 v4 test vector — a fixed guid avoids the 1/256 flakiness
		// where a random v4 guid's b[10] happens to equal 8, accidentally satisfying IsSqlLegacy()
		// after SQL byte conversion shifts the variant bits out of position.
		var v4 = new Guid("919108f7-52d1-4320-9bac-f847db4148a8");
		Should.Throw<ArgumentException>(() => new SequentialGuid(v4));
	}

	[Fact]
	void GuidConstructorThrowsForEmptyGuid()
	{
		Should.Throw<ArgumentException>(() => new SequentialGuid(Guid.Empty));
	}

	[Fact]
	void StringConstructorDelegatesToGuidConstructor()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		// Act
		SequentialGuid id = new(v7.ToString());
		// Assert
		id.Value.ShouldBe(v7);
	}

	[Fact]
	void StringConstructorThrowsForInvalidFormat()
	{
		Should.Throw<FormatException>(() => new SequentialGuid("not-a-guid"));
	}

	[Fact]
	void StringConstructorThrowsForNonSequentialGuid()
	{
		// Use the RFC 9562 §A.4 v4 test vector — a fixed guid avoids the 1/256 flakiness
		// where a random v4 guid's b[10] happens to equal 8, accidentally satisfying IsSqlLegacy()
		// after SQL byte conversion shifts the variant bits out of position.
		Should.Throw<ArgumentException>(() => new SequentialGuid("919108f7-52d1-4320-9bac-f847db4148a8"));
	}

	[Fact]
	void RecordEqualityBasedOnValue()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		SequentialGuid
			a = new(v7),
			b = new(v7);
		// Assert
		a.ShouldBe(b);
		(a == b).ShouldBeTrue();
	}

	[Fact]
	void RecordInequalityForDifferentValues()
	{
		// Act
		SequentialGuid
			a = new(),
			b = new();
		// Assert
		a.ShouldNotBe(b);
		(a != b).ShouldBeTrue();
	}

	[Fact]
	void ToStringShouldMatchGuid()
	{
		// Act
		SequentialGuid id = new();
		var expected = id.Value.ToString();

		// Assert
		id.ToString().ShouldBe(expected);
	}

	// --- Timestamp tests ---

	[Fact]
	void DefaultConstructorTimestampIsUtcAndCurrent()
	{
		// Arrange
		var before = DateTime.UtcNow.TruncateToMs();
		// Act
		SequentialGuid id = new();
		var after = DateTime.UtcNow.TruncateToMs();
		// Assert
		id.Timestamp.Kind.ShouldBe(DateTimeKind.Utc);
		id.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		id.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	void GuidConstructorPreservesTimestamp()
	{
		// Arrange
		SequentialGuid original = new();
		// Act
		SequentialGuid reconstructed = new(original.Value);
		// Assert
		reconstructed.Timestamp.ShouldBe(original.Timestamp);
	}

	// --- V8Custom constructor tests ---

	[Fact]
	void V8CustomConstructorCreatesVersion8()
	{
		// Act
		SequentialGuid id = new(SequentialGuidType.Rfc9562V8Custom);
		var bytes = id.Value.ToByteArray();
		// Assert
		bytes.IsRfc9562Version(8).ShouldBeTrue();
	}

	[Fact]
	void V8CustomConstructorTimestampIsUtcAndCurrent()
	{
		// Arrange
		var before = DateTime.UtcNow;
		// Act
		SequentialGuid id = new(SequentialGuidType.Rfc9562V8Custom);
		var after = DateTime.UtcNow;
		// Assert
		id.Timestamp.Kind.ShouldBe(DateTimeKind.Utc);
		id.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		id.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	void V8CustomConstructorProducesUniqueValues()
	{
		// Act
		SequentialGuid
			first = new(SequentialGuidType.Rfc9562V8Custom),
			second = new(SequentialGuidType.Rfc9562V8Custom);
		// Assert
		first.ShouldNotBe(second);
	}

	// --- Comparison operator tests ---

	[Fact]
	void LessThanOperator()
	{
		// Arrange — timestamps 1 s apart guarantee ordering
		var earlier = new SequentialGuid(GuidV7.NewGuid(1000L));
		var later = new SequentialGuid(GuidV7.NewGuid(2000L));
		// Assert
		(earlier < later).ShouldBeTrue();
		(later < earlier).ShouldBeFalse();
	}

	[Fact]
	void LessThanOrEqualOperator()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		SequentialGuid
			a = new(v7),
			b = new(v7);
		var later = new SequentialGuid(GuidV7.NewGuid(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60_000));
		// Assert
		(a <= b).ShouldBeTrue();  // equal
		(a <= later).ShouldBeTrue();  // less
		(later <= a).ShouldBeFalse();
	}

	[Fact]
	void GreaterThanOperator()
	{
		// Arrange
		var earlier = new SequentialGuid(GuidV7.NewGuid(1000L));
		var later = new SequentialGuid(GuidV7.NewGuid(2000L));
		// Assert
		(later > earlier).ShouldBeTrue();
		(earlier > later).ShouldBeFalse();
	}

	[Fact]
	void GreaterThanOrEqualOperator()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		SequentialGuid
			a = new(v7),
			b = new(v7);
		var later = new SequentialGuid(GuidV7.NewGuid(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60_000));
		// Assert
		(a >= b).ShouldBeTrue();  // equal
		(later >= a).ShouldBeTrue();  // greater
		(a >= later).ShouldBeFalse();
	}

#if NET7_0_OR_GREATER
	[Fact]
	void ParseStringReturnsMatchingSequentialGuid()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		// Act
		var id = SequentialGuid.Parse(v7.ToString(), null);
		// Assert
		id.Value.ShouldBe(v7);
	}

	[Fact]
	void ParseStringThrowsFormatExceptionForInvalidInput()
	{
		Should.Throw<FormatException>(() => SequentialGuid.Parse("not-a-guid", null));
	}

	[Fact]
	void ParseStringThrowsArgumentExceptionForNonSequentialGuid()
	{
		Should.Throw<ArgumentException>(() => SequentialGuid.Parse("919108f7-52d1-4320-9bac-f847db4148a8", null));
	}

	[Fact]
	void TryParseStringReturnsTrueForValidSequentialGuid()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		// Act
		var success = SequentialGuid.TryParse(v7.ToString(), null, out var id);
		// Assert
		success.ShouldBeTrue();
		id.Value.ShouldBe(v7);
	}

	[Fact]
	void TryParseStringReturnsFalseForInvalidInput()
	{
		SequentialGuid.TryParse("not-a-guid", null, out _).ShouldBeFalse();
	}

	[Fact]
	void TryParseStringReturnsFalseForNonSequentialGuid()
	{
		SequentialGuid.TryParse("919108f7-52d1-4320-9bac-f847db4148a8", null, out _).ShouldBeFalse();
	}

	[Fact]
	void TryParseStringReturnsFalseForNull()
	{
		string? val = null;
		SequentialGuid.TryParse(val, null, out _).ShouldBeFalse();
	}

	[Fact]
	void ParseSpanReturnsMatchingSequentialGuid()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		// Act
		var id = SequentialGuid.Parse(v7.ToString().AsSpan(), null);
		// Assert
		id.Value.ShouldBe(v7);
	}

	[Fact]
	void ParseSpanThrowsFormatExceptionForInvalidInput()
	{
		Should.Throw<FormatException>(() => SequentialGuid.Parse("not-a-guid".AsSpan(), null));
	}

	[Fact]
	void ParseSpanThrowsArgumentExceptionForNonSequentialGuid()
	{
		Should.Throw<ArgumentException>(() => SequentialGuid.Parse("919108f7-52d1-4320-9bac-f847db4148a8".AsSpan(), null));
	}

	[Fact]
	void TryParseSpanReturnsTrueForValidSequentialGuid()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		// Act
		var success = SequentialGuid.TryParse(v7.ToString().AsSpan(), null, out var id);
		// Assert
		success.ShouldBeTrue();
		id.Value.ShouldBe(v7);
	}

	[Fact]
	void TryParseSpanReturnsFalseForInvalidInput()
	{
		SequentialGuid.TryParse("not-a-guid".AsSpan(), null, out _).ShouldBeFalse();
	}

	[Fact]
	void TryParseSpanReturnsFalseForNonSequentialGuid()
	{
		SequentialGuid.TryParse("919108f7-52d1-4320-9bac-f847db4148a8".AsSpan(), null, out _).ShouldBeFalse();
	}
#endif

#if NET6_0_OR_GREATER
	[Fact]
	void TryFormatCharSpanWritesGuidString()
	{
		// Arrange
		SequentialGuid id = new();
		Span<char> buffer = stackalloc char[36];
		// Act
		var success = id.TryFormat(buffer, out var charsWritten, default, null);
		// Assert
		success.ShouldBeTrue();
		charsWritten.ShouldBe(36);
		new string(buffer).ShouldBe(id.Value.ToString());
	}
#endif

#if NET10_0_OR_GREATER
	[Fact]
	void TryFormatUtf8SpanWritesGuidBytes()
	{
		// Arrange
		SequentialGuid id = new();
		Span<byte> buffer = stackalloc byte[36];
		// Act
		var success = id.TryFormat(buffer, out var bytesWritten, default, null);
		// Assert
		success.ShouldBeTrue();
		bytesWritten.ShouldBe(36);
		Encoding.UTF8.GetString(buffer).ShouldBe(id.Value.ToString());
	}

	[Fact]
	void ParseUtf8SpanReturnsMatchingSequentialGuid()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		var utf8 = Encoding.UTF8.GetBytes(v7.ToString());
		// Act
		var id = SequentialGuid.Parse(utf8, null);
		// Assert
		id.Value.ShouldBe(v7);
	}

	[Fact]
	void ParseUtf8SpanThrowsFormatExceptionForInvalidInput()
	{
		var bytes = "not-a-guid"u8.ToArray();
		Should.Throw<FormatException>(() => SequentialGuid.Parse(bytes, null));
	}

	[Fact]
	void ParseUtf8SpanThrowsArgumentExceptionForNonSequentialGuid()
	{
		var bytes = "919108f7-52d1-4320-9bac-f847db4148a8"u8.ToArray();
		Should.Throw<ArgumentException>(() => SequentialGuid.Parse(bytes, null));
	}

	[Fact]
	void TryParseUtf8SpanReturnsTrueForValidSequentialGuid()
	{
		// Arrange
		var v7 = GuidV7.NewGuid();
		var utf8 = Encoding.UTF8.GetBytes(v7.ToString());
		// Act
		var success = SequentialGuid.TryParse(utf8, null, out var id);
		// Assert
		success.ShouldBeTrue();
		id.Value.ShouldBe(v7);
	}

	[Fact]
	void TryParseUtf8SpanReturnsFalseForInvalidInput()
	{
		SequentialGuid.TryParse("not-a-guid"u8, null, out _).ShouldBeFalse();
	}

	[Fact]
	void TryParseUtf8SpanReturnsFalseForNonSequentialGuid()
	{
		SequentialGuid.TryParse("919108f7-52d1-4320-9bac-f847db4148a8"u8, null, out _).ShouldBeFalse();
	}
#endif
}
