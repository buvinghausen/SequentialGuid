<!-- Captured 2026-06-10 for the v6.2 README; regenerate with: dotnet run -c Release --project benches/Benchmarks -- --filter *BclComparison* -->

# BCL Comparison Benchmarks

## Environment

**BenchmarkDotNet v0.15.8**, Windows 11 (10.0.26200.8390/25H2/2025Update/HudsonValley2)

Intel Core Ultra 9 185H 2.30GHz, 1 CPU, 22 logical and 16 physical cores

.NET SDK 11.0.100-preview.4.26230.115

**[Host]**: .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

**DefaultJob**: .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

## Results

| Method                           | Mean         | Error        | StdDev       | Ratio    | RatioSD | Allocated | Alloc Ratio |
|--------------------------------- |-------------:|-------------:|-------------:|---------:|--------:|----------:|------------:|
| Guid.NewGuid                     |     44.46 ns |     0.812 ns |     0.634 ns |     1.00 |    0.02 |         - |          NA |
| Guid.CreateVersion7              |     65.09 ns |     1.025 ns |     1.437 ns |     1.46 |    0.04 |         - |          NA |
| GuidV7.NewGuid                   |     82.59 ns |     1.501 ns |     2.055 ns |     1.86 |    0.05 |         - |          NA |
| 'Guid.CreateVersion7 ×1000 loop' | 67,284.17 ns | 1,299.937 ns | 1,152.361 ns | 1,513.81 |   32.54 |         - |          NA |
| 'GuidV7.NewGuid ×1000 loop'      | 88,345.82 ns | 1,731.024 ns | 2,892.152 ns | 1,987.67 |   69.76 |         - |          NA |
| 'GuidV7.Fill ×1000 bulk'         |  6,210.75 ns |    56.053 ns |    49.690 ns |   139.73 |    2.20 |         - |          NA |
