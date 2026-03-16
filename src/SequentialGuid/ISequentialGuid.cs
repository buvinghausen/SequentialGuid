#if NET8_0_OR_GREATER
namespace SequentialGuid;

/// <summary>
/// Defines the common shape shared by <see cref="SequentialGuid"/> and <see cref="SequentialSqlGuid"/>.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface ISequentialGuid<out TSelf>
	where TSelf : struct, ISequentialGuid<TSelf>
{
	/// <summary>Gets the underlying <see cref="Guid"/> value.</summary>
	Guid Value { get; }

	/// <summary>Creates an instance from an existing <see cref="Guid"/>.</summary>
	/// <param name="value">A sequential GUID value.</param>
	/// <returns>A new <typeparamref name="TSelf"/> wrapping <paramref name="value"/>.</returns>
	static abstract TSelf Create(Guid value);
}
#endif
