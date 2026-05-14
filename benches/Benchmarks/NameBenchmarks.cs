using System.Text;
using BenchmarkDotNet.Attributes;

namespace SequentialGuid.Benchmarks;

/// <summary>
/// Benchmarks deterministic UUID creation for both SHA-1 (GuidV5) and SHA-256 (GuidV8Name)
/// variants, parameterised by name length to show how intermediate buffer allocations scale.
/// Run with: dotnet run -c Release -- --filter *Name*
/// </summary>
[MemoryDiagnoser]
public class NameBenchmarks
{
	[Params(
		"https://example.com",
		"https://a.much.longer.url.example.com/with/a/path?and=query&more=params")]
	public string Name { get; set; } = null!;

	byte[] _nameBytes = null!;

	[GlobalSetup]
	public void Setup() =>
		_nameBytes = Encoding.UTF8.GetBytes(Name);

	[Benchmark(Description = "GuidV5.Create(string)")]
	public Guid GuidV5CreateString() =>
		GuidV5.Create(GuidV5.Namespaces.Url, Name);

	[Benchmark(Description = "GuidV5.Create(byte[])")]
	public Guid GuidV5CreateBytes() =>
		GuidV5.Create(GuidV5.Namespaces.Url, _nameBytes);

	[Benchmark(Description = "GuidV8Name.Create(string)")]
	public Guid GuidV8NameCreateString() =>
		GuidV8Name.Create(GuidV8Name.Namespaces.Url, Name);

	[Benchmark(Description = "GuidV8Name.Create(byte[])")]
	public Guid GuidV8NameCreateBytes() =>
		GuidV8Name.Create(GuidV8Name.Namespaces.Url, _nameBytes);
}
