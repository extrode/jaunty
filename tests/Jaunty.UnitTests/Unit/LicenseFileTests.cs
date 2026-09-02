using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using Xunit;

namespace Jaunty.Tests.Unit;

/// <summary>
/// LICENSE-EULA.md is not documentation: <c>src/Directory.Build.props</c> names it as
/// <c>PackageLicenseFile</c>, so it is packed into every Extrode.Jaunty.* package and is the
/// agreement a customer actually receives. It shipped in v1.0.0-rc.1 with all ten of its
/// bracketed placeholders unfilled - naming no licensor and no governing law - and nothing
/// failed, because nothing was looking.
/// </summary>
public class LicenseFileTests
{
    private const string Licensor = "Extrode LLC";

    [Fact]
    public void ThePackagedEulaCarriesNoUnfilledPlaceholders()
    {
        List<string> placeholders = new();

        // The 1.2 texts mention `[LICENSOR]` in their reproduction notice, in backticks, as prose
        // about the field rather than the field itself; only an unbackticked match is unfilled.
        foreach (Match match in Regex.Matches(ReadRepositoryFile("LICENSE-EULA.md"), @"(?<!`)\[[A-Z][A-Z ]{2,}\](?!`)"))
            placeholders.Add(match.Value);

        Assert.Equal(new string[0], placeholders.ToArray());
    }

    [Fact]
    public void TheLicenseFilesNameTheRealLicensor()
    {
        foreach (string file in LicenseFiles())
        {
            string text = ReadRepositoryFile(file);

            Assert.Contains("**Copyright (c) 2026 " + Licensor + ".", text, StringComparison.Ordinal);
            Assert.Contains("**1.2 \"Licensor\"** means " + Licensor, text, StringComparison.Ordinal);

            // The canonical ISL-P/ISL-C/ISL-R texts hard-code Ikhbat Foundation in 1.2 while the
            // adoption instructions say to fill in nothing but the copyright line. Copy one of
            // those verbatim and the written-consent mechanism in Section 2 runs to them, not us.
            Assert.DoesNotContain("means Ikhbat Foundation", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Both files were three upstream commits behind when this was written. Each clause below
    /// arrived in one of them, so their absence means the copy has drifted again.
    /// </summary>
    [Fact]
    public void TheLicenseFilesCarryTheCurrentUpstreamClauses()
    {
        foreach (string file in LicenseFiles())
        {
            string text = ReadRepositoryFile(file);

            Assert.Contains("## Notice on Reproducing This License", text, StringComparison.Ordinal);
            Assert.Contains("### 4.2 Riba / Interest", text, StringComparison.Ordinal);
            Assert.Contains("Interest (*riba*) is defined, per Islamic principles", text, StringComparison.Ordinal);

            // Removed upstream in d4cbe1b as redundant with the clarification above it.
            Assert.DoesNotContain("regardless of their\npersonal identity", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A rename of the packed file leaves the pack step green and ships a package whose license
    /// is whatever NuGet finds, so the path is asserted rather than assumed.
    /// </summary>
    [Fact]
    public void ThePackagedLicenseFileIsTheOneOnDisk()
    {
        string props = ReadRepositoryFile(Path.Combine("src", "Directory.Build.props"));

        Match declared = Regex.Match(props, @"<PackageLicenseFile>([^<]+)</PackageLicenseFile>");
        Assert.True(declared.Success, "src/Directory.Build.props declares no PackageLicenseFile.");

        string named = declared.Groups[1].Value;
        string onDisk = Path.Combine(LocateRepositoryRoot().FullName, named);

        Assert.True(File.Exists(onDisk),
            "Packages are packed with '" + named + "', which is not at the repository root.");

        Assert.Contains(
            "<None Include=\"$(MSBuildThisFileDirectory)..\\" + named + "\" Pack=\"true\"",
            props,
            StringComparison.Ordinal);
    }

    private static string[] LicenseFiles() => new[] { "LICENSE.md", "LICENSE-EULA.md" };

    private static string ReadRepositoryFile(string relativePath)
    {
        string path = Path.Combine(LocateRepositoryRoot().FullName, relativePath);

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
