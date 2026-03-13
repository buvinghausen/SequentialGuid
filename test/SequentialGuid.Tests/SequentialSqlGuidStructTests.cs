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
}
