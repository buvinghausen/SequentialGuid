#if NET10_0_OR_GREATER
using System.Text;
#endif
using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class SequentialSqlGuidStructTests
{
	[Fact]
	void DefaultConstructorCreatesVersion7SqlGuid()
	{
		// Act
		SequentialSqlGuid id = new();
		// The SQL guid, after converting back to regular byte order, should be version 7
		var bytes = id.Value.FromSqlGuid().ToByteArray();
		// Assert
		bytes.IsRfc9562Version(7).ShouldBeTrue();
	}

	[Fact]
	void DefaultConstructorProducesUniqueValues()
	{
		// Act
		SequentialSqlGuid
			first = new(),
			second = new();
		// Assert
		first.ShouldNotBe(second);
	}

	[Fact]
	void GuidConstructorThrowsForRandomGuid()
	{
		// Use the RFC 9562 §A.4 v4 test vector — a fixed guid avoids the 1/256 flakiness
		// where a random v4 guid's b[10] happens to equal 8, accidentally satisfying IsLegacy()
		// after SQL byte conversion shifts the variant bits out of position.
		Guid v4 = new ("919108f7-52d1-4320-9bac-f847db4148a8");
		Should.Throw<ArgumentException>(() => new SequentialSqlGuid(v4));
	}

	[Fact]
	void GuidConstructorThrowsForEmptyGuid()
	{
		Should.Throw<ArgumentException>(() => new SequentialSqlGuid(Guid.Empty));
	}

	[Fact]
	void StringConstructorDelegatesToGuidConstructor()
	{
		// Arrange
		var sqlGuid = GuidV7.NewSqlGuid();
		// Act
		SequentialSqlGuid id = new(sqlGuid.ToString());
		// Assert
		id.Value.ShouldBe(sqlGuid);
	}

	[Fact]
	void StringConstructorThrowsForInvalidFormat()
	{
		Should.Throw<FormatException>(() => new SequentialSqlGuid("not-a-guid"));
	}

	[Fact]
	void StringConstructorThrowsForNonSequentialGuid()
	{
		// Use the RFC 9562 §A.4 v4 test vector — a fixed guid avoids the 1/256 flakiness
		// where a random v4 guid's b[10] happens to equal 8, accidentally satisfying IsSqlLegacy()
		// after SQL byte conversion shifts the variant bits out of position.
		Should.Throw<ArgumentException>(() => new SequentialSqlGuid("919108f7-52d1-4320-9bac-f847db4148a8"));
	}

	[Fact]
	void RecordEqualityBasedOnValue()
	{
		// Arrange
		var sqlGuid = GuidV7.NewSqlGuid();
		SequentialSqlGuid
			a = new(sqlGuid),
			b = new(sqlGuid);
		// Assert
		a.ShouldBe(b);
		(a == b).ShouldBeTrue();
	}

	[Fact]
	void RecordInequalityForDifferentValues()
	{
		// Act
		SequentialSqlGuid
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
		SequentialSqlGuid id = new();
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
		SequentialSqlGuid id = new();
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
		SequentialSqlGuid original = new();
		// Act
		SequentialSqlGuid reconstructed = new(original.Value);
		// Assert
		reconstructed.Timestamp.ShouldBe(original.Timestamp);
	}

	// --- V8Custom constructor tests ---

	[Fact]
	void V8CustomConstructorCreatesVersion8SqlGuid()
	{
		// Act
		SequentialSqlGuid id = new(SequentialGuidType.Rfc9562V8Custom);
		// The SQL guid, after converting back to regular byte order, should be version 8
		var bytes = id.Value.FromSqlGuid().ToByteArray();
		// Assert
		bytes.IsRfc9562Version(8).ShouldBeTrue();
	}

	[Fact]
	void V8CustomConstructorTimestampIsUtcAndCurrent()
	{
		// Arrange
		var before = DateTime.UtcNow;
		// Act
		SequentialSqlGuid id = new(SequentialGuidType.Rfc9562V8Custom);
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
		SequentialSqlGuid
			first = new(SequentialGuidType.Rfc9562V8Custom),
			second = new(SequentialGuidType.Rfc9562V8Custom);
		// Assert
		first.ShouldNotBe(second);
	}

	// --- Comparison operator tests ---

	[Fact]
	void ComparisonOperatorsAreConsistent()
	{
		// Arrange — two distinct SQL guids
		SequentialSqlGuid
			a = new(),
			b = new();
		// Act — determine ordering
		var cmp = a.CompareTo(b);
		// Assert — operators agree with CompareTo
		(a < b).ShouldBe(cmp < 0);
		(a <= b).ShouldBe(cmp <= 0);
		(a > b).ShouldBe(cmp > 0);
		(a >= b).ShouldBe(cmp >= 0);
		// Antisymmetry
		(b < a).ShouldBe(cmp > 0);
		(b > a).ShouldBe(cmp < 0);
	}

	[Fact]
	void ComparisonOperatorsOnEqualValues()
	{
		// Arrange
		var sqlGuid = GuidV7.NewSqlGuid();
		SequentialSqlGuid
			a = new(sqlGuid),
			b = new(sqlGuid);
		// Assert
		(a <= b).ShouldBeTrue();
		(a >= b).ShouldBeTrue();
		(a < b).ShouldBeFalse();
		(a > b).ShouldBeFalse();
	}

#if NET7_0_OR_GREATER
	[Fact]
	void ParseStringReturnsMatchingSequentialSqlGuid()
	{
		// Arrange
		var sqlGuid = GuidV7.NewSqlGuid();
		// Act
		var id = SequentialSqlGuid.Parse(sqlGuid.ToString(), null);
		// Assert
		id.Value.ShouldBe(sqlGuid);
	}

	[Fact]
	void ParseStringThrowsFormatExceptionForInvalidInput()
	{
		Should.Throw<FormatException>(() => SequentialSqlGuid.Parse("not-a-guid", null));
	}

	[Fact]
	void ParseStringThrowsArgumentExceptionForNonSequentialGuid()
	{
		Should.Throw<ArgumentException>(() => SequentialSqlGuid.Parse("919108f7-52d1-4320-9bac-f847db4148a8", null));
	}

	[Fact]
	void TryParseStringReturnsTrueForValidSequentialSqlGuid()
	{
		// Arrange
		var sqlGuid = GuidV7.NewSqlGuid();
		// Act
		var success = SequentialSqlGuid.TryParse(sqlGuid.ToString(), null, out var id);
		// Assert
		success.ShouldBeTrue();
		id.Value.ShouldBe(sqlGuid);
	}

	[Fact]
	void TryParseStringReturnsFalseForInvalidInput()
	{
		SequentialSqlGuid.TryParse("not-a-guid", null, out _).ShouldBeFalse();
	}

	[Fact]
	void TryParseStringReturnsFalseForNonSequentialGuid()
	{
		SequentialSqlGuid.TryParse("919108f7-52d1-4320-9bac-f847db4148a8", null, out _).ShouldBeFalse();
	}

	[Fact]
	void TryParseStringReturnsFalseForNull()
	{
		string? val = null;
		SequentialSqlGuid.TryParse(val, null, out _).ShouldBeFalse();
	}

	[Fact]
	void ParseSpanReturnsMatchingSequentialSqlGuid()
	{
		// Arrange
		var sqlGuid = GuidV7.NewSqlGuid();
		// Act
		var id = SequentialSqlGuid.Parse(sqlGuid.ToString().AsSpan(), null);
		// Assert
		id.Value.ShouldBe(sqlGuid);
	}

	[Fact]
	void ParseSpanThrowsFormatExceptionForInvalidInput()
	{
		Should.Throw<FormatException>(() => SequentialSqlGuid.Parse("not-a-guid".AsSpan(), null));
	}

	[Fact]
	void ParseSpanThrowsArgumentExceptionForNonSequentialGuid()
	{
		Should.Throw<ArgumentException>(() => SequentialSqlGuid.Parse("919108f7-52d1-4320-9bac-f847db4148a8".AsSpan(), null));
	}

	[Fact]
	void TryParseSpanReturnsTrueForValidSequentialSqlGuid()
	{
		// Arrange
		var sqlGuid = GuidV7.NewSqlGuid();
		// Act
		var success = SequentialSqlGuid.TryParse(sqlGuid.ToString().AsSpan(), null, out var id);
		// Assert
		success.ShouldBeTrue();
		id.Value.ShouldBe(sqlGuid);
	}

	[Fact]
	void TryParseSpanReturnsFalseForInvalidInput()
	{
		SequentialSqlGuid.TryParse("not-a-guid".AsSpan(), null, out _).ShouldBeFalse();
	}

	[Fact]
	void TryParseSpanReturnsFalseForNonSequentialGuid()
	{
		SequentialSqlGuid.TryParse("919108f7-52d1-4320-9bac-f847db4148a8".AsSpan(), null, out _).ShouldBeFalse();
	}
#endif

#if NET6_0_OR_GREATER
	[Fact]
	void TryFormatCharSpanWritesGuidString()
	{
		// Arrange
		SequentialSqlGuid id = new();
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
		SequentialSqlGuid id = new();
		Span<byte> buffer = stackalloc byte[36];
		// Act
		var success = id.TryFormat(buffer, out var bytesWritten, default, null);
		// Assert
		success.ShouldBeTrue();
		bytesWritten.ShouldBe(36);
		Encoding.UTF8.GetString(buffer).ShouldBe(id.Value.ToString());
	}

	[Fact]
	void ParseUtf8SpanReturnsMatchingSequentialSqlGuid()
	{
		// Arrange
		var sqlGuid = GuidV7.NewSqlGuid();
		var utf8 = Encoding.UTF8.GetBytes(sqlGuid.ToString());
		// Act
		var id = SequentialSqlGuid.Parse(utf8, null);
		// Assert
		id.Value.ShouldBe(sqlGuid);
	}

	[Fact]
	void ParseUtf8SpanThrowsFormatExceptionForInvalidInput()
	{
		var bytes = "not-a-guid"u8.ToArray();
		Should.Throw<FormatException>(() => SequentialSqlGuid.Parse(bytes, null));
	}

	[Fact]
	void ParseUtf8SpanThrowsArgumentExceptionForNonSequentialGuid()
	{
		var bytes = "919108f7-52d1-4320-9bac-f847db4148a8"u8.ToArray();
		Should.Throw<ArgumentException>(() => SequentialSqlGuid.Parse(bytes, null));
	}

	[Fact]
	void TryParseUtf8SpanReturnsTrueForValidSequentialSqlGuid()
	{
		// Arrange
		var sqlGuid = GuidV7.NewSqlGuid();
		var utf8 = Encoding.UTF8.GetBytes(sqlGuid.ToString());
		// Act
		var success = SequentialSqlGuid.TryParse(utf8, null, out var id);
		// Assert
		success.ShouldBeTrue();
		id.Value.ShouldBe(sqlGuid);
	}

	[Fact]
	void TryParseUtf8SpanReturnsFalseForInvalidInput()
	{
		SequentialSqlGuid.TryParse("not-a-guid"u8, null, out _).ShouldBeFalse();
	}

	[Fact]
	void TryParseUtf8SpanReturnsFalseForNonSequentialGuid()
	{
		SequentialSqlGuid.TryParse("919108f7-52d1-4320-9bac-f847db4148a8"u8, null, out _).ShouldBeFalse();
	}
#endif
}
