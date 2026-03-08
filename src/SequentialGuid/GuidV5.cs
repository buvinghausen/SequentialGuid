using System.Security.Cryptography;
using System.Text;

namespace SequentialGuid;

/// <summary>
/// Provides RFC 9562 UUID Version 5 generation using SHA-1 hashing to produce
/// deterministic, namespace-based <see cref="Guid"/> values.
/// </summary>
public static class GuidV5
{
	/// <summary>
	/// Well-known namespace UUIDs defined in RFC 9562 Section 6.6.
	/// </summary>
	public static class Namespaces
	{
		/// <summary>Name string is a fully-qualified domain name.</summary>
		public static readonly Guid Dns = GuidNameBased.Namespaces.Dns;

		/// <summary>Name string is a URL.</summary>
		public static readonly Guid Url = GuidNameBased.Namespaces.Url;

		/// <summary>Name string is an ISO OID.</summary>
		public static readonly Guid Oid = GuidNameBased.Namespaces.Oid;

		/// <summary>Name string is an X.500 DN (in DER or a text output format).</summary>
		public static readonly Guid X500 = GuidNameBased.Namespaces.X500;
	}

	/// <summary>
	/// Creates a deterministic UUID version 5 from a namespace <see cref="Guid"/> and a UTF-8 encoded name string.
	/// </summary>
	/// <param name="namespaceId">The namespace UUID.</param>
	/// <param name="name">The name within the namespace.</param>
	/// <returns>A deterministic version 5 <see cref="Guid"/> derived from the namespace and name.</returns>
	public static Guid Create(Guid namespaceId, string name) =>
		Create(namespaceId, Encoding.UTF8.GetBytes(name));

	/// <summary>
	/// Creates a deterministic UUID version 5 from a namespace <see cref="Guid"/> and raw name bytes.
	/// </summary>
	/// <param name="namespaceId">The namespace UUID.</param>
	/// <param name="name">The raw name bytes within the namespace.</param>
	/// <returns>A deterministic version 5 <see cref="Guid"/> derived from the namespace and name.</returns>
#pragma warning disable CA5350 // SHA-1 is required by RFC 9562 for UUID version 5
	public static Guid Create(Guid namespaceId, byte[] name) =>
		GuidNameBased.Create(namespaceId, name, HashAlgorithmName.SHA1, 5);
#pragma warning restore CA5350
}
