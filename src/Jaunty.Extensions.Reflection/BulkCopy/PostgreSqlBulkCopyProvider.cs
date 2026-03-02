using System;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Jaunty.Configuration;
using Jaunty.Internals.BulkCopy;

namespace Jaunty.Extensions.Reflection.BulkCopy;

/// <summary>
/// PostgreSQL bulk copy provider using NpgsqlBinaryImporter via reflection.
/// Uses reflection to avoid hard dependency on Npgsql.
/// </summary>
internal sealed class PostgreSqlBulkCopyProvider : IBulkCopyProvider
{
    private static readonly Type? NpgsqlConnectionType = Type.GetType("Npgsql.NpgsqlConnection, Npgsql")
        ?? Type.GetType("Npgsql.NpgsqlConnection, Npgsql.NetStandard");

    private static readonly Type? NpgsqlBinaryImporterType = Type.GetType("Npgsql.NpgsqlBinaryImporter, Npgsql")
        ?? Type.GetType("Npgsql.NpgsqlBinaryImporter, Npgsql.NetStandard");

    private static readonly MethodInfo? BeginBinaryImportMethod = NpgsqlConnectionType?.GetMethod("BeginBinaryImport", new[] { typeof(string) });
    private static readonly MethodInfo? StartRowMethod = NpgsqlBinaryImporterType?.GetMethod("StartRow");
    private static readonly MethodInfo? WriteMethod = NpgsqlBinaryImporterType?.GetMethod("Write", new[] { typeof(object) });
    private static readonly MethodInfo? CompleteMethod = NpgsqlBinaryImporterType?.GetMethod("Complete");
    private static readonly MethodInfo? DisposeMethod = NpgsqlBinaryImporterType?.GetMethod("Dispose");

    /// <inheritdoc/>
    public bool IsSupported => NpgsqlConnectionType != null && NpgsqlBinaryImporterType != null;

    /// <inheritdoc/>
    public int CopyToServer(IDbConnection connection, string tableName, IDataReader data, BulkCopyOptions options)
    {
        if (NpgsqlConnectionType == null || NpgsqlBinaryImporterType == null)
            throw new InvalidOperationException("NpgsqlBinaryImporter is not available. Ensure Npgsql is installed.");

        var npgsqlConnection = connection as DbConnection
            ?? throw new ArgumentException("Connection must be a NpgsqlConnection.", nameof(connection));

        // Build COPY command
        var copyCommand = BuildCopyCommand(tableName, data);

        // Begin binary import
        var importer = BeginBinaryImportMethod?.Invoke(connection, new object[] { copyCommand });

        if (importer == null)
            throw new InvalidOperationException("Failed to begin binary import.");

        try
        {
            int rowCount = 0;
            int columnCount = data.FieldCount;

            while (data.Read())
            {
                StartRowMethod?.Invoke(importer, null);

                for (int i = 0; i < columnCount; i++)
                {
                    var value = data.GetValue(i);
                    WriteMethod?.Invoke(importer, new[] { value });
                }

                rowCount++;
            }

            CompleteMethod?.Invoke(importer, null);

            return rowCount;
        }
        catch
        {
            // Dispose will rollback the import
            DisposeMethod?.Invoke(importer, null);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> CopyToServerAsync(
        DbConnection connection,
        string tableName,
        IDataReader data,
        BulkCopyOptions options,
        CancellationToken cancellationToken)
    {
        if (NpgsqlConnectionType == null || NpgsqlBinaryImporterType == null)
            throw new InvalidOperationException("NpgsqlBinaryImporter is not available. Ensure Npgsql is installed.");

        // Build COPY command
        var copyCommand = BuildCopyCommand(tableName, data);

        // For async, we need to use the async API if available
        // NpgsqlBinaryImporter doesn't have native async methods, so we use sync in background
        return await Task.Run(() => CopyToServer(connection, tableName, data, options), cancellationToken);
    }

    /// <summary>
    /// Builds the COPY command for PostgreSQL.
    /// </summary>
    private static string BuildCopyCommand(string tableName, IDataReader data)
    {
        var columnNames = new List<string>();
        for (int i = 0; i < data.FieldCount; i++)
        {
            columnNames.Add($"\"{data.GetName(i)}\"");
        }

        var columns = string.Join(", ", columnNames);
        return $"COPY {tableName} ({columns}) FROM STDIN BINARY";
    }
}
