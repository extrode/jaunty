using System;
using System.Data;
using System.Data.Common;
using System.IO;
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

    // MySqlBulkLoader constructor requires a MySqlConnection parameter
    private static readonly ConstructorInfo? BulkLoaderCtor = MySqlBulkLoaderType?.GetConstructor(
        MySqlConnectionType != null ? new[] { MySqlConnectionType } : Type.EmptyTypes);

    private static readonly PropertyInfo? TableNameProperty = MySqlBulkLoaderType?.GetProperty("TableName");
    private static readonly PropertyInfo? SourceStreamProperty = MySqlBulkLoaderType?.GetProperty("SourceStream");
    private static readonly PropertyInfo? FieldTerminatorProperty = MySqlBulkLoaderType?.GetProperty("FieldTerminator");
    private static readonly PropertyInfo? FieldQuotationCharacterProperty = MySqlBulkLoaderType?.GetProperty("FieldQuotationCharacter");
    private static readonly PropertyInfo? FieldQuotationOptionalProperty = MySqlBulkLoaderType?.GetProperty("FieldQuotationOptional");
    private static readonly PropertyInfo? LineTerminatorProperty = MySqlBulkLoaderType?.GetProperty("LineTerminator");
    private static readonly MethodInfo? LoadMethod = MySqlBulkLoaderType?.GetMethod("Load");
    private static readonly MethodInfo? LoadAsyncMethod = MySqlBulkLoaderType?.GetMethod("LoadAsync");

    /// <inheritdoc/>
    public bool IsSupported => MySqlBulkLoaderType != null && BulkLoaderCtor != null;

    /// <inheritdoc/>
    public int CopyToServer(IDbConnection connection, string tableName, IDataReader data, BulkCopyOptions options)
    {
        if (MySqlBulkLoaderType == null || BulkLoaderCtor == null)
            throw new InvalidOperationException("MySqlBulkLoader is not available. Ensure MySqlConnector or MySql.Data is installed.");

        var tempFile = Path.GetTempFileName();
        try
        {
            int rowCount = WriteDataToCsv(data, tempFile);

            // Create MySqlBulkLoader with connection parameter
            var bulkLoader = BulkLoaderCtor.Invoke(new object[] { connection });
            if (bulkLoader == null)
                throw new InvalidOperationException("Failed to create MySqlBulkLoader instance.");

            try
            {
                ConfigureBulkLoader(bulkLoader, tableName, tempFile);

                var result = LoadMethod?.Invoke(bulkLoader, null);
                return result is int rows ? rows : rowCount;
            }
            finally
            {
                (bulkLoader as IDisposable)?.Dispose();
            }
        }
        finally
        {
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
        if (MySqlBulkLoaderType == null || BulkLoaderCtor == null)
            throw new InvalidOperationException("MySqlBulkLoader is not available. Ensure MySqlConnector or MySql.Data is installed.");

        var tempFile = Path.GetTempFileName();
        try
        {
            int rowCount = await WriteDataToCsvAsync(data, tempFile, cancellationToken);

            var bulkLoader = BulkLoaderCtor.Invoke(new object[] { connection });
            if (bulkLoader == null)
                throw new InvalidOperationException("Failed to create MySqlBulkLoader instance.");

            try
            {
                ConfigureBulkLoader(bulkLoader, tableName, tempFile);

                // LoadAsync returns Task<int>
                if (LoadAsyncMethod != null)
                {
                    var task = LoadAsyncMethod.Invoke(bulkLoader, new object[] { cancellationToken });
                    if (task is Task<int> intTask)
                    {
                        return await intTask.ConfigureAwait(false);
                    }
                    if (task is Task plainTask)
                    {
                        await plainTask.ConfigureAwait(false);
                        return rowCount;
                    }
                }

                // Fallback to sync
                var result = LoadMethod?.Invoke(bulkLoader, null);
                return result is int rows ? rows : rowCount;
            }
            finally
            {
                (bulkLoader as IDisposable)?.Dispose();
            }
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    /// <summary>
    /// Configures the MySqlBulkLoader instance with table name, stream, and CSV format settings.
    /// </summary>
    private static void ConfigureBulkLoader(object bulkLoader, string tableName, string tempFile)
    {
        TableNameProperty?.SetValue(bulkLoader, tableName);

        // Open stream and assign to SourceStream — MySqlBulkLoader takes ownership of the stream
        FileStream fileStream = File.OpenRead(tempFile);
        SourceStreamProperty?.SetValue(bulkLoader, fileStream);

        FieldTerminatorProperty?.SetValue(bulkLoader, ",");
        LineTerminatorProperty?.SetValue(bulkLoader, "\n");

        // Configure quoting to match our CSV output
        FieldQuotationCharacterProperty?.SetValue(bulkLoader, '"');
        FieldQuotationOptionalProperty?.SetValue(bulkLoader, true);
    }

    /// <summary>
    /// Writes data from IDataReader to a CSV file. Returns the number of rows written.
    /// </summary>
    private static int WriteDataToCsv(IDataReader data, string filePath)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        int rowCount = 0;
        while (data.Read())
        {
            for (int i = 0; i < data.FieldCount; i++)
            {
                if (i > 0) writer.Write(',');
                WriteCsvValue(writer, data.GetValue(i));
            }
            writer.WriteLine();
            rowCount++;
        }

        return rowCount;
    }

    /// <summary>
    /// Writes data from IDataReader to a CSV file asynchronously. Returns the number of rows written.
    /// </summary>
    private static async Task<int> WriteDataToCsvAsync(IDataReader data, string filePath, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        int rowCount = 0;
        while (data.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int i = 0; i < data.FieldCount; i++)
            {
                if (i > 0) await writer.WriteAsync(",");
                await WriteCsvValueAsync(writer, data.GetValue(i));
            }
            await writer.WriteLineAsync();
            rowCount++;
        }

        return rowCount;
    }

    /// <summary>
    /// Writes a single value in MySQL-compatible CSV format.
    /// </summary>
    private static void WriteCsvValue(StreamWriter writer, object value)
    {
        if (value is DBNull)
        {
            writer.Write("\\N");
            return;
        }

        // MySQL expects 1/0 for boolean, not True/False
        if (value is bool boolVal)
        {
            writer.Write(boolVal ? '1' : '0');
            return;
        }

        var str = value.ToString();
        if (str != null && (str.Contains(",") || str.Contains("\"") || str.Contains("\n")))
        {
            writer.Write('"');
            writer.Write(str.Replace("\"", "\"\""));
            writer.Write('"');
        }
        else
        {
            writer.Write(str);
        }
    }

    /// <summary>
    /// Writes a single value in MySQL-compatible CSV format asynchronously.
    /// </summary>
    private static async Task WriteCsvValueAsync(StreamWriter writer, object value)
    {
        if (value is DBNull)
        {
            await writer.WriteAsync("\\N");
            return;
        }

        // MySQL expects 1/0 for boolean, not True/False
        if (value is bool boolVal)
        {
            await writer.WriteAsync(boolVal ? "1" : "0");
            return;
        }

        var str = value.ToString();
        if (str != null && (str.Contains(",") || str.Contains("\"") || str.Contains("\n")))
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