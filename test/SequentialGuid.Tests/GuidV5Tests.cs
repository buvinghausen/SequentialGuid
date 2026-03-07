using System.Text;

namespace SequentialGuid.Tests;

public sealed class GuidV5Tests
{
	// Known test vectors: "python.org" is from the Python standard library uuid module
	// documentation; "www.example.com" is the official test vector from RFC 9562 Appendix A.4.
	[Theory]
	[InlineData("6ba7b810-9dad-11d1-80b4-00c04fd430c8", "python.org", "886313e1-3b8a-5372-9b90-0c9aee199e5d")]
	[InlineData("6ba7b810-9dad-11d1-80b4-00c04fd430c8", "www.example.com", "2ed6657d-e927-568b-95e1-2665a8aea6a2")] // RFC 9562 §A.4
	void KnownValueTests(string namespaceId, string name, string expected)
	{
		// Act
		var actual = GuidV5.Create(new(namespaceId), name);
		// Assert
		actual.ShouldBe(new(expected));
	}

	[Fact]
	void TestVersion5Bits()
	{
		// Act
		var id = GuidV5.Create(GuidV5.Namespaces.Dns, "test");
		var bytes = id.ToByteArray();
		// Assert - version is in the high nibble of bytes[7] (Data3 high byte, little-endian)
		(bytes[7] >> 4).ShouldBe(5);
#if NET9_0_OR_GREATER
		id.Version.ShouldBe(5);
#endif
		// At present the compiler can't access static instance-like properties across assemblies
		bytes.AreRfc9562(5).ShouldBeTrue();
	}

	[Fact]
	void TestVariantBits()
	{
		// Act
		var id = GuidV5.Create(GuidV5.Namespaces.Dns, "test");
		var bytes = id.ToByteArray();
		// Assert - RFC 9562 variant: bits 7-6 of bytes[8] (Data4[0]) must be 10
		(bytes[8] & 0xC0).ShouldBe(0x80);
#if NET9_0_OR_GREATER
		id.Variant.ShouldBeInRange(8, 11);
#endif
		// At present the compiler can't access static instance-like properties across assemblies
		bytes.AreRfc9562(5).ShouldBeTrue();
	}

	[Fact]
	void TestDeterministic()
	{
		// Act
		var first = GuidV5.Create(GuidV5.Namespaces.Dns, "same-name");
		var second = GuidV5.Create(GuidV5.Namespaces.Dns, "same-name");
		// Assert
		first.ShouldBe(second);
	}

	[Fact]
	void TestDifferentNamesProduceDifferentGuids()
	{
		// Act
		var a = GuidV5.Create(GuidV5.Namespaces.Dns, "name-a");
		var b = GuidV5.Create(GuidV5.Namespaces.Dns, "name-b");
		// Assert
		a.ShouldNotBe(b);
	}

	[Fact]
	void TestDifferentNamespacesProduceDifferentGuids()
	{
		// Act
		var dns = GuidV5.Create(GuidV5.Namespaces.Dns, "test");
		var url = GuidV5.Create(GuidV5.Namespaces.Url, "test");
		// Assert
		dns.ShouldNotBe(url);
	}

	[Fact]
	void TestByteArrayOverloadMatchesStringOverload()
	{
		// Arrange
		const string name = "test-name";
		// Act
		var fromString = GuidV5.Create(GuidV5.Namespaces.Url, name);
		var fromBytes = GuidV5.Create(GuidV5.Namespaces.Url, Encoding.UTF8.GetBytes(name));
		// Assert
		fromString.ShouldBe(fromBytes);
	}

	[Fact]
	void TestAllWellKnownNamespacesAreDistinct()
	{
		// Arrange
		Guid[] namespaces =
		[
			GuidV5.Namespaces.Dns,
			GuidV5.Namespaces.Url,
			GuidV5.Namespaces.Oid,
			GuidV5.Namespaces.X500
		];
		// Assert
		namespaces.Distinct().Count().ShouldBe(namespaces.Length);
	}
}
