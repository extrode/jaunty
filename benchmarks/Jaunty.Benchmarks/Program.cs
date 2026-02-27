using BenchmarkDotNet.Running;

using Jaunty.Benchmarks.Config;

BenchmarkSwitcher
    .FromAssembly(typeof(BenchmarkConfig).Assembly)
    .Run(args, new BenchmarkConfig());
