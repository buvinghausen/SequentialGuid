using System.Data.SqlTypes;
using BenchmarkDotNet.Attributes;

namespace SequentialGuid.Benchmarks;

/// <summary>
/// Benchmarks the conversion extension methods that each allocate one or more byte arrays today.
/// These are the primary targets for the stackalloc / Span&lt;byte&gt; optimisation.
/// Run with: dotnet run -c Release -- --filter *Conversion*
/// </summary>
[MemoryDiagnoser]
public sealed class ConversionBenchmarks
{
	private Guid _guidV7;
	private Guid _guidV8Time;
	private SqlGuid _sqlGuid;

	[GlobalSetup]
	public void Setup()
	{
		_guidV7 = GuidV7.NewGuid();
		_guidV8Time = GuidV8Time.NewGuid();
		_sqlGuid = _guidV7.ToSqlGuid();
	}

	[Benchmark(Description = "GuidV7.ToDateTime")]
	public DateTime? GuidV7ToDateTime() =>
		_guidV7.ToDateTime();

	[Benchmark(Description = "GuidV8Time.ToDateTime")]
	public DateTime? GuidV8TimeToDateTime() =>
		_guidV8Time.ToDateTime();

	[Benchmark(Description = "Guid.ToSqlGuid")]
	public SqlGuid GuidToSqlGuid() =>
		_guidV7.ToSqlGuid();

	[Benchmark(Description = "SqlGuid.ToGuid")]
	public Guid SqlGuidToGuid() =>
		_sqlGuid.ToGuid();
}
