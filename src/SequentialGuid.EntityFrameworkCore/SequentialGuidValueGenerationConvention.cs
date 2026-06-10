using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using SeqGuid = SequentialGuid.SequentialGuid;
using SeqSqlGuid = SequentialGuid.SequentialSqlGuid;

namespace SequentialGuid.EntityFrameworkCore;

/// <summary>
/// Model-finalizing convention that assigns RFC 9562 v7 sequential value generators to every
/// <see cref="Guid"/>, <see cref="SeqGuid"/>, and <see cref="SeqSqlGuid"/> primary-key property
/// that has no explicitly configured generator.
/// </summary>
sealed class SequentialGuidValueGenerationConvention(bool sqlServerByteOrder) : IModelFinalizingConvention
{
	// Factories are stateless; share one instance per generator type across the model.
	static readonly Func<IProperty, ITypeBase, ValueGenerator> _guid =
		static (_, _) => new SequentialGuidValueGenerator();
	static readonly Func<IProperty, ITypeBase, ValueGenerator> _sqlGuid =
		static (_, _) => new SequentialSqlGuidValueGenerator();
	static readonly Func<IProperty, ITypeBase, ValueGenerator> _struct =
		static (_, _) => new SequentialGuidStructValueGenerator();
	static readonly Func<IProperty, ITypeBase, ValueGenerator> _sqlStruct =
		static (_, _) => new SequentialSqlGuidStructValueGenerator();

	// Pre-boxed default sentinel values — zero-valued structs used as "not set" sentinels.
	// EF 8/9 computes DefaultSentinel via Activator.CreateInstance, which calls the parameterless
	// constructor and produces a live GUID, not the zero default. Explicitly setting the sentinel
	// here ensures EF recognises an unset property and triggers value generation. (EF 10 fixed this
	// by using RuntimeHelpers.GetUninitializedObject, which bypasses the constructor.)
	static readonly object _seqGuidSentinel = default(SeqGuid);
	static readonly object _seqSqlGuidSentinel = default(SeqSqlGuid);

	/// <inheritdoc/>
	public void ProcessModelFinalizing(
		IConventionModelBuilder modelBuilder,
		IConventionContext<IConventionModelBuilder> context)
	{
		foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
		{
			var key = entityType.FindPrimaryKey();
			if (key is null)
				continue;
			foreach (var property in key.Properties)
			{
				Func<IProperty, ITypeBase, ValueGenerator>? factory;
				object? sentinel;
				if (property.ClrType == typeof(Guid))
				{
					factory = sqlServerByteOrder ? _sqlGuid : _guid;
					sentinel = null; // EF resolves Guid.Empty correctly on all versions.
				}
				else if (property.ClrType == typeof(SeqGuid))
				{
					factory = _struct;
					sentinel = _seqGuidSentinel;
				}
				else if (property.ClrType == typeof(SeqSqlGuid))
				{
					factory = _sqlStruct;
					sentinel = _seqSqlGuidSentinel;
				}
				else
				{
					continue;
				}
				// EF's configuration-source precedence ensures that any DataAnnotation- or
				// Explicit-source factory already on the property cannot be overridden by
				// these Convention-source calls.
				property.Builder.HasValueGenerator(factory);
				property.Builder.ValueGenerated(ValueGenerated.OnAdd);
				if (sentinel is not null)
					property.Builder.HasSentinel(sentinel);
			}
		}
	}
}
