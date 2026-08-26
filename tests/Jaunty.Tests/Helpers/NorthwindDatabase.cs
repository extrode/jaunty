using System.Diagnostics;

using IoPath = System.IO.Path;

namespace Jaunty.Tests.Helpers;

/// <summary>
/// Resolves the Northwind SQLite fixture for tests.
///
/// Tests open <see cref="FilePath"/>, never <see cref="SourceFilePath"/>. The source is a tracked
/// file in <c>data/sqlite/</c>; a test that opened it directly dirtied the working tree on every
/// local run, because SQLite writes to the database file even for read-only workloads. This class
/// copies it into the test output directory once per process and hands out the copy, so a run
/// leaves the repository clean whatever the tests do to it.
/// </summary>
public static class NorthwindDatabase
{
    private static readonly Lazy<string> WorkingCopy =
        new Lazy<string>(CreateWorkingCopy, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The per-process writable copy. This is what tests open.</summary>
    public static string FilePath => WorkingCopy.Value;

    /// <summary>A <c>Data Source=</c> connection string over <see cref="FilePath"/>.</summary>
    public static string ConnectionString => "Data Source=" + FilePath;

    /// <summary>The tracked fixture in <c>data/sqlite/</c>. Read it; do not open a connection to it.</summary>
    public static string SourceFilePath => ResolveSourceFilePath();

    private static string CreateWorkingCopy()
    {
        var source = ResolveSourceFilePath();
        var directory = IoPath.Combine(AppContext.BaseDirectory, "northwind-work");
        Directory.CreateDirectory(directory);

        var destination = IoPath.Combine(
            directory,
            "Northwind." + Process.GetCurrentProcess().Id.ToString() + ".db");

        PruneAbandonedCopies(directory, destination);

        File.Copy(source, destination, overwrite: true);

        // File.Copy carries the source's attributes, and a fixture checked out read-only would
        // make every write test fail with a misleading UnauthorizedAccessException.
        new FileInfo(destination).IsReadOnly = false;

        return destination;
    }

    /// <summary>
    /// Copies would otherwise accumulate in the output directory, one per run. Only copies whose
    /// owning process has exited are removed: a mutation-testing run has many hosts alive at once,
    /// and between connections SQLite holds no handle, so deleting a live host's copy would succeed
    /// and leave it opening an empty database. Every uncertain case is resolved by keeping the file.
    /// </summary>
    internal static void PruneAbandonedCopies(string directory, string keep)
    {
        foreach (var file in Directory.GetFiles(directory, "Northwind.*.db"))
        {
            if (string.Equals(file, keep, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsAbandoned(file))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static bool IsAbandoned(string file)
    {
        var name = IoPath.GetFileNameWithoutExtension(file);
        var separator = name.IndexOf('.');

        if (separator < 0 || !int.TryParse(name.Substring(separator + 1), out var pid))
        {
            return false;
        }

        try
        {
            using (Process.GetProcessById(pid))
            {
                return false;
            }
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static string ResolveSourceFilePath()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = IoPath.Combine(dir, "data", "sqlite", "Northwind.db");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        throw new FileNotFoundException(
            "Could not locate data/sqlite/Northwind.db from the test output directory.");
    }
}
