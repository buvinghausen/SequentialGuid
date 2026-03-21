namespace SequentialGuid;

/// <summary>
/// Defines the common shape shared by <see cref="SequentialGuid"/> and <see cref="SequentialSqlGuid"/>.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface ISequentialGuid<TSelf> :
#if NET6_0_OR_GREATER
	ISpanFormattable,
#else
	IFormattable,
#endif
#if NET7_0_OR_GREATER
	ISpanParsable<TSelf>,
#endif
#if NET10_0_OR_GREATER
	IUtf8SpanFormattable,
	IUtf8SpanParsable<TSelf>,
#endif
	IComparable,
	IComparable<TSelf>,
	IEquatable<TSelf>
	where TSelf : struct, ISequentialGuid<TSelf>
{
	/// <summary>Gets the underlying <see cref="Guid"/> value.</summary>
	Guid Value { get; }

	/// <summary>Gets the UTC timestamp encoded in the <see cref="Value"/>.</summary>
	DateTime Timestamp { get; }

#if NET7_0_OR_GREATER
	/// <summary>Creates an instance from an existing <see cref="Guid"/>.</summary>
	/// <param name="value">A sequential GUID value.</param>
	/// <returns>A new <typeparamref name="TSelf"/> wrapping <paramref name="value"/>.</returns>
	static abstract TSelf Create(Guid value);
#endif
}
