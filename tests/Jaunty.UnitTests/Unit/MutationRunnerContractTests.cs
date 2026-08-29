using System;
using System.IO;
using System.Text.RegularExpressions;

using Xunit;

namespace Jaunty.Tests.Unit;

/// <summary>
/// The mutation job's runner, its timeout and its Stryker concurrency are three numbers that only
/// make sense together, and every way they can drift apart is quiet. Falling back to a 2-core
/// hosted runner does not fail - it runs until GitHub's 360-minute default kills it, hours later,
/// having produced nothing. Raising the vCPU tier without raising <c>--concurrency</c> pays for
/// cores that sit idle, and Blacksmith runners report the host's CPU count rather than the
/// allocated vCPUs, so nothing downstream can infer the right number for itself.
/// </summary>
public class MutationRunnerContractTests
{
    private const string ExpectedRunner = "blacksmith-16vcpu-ubuntu-2404";
    private const string ExpectedConcurrency = "16";
    private const int ExpectedTimeoutMinutes = 240;

    [Fact]
    public void TheMutationJobRunsOnTheSizedBlacksmithRunner()
    {
        string job = MutationJob();

        Assert.Contains(ExpectedRunner, job, StringComparison.Ordinal);
        Assert.DoesNotContain("vars.CI_RUNNER", job, StringComparison.Ordinal);
    }

    /// <summary>
    /// Blacksmith documents no maximum job duration, but GitHub's own 360-minute default applies to
    /// every runner regardless, so an explicit timeout is the only thing that bounds a hung run.
    /// </summary>
    [Fact]
    public void TheMutationJobBoundsItsOwnRuntime()
    {
        Match timeout = Regex.Match(MutationJob(), @"(?m)^    timeout-minutes: (\d+)$");

        Assert.True(timeout.Success,
            "The mutation job declares no timeout-minutes, so it inherits GitHub's 360-minute " +
            "default - long enough to burn most of the org's monthly free allowance on one hung run.");

        int minutes = int.Parse(timeout.Groups[1].Value);

        Assert.Equal(ExpectedTimeoutMinutes, minutes);
        Assert.True(minutes < 360, "An explicit timeout above GitHub's own default bounds nothing.");
    }

    [Fact]
    public void StrykerConcurrencyMatchesTheRunnersVcpuCount()
    {
        string job = MutationJob();

        Match vcpu = Regex.Match(ExpectedRunner, @"blacksmith-(\d+)vcpu");
        Assert.True(vcpu.Success, "The runner label no longer names a vCPU count.");

        Assert.Contains(
            "--concurrency ${{ vars.CI_MUTATION_CONCURRENCY || '" + ExpectedConcurrency + "' }}",
            job,
            StringComparison.Ordinal);

        Assert.Equal(vcpu.Groups[1].Value, ExpectedConcurrency);
    }

    private static string MutationJob()
    {
        string path = Path.Combine(LocateRepositoryRoot().FullName, ".github", "workflows", "nightly.yml");

        Assert.True(File.Exists(path), "'" + path + "' is missing; this suite cannot pass vacuously.");

        // CRLF file: '$' in multiline mode matches before '\n' and not before '\r'.
        string workflow = File.ReadAllText(path).Replace("\r\n", "\n");

        Match job = Regex.Match(workflow, @"(?m)^  mutation:$\n(?:^(?:    .*)?\n)+");
        Assert.True(job.Success, "No 'mutation' job was found in nightly.yml.");

        return job.Value;
    }

    private static DirectoryInfo LocateRepositoryRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Jaunty.slnx")))
                return dir;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate Jaunty.slnx walking up from '" + AppContext.BaseDirectory + "'.");
    }
}
