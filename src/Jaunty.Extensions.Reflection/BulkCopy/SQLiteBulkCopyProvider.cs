using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using Jaunty.Configuration;
using Jaunty.Internals.BulkCopy;

namespace Jaunty.Extensions.Reflection.BulkCopy;

/// <summary>
/// SQLite bulk copy provider using optimized INSERT with transactions and prepared statements.
/// SQLite does not have a native bulk copy API, so this provides the best possible performance
/// using standard ADO.NET patterns.
/// </summary>
internal sealed class SQLiteBulkCopyProvider : IBulkCopyProvider
{
    /// <inheritdoc/>
    public bool IsSupported => true;

    /// <inheritdoc/>
    public int CopyToServer(IDbConnection connection, string tableName, IDataReader data, BulkCopyOptions options)
    {
        var sqliteConnection = connection as IDbConnection
            ?? throw new ArgumentException("Connection must be a valid database connection.", nameof(connection));

        bool wasClosed = sqliteConnection.State == ConnectionState.Closed;
        IDbTransaction? transaction = options.Transaction;
        bool ownTransaction = transaction is null;

        try
        {
            if (wasClosed)
                sqliteConnection.Open();

            if (ownTransaction)
                transaction = sqliteConnection.BeginTransaction();

            // Optimize SQLite for bulk inserts
            using var pragmaCmd = sqliteConnection.CreateCommand();
            pragmaCmd.Transaction = transaction;

            // Use WAL mode for better write performance
            pragmaCmd.CommandText = "PRAGMA journal_mode=WAL";
            pragmaCmd.ExecuteNonQuery();

            // Set synchronous to NORMAL for better performance (acceptable durability trade-off for bulk ops)
            pragmaCmd.CommandText = "PRAGMA synchronous=NORMAL";
            pragmaCmd.ExecuteNonQuery();

            // Build INSERT command
            int columnCount = data.FieldCount;
            var columnNames = new string[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                columnNames[i] = data.GetName(i);
            }

            using var command = sqliteConnection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = BuildInsertSql(tableName, columnNames);

            if (options.Timeout > 0)
                command.CommandTimeout = options.Timeout;

            // Create parameters once
            var parameters = new IDataParameter[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                var param = command.CreateParameter();
                param.ParameterName = $"@p{i}";
                command.Parameters.Add(param);
                parameters[i] = param;
            }

            // Prepare the command once
            try { command.Prepare(); } catch { /* Best effort */ }

            int rowCount = 0;

            while (data.Read())
            {
                // Set parameter values for this row
                for (int i = 0; i < columnCount; i++)
                {
                    parameters[i].Value = data.GetValue(i) ?? DBNull.Value;
                }

                command.ExecuteNonQuery();
                rowCount++;
            }

            if (ownTransaction)
                transaction!.Commit();

            return rowCount;
        }
        catch
        {
            if (ownTransaction)
                transaction?.Rollback();
            throw;
        }
        finally
        {
            if (ownTransaction)
                transaction?.Dispose();

            if (wasClosed && sqliteConnection.State != ConnectionState.Closed)
                sqliteConnection.Close();
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
        // SQLite doesn't have true async I/O, but we can run on background thread
        return await Task.Run(() => CopyToServer(connection, tableName, data, options), cancellationToken);
    }

    /// <summary>
    /// Builds an INSERT SQL statement for the specified table and columns.
    /// </summary>
    private static string BuildInsertSql(string tableName, string[] columnNames)
    {
        var sb = new System.Text.StringBuilder(256);

        sb.Append("INSERT INTO ");
        sb.Append(tableName);
        sb.Append(" (");

        for (int i = 0; i < columnNames.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(columnNames[i]);
        }

        sb.Append(") VALUES (");

        for (int i = 0; i < columnNames.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append("@p");
            sb.Append(i);
        }

        sb.Append(")");

        return sb.ToString();
    }
}
