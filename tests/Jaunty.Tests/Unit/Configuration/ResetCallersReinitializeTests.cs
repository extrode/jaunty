using Jaunty.Tests.Helpers;

using Xunit;

namespace Jaunty.Tests.Unit.Configuration;

/// <summary>
/// <c>JauntyConfig.Reset()</c> clears process-wide state that <see cref="TestInitializer"/> set up
/// once for the whole assembly: the reflection mapper resolvers, the special-type mappers and the
/// Npgsql COPY provider. This assembly runs its collections serially, so a test class that calls
/// <c>Reset()</c> without putting that state back breaks every test that runs after it and needs
/// any of it. Found 2026-09-02 as 83, then 628, then 496 net8.0 failures on identical binaries -
/// the number moved with the test order, and every one of them was "No mapper found for type"
/// or a sibling. One <c>Dispose</c> was the cause.
/// </summary>
public class ResetCallersReinitializeTests
{
    [Fact]
    public void EveryFileThatCallsResetAlsoCallsTestInitializerInitialize()
    {
        string testRoot = Path.Combine(LocateRepositoryRoot().FullName, "tests", "Jaunty.Tests");
        List<string> offences = new();

        foreach (string file in Directory.GetFiles(testRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relative = file.Substring(testRoot.Length + 1).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal)
                || relative.StartsWith("obj/", StringComparison.Ordinal)
                || relative == "Helpers/TestInitializer.cs"
                || relative == "Unit/Configuration/ResetCallersReinitializeTests.cs")
            {
                continue;
            }

            string code = CodeWithoutCommentLines(file);
            if (code.IndexOf("JauntyConfig.Reset()", StringComparison.Ordinal) < 0)
                continue;

            if (code.IndexOf("TestInitializer.Initialize()", StringComparison.Ordinal) < 0)
                offences.Add(relative);
        }

        Assert.True(offences.Count == 0,
            "These files call JauntyConfig.Reset() and never call TestInitializer.Initialize() to put the "
            + "assembly-wide state back, so every test that runs after them and needs reflection mapping fails:"
            + Environment.NewLine + string.Join(Environment.NewLine, offences));
    }

    private static string CodeWithoutCommentLines(string file)
    {
        IEnumerable<string> lines = File.ReadLines(file)
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));

        return string.Join("\n", lines);
    }

    private static DirectoryInfo LocateRepositoryRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Jaunty.slnx")))
                return dir;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate Jaunty.slnx walking up from '{AppContext.BaseDirectory}'.");
    }
}
