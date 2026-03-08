using BenchmarkDotNet.Attributes;

namespace SequentialGuid.Benchmarks;

/// <summary>
/// Compares all UUID generation methods against the <see cref="Guid.NewGuid"/> baseline.
/// Run with: dotnet run -c Release -- --filter *Generation*
/// </summary>
[MemoryDiagnoser]
public sealed class GenerationBenchmarks
{
	[Benchmark(Baseline = true, Description = "Guid.NewGuid")]
	public static Guid SystemGuidNewGuid() =>
		Guid.NewGuid();

	[Benchmark(Description = "GuidV4.NewGuid")]
	public static Guid GuidV4NewGuid() =>
		GuidV4.NewGuid();

	[Benchmark(Description = "GuidV7.NewGuid")]
	public static Guid GuidV7NewGuid() =>
		GuidV7.NewGuid();

	[Benchmark(Description = "GuidV7.NewSqlGuid")]
	public static Guid GuidV7NewSqlGuid() =>
		GuidV7.NewSqlGuid();

	[Benchmark(Description = "GuidV8Time.NewGuid")]
	public static Guid GuidV8TimeNewGuid() =>
		GuidV8Time.NewGuid();

	[Benchmark(Description = "GuidV8Time.NewSqlGuid")]
	public static Guid GuidV8TimeNewSqlGuid() =>
		GuidV8Time.NewSqlGuid();
}
