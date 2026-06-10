#if NET8_0_OR_GREATER
using SequentialGuid.Extensions;

namespace SequentialGuid.Tests;

public sealed class TimeProviderTests
{
	// RFC 9562 Appendix A.6 moment
	static readonly DateTimeOffset _fixedNow = new(2022, 2, 22, 19, 22, 22, TimeSpan.Zero);
	const long RfcTestVectorMs = 1645557742000L;

	sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}

	static readonly TimeProvider _fixed = new FixedTimeProvider(_fixedNow);

	[Fact]
	void GuidV7NewGuidEmbedsProviderTime() =>
		GuidV7.NewGuid(_fixed).ToUnixMs().ShouldBe(RfcTestVectorMs);

	[Fact]
	void GuidV7NewSqlGuidEmbedsProviderTime() =>
		GuidV7.NewSqlGuid(_fixed).FromSqlGuid().ToUnixMs().ShouldBe(RfcTestVectorMs);

	[Fact]
	void GuidV7NullProviderThrows() =>
		Should.Throw<ArgumentNullException>(() => GuidV7.NewGuid(null!));

	[Fact]
	void GuidV8TimeNewGuidEmbedsProviderTime() =>
		GuidV8Time.NewGuid(_fixed).ToDateTime().ShouldBe(_fixedNow.UtcDateTime);

	[Fact]
	void GuidV8TimeNewSqlGuidEmbedsProviderTime() =>
		GuidV8Time.NewSqlGuid(_fixed).FromSqlGuid().ToDateTime().ShouldBe(_fixedNow.UtcDateTime);

	[Fact]
	void GuidV8TimeNullProviderThrows() =>
		Should.Throw<ArgumentNullException>(() => GuidV8Time.NewGuid(null!));

	[Fact]
	void GuidV7FillEmbedsProviderTime()
	{
		var ids = new Guid[10];
		GuidV7.Fill(ids, _fixed);
		foreach (var id in ids)
			id.ToUnixMs().ShouldBe(RfcTestVectorMs);
	}

	[Fact]
	void GuidV7FillSqlEmbedsProviderTime()
	{
		var ids = new Guid[10];
		GuidV7.FillSql(ids, _fixed);
		foreach (var id in ids)
			id.FromSqlGuid().ToUnixMs().ShouldBe(RfcTestVectorMs);
	}

	[Fact]
	void GuidV7NewGuidsEmbedsProviderTime()
	{
		foreach (var id in GuidV7.NewGuids(10, _fixed))
			id.ToUnixMs().ShouldBe(RfcTestVectorMs);
	}

	[Fact]
	void GuidV7NewSqlGuidsEmbedsProviderTime()
	{
		foreach (var id in GuidV7.NewSqlGuids(10, _fixed))
			id.FromSqlGuid().ToUnixMs().ShouldBe(RfcTestVectorMs);
	}

	[Fact]
	void GuidV7FillNullProviderThrows()
	{
		Should.Throw<ArgumentNullException>(() =>
		{
			var ids = new Guid[1];
			GuidV7.Fill(ids, (TimeProvider)null!);
		});
	}

	[Fact]
	void GuidV8TimeFillEmbedsProviderTime()
	{
		var ids = new Guid[10];
		GuidV8Time.Fill(ids, _fixed);
		foreach (var id in ids)
			id.ToDateTime().ShouldBe(_fixedNow.UtcDateTime);
	}

	[Fact]
	void GuidV8TimeFillSqlEmbedsProviderTime()
	{
		var ids = new Guid[10];
		GuidV8Time.FillSql(ids, _fixed);
		foreach (var id in ids)
			id.FromSqlGuid().ToDateTime().ShouldBe(_fixedNow.UtcDateTime);
	}

	[Fact]
	void GuidV8TimeNewGuidsEmbedsProviderTime()
	{
		foreach (var id in GuidV8Time.NewGuids(10, _fixed))
			id.ToDateTime().ShouldBe(_fixedNow.UtcDateTime);
	}

	[Fact]
	void GuidV8TimeNewSqlGuidsEmbedsProviderTime()
	{
		foreach (var id in GuidV8Time.NewSqlGuids(10, _fixed))
			id.FromSqlGuid().ToDateTime().ShouldBe(_fixedNow.UtcDateTime);
	}

	[Fact]
	void GuidV8TimeFillNullProviderThrows()
	{
		Should.Throw<ArgumentNullException>(() =>
		{
			var ids = new Guid[1];
			GuidV8Time.Fill(ids, (TimeProvider)null!);
		});
	}
}
#endif
