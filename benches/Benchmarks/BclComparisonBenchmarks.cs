using BenchmarkDotNet.Attributes;

namespace SequentialGuid.Benchmarks;

/// <summary>
/// Compares GuidV7 generation against the BCL's <see cref="Guid.CreateVersion7()"/> and
/// <see cref="Guid.NewGuid()"/>, single-call and bulk.
/// Run with: dotnet run -c Release -- --filter *BclComparison*
/// </summary>
[MemoryDiagnoser]
public class BclComparisonBenchmarks
{
	readonly Guid[] _buffer = new Guid[1000];

	[Benchmark(Baseline = true, Description = "Guid.NewGuid")]
	public Guid SystemGuidNewGuid() =>
		Guid.NewGuid();

	[Benchmark(Description = "Guid.CreateVersion7")]
	public Guid SystemCreateVersion7() =>
		Guid.CreateVersion7();

	[Benchmark(Description = "GuidV7.NewGuid")]
	public Guid GuidV7NewGuid() =>
		GuidV7.NewGuid();

	[Benchmark(Description = "Guid.CreateVersion7 ×1000 loop")]
	public Guid[] BclLoop()
	{
		for (var i = 0; i < _buffer.Length; i++)
			_buffer[i] = Guid.CreateVersion7();
		return _buffer;
	}

	[Benchmark(Description = "GuidV7.NewGuid ×1000 loop")]
	public Guid[] SingleCallLoop()
	{
		for (var i = 0; i < _buffer.Length; i++)
			_buffer[i] = GuidV7.NewGuid();
		return _buffer;
	}

	[Benchmark(Description = "GuidV7.Fill ×1000 bulk")]
	public Guid[] BulkFill()
	{
		GuidV7.Fill(_buffer);
		return _buffer;
	}
}
