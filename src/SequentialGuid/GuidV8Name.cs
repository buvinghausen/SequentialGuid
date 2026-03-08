using System.Security.Cryptography;
using System.Text;

namespace SequentialGuid;

/// <summary>
/// Provides RFC 9562 UUID Version 8 (name-based) generation using SHA-256 hashing to produce
/// deterministic, namespace-based <see cref="Guid"/> values.
/// </summary>
/// <remarks>
/// Implements the name-based UUIDv8 layout described in RFC 9562 Appendix B.2.
/// The algorithm follows the same structure as UUIDv5 (Section 5.5), substituting SHA-256
/// for SHA-1. The first 128 bits of the SHA-256 digest are mapped into the UUID fields,
/// with the mandatory version nibble (<c>0x8</c>) and variant bits (<c>10xxxxxx</c>)
/// overwriting their required positions. The remaining 128 bits of the SHA-256 output
/// are discarded.
/// </remarks>
public static class GuidV8Name
{
	/// <summary>
	/// Well-known namespace UUIDs defined in RFC 9562 Section 6.6.
	/// </summary>
	public static class Namespaces
	{
		/// <summary>Name string is a fully-qualified domain name.</summary>
		public static readonly Guid Dns = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

		/// <summary>Name string is a URL.</summary>
		public static readonly Guid Url = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

		/// <summary>Name string is an ISO OID.</summary>
		public static readonly Guid Oid = new("6ba7b812-9dad-11d1-80b4-00c04fd430c8");

		/// <summary>Name string is an X.500 DN (in DER or a text output format).</summary>
		public static readonly Guid X500 = new("6ba7b814-9dad-11d1-80b4-00c04fd430c8");
	}

	/// <summary>
	/// Creates a deterministic UUID version 8 from a namespace <see cref="Guid"/> and a UTF-8 encoded name string.
	/// </summary>
	/// <param name="namespaceId">The namespace UUID.</param>
	/// <param name="name">The name within the namespace.</param>
	/// <returns>A deterministic version 8 <see cref="Guid"/> derived from the namespace and name.</returns>
	public static Guid Create(Guid namespaceId, string name) =>
		Create(namespaceId, Encoding.UTF8.GetBytes(name));

	/// <summary>
	/// Creates a deterministic UUID version 8 from a namespace <see cref="Guid"/> and raw name bytes.
	/// </summary>
	/// <param name="namespaceId">The namespace UUID.</param>
	/// <param name="name">The raw name bytes within the namespace.</param>
	/// <returns>A deterministic version 8 <see cref="Guid"/> derived from the namespace and name.</returns>
	public static Guid Create(Guid namespaceId, byte[] name)
	{
#if NETFRAMEWORK
		using var sha = SHA256.Create();
		var nsBytes = namespaceId.ToByteArray().SwapByteOrder();
		sha.TransformBlock(nsBytes, 0, 16, null, 0);
		sha.TransformFinalBlock(name, 0, name.Length);
		var digest = sha.Hash!;
#else
		using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		hash.AppendData(namespaceId
#if NETSTANDARD
			.ToByteArray().SwapByteOrder()
#else
			.ToByteArray(true)
#endif
		);
		hash.AppendData(name);
		var digest = hash.GetHashAndReset();
#endif
		digest.SetRfc9562Version(8);
		digest.SetRfc9562Variant();
		return
#if NETFRAMEWORK || NETSTANDARD
			new(digest.SwapByteOrder());
#else
			new(digest.AsSpan(0, 16), true);
#endif
	}
}
