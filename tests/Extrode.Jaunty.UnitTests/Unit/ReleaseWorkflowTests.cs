using System;
using System.IO;
using System.Text.RegularExpressions;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit;

public class ReleaseWorkflowTests
{
    [Fact]
    public void TheGitHubReleaseIsMarkedPreReleaseWhenTheVersionHasASuffix()
    {
        Match step = Regex.Match(ReadReleaseWorkflow(), @"(?m)^      - name: Create GitHub Release$\n(?:^        .*\n)+");
        Assert.True(step.Success, "No 'Create GitHub Release' step was found in release.yml.");

        Assert.Matches(@"(?m)^          prerelease: \$\{\{ contains\(steps\.version\.outputs\.version, '-'\) \}\}$", step.Value);
    }

    [Fact]
    public void TheVersionStepTakesTheVersionFromTheTag()
    {
        Assert.Contains("echo \"version=${GITHUB_REF_NAME#v}\" >> \"$GITHUB_OUTPUT\"", ReadReleaseWorkflow(), StringComparison.Ordinal);
    }

    private static string ReadReleaseWorkflow()
    {
        string path = Path.Combine(LocateRepositoryRoot().FullName, ".github", "workflows", "release.yml");

        Assert.True(File.Exists(path), "'" + path + "' is missing; this suite cannot pass vacuously.");

        return File.ReadAllText(path).Replace("\r\n", "\n");
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
