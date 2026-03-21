using System.Runtime.CompilerServices;
using SequentialGuid.Extensions;

namespace SequentialGuid;

/// <summary>
/// Represents an immutable sequential <see cref="Guid"/> value stored in SQL Server byte order with an associated timestamp.
/// </summary>
[SkipLocalsInit]
public readonly record struct SequentialSqlGuid : ISequentialGuid<SequentialSqlGuid>
{
	/// <summary>Gets the underlying <see cref="Guid"/> value in SQL Server byte order.</summary>
	public Guid Value { get; }

	/// <summary>Gets the UTC timestamp encoded in the <see cref="Value"/>.</summary>
	public DateTime Timestamp { get; }

	/// <summary>Initializes a new <see cref="SequentialSqlGuid"/> using <see cref="SequentialGuidType.Rfc9562V7"/>.</summary>
	public SequentialSqlGuid() : this(SequentialGuidType.Rfc9562V7) { }

	/// <summary>Initializes a new <see cref="SequentialSqlGuid"/> of the specified <paramref name="type"/>.</summary>
	/// <param name="type">The algorithm to use when generating the GUID.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="type"/> is not a recognised <see cref="SequentialGuidType"/> value.</exception>
	public SequentialSqlGuid(SequentialGuidType type)
	{
		switch (type)
		{
			case SequentialGuidType.Rfc9562V7:
				Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).UtcDateTime;
				Value = GuidV7.NewSqlGuid(Timestamp);
				break;
			case SequentialGuidType.Rfc9562V8Custom:
				Timestamp = DateTime.UtcNow;
				Value = GuidV8Time.NewSqlGuid(Timestamp);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(type), type, null);
		}
	}

	/// <summary>Initializes a <see cref="SequentialSqlGuid"/> from an existing sequential <see cref="Guid"/>.</summary>
	/// <param name="value">A version 7, version 8, or legacy sequential GUID in standard or SQL Server byte order.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is not a recognised sequential GUID.</exception>
	public SequentialSqlGuid(Guid value)
	{
#if NET6_0_OR_GREATER
		Span<byte> bytes = stackalloc byte[16];
		value.TryWriteBytes(bytes);
#else
		var bytes = value.ToByteArray();
#endif
		// First: if the bytes can convert to a valid timestamp.
		// The id needs to be re-shuffled to sql sort order.
		// Guard against SQL-ordered V8 GUIDs whose counter byte (mapped to position [7])
		// accidentally has high nibble 7 or 8, which makes IsRfc9562Version fire as a false
		// positive. Disambiguate by requiring a valid timestamp when SQL detection also fires.
		if ((bytes.IsRfc9562Version(7) || bytes.IsRfc9562Version(8) || bytes.IsLegacy()) &&
			(bytes.ToTicks() is { IsDateTime: true } ||
			 !bytes.IsSqlRfc9562Version(7) && !bytes.IsSqlRfc9562Version(8) && !bytes.IsSqlLegacy()))
		{
#if NET6_0_OR_GREATER
			Span<byte> sqlBytes = stackalloc byte[16];
			bytes.WriteToSqlByteOrder(sqlBytes);
			Value = new(sqlBytes);
#else
			Value = new(bytes.ToSqlByteOrder());
#endif
		}
		else
		{
			Value = bytes.IsSqlRfc9562Version(7) || bytes.IsSqlRfc9562Version(8) || bytes.IsSqlLegacy()
				? value
				: throw new ArgumentException(
				"Guid must be a version 7, version 8, or legacy sequential guid in standard or SQL Server byte order.",
				nameof(value));
		}
		Timestamp = Value.ToDateTime().GetValueOrDefault();
	}

	/// <summary>Initializes a <see cref="SequentialSqlGuid"/> by parsing a GUID string representation.</summary>
	/// <param name="value">A string that contains a GUID.</param>
	/// <exception cref="FormatException">Thrown when <paramref name="value"/> is not in a recognised GUID format.</exception>
	/// <exception cref="ArgumentException">Thrown when the parsed GUID is not a recognised sequential GUID.</exception>
	public SequentialSqlGuid(string value) :
		this(Guid.Parse(value))
	{ }

	/// <inheritdoc />
	public static SequentialSqlGuid Create(Guid value) =>
		new(value);

	/// <inheritdoc />
	public override string ToString() =>
		Value.ToString();

	/// <inheritdoc/>
	public string ToString(string? format, IFormatProvider? formatProvider) =>
		Value.ToString(format, formatProvider);

	/// <inheritdoc/>
	public int CompareTo(object? obj) =>
		obj switch
		{
			null => 1,
			SequentialSqlGuid otherSequential => CompareTo(otherSequential),
			Guid otherGuid => Value.CompareTo(otherGuid),
			_ => throw new ArgumentException($"Object must be of type {nameof(SequentialSqlGuid)} or {nameof(Guid)}.", nameof(obj))
		};

	/// <inheritdoc/>
	public int CompareTo(SequentialSqlGuid other) =>
		Value.CompareTo(other.Value);

	/// <inheritdoc/>
	public bool Equals(SequentialSqlGuid other) =>
		Value.Equals(other.Value);

	/// <inheritdoc />
	public override int GetHashCode() =>
		Value.GetHashCode();

	/// <inheritdoc cref="IComparable{T}"/>
	public static bool operator <(SequentialSqlGuid left, SequentialSqlGuid right) =>
		left.CompareTo(right) < 0;

	/// <inheritdoc cref="IComparable{T}"/>
	public static bool operator <=(SequentialSqlGuid left, SequentialSqlGuid right) =>
		left.CompareTo(right) <= 0;

	/// <inheritdoc cref="IComparable{T}"/>
	public static bool operator >(SequentialSqlGuid left, SequentialSqlGuid right) =>
		left.CompareTo(right) > 0;

	/// <inheritdoc cref="IComparable{T}"/>
	public static bool operator >=(SequentialSqlGuid left, SequentialSqlGuid right) =>
		left.CompareTo(right) >= 0;

	/// <summary>Implicitly converts a <see cref="SequentialSqlGuid"/> to its underlying <see cref="Guid"/> value.</summary>
	public static implicit operator Guid(SequentialSqlGuid sequentialSqlGuid) =>
		sequentialSqlGuid.Value;

	/// <summary>Implicitly converts a <see cref="Guid"/> to a <see cref="SequentialSqlGuid"/>.</summary>
	public static implicit operator SequentialSqlGuid(Guid value) =>
		new(value);

	/// <summary>Implicitly converts a <see cref="SequentialSqlGuid"/> to its <see cref="string"/> representation.</summary>
	public static implicit operator string(SequentialSqlGuid sequentialSqlGuid) =>
		sequentialSqlGuid.ToString();

	/// <summary>Implicitly converts a <see cref="string"/> to a <see cref="SequentialSqlGuid"/>.</summary>
	public static implicit operator SequentialSqlGuid(string value) =>
		new(value);

	/// <inheritdoc/>
	public static SequentialSqlGuid Parse(string s
#if NET6_0_OR_GREATER
		, IFormatProvider? provider
#endif
	) =>
		new(Guid.Parse(s
#if NET6_0_OR_GREATER
			, provider
#endif
		));

	/// <inheritdoc/>
	public static bool TryParse(string? s
#if NET6_0_OR_GREATER
		, IFormatProvider? provider
#endif
		, out SequentialSqlGuid result)
	{
		if (Guid.TryParse(s
#if NET6_0_OR_GREATER
				, provider
#endif
			    , out var guid))
		{
			try
			{
				result = new(guid);
				return true;
			}
			catch (ArgumentException) { }
		}
		result = default;
		return false;
	}

#if NET6_0_OR_GREATER
	/// <inheritdoc/>
	public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
		Value.TryFormat(destination, out charsWritten, format);
#endif

#if NET7_0_OR_GREATER
	/// <inheritdoc/>
	public static SequentialSqlGuid Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
		new(Guid.Parse(s, provider));

	/// <inheritdoc/>
	public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SequentialSqlGuid result)
	{
		if (Guid.TryParse(s, provider, out var guid))
		{
			try
			{
				result = new(guid);
				return true;
			}
			catch (ArgumentException) { }
		}
		result = default;
		return false;
	}
#endif

#if NET10_0_OR_GREATER
	/// <inheritdoc/>
	public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
		Value.TryFormat(utf8Destination, out bytesWritten, format);

	/// <inheritdoc/>
	public static SequentialSqlGuid Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) =>
		new(Guid.Parse(utf8Text, provider));

	/// <inheritdoc/>
	public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out SequentialSqlGuid result)
	{
		if (Guid.TryParse(utf8Text, provider, out var guid))
		{
			try
			{
				result = new(guid);
				return true;
			}
			catch (ArgumentException) { }
		}
		result = default;
		return false;
	}
#endif
}
