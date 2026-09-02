using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;

namespace Jaunty.Benchmarks.Config;

public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        // Cold-start: no warmup, 1 invocation per iteration, 1 iteration.
        // BDN spawns a new process per (method, job), so static caches are genuinely empty.
        // The first invocation hits all cold paths (OrdinalMap, MappedCache, ParameterCache, etc.).
        AddJob(Job.Default
            .WithId("Cold")
            .WithWarmupCount(0)
            .WithIterationCount(1)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));

        // Warm: 2 warmup iterations populate all ORM caches before measurement begins.
        // Measures steady-state throughput with all caches hot. 15 iterations, not 5: the
        // libraries sit within a few percent of each other at 10k rows, and 5 iterations gave
        // error bars of 5-10% of the mean, wider than the differences being reported.
        AddJob(Job.Default
            .WithId("Warm")
            .WithWarmupCount(2)
            .WithIterationCount(15));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        HideColumns(Column.InvocationCount, Column.IterationCount, Column.UnrollFactor, Column.WarmupCount);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CsvExporter.Default);

        WithSummaryStyle(SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend));

        // Redirect artifacts to docs/benchmark-artifacts/
        ArtifactsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "benchmark-artifacts"));
    }
}

/// <summary>
/// Quick config for dev feedback: minimal iterations, fast turnaround.
/// Usage: dotnet run -- --quick --filter "*Query*"
/// </summary>
public class QuickConfig : ManualConfig
{
    public QuickConfig()
    {
        AddJob(Job.Dry); // 1 warmup, 1 iteration

        AddDiagnoser(MemoryDiagnoser.Default);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        HideColumns(Column.InvocationCount, Column.IterationCount, Column.UnrollFactor, Column.WarmupCount);
        AddExporter(MarkdownExporter.GitHub);

        WithSummaryStyle(SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend));

        // Same artifact path
        ArtifactsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "benchmark-artifacts"));
    }
}