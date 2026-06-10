using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using SeqGuid = SequentialGuid.SequentialGuid;
using SeqSqlGuid = SequentialGuid.SequentialSqlGuid;

namespace SequentialGuid.EntityFrameworkCore.Tests;

sealed class GuidKeyEntity
{
	public Guid Id { get; set; }
	public Guid Payload { get; set; }
}

sealed class StructKeyEntity
{
	public SeqGuid Id { get; set; }
}

sealed class SqlStructKeyEntity
{
	public SeqSqlGuid Id { get; set; }
}

sealed class ExplicitGeneratorEntity
{
	public Guid Id { get; set; }
}

sealed class CompositeKeyEntity
{
	public string Code { get; set; } = null!;
	public Guid Id { get; set; }
}

sealed class FixedGuidValueGenerator : ValueGenerator<Guid>
{
	internal static readonly Guid Fixed = new("11111111-1111-7111-8111-111111111111");

	public override bool GeneratesTemporaryValues => false;

	public override Guid Next(EntityEntry entry) =>
		Fixed;
}

sealed class ConventionDbContext(DbContextOptions<ConventionDbContext> options) : DbContext(options)
{
	public DbSet<GuidKeyEntity> GuidKeyEntities => Set<GuidKeyEntity>();
	public DbSet<StructKeyEntity> StructKeyEntities => Set<StructKeyEntity>();
	public DbSet<SqlStructKeyEntity> SqlStructKeyEntities => Set<SqlStructKeyEntity>();
	public DbSet<ExplicitGeneratorEntity> ExplicitGeneratorEntities => Set<ExplicitGeneratorEntity>();
	public DbSet<CompositeKeyEntity> CompositeKeyEntities => Set<CompositeKeyEntity>();

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		configurationBuilder.AddSequentialGuidValueConverters();
		configurationBuilder.UseSequentialGuidValueGeneration();
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ExplicitGeneratorEntity>()
			.Property(e => e.Id)
			.HasValueGenerator<FixedGuidValueGenerator>();
		modelBuilder.Entity<CompositeKeyEntity>()
			.HasKey(e => new { e.Code, e.Id });
	}
}

sealed class SqlConventionDbContext(DbContextOptions<SqlConventionDbContext> options) : DbContext(options)
{
	public DbSet<GuidKeyEntity> GuidKeyEntities => Set<GuidKeyEntity>();

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
		configurationBuilder.UseSequentialGuidValueGeneration(sqlServerByteOrder: true);
}

public sealed class ValueGenerationConventionTests
{
	static DbContextOptions<T> CreateOptions<T>(string dbName) where T : DbContext =>
		new DbContextOptionsBuilder<T>()
			.UseInMemoryDatabase(dbName)
			.Options;

	[Fact]
	void GuidPrimaryKeyIsGeneratedAsV7OnAdd()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(GuidPrimaryKeyIsGeneratedAsV7OnAdd)));
		GuidKeyEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Id.ShouldNotBe(Guid.Empty);
		GuidVersion.Standard(entity.Id).ShouldBe(7);
		entity.Id.IsSequentialGuid().ShouldBeTrue();
	}

	[Fact]
	void NonKeyGuidPropertyIsNotGenerated()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(NonKeyGuidPropertyIsNotGenerated)));
		GuidKeyEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Payload.ShouldBe(Guid.Empty);
	}

	[Fact]
	void StructKeyIsGeneratedOnAdd()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(StructKeyIsGeneratedOnAdd)));
		StructKeyEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Id.Value.ShouldNotBe(Guid.Empty);
		GuidVersion.Standard(entity.Id.Value).ShouldBe(7);
	}

	[Fact]
	void SqlStructKeyIsGeneratedOnAdd()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(SqlStructKeyIsGeneratedOnAdd)));
		SqlStructKeyEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Id.Value.ShouldNotBe(Guid.Empty);
		GuidVersion.Sql(entity.Id.Value).ShouldBe(7);
	}

	[Fact]
	void ExplicitGeneratorWins()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(ExplicitGeneratorWins)));
		ExplicitGeneratorEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Id.ShouldBe(FixedGuidValueGenerator.Fixed);
	}

	[Fact]
	void CompositeKeyGuidMemberIsGenerated()
	{
		using var db = new ConventionDbContext(CreateOptions<ConventionDbContext>(nameof(CompositeKeyGuidMemberIsGenerated)));
		CompositeKeyEntity entity = new() { Code = "A" };
		db.Add(entity);
		db.SaveChanges();
		entity.Id.ShouldNotBe(Guid.Empty);
		GuidVersion.Standard(entity.Id).ShouldBe(7);
	}

	[Fact]
	void SqlByteOrderFlagProducesSqlOrderedV7()
	{
		using var db = new SqlConventionDbContext(CreateOptions<SqlConventionDbContext>(nameof(SqlByteOrderFlagProducesSqlOrderedV7)));
		GuidKeyEntity entity = new();
		db.Add(entity);
		db.SaveChanges();
		entity.Id.ShouldNotBe(Guid.Empty);
		GuidVersion.Sql(entity.Id).ShouldBe(7);
		entity.Id.IsSequentialGuid().ShouldBeTrue();
	}
}
