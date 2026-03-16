using SequentialGuid.EntityFrameworkCore;
using SeqGuid = SequentialGuid.SequentialGuid;
using SequentialSqlGuid = SequentialGuid.SequentialSqlGuid;

// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extensions for <see cref="ModelConfigurationBuilder"/> to register sequential GUID value converters.
/// </summary>
public static class ModelConfigurationBuilderExtensions
{
	extension(ModelConfigurationBuilder configurationBuilder)
	{
		/// <summary>
		/// Registers value converters for <see cref="SequentialGuid"/> and <see cref="SequentialSqlGuid"/>
		/// so that Entity Framework Core can automatically convert these types to and from <see cref="Guid"/>.
		/// </summary>
		public void AddSequentialGuidValueConverters()
		{
			configurationBuilder
				.Properties<SeqGuid>()
				.HaveConversion<SequentialGuidValueConverter<SeqGuid>>();
			configurationBuilder
				.Properties<SequentialSqlGuid>()
				.HaveConversion<SequentialGuidValueConverter<SequentialSqlGuid>>();
		}
	}
}
