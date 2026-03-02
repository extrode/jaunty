using System;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Jaunty.Configuration;
using Jaunty.Internals.BulkCopy;

namespace Jaunty.Extensions.Reflection.BulkCopy;

/// <summary>
/// MySQL bulk copy provider using MySqlBulkLoader via reflection.
/// Uses reflection to avoid hard dependency on MySqlConnector or MySql.Data.
/// </summary>
internal sealed class MySqlBulkCopyProvider : IBulkCopyProvider
{
    private static readonly Type? MySqlConnectionType = Type.GetType("MySql.Data.MySqlClient.MySqlConnection, MySql.Data")
        ?? Type.GetType("MySqlConnector.MySqlConnection, MySqlConnector");

    private static readonly Type? MySqlBulkLoaderType = Type.GetType("MySql.Data.MySqlClient.MySqlBulkLoader, MySql.Data")
        ?? Type.GetType("MySqlConnector.MySqlBulkLoader, MySqlConnector");

    private static readonly PropertyInfo? ConnectionProperty = MySqlBulkLoaderType?.GetProperty("Connection");
    private static readonly PropertyInfo? TableNameProperty = MySqlBulkLoaderType?.GetProperty("TableName");
    private static readonly PropertyInfo? SourceStreamProperty = MySqlBulkLoaderType?.GetProperty("SourceStream");
    private static readonly PropertyInfo? FieldTerminatorProperty = MySqlBulkLoaderType?.GetProperty("FieldTerminator");
    private static readonly PropertyInfo? LineTerminatorProperty = MySqlBulkLoaderType?.GetProperty("LineTerminator");
    private static readonly MethodInfo? LoadMethod = MySqlBulkLoaderType?.GetMethod("Load");
    private static readonly MethodInfo? LoadAsyncMethod = MySqlBulkLoaderType?.GetMethod("LoadAsync");
    private static readonly MethodInfo? DisposeMethod = MySqlBulkLoaderType?.GetMethod("Dispose");

    /// <inheritdoc/>
    public bool IsSupported => MySqlBulkLoaderType != null;

    /// <inheritdoc/>
    public int CopyToServer(IDbConnection connection, string tableName, IDataReader data, BulkCopyOptions options)
    {
        if (MySqlBulkLoaderType == null)
            throw new InvalidOperationException("MySqlBulkLoader is not available. Ensure MySqlConnector or MySql.Data is installed.");

        // Create temporary CSV file
        var tempFile = Path.GetTempFileName();
        try
        {
            // Write data to CSV
            WriteDataToCsv(data, tempFile);

            // Create and configure MySqlBulkLoader
            var bulkLoader = Activator.CreateInstance(MySqlBulkLoaderType);
            if (bulkLoader == null)
                throw new InvalidOperationException("Failed to create MySqlBulkLoader instance.");

            try
            {
                ConnectionProperty?.SetValue(bulkLoader, connection);
                TableNameProperty?.SetValue(bulkLoader, tableName);

                var fileStream = File.OpenRead(tempFile);
                SourceStreamProperty?.SetValue(bulkLoader, fileStream);

                FieldTerminatorProperty?.SetValue(bulkLoader, ",");
                LineTerminatorProperty?.SetValue(bulkLoader, "\n");

                // Execute bulk load
                var result = LoadMethod?.Invoke(bulkLoader, null);

                // Return rows affected (from result or -1 if unknown)
                return result is int rows ? rows : -1;
            }
            finally
            {
                DisposeMethod?.Invoke(bulkLoader, null);
            }
        }
        finally
        {
            // Clean up temp file
            try { File.Delete(tempFile); } catch { }
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
        if (MySqlBulkLoaderType == null)
            throw new InvalidOperationException("MySqlBulkLoader is not available. Ensure MySqlConnector or MySql.Data is installed.");

        // Create temporary CSV file
        var tempFile = Path.GetTempFileName();
        try
        {
            // Write data to CSV
            await WriteDataToCsvAsync(data, tempFile, cancellationToken);

            // Create and configure MySqlBulkLoader
            var bulkLoader = Activator.CreateInstance(MySqlBulkLoaderType);
            if (bulkLoader == null)
                throw new InvalidOperationException("Failed to create MySqlBulkLoader instance.");

            try
            {
                ConnectionProperty?.SetValue(bulkLoader, connection);
                TableNameProperty?.SetValue(bulkLoader, tableName);

                var fileStream = File.OpenRead(tempFile);
                SourceStreamProperty?.SetValue(bulkLoader, fileStream);

                FieldTerminatorProperty?.SetValue(bulkLoader, ",");
                LineTerminatorProperty?.SetValue(bulkLoader, "\n");

                // Execute bulk load asynchronously
                if (LoadAsyncMethod != null)
                {
                    var task = LoadAsyncMethod.Invoke(bulkLoader, new object[] { cancellationToken }) as Task;
                    if (task != null)
                    {
                        await task.ConfigureAwait(false);
                        return -1; // Rows affected unknown
                    }
                }

                // Fallback to sync
                var result = LoadMethod?.Invoke(bulkLoader, null);
                return result is int rows ? rows : -1;
            }
            finally
            {
                DisposeMethod?.Invoke(bulkLoader, null);
            }
        }
        finally
        {
            // Clean up temp file
            try { File.Delete(tempFile); } catch { }
        }
    }

    /// <summary>
    /// Writes data from IDataReader to a CSV file.
    /// </summary>
    private static void WriteDataToCsv(IDataReader data, string filePath)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        while (data.Read())
        {
            for (int i = 0; i < data.FieldCount; i++)
            {
                if (i > 0) writer.Write(',');

                var value = data.GetValue(i);
                if (value is DBNull)
                {
                    // Write NULL as \N for MySQL
                    writer.Write("\\N");
                }
                else
                {
                    var str = value!.ToString();
                    if (str!.Contains(',') || str.Contains('"') || str.Contains('\n'))
                    {
                        // Escape quotes and wrap in quotes
                        writer.Write('"');
                        writer.Write(str.Replace("\"", "\"\""));
                        writer.Write('"');
                    }
                    else
                    {
                        writer.Write(str);
                    }
                }
            }

            writer.WriteLine();
        }
    }

    /// <summary>
    /// Writes data from IDataReader to a CSV file asynchronously.
    /// </summary>
    private static async Task WriteDataToCsvAsync(IDataReader data, string filePath, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        while (data.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int i = 0; i < data.FieldCount; i++)
            {
                if (i > 0) await writer.WriteAsync(",");

                var value = data.GetValue(i);
                if (value is DBNull)
                {
                    await writer.WriteAsync("\\N");
                }
                else
                {
                    var str = value!.ToString();
                    if (str!.Contains(',') || str.Contains('"') || str.Contains('\n'))
                    {
                        await writer.WriteAsync("\"");
                        await writer.WriteAsync(str.Replace("\"", "\"\""));
                        await writer.WriteAsync("\"");
                    }
                    else
                    {
                        await writer.WriteAsync(str);
                    }
                }
            }

            await writer.WriteLineAsync();
        }
    }
}
