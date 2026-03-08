using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

using Jaunty.Benchmarks.Config;

// Handle --setup flag for TPC-H data import
if (args.Contains("--setup", StringComparer.OrdinalIgnoreCase))
{
    var providers = new[] { DatabaseProvider.Sqlite };

    // Check for --provider flag
    int providerIdx = Array.FindIndex(args, a => a.Equals("--provider", StringComparison.OrdinalIgnoreCase));
    if (providerIdx >= 0 && providerIdx + 1 < args.Length)
    {
        if (Enum.TryParse<DatabaseProvider>(args[providerIdx + 1], true, out var p))
            providers = new[] { p };
        else if (args[providerIdx + 1].Equals("all", StringComparison.OrdinalIgnoreCase))
            providers = Enum.GetValues(typeof(DatabaseProvider)).Cast<DatabaseProvider>().ToArray();
    }

    foreach (var provider in providers)
    {
        try
        {
            TpcDataImporter.Import(provider);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to import for {provider}: {ex.Message}");
        }
    }

    return;
}

// Determine config: --quick uses QuickConfig, default uses BenchmarkConfig
IConfig config = args.Contains("--quick", StringComparer.OrdinalIgnoreCase)
    ? new QuickConfig()
    : new BenchmarkConfig();

// Filter out custom flags before passing to BDN
var bdnArgs = args.Where(a =>
    !a.Equals("--quick", StringComparison.OrdinalIgnoreCase) &&
    !a.Equals("--setup", StringComparison.OrdinalIgnoreCase) &&
    !a.Equals("--provider", StringComparison.OrdinalIgnoreCase))
    .ToArray();

BenchmarkSwitcher
    .FromAssembly(typeof(BenchmarkConfig).Assembly)
    .Run(bdnArgs, config);