using System.Text;

namespace SequentialGuid.Tests;

public sealed class GuidV8NameTests
{
	// RFC 9562 Appendix B.2 official test vector: DNS namespace + "www.example.com"
	[Theory]
	[InlineData("www.example.com", "5c146b14-3c52-8afd-938a-375d0df1fbf6")] // RFC 9562 §B.2
	void KnownValueTests(string name, string expected)
	{
		// Act
		var actual = GuidV8Name.Create(GuidV8Name.Namespaces.Dns, name);
		// Assert
		actual.ShouldBe(new(expected));
	}

	[Fact]
	void TestVersion8Bits()
	{
		// Act
		var id = GuidV8Name.Create(GuidV8Name.Namespaces.Dns, "test");
		var bytes = id.ToByteArray();

#if NET9_0_OR_GREATER
		id.Version.ShouldBe(8);
#endif
		bytes.IsRfc9562Version(8).ShouldBeTrue();
	}

	[Fact]
	void TestVariantBits()
	{
		// Act
		var id = GuidV8Name.Create(GuidV8Name.Namespaces.Dns, "test");
		var bytes = id.ToByteArray();

#if NET9_0_OR_GREATER
		id.Variant.ShouldBeInRange(8, 11);
#endif
		bytes.VariantIsRfc9562().ShouldBeTrue();
	}

	[Fact]
	void TestDeterministic()
	{
		// Act
		var first = GuidV8Name.Create(GuidV8Name.Namespaces.Dns, "same-name");
		var second = GuidV8Name.Create(GuidV8Name.Namespaces.Dns, "same-name");
		// Assert
		first.ShouldBe(second);
	}

	[Fact]
	void TestDifferentNamesProduceDifferentGuids()
	{
		// Act
		var a = GuidV8Name.Create(GuidV8Name.Namespaces.Dns, "name-a");
		var b = GuidV8Name.Create(GuidV8Name.Namespaces.Dns, "name-b");
		// Assert
		a.ShouldNotBe(b);
	}

	[Fact]
	void TestDifferentNamespacesProduceDifferentGuids()
	{
		// Act
		var dns = GuidV8Name.Create(GuidV8Name.Namespaces.Dns, "test");
		var url = GuidV8Name.Create(GuidV8Name.Namespaces.Url, "test");
		// Assert
		dns.ShouldNotBe(url);
	}

	[Fact]
	void TestByteArrayOverloadMatchesStringOverload()
	{
		// Arrange
		const string name = "test-name";
		// Act
		var fromString = GuidV8Name.Create(GuidV8Name.Namespaces.Url, name);
		var fromBytes = GuidV8Name.Create(GuidV8Name.Namespaces.Url, Encoding.UTF8.GetBytes(name));
		// Assert
		fromString.ShouldBe(fromBytes);
	}

	[Fact]
	void TestAllWellKnownNamespacesAreDistinct()
	{
		// Arrange
		Guid[] namespaces =
		[
			GuidV8Name.Namespaces.Dns,
			GuidV8Name.Namespaces.Url,
			GuidV8Name.Namespaces.Oid,
			GuidV8Name.Namespaces.X500
		];
		// Assert
		namespaces.Distinct().Count().ShouldBe(namespaces.Length);
	}

	[Fact]
	void TestProducesDifferentResultThanV5ForSameInput()
	{
		// Act
		var v5 = GuidV5.Create(GuidV5.Namespaces.Dns, "www.example.com");
		var v8 = GuidV8Name.Create(GuidV8Name.Namespaces.Dns, "www.example.com");
		// Assert
		v5.ShouldNotBe(v8);
	}
}
