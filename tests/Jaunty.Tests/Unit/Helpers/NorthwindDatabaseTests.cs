using System.Data.SQLite;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Unit.Helpers;

public class NorthwindDatabaseTests
{
    [Fact]
    public void SourceFilePathResolvesToTheTrackedFixture()
    {
        string source = NorthwindDatabase.SourceFilePath;

        Assert.True(File.Exists(source));
        Assert.Equal("Northwind.db", Path.GetFileName(source));
        Assert.Equal("sqlite", Path.GetFileName(Path.GetDirectoryName(source)));
    }

    [Fact]
    public void FilePathIsAnExistingFileThatIsNotTheTrackedFixture()
    {
        string working = NorthwindDatabase.FilePath;

        Assert.True(File.Exists(working));
        Assert.NotEqual(
            Path.GetFullPath(NorthwindDatabase.SourceFilePath),
            Path.GetFullPath(working));
    }

    [Fact]
    public void FilePathLivesUnderTheTestOutputDirectory()
    {
        string working = Path.GetFullPath(NorthwindDatabase.FilePath);
        string output = Path.GetFullPath(AppContext.BaseDirectory);

        Assert.StartsWith(output, working, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilePathIsStableAcrossCalls()
    {
        Assert.Equal(NorthwindDatabase.FilePath, NorthwindDatabase.FilePath);
    }

    [Fact]
    public void ConnectionStringPointsAtTheWorkingCopy()
    {
        Assert.Equal("Data Source=" + NorthwindDatabase.FilePath, NorthwindDatabase.ConnectionString);
    }

    [Fact]
    public void TheWorkingCopyCarriesTheFixtureData()
    {
        using var connection = new SQLiteConnection(NorthwindDatabase.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM categories";

        Assert.True(Convert.ToInt64(command.ExecuteScalar()) > 0);
    }

    [Fact]
    public void WritingThroughTheConnectionStringLeavesTheTrackedFixtureByteIdentical()
    {
        string source = NorthwindDatabase.SourceFilePath;
        byte[] before = Hash(source);

        using (var connection = new SQLiteConnection(NorthwindDatabase.ConnectionString))
        {
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS fixture_write_probe (id INTEGER PRIMARY KEY); " +
                "INSERT INTO fixture_write_probe (id) VALUES (1); " +
                "DROP TABLE fixture_write_probe;";
            command.ExecuteNonQuery();
        }

        Assert.Equal(before, Hash(source));
    }

    [Fact]
    public void NoTestSourceOpensTheTrackedFixtureDirectly()
    {
        DirectoryInfo repoRoot = LocateRepositoryRoot();
        string testsRoot = Path.Combine(repoRoot.FullName, "tests");
        string helper = Path.GetFullPath(
            Path.Combine(testsRoot, "Jaunty.Tests", "Helpers", "NorthwindDatabase.cs"));

        var pattern = new Regex(
            @"Data Source=[^""]*data[/\\]sqlite[/\\]Northwind\.db",
            RegexOptions.IgnoreCase);

        List<string> offenders = new();

        foreach (string file in Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relative = file.Substring(repoRoot.FullName.Length)
                .Replace('\\', '/')
                .TrimStart('/');

            if (relative.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                relative.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            if (string.Equals(Path.GetFullPath(file), helper, StringComparison.OrdinalIgnoreCase))
                continue;

            if (pattern.IsMatch(File.ReadAllText(file)))
                offenders.Add(relative);
        }

        Assert.True(offenders.Count == 0,
            "These test sources open the tracked data/sqlite/Northwind.db directly, so running them " +
            "dirties the working tree. Use NorthwindDatabase.ConnectionString instead: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void PruneRemovesACopyWhoseOwningProcessHasExited()
    {
        string directory = CreateScratchDirectory();
        try
        {
            string abandoned = Path.Combine(directory, "Northwind." + int.MaxValue + ".db");
            File.WriteAllBytes(abandoned, new byte[] { 1 });

            NorthwindDatabase.PruneAbandonedCopies(directory, Path.Combine(directory, "Northwind.1.db"));

            Assert.False(File.Exists(abandoned));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PruneKeepsACopyWhoseOwningProcessIsStillAlive()
    {
        string directory = CreateScratchDirectory();
        try
        {
            int alive = Process.GetCurrentProcess().Id;
            string live = Path.Combine(directory, "Northwind." + alive + ".db");
            File.WriteAllBytes(live, new byte[] { 1 });

            NorthwindDatabase.PruneAbandonedCopies(directory, Path.Combine(directory, "Northwind.1.db"));

            Assert.True(File.Exists(live));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PruneNeverRemovesTheCopyItWasToldToKeep()
    {
        string directory = CreateScratchDirectory();
        try
        {
            string keep = Path.Combine(directory, "Northwind." + int.MaxValue + ".db");
            File.WriteAllBytes(keep, new byte[] { 1 });

            NorthwindDatabase.PruneAbandonedCopies(directory, keep);

            Assert.True(File.Exists(keep));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PruneKeepsAFileWhoseNameCarriesNoProcessId()
    {
        string directory = CreateScratchDirectory();
        try
        {
            string opaque = Path.Combine(directory, "Northwind.baseline.db");
            File.WriteAllBytes(opaque, new byte[] { 1 });

            NorthwindDatabase.PruneAbandonedCopies(directory, Path.Combine(directory, "Northwind.1.db"));

            Assert.True(File.Exists(opaque));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateScratchDirectory()
    {
        string directory = Path.Combine(
            AppContext.BaseDirectory,
            "northwind-prune-tests",
            Guid.NewGuid().ToString("n"));

        Directory.CreateDirectory(directory);
        return directory;
    }

    private static byte[] Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create();
        return sha.ComputeHash(stream);
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
