namespace SequentialGuid;

/// <summary>
/// Specifies the type of sequential GUID to generate.
/// </summary>
public enum SequentialGuidType
{
	/// <summary>
	/// RFC 9562 Version 7 sequential GUID.
	/// </summary>
	Rfc9562V7 = 1,

	/// <summary>
	/// RFC 9562 Version 8 custom sequential GUID.
	/// </summary>
	Rfc9562V8Custom = 2
}
