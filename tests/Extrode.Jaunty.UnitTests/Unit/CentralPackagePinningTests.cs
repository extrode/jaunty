using System;
using System.IO;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit;

public class CentralPackagePinningTests
{
    [Fact]
    public void TransitivePinningStaysOffBecauseItLeaksPinnedVersionsIntoPackedNuspecs()
    {
        string props = File.ReadAllText(Path.Combine(LocateRepositoryRoot().FullName, "Directory.Packages.props"));

        Assert.DoesNotContain("<CentralPackageTransitivePinningEnabled>true<", props, StringComparison.OrdinalIgnoreCase);
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
