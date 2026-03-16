using Microsoft.EntityFrameworkCore;
using SeqGuid = SequentialGuid.SequentialGuid;
using SeqSqlGuid = SequentialGuid.SequentialSqlGuid;

namespace SequentialGuid.EntityFrameworkCore.Tests;

sealed record TestEntity(int Id, SeqGuid SequentialGuid, SeqSqlGuid SequentialSqlGuid);

sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
	public DbSet<TestEntity> TestEntities => Set<TestEntity>();

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
		configurationBuilder.AddSequentialGuidValueConverters();

	protected override void OnModelCreating(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<TestEntity>().HasKey(e => e.Id);
}

public sealed class ValueConverterTests
{
	static DbContextOptions<TestDbContext> CreateOptions(string dbName) =>
		new DbContextOptionsBuilder<TestDbContext>()
			.UseInMemoryDatabase(dbName)
			.Options;

	[Fact]
	void SequentialGuidConverterIsRegisteredWithGuidProviderType()
	{
		using var db = new TestDbContext(CreateOptions(nameof(SequentialGuidConverterIsRegisteredWithGuidProviderType)));
		var converter = db.Model
			.FindEntityType(typeof(TestEntity))!
			.FindProperty(nameof(TestEntity.SequentialGuid))!
			.GetValueConverter();

		converter.ShouldNotBeNull();
		converter.ModelClrType.ShouldBe(typeof(SeqGuid));
		converter.ProviderClrType.ShouldBe(typeof(Guid));
	}

	[Fact]
	void SequentialSqlGuidConverterIsRegisteredWithGuidProviderType()
	{
		using var db = new TestDbContext(CreateOptions(nameof(SequentialSqlGuidConverterIsRegisteredWithGuidProviderType)));
		var converter = db.Model
			.FindEntityType(typeof(TestEntity))!
			.FindProperty(nameof(TestEntity.SequentialSqlGuid))!
			.GetValueConverter();

		converter.ShouldNotBeNull();
		converter.ModelClrType.ShouldBe(typeof(SeqSqlGuid));
		converter.ProviderClrType.ShouldBe(typeof(Guid));
	}

	[Fact]
	void SequentialGuidConvertToProviderReturnsUnderlyingGuid()
	{
		using var db = new TestDbContext(CreateOptions(nameof(SequentialGuidConvertToProviderReturnsUnderlyingGuid)));
		var converter = db.Model
			.FindEntityType(typeof(TestEntity))!
			.FindProperty(nameof(TestEntity.SequentialGuid))!
			.GetValueConverter()!;

		SeqGuid seqGuid = new();
		var result = (Guid)converter.ConvertToProvider(seqGuid)!;

		result.ShouldBe(seqGuid.Value);
	}

	[Fact]
	void SequentialGuidConvertFromProviderRestoresValue()
	{
		using var db = new TestDbContext(CreateOptions(nameof(SequentialGuidConvertFromProviderRestoresValue)));
		var converter = db.Model
			.FindEntityType(typeof(TestEntity))!
			.FindProperty(nameof(TestEntity.SequentialGuid))!
			.GetValueConverter()!;

		SeqGuid seqGuid = new();
		var result = (SeqGuid)converter.ConvertFromProvider(seqGuid.Value)!;

		result.ShouldBe(seqGuid);
	}

	[Fact]
	void SequentialSqlGuidConvertToProviderReturnsUnderlyingGuid()
	{
		using var db = new TestDbContext(CreateOptions(nameof(SequentialSqlGuidConvertToProviderReturnsUnderlyingGuid)));
		var converter = db.Model
			.FindEntityType(typeof(TestEntity))!
			.FindProperty(nameof(TestEntity.SequentialSqlGuid))!
			.GetValueConverter()!;

		SeqSqlGuid seqSqlGuid = new();
		var result = (Guid)converter.ConvertToProvider(seqSqlGuid)!;

		result.ShouldBe(seqSqlGuid.Value);
	}

	[Fact]
	void SequentialSqlGuidConvertFromProviderRestoresValue()
	{
		using var db = new TestDbContext(CreateOptions(nameof(SequentialSqlGuidConvertFromProviderRestoresValue)));
		var converter = db.Model
			.FindEntityType(typeof(TestEntity))!
			.FindProperty(nameof(TestEntity.SequentialSqlGuid))!
			.GetValueConverter()!;

		SeqSqlGuid seqSqlGuid = new();
		var result = (SeqSqlGuid)converter.ConvertFromProvider(seqSqlGuid.Value)!;

		result.ShouldBe(seqSqlGuid);
	}

	[Fact]
	void SequentialGuidRoundTrips()
	{
		var options = CreateOptions(nameof(SequentialGuidRoundTrips));
		SeqGuid seqGuid = new();
		SeqSqlGuid seqSqlGuid = new();

		using (var db = new TestDbContext(options))
		{
			db.TestEntities.Add(new TestEntity(1, seqGuid, seqSqlGuid));
			db.SaveChanges();
		}

		using (var db = new TestDbContext(options))
		{
			var loaded = db.TestEntities.Find(1)!;
			loaded.SequentialGuid.ShouldBe(seqGuid);
			loaded.SequentialSqlGuid.ShouldBe(seqSqlGuid);
		}
	}
}
