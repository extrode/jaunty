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
        // Cold-start: no warmup, 1 invocation per iteration, 3 iterations.
        // BDN spawns a new process per (method, job), so static caches are genuinely empty.
        // The first invocation hits all cold paths (OrdinalMap, MappedCache, ParameterCache, etc.).
        AddJob(Job.Default
            .WithId("Cold")
            .WithWarmupCount(0)
            .WithIterationCount(3)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));

        // Warm: 5 warmup iterations populate all ORM caches before measurement begins.
        // Measures steady-state throughput with all caches hot.
        AddJob(Job.Default
            .WithId("Warm")
            .WithWarmupCount(5)
            .WithIterationCount(10));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        HideColumns(Column.InvocationCount, Column.IterationCount, Column.UnrollFactor, Column.WarmupCount);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CsvExporter.Default);

        WithSummaryStyle(SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend));
    }
}
