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
        AddJob(Job.ShortRun
            .WithWarmupCount(3)
            .WithIterationCount(10));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddLogger(ConsoleLogger.Default);

        AddColumnProvider(DefaultColumnProviders.Instance);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CsvExporter.Default);

        WithSummaryStyle(SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend));
    }
}
