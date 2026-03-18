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
}
