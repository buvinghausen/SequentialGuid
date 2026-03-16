using System.Runtime.CompilerServices;

namespace SequentialGuid;

/// <summary>
/// Represents an immutable sequential <see cref="Guid"/> value stored in SQL Server byte order with an associated timestamp.
/// </summary>
[SkipLocalsInit]
public readonly record struct SequentialSqlGuid : IComparable<SequentialSqlGuid>, IComparable
#if NET8_0_OR_GREATER
	, ISequentialGuid<SequentialSqlGuid>
#endif
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
		Value = type switch
		{
			SequentialGuidType.Rfc9562V7 =>
				GuidV7.NewSqlGuid(),
			SequentialGuidType.Rfc9562V8Custom =>
				GuidV8Time.NewSqlGuid(),
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
		Timestamp = Value.ToDateTime().GetValueOrDefault();
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
		else if (bytes.IsSqlRfc9562Version(7) || bytes.IsSqlRfc9562Version(8) || bytes.IsSqlLegacy())
		{
			Value = value;
		}
		else
		{
			throw new ArgumentException(
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

	/// <summary>Compares the current instance with another <see cref="SequentialSqlGuid"/> by comparing the underlying <see cref="Value"/>.</summary>
	public int CompareTo(SequentialSqlGuid other) =>
		Value.CompareTo(other.Value);

	/// <summary>Compares the current instance with another object.</summary>
	/// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not a <see cref="SequentialSqlGuid"/>.</exception>
	public int CompareTo(object? obj) =>
		obj is SequentialSqlGuid other ?
			CompareTo(other) :
			throw new ArgumentException($"Object must be of type {nameof(SequentialSqlGuid)}", nameof(obj));

	/// <summary>Implicitly converts a <see cref="SequentialSqlGuid"/> to its underlying <see cref="Guid"/>.</summary>
	public static implicit operator Guid(SequentialSqlGuid sequentialSqlGuid) =>
		sequentialSqlGuid.Value;

	/// <summary>Implicitly converts a <see cref="Guid"/> to a <see cref="SequentialSqlGuid"/>.</summary>
	public static implicit operator SequentialSqlGuid(Guid value) =>
		new(value);

	/// <summary>Implicitly converts a <see cref="SequentialSqlGuid"/> to its <see cref="string"/> representation.</summary>
	public static implicit operator string(SequentialSqlGuid sequentialSqlGuid) =>
		sequentialSqlGuid.Value.ToString();

	/// <summary>Implicitly converts a <see cref="string"/> to a <see cref="SequentialSqlGuid"/>.</summary>
	public static implicit operator SequentialSqlGuid(string value) =>
		new(value);

	/// <summary>Implicitly converts a <see cref="SequentialGuid"/> to a <see cref="SequentialSqlGuid"/>.</summary>
	public static implicit operator SequentialSqlGuid(SequentialGuid sequentialGuid) =>
		new(sequentialGuid.Value);

#if NET8_0_OR_GREATER
	/// <inheritdoc />
	static SequentialSqlGuid ISequentialGuid<SequentialSqlGuid>.Create(Guid value) => new(value);
#endif

	/// <summary>Determines whether <paramref name="left"/> is less than <paramref name="right"/>.</summary>
	public static bool operator <(SequentialSqlGuid left, SequentialSqlGuid right) =>
		left.CompareTo(right) < 0;

	/// <summary>Determines whether <paramref name="left"/> is greater than <paramref name="right"/>.</summary>
	public static bool operator >(SequentialSqlGuid left, SequentialSqlGuid right) =>
		left.CompareTo(right) > 0;

	/// <summary>Determines whether <paramref name="left"/> is less than or equal to <paramref name="right"/>.</summary>
	public static bool operator <=(SequentialSqlGuid left, SequentialSqlGuid right) =>
		left.CompareTo(right) <= 0;

	/// <summary>Determines whether <paramref name="left"/> is greater than or equal to <paramref name="right"/>.</summary>
	public static bool operator >=(SequentialSqlGuid left, SequentialSqlGuid right) =>
		left.CompareTo(right) >= 0;
}
