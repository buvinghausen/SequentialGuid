using SequentialGuid.EntityFrameworkCore;
using SeqGuid = SequentialGuid.SequentialGuid;
using SeqSqlGuid = SequentialGuid.SequentialSqlGuid;

// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extensions for <see cref="ModelConfigurationBuilder"/> to register sequential GUID value converters and value generation.
/// </summary>
public static class ModelConfigurationBuilderExtensions
{
	extension(ModelConfigurationBuilder configurationBuilder)
	{
		/// <summary>
		/// Registers value converters for <see cref="SeqGuid"/> and <see cref="SeqSqlGuid"/>
		/// so that Entity Framework Core can automatically convert these types to and from <see cref="Guid"/>.
		/// </summary>
		public void AddSequentialGuidValueConverters()
		{
			configurationBuilder
				.Properties<SeqGuid>()
				.HaveConversion<SequentialGuidValueConverter<SeqGuid>>();
			configurationBuilder
				.Properties<SeqSqlGuid>()
				.HaveConversion<SequentialGuidValueConverter<SeqSqlGuid>>();
		}

		/// <summary>
		/// Registers a model-finalizing convention that assigns RFC 9562 v7 sequential value
		/// generators to every <see cref="Guid"/>, <see cref="SeqGuid"/>, and <see cref="SeqSqlGuid"/>
		/// primary-key property, so keys are generated client-side on Add.
		/// Explicit per-property configuration always takes precedence.
		/// </summary>
		/// <param name="sqlServerByteOrder">
		/// When <see langword="true"/>, plain <see cref="Guid"/> keys are generated in SQL Server
		/// byte order (<c>uniqueidentifier</c> sort order). The struct types carry their byte
		/// order in the type itself and are unaffected.
		/// </param>
		public void UseSequentialGuidValueGeneration(bool sqlServerByteOrder = false) =>
			configurationBuilder.Conventions.Add(
				_ => new SequentialGuidValueGenerationConvention(sqlServerByteOrder));
	}
}
